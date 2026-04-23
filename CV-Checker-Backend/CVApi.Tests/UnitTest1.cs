using BusinessLogic.DTOs;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using CVApi.Contracts.Auth;
using CVApi.Controllers;
using DAL.Api;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;

namespace CVApi.Tests;

public class CVControllerTests
{
    [Fact]
    public async Task GetGeneratedStats_ReturnsDailyStatsGroupedByDate()
    {
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

        var action = await controller.GetGeneratedStats();

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
        var dbOptions = new DbContextOptionsBuilder<ApiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApiContext(dbOptions);

        var controller = new CVController(new FakeCvService(), new FakeJobOfferService(), db, new CVExtractionRunner());

        var action = await controller.GetGeneratedStats();

        var ok = Assert.IsType<OkObjectResult>(action);
        var dailyStatsObj = ok.Value!.GetType().GetProperty("dailyStats")!.GetValue(ok.Value)!;
        var entries = ((IEnumerable<object>)dailyStatsObj).ToList();

        Assert.Empty(entries);
    }

    [Fact]
    public async Task Generate_UsesProfileData_WhenProfileExists()
    {
        var userId = Guid.NewGuid();
        var dbOptions = new DbContextOptionsBuilder<ApiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApiContext(dbOptions);

        db.PersonalInfos.Add(new PersonalInfo
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirstName = "Profile",
            LastName = "User",
            PhoneNumber = "111-111",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Skills.Add(new Skill
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "C#",
            Level = "Advanced",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.CVs.Add(new CV
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Content = "{\"FullName\":\"AI Name\",\"Phone\":\"999\",\"Skills\":[\"Python\",\"SQL\"]}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var controller = BuildCvController(db, userId);
        var action = await controller.Generate(new CVController.GenerateBody
        {
            JobTitle = "Backend",
            JobDescription = "Build APIs"
        });

        var ok = Assert.IsType<OkObjectResult>(action);
        var generatedCv = ok.Value!.GetType().GetProperty("generatedCv")!.GetValue(ok.Value)!;
        var sections = generatedCv.GetType().GetProperty("sections")!.GetValue(generatedCv)!;
        var profile = sections.GetType().GetProperty("profile")!.GetValue(sections)!;
        var skillsObj = sections.GetType().GetProperty("skills")!.GetValue(sections)!;
        var skillsList = ((IEnumerable<object>)skillsObj).ToList();

        Assert.NotEmpty(skillsList);

        var firstSkill = skillsList.First();

        Assert.Equal("Profile User", profile.GetType().GetProperty("fullName")!.GetValue(profile)!.ToString());
        Assert.Equal("111-111", profile.GetType().GetProperty("phoneNumber")!.GetValue(profile)!.ToString());
        Assert.Equal("C#", firstSkill.GetType().GetProperty("name")!.GetValue(firstSkill)!.ToString());
        Assert.Equal("profile", profile.GetType().GetProperty("source")!.GetValue(profile)!.ToString());
    }

    [Fact]
    public async Task Generate_UsesUploadedCvAiData_WhenProfileDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var dbOptions = new DbContextOptionsBuilder<ApiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApiContext(dbOptions);

        db.CVs.Add(new CV
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Content = "{\"FullName\":\"AI Name\",\"Phone\":\"999\",\"Skills\":[\"Python\",\"SQL\"],\"WorkExperience\":\"AI Work\",\"Education\":\"AI Edu\"}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var controller = BuildCvController(db, userId);
        var action = await controller.Generate(new CVController.GenerateBody
        {
            JobTitle = "Backend",
            JobDescription = "Build APIs"
        });

        var ok = Assert.IsType<OkObjectResult>(action);
        var generatedCv = ok.Value!.GetType().GetProperty("generatedCv")!.GetValue(ok.Value)!;
        var sections = generatedCv.GetType().GetProperty("sections")!.GetValue(generatedCv)!;
        var profile = sections.GetType().GetProperty("profile")!.GetValue(sections)!;
        var skillsObj = sections.GetType().GetProperty("skills")!.GetValue(sections)!;
        var skillsList = ((IEnumerable<object>)skillsObj).ToList();

        Assert.NotEmpty(skillsList);

        var firstSkill = skillsList.First();

        Assert.Equal("AI Name", profile.GetType().GetProperty("fullName")!.GetValue(profile)!.ToString());
        Assert.Equal("999", profile.GetType().GetProperty("phoneNumber")!.GetValue(profile)!.ToString());
        Assert.Equal("Python", firstSkill.GetType().GetProperty("name")!.GetValue(firstSkill)!.ToString());
        Assert.Equal("ai-fallback", profile.GetType().GetProperty("source")!.GetValue(profile)!.ToString());
    }

    [Fact]
    public async Task Generate_UsesUploadedCvAiData_WhenPersonalRowExistsButIsEmpty()
    {
        var userId = Guid.NewGuid();
        var dbOptions = new DbContextOptionsBuilder<ApiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApiContext(dbOptions);

        db.PersonalInfos.Add(new PersonalInfo
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirstName = "",
            LastName = "",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.CVs.Add(new CV
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Content = "{\"FullName\":\"AI Name\",\"Phone\":\"999\",\"Skills\":[\"Python\",\"SQL\"]}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var controller = BuildCvController(db, userId);
        var action = await controller.Generate(new CVController.GenerateBody
        {
            JobTitle = "Backend",
            JobDescription = "Build APIs"
        });

        var ok = Assert.IsType<OkObjectResult>(action);
        var generatedCv = ok.Value!.GetType().GetProperty("generatedCv")!.GetValue(ok.Value)!;
        var sections = generatedCv.GetType().GetProperty("sections")!.GetValue(generatedCv)!;
        var profile = sections.GetType().GetProperty("profile")!.GetValue(sections)!;
        var skillsObj = sections.GetType().GetProperty("skills")!.GetValue(sections)!;
        var skillsList = ((IEnumerable<object>)skillsObj).ToList();

        Assert.NotEmpty(skillsList);

        var firstSkill = skillsList.First();

        Assert.Equal("AI Name", profile.GetType().GetProperty("fullName")!.GetValue(profile)!.ToString());
        Assert.Equal("999", profile.GetType().GetProperty("phoneNumber")!.GetValue(profile)!.ToString());
        Assert.Equal("Python", firstSkill.GetType().GetProperty("name")!.GetValue(firstSkill)!.ToString());
        Assert.Equal("ai-fallback", profile.GetType().GetProperty("source")!.GetValue(profile)!.ToString());
    }

    private static CVController BuildCvController(ApiContext db, Guid userId)
    {
        var controller = new CVController(new FakeCvService(), new FakeJobOfferService(), db, new CVExtractionRunner());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "TestAuth"))
            }
        };
        return controller;
    }
}

public class CVComparisonControllerTests
{
    [Fact]
    public async Task CompareCVAutoWithCvAndJobOffer_ReturnsBadRequest_WhenUserIdMissing()
    {
        var controller = new CVComparisonController(
            new FakeComparisonService(),
            new FakeCvService(),
            new FakeJobOfferService());

        var request = new CreateAutoCVComparisonUploadDTO
        {
            UserId = Guid.Empty,
            CVFile = BuildPdfFile("cv.pdf"),
            Title = "Backend Developer",
            Description = "Build APIs"
        };

        var action = await controller.CompareCVAutoWithCvAndJobOffer(request);

        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        var message = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)?.ToString();

