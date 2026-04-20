using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using CVApi.Auth;

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
                var userId = HttpUser.GetUserId(User);
                if (userId == null)
                    return Unauthorized(new { message = "Invalid token." });

                var cv = await _cvService.GetByIdAsync(id);

                if (cv == null)
                    return NotFound(new { message = "CV not found." });

                if (cv.UserId != userId)
                    return Forbid();

                string? tempPath = null;
                try
                {
                    var pathToExtract = cv.FilePath;
                    if (string.IsNullOrWhiteSpace(pathToExtract))
                    {
                        if (cv.FileData == null || cv.FileData.Length == 0)
                            return BadRequest(new { message = "CV file path and file data are both missing." });

                        tempPath = Path.Combine(Path.GetTempPath(), $"{cv.Id}.pdf");
                        await System.IO.File.WriteAllBytesAsync(tempPath, cv.FileData);
                        pathToExtract = tempPath;
                    }

                    var result = await _runner.ExtractFromFileAsync(pathToExtract);

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
                finally
                {
                    if (!string.IsNullOrWhiteSpace(tempPath) && System.IO.File.Exists(tempPath))
                        System.IO.File.Delete(tempPath);
                }
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