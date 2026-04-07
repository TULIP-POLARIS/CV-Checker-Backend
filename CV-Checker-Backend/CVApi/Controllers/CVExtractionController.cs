using Microsoft.AspNetCore.Mvc;
using BusinessLogic.Services;
using Domain.Entities;
using BusinessLogic.Interface;

namespace CVApi.Controllers
{
    public class CVExtractionController : Controller //extraction only
    {
        [ApiController]
        [Route("api/cv-extraction")]
        public class CvExtractionController : ControllerBase
        {
            private readonly ICVService _cvService;
            private readonly CVExtractionRunner _runner;

            public CvExtractionController(ICVService cvService)
            {
                _cvService = cvService;
                _runner = new CVExtractionRunner();
            }

            // POST /api/cv-extraction/{id}
            [HttpPost("{id}")]
            public async Task<IActionResult> ExtractCv(Guid id)
            {
                try
                {
                    var cv = await _cvService.GetByIdAsync(id);

                    if (cv == null)
                        return NotFound(new { message = "CV not found." });

                    if (cv.FileData == null || cv.FileData.Length == 0)
                        return NotFound(new { message = "CV file not found." });

                    CVExtractionResult result = await _runner.ExtractFromBytesAsync(cv.FileData, cv.FileName);

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
                        message = "An error occurred while extracting CV data.",
                        error = ex.Message
                    });
                }
            }
        }
    }
}
