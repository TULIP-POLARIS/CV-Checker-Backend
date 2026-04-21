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
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 1. Save CV
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
                Content = !string.IsNullOrWhiteSpace(aiResult?.Error)
                    ? extractedText
                    : JsonSerializer.Serialize(aiResult),
                CreatedAt = DateTime.UtcNow
            };

            var savedCv = await _cvService.CreateCVAsync(cv);

            // 2. Save background
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

            // 3. Save job offer
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

            // 4. Build generated CV payload using the job info saved above
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
                    aiExtractionOk = aiResult != null && string.IsNullOrWhiteSpace(aiResult.Error)
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
        var totalGeneratedCvs = await _db.CvGenerations.AsNoTracking().CountAsync();
        var totalUsers = await _db.CvGenerations.AsNoTracking()
            .Select(g => g.UserId)
            .Distinct()
            .CountAsync();

        return Ok(new
        {
            totalGeneratedCvs,
            totalUsers
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

        var hasProfileSkills = skills.Count > 0;
        var aiFallbackSkills = hasProfileSkills
            ? new List<string>()
            : await GetAiFallbackSkillsAsync(userId);

        var normalizedSkills = hasProfileSkills
            ? skills.Select(s => new { name = s.Name, level = s.Level, source = "profile" })
            : aiFallbackSkills.Select(s => new { name = s, level = "Not specified", source = "ai-fallback" });

        var langs = await _db.Languages.AsNoTracking().Where(l => l.UserId == userId).ToListAsync();

        var hasBackground = !string.IsNullOrWhiteSpace(bg?.BackgroundText);
        var hasLanguages = langs.Count > 0;
        var hasEducation = edu.Count > 0;
        var hasWorkExperience = work.Count > 0;

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
                    personalDetails = "profile",
                    summary = hasBackground ? "profile-background" : "none",
                    skills = hasProfileSkills ? "profile" : "ai-fallback-from-uploaded-cv",
                    languages = hasLanguages ? "profile" : "none",
                    education = hasEducation ? "profile" : "none",
                    workExperience = hasWorkExperience ? "profile" : "none"
                }
            },
            sections = new
            {
                profile = new
                {
                    fullName = personal == null
                    ? string.Empty
                    : $"{personal.FirstName} {personal.LastName}".Trim(),
                    firstName = personal?.FirstName ?? string.Empty,
                    lastName = personal?.LastName ?? string.Empty,
                    dateOfBirth = personal?.DateOfBirth?.ToString("yyyy-MM-dd"),
                    phoneNumber = personal?.PhoneNumber ?? string.Empty,
                    address = personal?.Address ?? string.Empty,
                    nationality = personal?.Nationality ?? string.Empty,
                    gender = personal?.Gender ?? string.Empty,
                    countryOfResidence = personal?.CountryOfResidence ?? string.Empty,
                    profilePictureUrl = personal?.ProfilePictureData != null && personal.ProfilePictureData.Length > 0
                    ? "/api/profile/personal/picture"
                    : null,
                    source = "profile"
                },
                summary = new
                {
                    text = bg?.BackgroundText ?? string.Empty,
                    source = hasBackground ? "profile-background" : "none"
                },
                skills = normalizedSkills,
                languages = langs.Select(l => new
                {
                    name = l.Name,
                    level = l.Level,
                    source = "profile"
                }),
                education = edu.Select(e => new
                {
                    degree = e.Degree,
                    fieldOfStudy = e.FieldOfStudy,
                    institution = e.Institution,
                    startDate = e.StartDate,
                    endDate = e.EndDate,
                    description = e.Description,
                    source = "profile"
                }),
                workExperience = work.Select(w => new
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
            }
        };
    }

    private async Task<List<string>> GetAiFallbackSkillsAsync(Guid userId)
    {
        var latestCv = await _db.CVs.AsNoTracking()
            .Where(c => c.UserId == userId && !string.IsNullOrWhiteSpace(c.Content))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (latestCv == null || string.IsNullOrWhiteSpace(latestCv.Content))
            return new List<string>();

        CVExtractionResult? extraction = null;
        try
        {
            extraction = JsonSerializer.Deserialize<CVExtractionResult>(latestCv.Content);
        }
        catch (JsonException)
        {
            extraction = null;
        }

        var rawSkills = extraction?.Skills;
        if (string.IsNullOrWhiteSpace(rawSkills) || string.Equals(rawSkills, "Not found", StringComparison.OrdinalIgnoreCase))
            return new List<string>();

        return rawSkills
            .Split(new[] { ',', ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Regex.Replace(s, @"\s+", " ").Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
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