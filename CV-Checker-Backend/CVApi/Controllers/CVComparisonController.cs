using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using BusinessLogic.Interface;
using BusinessLogic.DTOs;
using System.Text.RegularExpressions;
using System.Text;
using UglyToad.PdfPig;
using Microsoft.AspNetCore.Http;

namespace CVApi.Controllers
{
    [Route("api/compare-cv")]
    [ApiController]
    public class CVComparisonController : ControllerBase
    {
        private const long MaxPdfSizeBytes = 10 * 1024 * 1024;

        private readonly ICVComparisonService _comparisonService;
        private readonly ICVService _cvService;
        private readonly IJobOfferService _jobOfferService;

        public CVComparisonController(
            ICVComparisonService comparisonService,
            ICVService cvService,
            IJobOfferService jobOfferService)
        {
            _comparisonService = comparisonService;
            _cvService = cvService;
            _jobOfferService = jobOfferService;
        }

        // POST /api/compare-cv
        [HttpPost]
        public async Task<ActionResult<CVComparison>> CompareCV([FromBody] CreateCVComparisonDTO dto)
        {
            try
            {
                var comparison = new CVComparison
                {
                    Id = Guid.NewGuid(),
                    CVId = dto.CVId,
                    JobOfferId = dto.JobOfferId,
                    UserId = dto.UserId,
                    MatchScore = (int?)dto.MatchScore,
                    Strengths = dto.Strengths,
                    Weaknesses = dto.Weaknesses,
                    Suggestions = dto.Suggestions,
                    AnalysisResult = dto.AnalysisResult,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _comparisonService.CreateCVComparisonAsync(comparison);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while creating the comparison.",
                    error = ex.Message
                });
            }
        }

        // POST /api/compare-cv/auto
        [HttpPost("auto")]
        public async Task<ActionResult<object>> CompareCVAuto([FromBody] CreateAutoCVComparisonDTO dto)
        {
            try
            {
                var result = await _comparisonService.CreateAutoCVComparisonAsync(dto);
                return Ok(BuildAutoComparisonResponse(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while auto-comparing the CV.",
                    error = ex.Message
                });
            }
        }

        // POST /api/compare-cv/auto-upload
        // multipart/form-data:
        // - UserId
        // - CVFile
        // - Title
        // - Company
        // - Description
        // - Requirements
        // - Location
        [HttpPost("auto-upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<object>> CompareCVAutoWithCvAndJobOffer([FromForm] CreateAutoCVComparisonUploadDTO form)
        {
            try
            {
                if (form.UserId == Guid.Empty)
                    return BadRequest(new { message = "UserId is required." });

                if (form.CVFile == null)
                    return BadRequest(new { message = "CVFile is required." });

                if (form.CVFile.Length == 0)
                    return BadRequest(new { message = "CV file is empty." });

                if (form.CVFile.Length > MaxPdfSizeBytes)
                    return BadRequest(new { message = "PDF too large. Max allowed size is 10MB." });

                var extension = Path.GetExtension(form.CVFile.FileName);
                if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Only PDF files are allowed." });

                if (string.IsNullOrWhiteSpace(form.Title))
                    return BadRequest(new { message = "Job offer title is required." });

                if (string.IsNullOrWhiteSpace(form.Description))
                    return BadRequest(new { message = "Job offer description is required." });

                // 1) Save CV
                await using var ms = new MemoryStream();
                await form.CVFile.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                var extractedText = ExtractTextFromPdf(fileBytes);

                var cv = new CV
                {
                    Id = Guid.NewGuid(),
                    UserId = form.UserId,
                    FileName = form.CVFile.FileName,
                    FileData = fileBytes,
                    ContentType = string.IsNullOrWhiteSpace(form.CVFile.ContentType)
                        ? "application/pdf"
                        : form.CVFile.ContentType,
                    FileSizeBytes = form.CVFile.Length,
                    Content = extractedText,
                    CreatedAt = DateTime.UtcNow
                };

                var createdCv = await _cvService.CreateCVAsync(cv);

                // 2) Save JobOffer
                var jobOffer = new JobOffer
                {
                    Id = Guid.NewGuid(),
                    UserId = form.UserId,
                    Title = form.Title,
                    Company = form.Company,
                    Description = form.Description,
                    Requirements = form.Requirements,
                    Location = form.Location,
                    CreatedAt = DateTime.UtcNow
                };

                var createdJobOffer = await _jobOfferService.CreateJobOfferAsync(jobOffer);

                // 3) Compare using newly created ids
                var dto = new CreateAutoCVComparisonDTO
                {
                    CVId = createdCv.Id,
                    JobOfferId = createdJobOffer.Id,
                    UserId = form.UserId
                };

                var result = await _comparisonService.CreateAutoCVComparisonAsync(dto);
                return Ok(BuildAutoComparisonResponse(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while auto-comparing the CV.",
                    error = ex.Message
                });
            }
        }

        // GET /api/compare-cv/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<CVComparison>> GetComparison(Guid id)
        {
            try
            {
                var comparison = await _comparisonService.GetByIdAsync(id);

                if (comparison == null)
                    return NotFound(new { message = "Comparison not found." });

                return Ok(comparison);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving the comparison.",
                    error = ex.Message
                });
            }
        }

