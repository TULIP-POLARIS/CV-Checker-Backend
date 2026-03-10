using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using BusinessLogic.Interface;
using BusinessLogic.DTOs;

namespace CVApi.Controllers
{
    [Route("api/cv")]
    [ApiController]
    public class CVController : ControllerBase
    {
        private readonly ICVService _cvService;

        public CVController(ICVService cvService)
        {
            _cvService = cvService;
        }

        // POST /api/cv/upload
        [HttpPost("upload")]
        public async Task<ActionResult<CV>> UploadCV([FromBody] CreateCVDTO dto)
        {
            try
            {
                var cv = new CV
                {
                    Id = Guid.NewGuid(),
                    UserId = dto.UserId,
                    FileName = dto.FileName,
                    FilePath = dto.FilePath,
                    TemplateId = dto.TemplateId,
                    Content = dto.Content,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _cvService.CreateCVAsync(cv);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while uploading the CV.", error = ex.Message });
            }
        }

        // GET /api/cv/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<CV>> GetCV(Guid id)
        {
            try
            {
                var cv = await _cvService.GetByIdAsync(id);
                if (cv == null)
                {
                    return NotFound(new { message = "CV not found." });
                }
                return Ok(cv);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the CV.", error = ex.Message });
            }
        }

        // GET /api/cv/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<CV>>> GetCVsByUser(Guid userId)
        {
            try
            {
                var cvs = await _cvService.GetByUserIdAsync(userId);
                return Ok(cvs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving CVs.", error = ex.Message });
            }
        }
    }
}
