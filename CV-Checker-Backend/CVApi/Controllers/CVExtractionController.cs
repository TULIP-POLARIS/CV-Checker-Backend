using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessLogic.Interface;
using BusinessLogic.Services;

namespace CVApi.Controllers
{
    [ApiController]
    [Route("api/cv")]
    [Authorize]
    public class CVExtractionController : ControllerBase
    {
        private readonly ICVService _cvService;
        private readonly CVExtractionRunner _runner;

        public CVExtractionController(ICVService cvService)
        {
            _cvService = cvService;
            _runner = new CVExtractionRunner();
        }

        [HttpPost("{id}/extract")]
        public async Task<IActionResult> ExtractCv(Guid id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrWhiteSpace(userIdClaim))
                    return Unauthorized(new { message = "Invalid token." });

                var cv = await _cvService.GetByIdAsync(id);

                if (cv == null)
                    return NotFound(new { message = "CV not found." });

                if (cv.UserId.ToString() != userIdClaim)
                    return Forbid();

                if (string.IsNullOrWhiteSpace(cv.FilePath))
                    return BadRequest(new { message = "CV file path is missing." });

                var result = await _runner.ExtractFromFileAsync(cv.FilePath);

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    return StatusCode(500, new
                    {
                        message = "CV extraction failed.",
                        error = result.Error
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while extracting CV.",
                    error = ex.Message
                });
            }
        }
    }
}