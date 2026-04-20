//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Authorization;
//using System.Security.Claims;
//using Domain.Entities;
//using BusinessLogic.Interface;
//using BusinessLogic.DTOs;
//using BusinessLogic.Services;

//namespace CVApi.Controllers
//{
//    [Route("api/joboffers")]
//    [ApiController]
//    [Authorize]
//    public class JobOfferController : ControllerBase
//    {
//        private readonly IJobOfferService _jobOfferService;
//        private readonly JobOfferReadinessService _jobOfferReadinessService;

//        public JobOfferController(
//            IJobOfferService jobOfferService,
//            JobOfferReadinessService jobOfferReadinessService)
//        {
//            _jobOfferService = jobOfferService;
//            _jobOfferReadinessService = jobOfferReadinessService;
//        }

//        // POST /api/joboffers
//        [HttpPost]
//        public async Task<ActionResult<JobOfferResponseDTO>> CreateJobOffer([FromBody] CreateJobOfferDTO dto)
//        {
//            try
//            {
//                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

//                if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
//                    return Unauthorized(new { message = "Invalid token." });

//                var jobOffer = new JobOffer
//                {
//                    Id = Guid.NewGuid(),
//                    UserId = userId,
//                    Title = dto.Title,
//                    Company = dto.Company,
//                    Description = dto.Description,
//                    Requirements = dto.Requirements,
//                    Location = dto.Location,
//                    TextContent = $"{dto.Title} {dto.Description} {dto.Requirements} {dto.Location} {dto.Company}".Trim(),
//                    CreatedAt = DateTime.UtcNow
//                };

//                var result = await _jobOfferService.CreateJobOfferAsync(jobOffer);

//                var response = new JobOfferResponseDTO
//                {
//                    Id = result.Id,
//                    UserId = result.UserId,
//                    Title = result.Title,
//                    Company = result.Company,
//                    Description = result.Description,
//                    Requirements = result.Requirements,
//                    Location = result.Location,
//                    SourceFileId = result.SourceFileId,
//                    TextContent = result.TextContent,
//                    CreatedAt = result.CreatedAt,
//                    UpdatedAt = result.UpdatedAt
//                };

//                return Ok(response);
//            }
//            catch (ArgumentException ex)
//            {
//                return BadRequest(new { message = ex.Message });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    message = "An error occurred while creating the job offer.",
//                    error = ex.Message
//                });
//            }
//        }

//        // GET /api/joboffers
//        [HttpGet]
//        public async Task<ActionResult<IEnumerable<JobOfferResponseDTO>>> GetJobOffers()
//        {
//            try
//            {
//                var jobOffers = await _jobOfferService.GetAllAsync();

//                var response = jobOffers.Select(j => new JobOfferResponseDTO
//                {
//                    Id = j.Id,
//                    UserId = j.UserId,
//                    Title = j.Title,
//                    Company = j.Company,
//                    Description = j.Description,
//                    Requirements = j.Requirements,
//                    Location = j.Location,
//                    SourceFileId = j.SourceFileId,
//                    TextContent = j.TextContent,
//                    CreatedAt = j.CreatedAt,
//                    UpdatedAt = j.UpdatedAt
//                }).ToList();

//                return Ok(response);
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    message = "An error occurred while retrieving job offers.",
//                    error = ex.Message
//                });
//            }
//        }

//        // GET /api/joboffers/{id}
//        [HttpGet("{id}")]
//        public async Task<ActionResult<JobOfferResponseDTO>> GetJobOffer(Guid id)
//        {
//            try
//            {
//                var jobOffer = await _jobOfferService.GetByIdAsync(id);

//                if (jobOffer == null)
//                    return NotFound(new { message = "Job offer not found." });

//                var response = new JobOfferResponseDTO
//                {
//                    Id = jobOffer.Id,
//                    UserId = jobOffer.UserId,
//                    Title = jobOffer.Title,
//                    Company = jobOffer.Company,
//                    Description = jobOffer.Description,
//                    Requirements = jobOffer.Requirements,
//                    Location = jobOffer.Location,
//                    SourceFileId = jobOffer.SourceFileId,
//                    TextContent = jobOffer.TextContent,
//                    CreatedAt = jobOffer.CreatedAt,
//                    UpdatedAt = jobOffer.UpdatedAt
//                };

//                return Ok(response);
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    message = "An error occurred while retrieving the job offer.",
//                    error = ex.Message
//                });
//            }
//        }

//        // POST /api/joboffers/{id}/check
//        [HttpPost("{id}/check")]
//        public async Task<IActionResult> CheckJobOffer(Guid id)
//        {
//            try
//            {
//                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

//                if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
//                    return Unauthorized(new { message = "Invalid token." });

//                var jobOffer = await _jobOfferService.GetByIdAsync(id);

//                if (jobOffer == null)
//                    return NotFound(new { message = "Job offer not found." });

//                if (jobOffer.UserId != userId)
//                    return Forbid();

//                var readiness = await _jobOfferReadinessService.CheckUserReadinessAsync(userId);

//                if (!readiness.CanProceed)
//                    return BadRequest(readiness);

//                return Ok(new
//                {
//                    message = "Profile is sufficient for job offer analysis.",
//                    jobOfferId = id
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    message = "An error occurred while checking the job offer.",
//                    error = ex.Message
//                });
//            }
//        }
//    }
//}