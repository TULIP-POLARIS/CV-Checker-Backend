using BusinessLogic.DTOs;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using CVApi.Contracts.Auth;
using CVApi.Controllers;
using DAL.Api;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CVApi.Tests;

public class CVControllerTests
{
    [Fact]
    public async Task GetGeneratedStats_ReturnsDailyStatsGroupedByDate()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<ApiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApiContext(dbOptions);
        db.CvGenerations.AddRange(
            new CvGenerated
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CreatedAt = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new CvGenerated
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CreatedAt = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc)
            },
            new CvGenerated
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                CreatedAt = new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc)
            }
        );
        await db.SaveChangesAsync();

        var controller = new CVController(new FakeCvService(), new FakeJobOfferService(), db, new CVExtractionRunner());

        // Act
        var action = await controller.GetGeneratedStats();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(action);
        var dailyStatsObj = ok.Value!.GetType().GetProperty("dailyStats")!.GetValue(ok.Value)!;
        var entries = ((IEnumerable<object>)dailyStatsObj).ToList();

        Assert.Equal(2, entries.Count);
        Assert.Equal("2026-04-01", entries[0].GetType().GetProperty("date")!.GetValue(entries[0])!.ToString());
        Assert.Equal(2, (int)entries[0].GetType().GetProperty("generatedCvs")!.GetValue(entries[0])!);
        Assert.Equal(1, (int)entries[0].GetType().GetProperty("users")!.GetValue(entries[0])!);

        Assert.Equal("2026-04-02", entries[1].GetType().GetProperty("date")!.GetValue(entries[1])!.ToString());
        Assert.Equal(1, (int)entries[1].GetType().GetProperty("generatedCvs")!.GetValue(entries[1])!);
        Assert.Equal(1, (int)entries[1].GetType().GetProperty("users")!.GetValue(entries[1])!);
    }

    [Fact]
    public async Task GetGeneratedStats_ReturnsEmptyDailyStats_WhenNoRowsExist()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<ApiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApiContext(dbOptions);

        var controller = new CVController(new FakeCvService(), new FakeJobOfferService(), db, new CVExtractionRunner());

        // Act
        var action = await controller.GetGeneratedStats();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(action);
        var dailyStatsObj = ok.Value!.GetType().GetProperty("dailyStats")!.GetValue(ok.Value)!;
        var entries = ((IEnumerable<object>)dailyStatsObj).ToList();
        Assert.Empty(entries);
    }
}

public class CVComparisonControllerTests
{
    [Fact]
    public async Task CompareCVAutoWithCv_ReturnsBadRequest_WhenCvIdAndCvFileAreMissing()
    {
        // Arrange
        var controller = new CVComparisonController(new FakeComparisonService(), new FakeCvService());
        var request = new CreateAutoCVComparisonFormDTO
        {
            CVId = Guid.Empty,
            JobOfferId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CVFile = null
        };

        // Act
        var action = await controller.CompareCVAutoWithCv(request);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        var message = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)?.ToString();
        Assert.Equal("Provide either CVId or CVFile.", message);
    }

    [Fact]
    public async Task CompareCVAutoWithCv_ReturnsBadRequest_WhenJobOfferIdMissing()
    {
        // Arrange
        var controller = new CVComparisonController(new FakeComparisonService(), new FakeCvService());
        var request = new CreateAutoCVComparisonFormDTO
        {
            CVId = Guid.NewGuid(),
            JobOfferId = Guid.Empty,
            UserId = Guid.NewGuid()
        };

        // Act
        var action = await controller.CompareCVAutoWithCv(request);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        var message = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)?.ToString();
        Assert.Equal("JobOfferId is required.", message);
    }

    [Fact]
    public async Task CompareCVAutoWithCv_ReturnsBadRequest_WhenUserIdMissing()
    {
        // Arrange
        var controller = new CVComparisonController(new FakeComparisonService(), new FakeCvService());
        var request = new CreateAutoCVComparisonFormDTO
        {
            CVId = Guid.NewGuid(),
            JobOfferId = Guid.NewGuid(),
            UserId = Guid.Empty
        };

        // Act
        var action = await controller.CompareCVAutoWithCv(request);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        var message = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)?.ToString();
        Assert.Equal("UserId is required.", message);
    }

    [Fact]
    public async Task CompareCVAutoWithCv_ReturnsBadRequest_WhenFileIsNotPdf()
    {
        // Arrange
        var controller = new CVComparisonController(new FakeComparisonService(), new FakeCvService());
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var file = new FormFile(stream, 0, stream.Length, "cvFile", "cv.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
        var request = new CreateAutoCVComparisonFormDTO
        {
            CVId = Guid.Empty,
            JobOfferId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CVFile = file
        };

        // Act
        var action = await controller.CompareCVAutoWithCv(request);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        var message = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)?.ToString();
        Assert.Equal("Only PDF files are allowed.", message);
    }

    [Fact]
    public async Task CompareCVAutoWithCv_ReturnsOk_WhenCvIdIsProvided()
    {
        // Arrange
        var controller = new CVComparisonController(new FakeComparisonService(), new FakeCvService());
        var request = new CreateAutoCVComparisonFormDTO
        {
            CVId = Guid.NewGuid(),
            JobOfferId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        // Act
        var action = await controller.CompareCVAutoWithCv(request);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.NotNull(ok.Value);
    }
}

