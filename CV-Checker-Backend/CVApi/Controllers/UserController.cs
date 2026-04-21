using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using DAL.Api;
using CVApi.Contracts.Users;
using Microsoft.EntityFrameworkCore;

namespace CVApi.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApiContext _context;

        public UserController(ApiContext context)
        {
            _context = context;
        }

        [HttpPost]
        public ActionResult CreateUser()
        {
            return BadRequest("Use POST api/auth/register to create a user.");
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserResponse>> GetUserById(Guid id)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(new UserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                CreatedAt = user.CreatedAt
            });
        }
    }
}