using System.Text;
using BusinessLogic.Interface;
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
    private readonly ApiContext _db;

    public CVController(ICVService cvService, ApiContext db)
    {
        _cvService = cvService;
        _db = db;
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

        var cv = new CV
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            FileName = file.FileName,
            FileData = fileBytes,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType,
            FileSizeBytes = file.Length,
            Content = extractedText,
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
                message = "CV uploaded successfully"
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

    /// <summary>Gera um CV em texto a partir do perfil + vaga (substitui chamada LLM por composição simples).</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateBody body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var id = Guid.NewGuid();
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var cvUrl = $"{baseUrl}/api/cv/generated/{id}/file";

        var row = new CvGenerated
        {
            Id = id,
            UserId = userId.Value,
            JobTitle = body.JobTitle ?? "",
            JobDescription = body.JobDescription ?? "",
            CvUrl = cvUrl,
            CreatedAt = DateTime.UtcNow
        };
        _db.CvGenerations.Add(row);
        await _db.SaveChangesAsync();

        return Ok(new { id, cvUrl, message = "CV generated successfully" });
    }

    [HttpGet("generated/{id:guid}/file")]
    public async Task<IActionResult> DownloadGenerated(Guid id)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var gen = await _db.CvGenerations.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
        if (gen == null || gen.UserId != userId) return NotFound();

        var text = await BuildGeneratedCvText(userId.Value, gen);
        var bytes = Encoding.UTF8.GetBytes(text);
        var safeTitle = string.IsNullOrWhiteSpace(gen.JobTitle) ? "cv" : string.Join("-", gen.JobTitle.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return File(bytes, "text/plain; charset=utf-8", $"{safeTitle}.txt");
    }

    private async Task<string> BuildGeneratedCvText(Guid userId, CvGenerated gen)
    {
        var sb = new StringBuilder();
        sb.AppendLine(gen.JobTitle);
        sb.AppendLine(new string('=', gen.JobTitle.Length));
        sb.AppendLine();
        sb.AppendLine("Job description:");
        sb.AppendLine(gen.JobDescription);
        sb.AppendLine();

        var personal = await _db.PersonalInfos.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        if (personal != null)
        {
            sb.AppendLine("Personal");
            sb.AppendLine($"{personal.FirstName} {personal.LastName}");
            if (personal.DateOfBirth.HasValue) sb.AppendLine($"Born: {personal.DateOfBirth:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(personal.PhoneNumber)) sb.AppendLine($"Phone: {personal.PhoneNumber}");
            if (!string.IsNullOrWhiteSpace(personal.Address)) sb.AppendLine($"Address: {personal.Address}");
            sb.AppendLine();
        }

        var bg = await _db.CvBackgrounds.AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();
        if (bg != null && !string.IsNullOrWhiteSpace(bg.BackgroundText))
        {
            sb.AppendLine("Background");
            sb.AppendLine(bg.BackgroundText);
            sb.AppendLine();
        }

        var edu = await _db.Educations.AsNoTracking().Where(e => e.UserId == userId).ToListAsync();
        if (edu.Count > 0)
        {
            sb.AppendLine("Education");
            foreach (var e in edu)
                sb.AppendLine($"- {e.Degree}, {e.FieldOfStudy} @ {e.Institution} ({e.StartDate} – {e.EndDate ?? "present"})");
            sb.AppendLine();
        }

        var work = await _db.WorkExperiences.AsNoTracking().Where(w => w.UserId == userId).ToListAsync();
        if (work.Count > 0)
        {
            sb.AppendLine("Experience");
            foreach (var w in work)
            {
                sb.AppendLine($"- {w.JobTitle} @ {w.Company} ({w.StartDate} – {(w.CurrentlyWorking ? "present" : w.EndDate)})");
                if (!string.IsNullOrWhiteSpace(w.Description)) sb.AppendLine($"  {w.Description}");
            }
            sb.AppendLine();
        }

        var skills = await _db.Skills.AsNoTracking().Where(s => s.UserId == userId).ToListAsync();
        if (skills.Count > 0)
        {
            sb.AppendLine("Skills");
            foreach (var s in skills) sb.AppendLine($"- {s.Name} ({s.Level})");
            sb.AppendLine();
        }

        var langs = await _db.Languages.AsNoTracking().Where(l => l.UserId == userId).ToListAsync();
        if (langs.Count > 0)
        {
            sb.AppendLine("Languages");
            foreach (var l in langs) sb.AppendLine($"- {l.Name} ({l.Level})");
        }

        sb.AppendLine();
        sb.AppendLine("— Generated from your profile (template output; plug in an LLM provider here if needed).");
        return sb.ToString();
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