        Assert.Equal("UserId is required.", message);
    }

    [Fact]
    public async Task CompareCVAutoWithCvAndJobOffer_ReturnsBadRequest_WhenCvFileMissing()
    {
        var controller = new CVComparisonController(
            new FakeComparisonService(),
            new FakeCvService(),
            new FakeJobOfferService());

        var request = new CreateAutoCVComparisonUploadDTO
        {
            UserId = Guid.NewGuid(),
            CVFile = null,
            Title = "Backend Developer",
            Description = "Build APIs"
        };

        var action = await controller.CompareCVAutoWithCvAndJobOffer(request);

        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        var message = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)?.ToString();

        Assert.Equal("CVFile is required.", message);
    }

    [Fact]
    public async Task CompareCVAutoWithCvAndJobOffer_ReturnsBadRequest_WhenFileIsNotPdf()
    {
        var controller = new CVComparisonController(
            new FakeComparisonService(),
            new FakeCvService(),
            new FakeJobOfferService());

        await using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var file = new FormFile(stream, 0, stream.Length, "cvFile", "cv.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var request = new CreateAutoCVComparisonUploadDTO
        {
            UserId = Guid.NewGuid(),
            CVFile = file,
            Title = "Backend Developer",
            Description = "Build APIs"
        };

        var action = await controller.CompareCVAutoWithCvAndJobOffer(request);

        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        var message = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)?.ToString();

        Assert.Equal("Only PDF files are allowed.", message);
    }

    [Fact]
    public async Task CompareCVAutoWithCvAndJobOffer_ReturnsBadRequest_WhenTitleMissing()
    {
        var controller = new CVComparisonController(
            new FakeComparisonService(),
            new FakeCvService(),
            new FakeJobOfferService());

        var request = new CreateAutoCVComparisonUploadDTO
        {
            UserId = Guid.NewGuid(),
            CVFile = BuildPdfFile("cv.pdf"),
            Title = "",
            Description = "Build APIs"
        };

        var action = await controller.CompareCVAutoWithCvAndJobOffer(request);

        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        var message = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)?.ToString();

        Assert.Equal("Job offer title is required.", message);
    }

    [Fact]
    public async Task CompareCVAutoWithCvAndJobOffer_ReturnsBadRequest_WhenDescriptionMissing()
    {
        var controller = new CVComparisonController(
            new FakeComparisonService(),
            new FakeCvService(),
            new FakeJobOfferService());

        var request = new CreateAutoCVComparisonUploadDTO
        {
            UserId = Guid.NewGuid(),
            CVFile = BuildPdfFile("cv.pdf"),
            Title = "Backend Developer",
            Description = ""
        };

        var action = await controller.CompareCVAutoWithCvAndJobOffer(request);

        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        var message = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)?.ToString();

        Assert.Equal("Job offer description is required.", message);
    }

    [Fact]
    public async Task CompareCVAutoWithCvAndJobOffer_ReturnsOk_WhenValidUploadIsProvided()
    {
        var controller = new CVComparisonController(
            new FakeComparisonService(),
            new FakeCvService(),
            new FakeJobOfferService());

        var request = new CreateAutoCVComparisonUploadDTO
        {
            UserId = Guid.NewGuid(),
            CVFile = BuildPdfFile("cv.pdf"),
            Title = "Backend Developer",
            Company = "Test Company",
            Description = "Build APIs",
            Requirements = "C#, ASP.NET Core, SQL",
            Location = "Finland"
        };

        var action = await controller.CompareCVAutoWithCvAndJobOffer(request);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task GetComparisonsByUserPost_ReturnsOk()
    {
        var controller = new CVComparisonController(
            new FakeComparisonService(),
            new FakeCvService(),
            new FakeJobOfferService());

        var userId = Guid.NewGuid();

        var action = await controller.GetComparisonsByUserPost(userId);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.NotNull(ok.Value);
    }

    private static IFormFile BuildPdfFile(string fileName)
    {
        var pdfBytes = Encoding.UTF8.GetBytes(
@"%PDF-1.1
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Count 1 /Kids [3 0 R] >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] >>
endobj
xref
0 4
0000000000 65535 f 
0000000010 00000 n 
0000000060 00000 n 
0000000117 00000 n 
trailer
<< /Root 1 0 R /Size 4 >>
startxref
178
%%EOF");

        var stream = new MemoryStream(pdfBytes);

        return new FormFile(stream, 0, stream.Length, "CVFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
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

    public Task<IEnumerable<CV>> GetByUserIdAsync(Guid userId) =>
        Task.FromResult<IEnumerable<CV>>(Array.Empty<CV>());

    public Task<CV> CreateCVAsync(CV cv) => Task.FromResult(cv);

    public Task<CV> UpdateCVAsync(CV cv) => Task.FromResult(cv);

    public Task<bool> DeleteCVAsync(Guid id) => Task.FromResult(true);
}

internal sealed class FakeComparisonService : ICVComparisonService
{
    public Task<CVComparison?> GetByIdAsync(Guid id) => Task.FromResult<CVComparison?>(null);

    public Task<IEnumerable<CVComparison>> GetByUserIdAsync(Guid userId) =>
        Task.FromResult<IEnumerable<CVComparison>>(Array.Empty<CVComparison>());

    public Task<CVComparison> CreateCVComparisonAsync(CVComparison comparison) =>
        Task.FromResult(comparison);

    public Task<CVComparison> CreateAutoCVComparisonAsync(CreateAutoCVComparisonDTO dto)
    {
        return Task.FromResult(new CVComparison
        {
            Id = Guid.NewGuid(),
            CVId = dto.CVId,
            JobOfferId = dto.JobOfferId,
            UserId = dto.UserId,
            MatchScore = 70,
            Strengths = "Matched keywords: C#, ASP.NET Core",
            Weaknesses = "Missing keywords: Azure",
            Suggestions = "Add more cloud experience",
            AnalysisResult = "Good match",
            CreatedAt = DateTime.UtcNow
        });
    }
}

internal sealed class FakeJobOfferService : IJobOfferService
{
    public Task<JobOffer?> GetByIdAsync(Guid id) => Task.FromResult<JobOffer?>(null);

    public Task<IEnumerable<JobOffer>> GetAllAsync() =>
        Task.FromResult<IEnumerable<JobOffer>>(Array.Empty<JobOffer>());

    public Task<JobOffer> CreateJobOfferAsync(JobOffer jobOffer) => Task.FromResult(jobOffer);

    public Task<JobOffer> UpdateJobOfferAsync(JobOffer jobOffer) => Task.FromResult(jobOffer);

    public Task<bool> DeleteJobOfferAsync(Guid id) => Task.FromResult(true);
}