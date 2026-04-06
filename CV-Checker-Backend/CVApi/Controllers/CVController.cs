using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using BusinessLogic.Interface;
using BusinessLogic.DTOs;
using System.IO;

namespace CVApi.Controllers
{
    [Route("api/cv")]
    [ApiController]
    public class CVController : ControllerBase
    {
        private const long MaxPdfSizeBytes = 10 * 1024 * 1024; // 10 MB
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

        // POST /api/cv/upload-pdf
        [HttpPost("upload-pdf")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<CV>> UploadCVPdf([FromForm] UploadCVPdfDTO dto)
        {
            try
            {
                if (dto.File == null || dto.File.Length == 0)
                    return BadRequest(new { message = "PDF file is required." });

                if (dto.UserId == Guid.Empty)
                    return BadRequest(new { message = "UserId is required." });

                if (dto.File.Length > MaxPdfSizeBytes)
                    return BadRequest(new { message = "PDF too large. Max allowed size is 10MB." });

                var extension = Path.GetExtension(dto.File.FileName);
                if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Only PDF files are allowed." });

                await using var ms = new MemoryStream();
                await dto.File.CopyToAsync(ms);

                var cv = new CV
                {
                    Id = Guid.NewGuid(),
                    UserId = dto.UserId,
                    FileName = dto.File.FileName,
                    TemplateId = dto.TemplateId,
                    FileData = ms.ToArray(),
                    ContentType = string.IsNullOrWhiteSpace(dto.File.ContentType)
                        ? "application/pdf"
                        : dto.File.ContentType,
                    FileSizeBytes = dto.File.Length,
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
                return StatusCode(500, new { message = "An error occurred while uploading the PDF CV.", error = ex.Message });
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

        // GET /api/cv/{id}/file
        [HttpGet("{id}/file")]
        public async Task<IActionResult> DownloadCVFile(Guid id)
        {
            try
            {
                var cv = await _cvService.GetByIdAsync(id);
                if (cv == null)
                    return NotFound(new { message = "CV not found." });

                if (cv.FileData == null || cv.FileData.Length == 0)
                    return NotFound(new { message = "CV file not found." });

                var contentType = string.IsNullOrWhiteSpace(cv.ContentType) ? "application/pdf" : cv.ContentType;
                var fileName = string.IsNullOrWhiteSpace(cv.FileName) ? $"cv-{id}.pdf" : cv.FileName;

                return File(cv.FileData, contentType, fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while downloading CV file.", error = ex.Message });
            }
        }
    }
}
