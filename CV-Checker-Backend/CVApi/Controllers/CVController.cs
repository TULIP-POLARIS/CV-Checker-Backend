using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using CVApi.Auth;
using DAL.Api;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace CVApi.Controllers;

[Authorize]
[Route("api/cv")]
[ApiController]
public sealed class CVController : ControllerBase
{
    private const long MaxPdfSizeBytes = 10 * 1024 * 1024;

    private readonly ICVService _cvService;
    private readonly IJobOfferService _jobOfferService;
    private readonly ApiContext _db;
    private readonly CVExtractionRunner _runner;

    public CVController(
        ICVService cvService,
        IJobOfferService jobOfferService,
        ApiContext db,
        CVExtractionRunner runner)
    {
        _cvService = cvService;
        _jobOfferService = jobOfferService;
        _db = db;
        _runner = runner;
    }

    /// <summary>
    /// POST /api/cv/upload
    /// multipart/form-data fields:
    /// file, backgroundText, jobTitle, jobDescription, company, requirements, location
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] UploadCvRequest request)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { message = "PDF file is required (form field: file)." });

        if (request.File.Length > MaxPdfSizeBytes)
            return BadRequest(new { message = "PDF too large. Max allowed size is 10MB." });

        var extension = Path.GetExtension(request.File.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only PDF files are allowed." });

        await using var ms = new MemoryStream();
        await request.File.CopyToAsync(ms);
        var fileBytes = ms.ToArray();

        var extractedText = ExtractTextFromPdf(fileBytes);
        CVExtractionResult? aiResult = null;

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        try
        {
            await System.IO.File.WriteAllBytesAsync(tempPath, fileBytes);
            aiResult = await _runner.ExtractFromFileAsync(tempPath);

            Console.WriteLine("===== PYTHON RESULT =====");
            Console.WriteLine(JsonSerializer.Serialize(aiResult, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            Console.WriteLine("=========================");
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var extractionToPersist = aiResult;
            if (!string.IsNullOrWhiteSpace(aiResult?.Error))
                extractionToPersist = BuildFallbackExtractionFromText(extractedText, aiResult.Error);

            var cv = new CV
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                FileName = request.File.FileName,
                FileData = fileBytes,
                ContentType = string.IsNullOrWhiteSpace(request.File.ContentType)
                    ? "application/pdf"
                    : request.File.ContentType,
                FileSizeBytes = request.File.Length,
                Content = JsonSerializer.Serialize(extractionToPersist ?? new CVExtractionResult()),
                CreatedAt = DateTime.UtcNow
            };

            var savedCv = await _cvService.CreateCVAsync(cv);

            if (extractionToPersist != null && string.IsNullOrWhiteSpace(extractionToPersist.Error))
            {
                await FillProfileFromExtractionAsync(userId.Value, extractionToPersist);
            }

            CvBackground? savedBackground = null;
            if (!string.IsNullOrWhiteSpace(request.BackgroundText))
            {
                savedBackground = new CvBackground
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    BackgroundText = request.BackgroundText,
                    CreatedAt = DateTime.UtcNow
                };

                _db.CvBackgrounds.Add(savedBackground);
            }

            JobOffer? savedJobOffer = null;
            if (!string.IsNullOrWhiteSpace(request.JobTitle) ||
                !string.IsNullOrWhiteSpace(request.JobDescription) ||
                !string.IsNullOrWhiteSpace(request.Company) ||
                !string.IsNullOrWhiteSpace(request.Requirements) ||
                !string.IsNullOrWhiteSpace(request.Location))
            {
                savedJobOffer = new JobOffer
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    Title = request.JobTitle ?? "",
                    Company = request.Company ?? "",
                    Description = request.JobDescription ?? "",
                    Requirements = request.Requirements ?? "",
                    Location = request.Location ?? "",
                    SourceFileId = savedCv.Id,
                    TextContent = $"{request.JobTitle} {request.JobDescription} {request.Requirements} {request.Location} {request.Company}".Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                await _jobOfferService.CreateJobOfferAsync(savedJobOffer);
            }

            if (savedBackground != null)
                await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            object? generatedCv = null;
            if (savedJobOffer != null)
            {
                var fakeGeneration = new CvGenerated
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    JobTitle = savedJobOffer.Title ?? "",
                    JobDescription = savedJobOffer.Description ?? "",
                    CvUrl = "",
                    CreatedAt = DateTime.UtcNow
                };

                generatedCv = await BuildGeneratedCvSections(userId.Value, fakeGeneration);
            }

            return Ok(new
            {
                message = "CV uploaded successfully",
                cv = new
                {
                    id = savedCv.Id,
                    fileName = savedCv.FileName,
                    uploadedAt = savedCv.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                    aiExtractionError = aiResult?.Error
                },
                background = savedBackground == null ? null : new
                {
                    id = savedBackground.Id,
                    text = savedBackground.BackgroundText
                },
                jobOffer = savedJobOffer == null ? null : new
                {
                    id = savedJobOffer.Id,
                    title = savedJobOffer.Title,
                    company = savedJobOffer.Company,
                    description = savedJobOffer.Description,
                    requirements = savedJobOffer.Requirements,
                    location = savedJobOffer.Location
                },
                generatedCv
            });
        }
        catch (ArgumentException ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new
            {
                message = "An error occurred while uploading the CV and saving related data.",
                error = ex.Message
            });
        }
    }

    [HttpPost("background")]
    public async Task<IActionResult> SaveBackground([FromBody] BackgroundBody body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var id = Guid.NewGuid();
        var row = new CvBackground
        {
            Id = id,
            UserId = userId.Value,
            BackgroundText = body.BackgroundText ?? "",
            CreatedAt = DateTime.UtcNow
        };
        _db.CvBackgrounds.Add(row);
        await _db.SaveChangesAsync();

        return Ok(new { id, message = "Background saved successfully" });
    }

    public sealed class GenerateBody
    {
        public string? JobTitle { get; set; }
        public string? JobDescription { get; set; }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateBody body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var id = Guid.NewGuid();

        var row = new CvGenerated
        {
            Id = id,
            UserId = userId.Value,
            JobTitle = body.JobTitle ?? "",
            JobDescription = body.JobDescription ?? "",
            CvUrl = "",
            CreatedAt = DateTime.UtcNow
        };
        _db.CvGenerations.Add(row);
        await _db.SaveChangesAsync();

        var generatedCv = await BuildGeneratedCvSections(userId.Value, row);

        return Ok(new
        {
            id,
            message = "CV generated successfully",
            generatedCv
        });
    }

    [HttpGet("generated/{id:guid}/file")]
    public async Task<IActionResult> DownloadGenerated(Guid id)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var gen = await _db.CvGenerations.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
        if (gen == null || gen.UserId != userId) return NotFound();

        var generatedCv = await BuildGeneratedCvSections(userId.Value, gen);
        return Ok(new { id = gen.Id, generatedCv });
    }

    [HttpGet("generated/stats")]
    public async Task<IActionResult> GetGeneratedStats()
    {
        var dailyStats = await _db.CvGenerations.AsNoTracking()
            .GroupBy(g => new { g.CreatedAt.Year, g.CreatedAt.Month, g.CreatedAt.Day })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                generatedCvs = group.Count(),
                users = group.Select(g => g.UserId).Distinct().Count()
            })
            .OrderBy(stat => stat.Year)
            .ThenBy(stat => stat.Month)
            .ThenBy(stat => stat.Day)
            .ToListAsync();

        return Ok(new
        {
            dailyStats = dailyStats.Select(stat => new
            {
                date = new DateTime(stat.Year, stat.Month, stat.Day).ToString("yyyy-MM-dd"),
                stat.generatedCvs,
                stat.users
            })
        });
    }

    private async Task<object> BuildGeneratedCvSections(Guid userId, CvGenerated gen)
    {
        var personal = await _db.PersonalInfos.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var bg = await _db.CvBackgrounds.AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();

        var edu = await _db.Educations.AsNoTracking().Where(e => e.UserId == userId).ToListAsync();
        var work = await _db.WorkExperiences.AsNoTracking().Where(w => w.UserId == userId).ToListAsync();
        var skills = await _db.Skills.AsNoTracking().Where(s => s.UserId == userId).ToListAsync();
        var langs = await _db.Languages.AsNoTracking().Where(l => l.UserId == userId).ToListAsync();
        var extraction = await GetLatestExtractionResultAsync(userId);

        var hasProfileInfo = HasMeaningfulProfileInfo(personal, edu.Count, work.Count, skills.Count);
        var useProfileData = hasProfileInfo;

        var aiFallbackSkills = useProfileData
            ? new List<string>()
            : (extraction?.Skills ?? new List<string>());

        IEnumerable<object> normalizedSkills = useProfileData
            ? skills.Select(s => (object)new
            {
                name = s.Name,
                level = s.Level,
                source = "profile"
            })
            : aiFallbackSkills.Select(s => (object)new
            {
                name = s,
                level = "Not specified",
                source = "ai-fallback"
            });

        var profileFullName = personal == null
            ? string.Empty
            : $"{personal.FirstName} {personal.LastName}".Trim();

        var aiFullName = IsMeaningfulExtractionValue(extraction?.FullName)
            ? extraction!.FullName
            : string.Empty;

        var aiFirstName = string.Empty;
        var aiLastName = string.Empty;

        if (!useProfileData && !string.IsNullOrWhiteSpace(aiFullName))
        {
            var blockedNameWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "skills", "languages", "education", "profile", "contact",
                "experience", "projects", "summary", "technical"
            };

            var parts = aiFullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var looksLikeBadName = parts.Any(p => blockedNameWords.Contains(p));

            if (!looksLikeBadName)
            {
                if (parts.Length >= 2)
                {
                    aiFirstName = parts[0];
                    aiLastName = string.Join(" ", parts.Skip(1));
                }
                else if (parts.Length == 1)
                {
                    aiFirstName = parts[0];
                }
            }
        }

        var profilePhone = personal?.PhoneNumber ?? string.Empty;
        var aiPhone = IsMeaningfulExtractionValue(extraction?.Phone)
            ? extraction!.Phone
            : string.Empty;

        IEnumerable<object> fallbackLanguages = useProfileData
            ? Enumerable.Empty<object>()
            : (extraction?.Languages ?? new List<string>())
                .Where(IsMeaningfulExtractionValue)
                .Select(l => (object)new
                {
                    name = l,
                    level = "Not specified",
                    source = "ai-fallback"
                });

        IEnumerable<object> fallbackEducation = useProfileData
            ? Enumerable.Empty<object>()
            : (extraction?.Education ?? new List<EducationItem>())
                .Where(e =>
                    IsMeaningfulExtractionValue(e.Institution) ||
                    IsMeaningfulExtractionValue(e.Degree) ||
                    IsMeaningfulExtractionValue(e.Description) ||
                    IsMeaningfulExtractionValue(e.Raw))
                .Select(e => (object)new
                {
                    degree = e.Degree ?? string.Empty,
                    fieldOfStudy = string.Empty,
                    institution = e.Institution ?? string.Empty,
                    startDate = e.StartDate ?? string.Empty,
                    endDate = e.EndDate ?? string.Empty,
                    description = !string.IsNullOrWhiteSpace(e.Raw)
                        ? e.Raw
                        : (e.Description ?? string.Empty),
                    source = "ai-fallback"
                });

        IEnumerable<object> fallbackWorkExperience = useProfileData
            ? Enumerable.Empty<object>()
            : (extraction?.WorkExperience ?? new List<WorkExperienceItem>())
                .Where(w =>
                    IsMeaningfulExtractionValue(w.Role) ||
                    IsMeaningfulExtractionValue(w.Company) ||
                    IsMeaningfulExtractionValue(w.Description) ||
                    IsMeaningfulExtractionValue(w.Raw))
                .Select(w => (object)new
                {
                    jobTitle = w.Role ?? string.Empty,
                    company = w.Company ?? string.Empty,
                    location = string.Empty,
                    startDate = w.StartDate ?? string.Empty,
                    endDate = w.EndDate ?? string.Empty,
                    currentlyWorking = string.Equals(w.EndDate, "Present", StringComparison.OrdinalIgnoreCase),
                    description = !string.IsNullOrWhiteSpace(w.Raw)
                        ? w.Raw
                        : (w.Description ?? string.Empty),
                    source = "ai-fallback"
                });

        var hasBackground = !string.IsNullOrWhiteSpace(bg?.BackgroundText);
        var hasLanguages = useProfileData ? langs.Count > 0 : fallbackLanguages.Any();
        var hasEducation = useProfileData ? edu.Count > 0 : fallbackEducation.Any();
        var hasWorkExperience = useProfileData ? work.Count > 0 : fallbackWorkExperience.Any();

        return new
        {
            metadata = new
            {
                generatedId = gen.Id,
                createdAt = gen.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                targetJobTitle = gen.JobTitle,
                targetJobDescription = gen.JobDescription,
                dataSources = new
                {
                    personalDetails = useProfileData ? "profile" : "ai-fallback-from-uploaded-cv",
                    summary = hasBackground ? "profile-background" : "none",
                    skills = useProfileData ? "profile" : "ai-fallback-from-uploaded-cv",
                    languages = hasLanguages
                        ? (useProfileData ? "profile" : "ai-fallback-from-uploaded-cv")
                        : "none",
                    education = hasEducation
                        ? (useProfileData ? "profile" : "ai-fallback-from-uploaded-cv")
                        : "none",
                    workExperience = hasWorkExperience
                        ? (useProfileData ? "profile" : "ai-fallback-from-uploaded-cv")
                        : "none"
                }
            },
            sections = new
            {
                profile = new
                {
                    fullName = useProfileData ? profileFullName : aiFullName,
                    firstName = useProfileData ? personal?.FirstName ?? string.Empty : aiFirstName,
                    lastName = useProfileData ? personal?.LastName ?? string.Empty : aiLastName,
                    dateOfBirth = personal?.DateOfBirth?.ToString("yyyy-MM-dd"),
                    phoneNumber = useProfileData ? profilePhone : aiPhone,
                    address = useProfileData ? personal?.Address ?? string.Empty : string.Empty,
                    nationality = useProfileData ? personal?.Nationality ?? string.Empty : string.Empty,
                    gender = useProfileData ? personal?.Gender ?? string.Empty : string.Empty,
                    countryOfResidence = useProfileData ? personal?.CountryOfResidence ?? string.Empty : string.Empty,
                    profilePictureUrl = useProfileData && personal?.ProfilePictureData != null && personal.ProfilePictureData.Length > 0
                        ? $"/api/profile/personal/picture/user/{personal.UserId}"
                        : null,
                    source = useProfileData ? "profile" : "ai-fallback"
                },
                summary = new
                {
                    text = hasBackground
                        ? bg!.BackgroundText
                        : (extraction?.ProfessionalSummary ?? string.Empty),
                    source = hasBackground
                        ? "profile-background"
                        : IsMeaningfulExtractionValue(extraction?.ProfessionalSummary) ? "ai-fallback" : "none"
                },
                skills = normalizedSkills,
                languages = useProfileData
                    ? langs.Select(l => (object)new
                    {
                        name = l.Name,
                        level = l.Level,
                        source = "profile"
                    })
                    : fallbackLanguages,
                education = useProfileData
                    ? edu.Select(e => (object)new
                    {
                        degree = e.Degree,
                        fieldOfStudy = e.FieldOfStudy,
                        institution = e.Institution,
                        startDate = e.StartDate,
                        endDate = e.EndDate,
                        description = e.Description,
                        source = "profile"
                    })
                    : fallbackEducation,
                workExperience = useProfileData
                    ? work.Select(w => (object)new
                    {
                        jobTitle = w.JobTitle,
                        company = w.Company,
                        location = w.Location,
                        startDate = w.StartDate,
                        endDate = w.EndDate,
                        currentlyWorking = w.CurrentlyWorking,
                        description = w.Description,
                        source = "profile"
                    })
                    : fallbackWorkExperience
            }
        };
    }

    private async Task<CVExtractionResult?> GetLatestExtractionResultAsync(Guid userId)
    {
        var latestCv = await _db.CVs.AsNoTracking()
            .Where(c => c.UserId == userId && !string.IsNullOrWhiteSpace(c.Content))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (latestCv == null || string.IsNullOrWhiteSpace(latestCv.Content))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CVExtractionResult>(
                latestCv.Content,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task FillProfileFromExtractionAsync(Guid userId, CVExtractionResult extraction)
    {
        if (extraction == null)
            return;

        var now = DateTime.UtcNow;

        var personal = await _db.PersonalInfos.FirstOrDefaultAsync(p => p.UserId == userId);

        if (personal == null)
        {
            personal = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.PersonalInfos.Add(personal);
        }

        if (!string.IsNullOrWhiteSpace(extraction.FullName))
        {
            var parts = extraction.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (string.IsNullOrWhiteSpace(personal.FirstName) && parts.Length > 0)
                personal.FirstName = parts[0];

            if (string.IsNullOrWhiteSpace(personal.LastName) && parts.Length > 1)
                personal.LastName = string.Join(" ", parts.Skip(1));
        }

        if (string.IsNullOrWhiteSpace(personal.PhoneNumber) && !string.IsNullOrWhiteSpace(extraction.Phone))
            personal.PhoneNumber = extraction.Phone;

        if (string.IsNullOrWhiteSpace(personal.Address) && !string.IsNullOrWhiteSpace(extraction.Location))
            personal.Address = extraction.Location;

        personal.UpdatedAt = now;

        var existingSkills = await _db.Skills
            .Where(s => s.UserId == userId)
            .Select(s => s.Name)
            .ToListAsync();

        var existingSkillSet = new HashSet<string>(
            existingSkills.Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var skillName in extraction.Skills ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(skillName))
                continue;

            var normalized = skillName.Trim();
            if (existingSkillSet.Contains(normalized))
                continue;

            _db.Skills.Add(new Skill
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = normalized,
                Level = "Not specified",
                CreatedAt = now,
                UpdatedAt = now
            });

            existingSkillSet.Add(normalized);
        }

        var existingLanguages = await _db.Languages
            .Where(l => l.UserId == userId)
            .Select(l => l.Name)
            .ToListAsync();

        var existingLanguageSet = new HashSet<string>(
            existingLanguages.Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var languageRaw in extraction.Languages ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(languageRaw))
                continue;

            var name = languageRaw.Trim();
            var level = "Not specified";

            var match = Regex.Match(languageRaw, @"^(.*?)\s*\((.*?)\)\s*$");
            if (match.Success)
            {
                name = match.Groups[1].Value.Trim();
                level = match.Groups[2].Value.Trim();
            }

            if (existingLanguageSet.Contains(name))
                continue;

            _db.Languages.Add(new Language
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = name,
                Level = level,
                CreatedAt = now,
                UpdatedAt = now
            });

            existingLanguageSet.Add(name);
        }

        var existingEducations = await _db.Educations
            .Where(e => e.UserId == userId)
            .ToListAsync();

        foreach (var edu in extraction.Education ?? new List<EducationItem>())
        {
            if (edu == null)
                continue;

            var institution = edu.Institution?.Trim() ?? "";
            var degree = edu.Degree?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(institution) &&
                string.IsNullOrWhiteSpace(degree) &&
                string.IsNullOrWhiteSpace(edu.Description) &&
                string.IsNullOrWhiteSpace(edu.Raw))
                continue;

            var alreadyExists = existingEducations.Any(e =>
                string.Equals(e.Institution ?? "", institution, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Degree ?? "", degree, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.StartDate ?? "", edu.StartDate ?? "", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.EndDate ?? "", edu.EndDate ?? "", StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
                continue;

            var newEdu = new Education
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Institution = institution,
                Degree = degree,
                FieldOfStudy = "",
                StartDate = edu.StartDate ?? "",
                EndDate = edu.EndDate,
                Description = !string.IsNullOrWhiteSpace(edu.Description) ? edu.Description : edu.Raw,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Educations.Add(newEdu);
            existingEducations.Add(newEdu);
        }

        var existingWork = await _db.WorkExperiences
            .Where(w => w.UserId == userId)
            .ToListAsync();

        foreach (var work in extraction.WorkExperience ?? new List<WorkExperienceItem>())
        {
            if (work == null)
                continue;

            var role = work.Role?.Trim() ?? "";
            var company = work.Company?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(role) &&
                string.IsNullOrWhiteSpace(company) &&
                string.IsNullOrWhiteSpace(work.Description) &&
                string.IsNullOrWhiteSpace(work.Raw))
                continue;

            var alreadyExists = existingWork.Any(w =>
                string.Equals(w.JobTitle ?? "", role, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(w.Company ?? "", company, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(w.StartDate ?? "", work.StartDate ?? "", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(w.EndDate ?? "", work.EndDate ?? "", StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
                continue;

            var newWork = new WorkExperience
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                JobTitle = role,
                Company = company,
                Location = extraction.Location,
                StartDate = work.StartDate ?? "",
                EndDate = work.EndDate,
                CurrentlyWorking = string.Equals(work.EndDate, "Present", StringComparison.OrdinalIgnoreCase),
                Description = !string.IsNullOrWhiteSpace(work.Description) ? work.Description : work.Raw,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.WorkExperiences.Add(newWork);
            existingWork.Add(newWork);
        }

        await _db.SaveChangesAsync();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CV>> GetCV(Guid id)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var cv = await _cvService.GetByIdAsync(id);
        if (cv == null || cv.UserId != userId)
            return NotFound(new { message = "CV not found." });

        return Ok(cv);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<IEnumerable<CV>>> GetCVsByUser(Guid userId)
    {
        var tokenUser = HttpUser.GetUserId(User);
        if (tokenUser == null) return Unauthorized();
        if (tokenUser != userId) return Forbid();

        var cvs = await _cvService.GetByUserIdAsync(userId);
        return Ok(cvs);
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> DownloadCVFile(Guid id)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var cv = await _cvService.GetByIdAsync(id);
        if (cv == null || cv.UserId != userId)
            return NotFound(new { message = "CV not found." });

        if (cv.FileData == null || cv.FileData.Length == 0)
            return NotFound(new { message = "CV file not found." });

        var contentType = string.IsNullOrWhiteSpace(cv.ContentType) ? "application/pdf" : cv.ContentType;
        var fileName = string.IsNullOrWhiteSpace(cv.FileName) ? $"cv-{id}.pdf" : cv.FileName;

        return File(cv.FileData, contentType, fileName);
    }

    private static string ExtractTextFromPdf(byte[] pdfBytes)
    {
        if (pdfBytes == null || pdfBytes.Length == 0)
            return string.Empty;

        using var stream = new MemoryStream(pdfBytes);
        using var document = PdfDocument.Open(stream);
        var textBuilder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
                textBuilder.AppendLine(pageText);
        }

        return textBuilder.ToString().Trim();
    }

    private static CVExtractionResult BuildFallbackExtractionFromText(string extractedText, string originalError)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
            return new CVExtractionResult { Error = originalError };

        var lines = extractedText
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var firstLine = lines.FirstOrDefault() ?? string.Empty;
        var fullName = Regex.IsMatch(firstLine, @"^[A-Za-z][A-Za-z\s\-'`]{2,60}$")
            ? firstLine
            : string.Empty;

        var emailMatch = Regex.Match(extractedText, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b");
        var phoneMatch = Regex.Match(extractedText, @"(\+\d{1,3}[\s\-]?\(?\d+\)?(?:[\s\-]?\d+){5,}|\(?\d{2,4}\)?[\s\-]?\d{3,4}[\s\-]?\d{3,4})");

        var skillHints = new List<string>();
        var lowerText = extractedText.ToLowerInvariant();

        if (lowerText.Contains("c#")) skillHints.Add("C#");
        if (lowerText.Contains(".net")) skillHints.Add(".NET");
        if (lowerText.Contains("sql")) skillHints.Add("SQL");
        if (lowerText.Contains("python")) skillHints.Add("Python");
        if (lowerText.Contains("javascript")) skillHints.Add("JavaScript");
        if (lowerText.Contains("react")) skillHints.Add("React");
        if (lowerText.Contains("azure")) skillHints.Add("Azure");
        if (lowerText.Contains("typescript")) skillHints.Add("TypeScript");
        if (lowerText.Contains("node.js") || lowerText.Contains("nodejs")) skillHints.Add("Node.js");
        if (lowerText.Contains("postgresql")) skillHints.Add("PostgreSQL");
        if (lowerText.Contains("docker")) skillHints.Add("Docker");

        var fallbackEducation = lowerText.Contains("education")
            ? new List<EducationItem>
            {
                new EducationItem
                {
                    Raw = extractedText,
                    Description = extractedText
                }
            }
            : new List<EducationItem>();

        var fallbackWorkExperience = lowerText.Contains("experience")
            ? new List<WorkExperienceItem>
            {
                new WorkExperienceItem
                {
                    Raw = extractedText,
                    Description = extractedText
                }
            }
            : new List<WorkExperienceItem>();

        return new CVExtractionResult
        {
            FullName = fullName,
            Email = emailMatch.Success ? emailMatch.Value : string.Empty,
            Phone = phoneMatch.Success ? phoneMatch.Value.Trim() : string.Empty,
            Skills = skillHints.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Education = fallbackEducation,
            WorkExperience = fallbackWorkExperience,
            ProfessionalSummary = string.Empty,
            Error = originalError,
            RawExtractedText = extractedText
        };
    }

    private static bool HasMeaningfulProfileInfo(PersonalInfo? personal, int educationCount, int workCount, int skillsCount)
    {
        if (skillsCount > 0 || educationCount > 0 || workCount > 0)
            return true;

        if (personal == null)
            return false;

        return !string.IsNullOrWhiteSpace(personal.FirstName)
            || !string.IsNullOrWhiteSpace(personal.LastName)
            || !string.IsNullOrWhiteSpace(personal.PhoneNumber)
            || !string.IsNullOrWhiteSpace(personal.Address)
            || !string.IsNullOrWhiteSpace(personal.Nationality)
            || !string.IsNullOrWhiteSpace(personal.Gender)
            || !string.IsNullOrWhiteSpace(personal.CountryOfResidence)
            || personal.DateOfBirth.HasValue;
    }

    private static bool IsMeaningfulExtractionValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return !string.Equals(value.Trim(), "Not found", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class BackgroundBody
{
    public string? BackgroundText { get; set; }
}

public sealed class UploadCvRequest
{
    public IFormFile? File { get; set; }
    public string? BackgroundText { get; set; }
    public string? JobTitle { get; set; }
    public string? JobDescription { get; set; }
    public string? Company { get; set; }
    public string? Requirements { get; set; }
    public string? Location { get; set; }
}