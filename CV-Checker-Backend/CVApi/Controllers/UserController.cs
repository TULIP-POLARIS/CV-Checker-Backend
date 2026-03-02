using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BusinessLogic.Entities;
using BusinessLogic;

namespace CVApi.Controllers
{
    [Route("api/[controller]")]
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
    }
}