public class AuthControllerTests
{
    [Fact]
    public async Task ForgotPassword_ReturnsOk_WhenEmailExists()
    {
        await using var db = BuildDbContext();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            PasswordHash = "hash"
        });
        await db.SaveChangesAsync();

        var controller = BuildAuthController(db);
        var action = await controller.ForgotPassword(new ForgotPasswordRequest { Email = "existing@test.com" });

        var ok = Assert.IsType<OkObjectResult>(action);
        var userExists = (bool)(ok.Value!.GetType().GetProperty("userExists")!.GetValue(ok.Value)!);
        Assert.True(userExists);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsNotFound_WhenEmailDoesNotExist()
    {
        await using var db = BuildDbContext();
        var controller = BuildAuthController(db);

        var action = await controller.ForgotPassword(new ForgotPasswordRequest { Email = "missing@test.com" });

        var notFound = Assert.IsType<NotFoundObjectResult>(action);
        var userExists = (bool)(notFound.Value!.GetType().GetProperty("userExists")!.GetValue(notFound.Value)!);
        Assert.False(userExists);
    }

    [Fact]
    public async Task ResetPassword_ChangesStoredPassword_WhenEmailExists()
    {
        await using var db = BuildDbContext();
        var seededUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            PasswordHash = "old-hash"
        };
        db.Users.Add(seededUser);
        await db.SaveChangesAsync();

        var controller = BuildAuthController(db);
        var action = await controller.ResetPassword("existing@test.com", new ResetPasswordRequest
        {
            NewPassword = "NewPassword123!"
        });

        Assert.IsType<OkObjectResult>(action);
        var updatedUser = await db.Users.FirstAsync(u => u.Email == "existing@test.com");
        Assert.NotEqual("old-hash", updatedUser.PasswordHash);

        var hasher = new PasswordHasher<User>();
        var verify = hasher.VerifyHashedPassword(updatedUser, updatedUser.PasswordHash!, "NewPassword123!");
        Assert.NotEqual(PasswordVerificationResult.Failed, verify);
    }

    [Fact]
    public async Task ResetPassword_ReturnsNotFound_WhenEmailDoesNotExist()
    {
        await using var db = BuildDbContext();
        var controller = BuildAuthController(db);

        var action = await controller.ResetPassword("missing@test.com", new ResetPasswordRequest
        {
            NewPassword = "NewPassword123!"
        });

        Assert.IsType<NotFoundObjectResult>(action);
    }

    private static ApiContext BuildDbContext()
    {
        var dbOptions = new DbContextOptionsBuilder<ApiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApiContext(dbOptions);
    }

    private static AuthController BuildAuthController(ApiContext db)
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Key = "this-is-a-test-key-with-at-least-32-characters",
            Issuer = "test",
            Audience = "test",
            ExpiresMinutes = 60
        });

        return new AuthController(db, new PasswordHasher<User>(), jwtOptions);
    }
}

internal sealed class FakeCvService : ICVService
{
    public Task<CV?> GetByIdAsync(Guid id) => Task.FromResult<CV?>(null);
    public Task<IEnumerable<CV>> GetByUserIdAsync(Guid userId) => Task.FromResult<IEnumerable<CV>>(Array.Empty<CV>());
    public Task<CV> CreateCVAsync(CV cv) => Task.FromResult(cv);
    public Task<CV> UpdateCVAsync(CV cv) => Task.FromResult(cv);
    public Task<bool> DeleteCVAsync(Guid id) => Task.FromResult(true);
}

internal sealed class FakeComparisonService : ICVComparisonService
{
    public Task<CVComparison?> GetByIdAsync(Guid id) => Task.FromResult<CVComparison?>(null);
    public Task<IEnumerable<CVComparison>> GetByUserIdAsync(Guid userId) => Task.FromResult<IEnumerable<CVComparison>>(Array.Empty<CVComparison>());
    public Task<CVComparison> CreateCVComparisonAsync(CVComparison comparison) => Task.FromResult(comparison);
    public Task<CVComparison> CreateAutoCVComparisonAsync(CreateAutoCVComparisonDTO dto)
    {
        return Task.FromResult(new CVComparison
        {
            Id = Guid.NewGuid(),
            CVId = dto.CVId,
            JobOfferId = dto.JobOfferId,
            UserId = dto.UserId,
            MatchScore = 70,
            CreatedAt = DateTime.UtcNow
        });
    }
}

internal sealed class FakeJobOfferService : IJobOfferService
{
    public Task<JobOffer?> GetByIdAsync(Guid id) => Task.FromResult<JobOffer?>(null);
    public Task<IEnumerable<JobOffer>> GetAllAsync() => Task.FromResult<IEnumerable<JobOffer>>(Array.Empty<JobOffer>());
    public Task<JobOffer> CreateJobOfferAsync(JobOffer jobOffer) => Task.FromResult(jobOffer);
    public Task<JobOffer> UpdateJobOfferAsync(JobOffer jobOffer) => Task.FromResult(jobOffer);
    public Task<bool> DeleteJobOfferAsync(Guid id) => Task.FromResult(true);
}
