using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using BusinessLogic.Interface;
using BusinessLogic.DTOs;

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
    }
}

