using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using BusinessLogic.Interface;
using BusinessLogic.DTOs;
using System.Text.RegularExpressions;

namespace CVApi.Controllers
{
    [Route("api/compare-cv")]
    [ApiController]
    public class CVComparisonController : ControllerBase
    {
        private readonly ICVComparisonService _comparisonService;

        public CVComparisonController(ICVComparisonService comparisonService)
        {
            _comparisonService = comparisonService;
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
                return StatusCode(500, new { message = "An error occurred while creating the comparison.", error = ex.Message });
            }
        }

        // POST /api/compare-cv/auto
        [HttpPost("auto")]
        public async Task<ActionResult<CVComparison>> CompareCVAuto([FromBody] CreateAutoCVComparisonDTO dto)
        {
            try
            {
                var result = await _comparisonService.CreateAutoCVComparisonAsync(dto);
                var score = result.MatchScore ?? 0;
                var isMatch = score >= 60;
                var matchMessage = isMatch
                    ? $"Match found ({score}%)."
                    : $"Not a strong match yet ({score}%).";
                var matchedKeywords = ExtractKeywordList(result.Strengths, "Matched keywords:");
                var missingKeywords = ExtractKeywordList(result.Weaknesses, "Missing keywords:");
                var improvementSuggestions = BuildImprovementSuggestions(score, missingKeywords);

                return Ok(new
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
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while auto-comparing the CV.", error = ex.Message });
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
                {
                    return NotFound(new { message = "Comparison not found." });
                }
                return Ok(comparison);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the comparison.", error = ex.Message });
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
                return StatusCode(500, new { message = "An error occurred while retrieving comparisons.", error = ex.Message });
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
    }
}

