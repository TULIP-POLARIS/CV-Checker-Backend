using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using DAL.Api;
using BusinessLogic;

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

        // Create a new user
        [HttpPost]
        public JsonResult CreateUser(User user)
            {
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Name = user.Name,
                Email = user.Email,
                PasswordHash = user.PasswordHash
            };
            _context.Users.Add(newUser);
            _context.SaveChanges();
            return new JsonResult(Ok(newUser));
        }

        // Get a user by ID

        [HttpGet("user")]
        public JsonResult GetUserById(Guid id)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return new JsonResult(NotFound());
            }
            return new JsonResult(Ok(user));
        }
    }
}

