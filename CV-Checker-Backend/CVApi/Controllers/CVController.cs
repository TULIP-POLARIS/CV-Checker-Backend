using System.Text;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using CVApi.Auth;
using DAL.Api;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace CVApi.Controllers;

[Authorize]
[Route("api/cv")]
[ApiController]
public sealed class CVController : ControllerBase
{
    private const long MaxPdfSizeBytes = 10 * 1024 * 1024;

    private readonly ICVService _cvService;
    private readonly ApiContext _db;
    private readonly CVExtractionRunner _runner;

    public CVController(ICVService cvService, ApiContext db, CVExtractionRunner runner)
    {
        _cvService = cvService;
        _db = db;
        _runner = runner;
    }

    /// <summary>POST /api/cv/upload — multipart field name: file</summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "PDF file is required (form field: file)." });

        if (file.Length > MaxPdfSizeBytes)
            return BadRequest(new { message = "PDF too large. Max allowed size is 10MB." });

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only PDF files are allowed." });

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var fileBytes = ms.ToArray();
        var extractedText = ExtractTextFromPdf(fileBytes);
        CVExtractionResult? aiResult = null;

        // Runs the Python extractor using a temp file without requiring FilePath persistence.
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

        var cv = new CV
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            FileName = file.FileName,
            FileData = fileBytes,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType,
            FileSizeBytes = file.Length,
            Content = !string.IsNullOrWhiteSpace(aiResult?.Error)
                ? extractedText
                : JsonSerializer.Serialize(aiResult),
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            var result = await _cvService.CreateCVAsync(cv);
            return Ok(new
            {
                id = result.Id,
                fileName = result.FileName,
                uploadedAt = result.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                message = "CV uploaded successfully",
                aiExtractionOk = aiResult != null && string.IsNullOrWhiteSpace(aiResult.Error)
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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

    /// <summary>Generates a CV payload from profile + job input (template composition, no LLM call).</summary>
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
        if (cv == null || cv.UserId != userId) return NotFound(new { message = "CV not found." });
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
        if (cv == null || cv.UserId != userId) return NotFound(new { message = "CV not found." });

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
