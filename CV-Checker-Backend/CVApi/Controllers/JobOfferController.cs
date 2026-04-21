using BusinessLayer.Services;
using BusinessLogic.DTOs;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CVApi.Controllers
{
    [Route("api/joboffers")]
    [ApiController]
    [Authorize]
    public class JobOfferController : ControllerBase
    {
        private readonly IJobOfferService _jobOfferService;
        private readonly JobOfferReadinessService _jobOfferReadinessService;
        private readonly CVGenerationService _cvGenerationService;

        public JobOfferController(
            IJobOfferService jobOfferService,
            JobOfferReadinessService jobOfferReadinessService,
            CVGenerationService cvGenerationService)
        {
            _jobOfferService = jobOfferService;
            _jobOfferReadinessService = jobOfferReadinessService;
            _cvGenerationService = cvGenerationService;
        }

        // POST /api/joboffers
        [HttpPost]
        public async Task<ActionResult<JobOffer>> CreateJobOffer([FromBody] CreateJobOfferDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                    return Unauthorized(new { message = "Invalid token." });

                var jobOffer = new JobOffer
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = dto.Title,
                    Company = dto.Company,
                    Description = dto.Description,
                    Requirements = dto.Requirements,
                    Location = dto.Location,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _jobOfferService.CreateJobOfferAsync(jobOffer);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the job offer.", error = ex.Message });
            }
        }

        // GET /api/joboffers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<JobOffer>>> GetJobOffers()
        {
            try
            {
                var jobOffers = await _jobOfferService.GetAllAsync();
                return Ok(jobOffers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving job offers.", error = ex.Message });
            }
        }

        // GET /api/joboffers/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<JobOffer>> GetJobOffer(Guid id)
        {
            try
            {
                var jobOffer = await _jobOfferService.GetByIdAsync(id);
                if (jobOffer == null)
                {
                    return NotFound(new { message = "Job offer not found." });
                }
                return Ok(jobOffer);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the job offer.", error = ex.Message });
            }
        }

        // POST /api/joboffers/{id}/check
        [HttpPost("{id}/check")]
        public async Task<IActionResult> CheckJobOffer(Guid id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                    return Unauthorized(new { message = "Invalid token." });

                var jobOffer = await _jobOfferService.GetByIdAsync(id);

                if (jobOffer == null)
                    return NotFound(new { message = "Job offer not found." });

                if (jobOffer.UserId != userId)
                    return Forbid();

                var readiness = await _jobOfferReadinessService.CheckUserReadinessAsync(userId);

                if (!readiness.CanProceed)
                    return BadRequest(readiness);

                return Ok(new
                {
                    message = "Profile is sufficient for job offer analysis.",
                    jobOfferId = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while checking the job offer.",
                    error = ex.Message
                });
            }
        }
        // POST /api/joboffers/generate-cv
        [HttpPost("generate-cv")]
        public async Task<IActionResult> GenerateCv()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                    return Unauthorized(new { message = "Invalid token." });

                var readiness = await _jobOfferReadinessService.CheckUserReadinessAsync(userId);

                if (!readiness.CanProceed)
                    return BadRequest(readiness);

                var generatedCv = await _cvGenerationService.BuildGeneratedCvAsync(userId);

                return Ok(generatedCv);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while generating CV data.",
                    error = ex.Message
                });
            }
        }
    }
}