        // GET /api/compare-cv/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<CVComparison>>> GetComparisonsByUser(Guid userId)
        {
            try
            {
                var comparisons = await _comparisonService.GetByUserIdAsync(userId);
                return Ok(comparisons);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving comparisons.",
                    error = ex.Message
                });
            }
        }

        // POST /api/compare-cv/user/{userId}
        [HttpPost("user/{userId}")]
        public async Task<ActionResult<IEnumerable<CVComparison>>> GetComparisonsByUserPost(Guid userId)
        {
            try
            {
                var comparisons = await _comparisonService.GetByUserIdAsync(userId);
                return Ok(comparisons);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving comparisons.",
                    error = ex.Message
                });
            }
        }

        private static List<string> ExtractKeywordList(string? source, string prefix)
        {
            if (string.IsNullOrWhiteSpace(source))
                return new List<string>();

            var normalized = source.Trim();

            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized[prefix.Length..].Trim();

            return normalized
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => Regex.Replace(k, @"\s+", " ").Trim())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();
        }

        private static List<string> BuildImprovementSuggestions(int score, List<string> missingKeywords)
        {
            var suggestions = new List<string>();

            if (missingKeywords.Count > 0)
            {
                foreach (var keyword in missingKeywords.Take(5))
                {
                    suggestions.Add($"Add concrete evidence for '{keyword}' in your work experience or projects.");
                }
            }

            if (score < 40)
            {
                suggestions.Add("Rewrite your CV summary to mirror the role title and top job requirements.");
                suggestions.Add("Prioritize the most relevant experience at the top of the CV.");
            }
            else if (score < 70)
            {
                suggestions.Add("Add quantified achievements (numbers, impact, KPIs) for relevant roles.");
                suggestions.Add("Tailor your skills section to include exact terms from the job offer.");
            }
            else
            {
                suggestions.Add("Fine-tune wording to match the job description and improve keyword coverage.");
            }

            return suggestions
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
        }

        private static object BuildAutoComparisonResponse(CVComparison result)
        {
            var score = result.MatchScore ?? 0;
            var isMatch = score >= 60;
            var matchMessage = isMatch
                ? $"Match found ({score}%)."
                : $"Not a strong match yet ({score}%).";

            var matchedKeywords = ExtractKeywordList(result.Strengths, "Matched keywords:");
            var missingKeywords = ExtractKeywordList(result.Weaknesses, "Missing keywords:");
            var improvementSuggestions = BuildImprovementSuggestions(score, missingKeywords);

            return new
            {
                result.Id,
                result.CVId,
                result.JobOfferId,
                result.UserId,
                result.MatchScore,
                isMatch,
                matchMessage,
                result.Strengths,
                result.Weaknesses,
                result.Suggestions,
                result.AnalysisResult,
                result.CreatedAt,
                feedback = new
                {
                    strengths = matchedKeywords,
                    gaps = missingKeywords,
                    suggestWhatToImprove = improvementSuggestions
                }
            };
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

    public class CreateAutoCVComparisonUploadDTO
    {
        public Guid UserId { get; set; }
        public IFormFile? CVFile { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Requirements { get; set; }
        public string? Location { get; set; }
    }
}