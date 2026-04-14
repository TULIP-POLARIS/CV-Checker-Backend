using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using BusinessLogic.Interface;
using BusinessLogic.DTOs;
using CVApi.Auth;
using Microsoft.AspNetCore.Authorization;

namespace CVApi.Controllers
{
    [Route("api/joboffers")]
    [ApiController]
    [Authorize]
    public class JobOfferController : ControllerBase
    {
        private readonly IJobOfferService _jobOfferService;

        public JobOfferController(IJobOfferService jobOfferService)
        {
            _jobOfferService = jobOfferService;
        }

        // POST /api/joboffers
        [HttpPost]
        public async Task<ActionResult<JobOffer>> CreateJobOffer([FromBody] CreateJobOfferDTO dto)
        {
            try
            {
                var userId = HttpUser.GetUserId(User);
                if (userId == null)
                    return Unauthorized(new { message = "Invalid token." });

                var jobOffer = new JobOffer
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    Title = dto.Title,
                    Company = dto.Company,
                    Description = dto.Description,
                    Requirements = dto.Requirements,
                    Location = dto.Location,
                    TextContent = string.Join(
                        "\n",
                        new[] { dto.Title, dto.Description, dto.Requirements, dto.Location, dto.Company }
                            .Where(v => !string.IsNullOrWhiteSpace(v))),
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
    }
}

