using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CVApi.Contracts.Auth;
using DAL.Api;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CVApi.Controllers;

[ApiController]
[Route("api/auth")]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly ApiContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtOptions _jwt;

    public AuthController(ApiContext db, IPasswordHasher<User> passwordHasher, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt.Value;
    }

    [HttpPost("register")]
    public async Task<ActionResult<object>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Email and password are required.");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var existing = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);

            if (existing != null)
                return Conflict("Email already registered.");

            var now = DateTime.UtcNow;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber,
                CreatedAt = now,
                UpdatedAt = now
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var (token, expiresAtUtc) = CreateJwt(user);

            return Ok(new AuthResponse
            {
                Token = token,
                ExpiresAtUtc = expiresAtUtc,
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = ex.Message,
                inner = ex.InnerException?.Message,
                stack = ex.StackTrace
            });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            Console.WriteLine("=== LOGIN HIT ===");
            Console.WriteLine($"Email: {request.Email}");

            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Email and password are required.");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);
            if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                Console.WriteLine("User not found or password hash missing.");
                return Unauthorized();
            }

            var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verify == PasswordVerificationResult.Failed)
            {
                Console.WriteLine("Password verification failed.");
                return Unauthorized();
            }

            var (token, expiresAtUtc) = CreateJwt(user);
            Console.WriteLine("Login successful, JWT created.");

            return Ok(new AuthResponse
            {
                Token = token,
                ExpiresAtUtc = expiresAtUtc,
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("=== LOGIN ERROR ===");
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.InnerException?.Message);
            Console.WriteLine(ex.StackTrace);

            return StatusCode(500, new
            {
                message = "An error occurred while logging in.",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }

    [HttpGet("register-status")]
    public async Task<ActionResult<RegisterStatusResponse>> RegisterStatus([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Query parameter email is required." });

        var normalized = email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Email != null && u.Email.ToLower() == normalized);
        return Ok(new RegisterStatusResponse { Registered = exists });
    }

    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        return Ok(new { message = "If the email exists, you will receive reset instructions." });
    }

    private (string token, DateTime expiresAtUtc) CreateJwt(User user)
    {
        if (string.IsNullOrWhiteSpace(_jwt.Key) || _jwt.Key.Length < 32)
            throw new InvalidOperationException("JWT key not configured (Jwt:Key must be at least 32 chars).");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_jwt.ExpiresMinutes <= 0 ? 60 : _jwt.ExpiresMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new("name", user.Name ?? "")
        };

        var jwt = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}

public sealed class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpiresMinutes { get; set; } = 60;
}