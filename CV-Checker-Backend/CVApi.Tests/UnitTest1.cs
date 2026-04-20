using BusinessLogic.DTOs;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using CVApi.Controllers;
using DAL.Api;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CVApi.Tests;

public class CVControllerTests
{
    [Fact]
    public async Task GetGeneratedStats_ReturnsTotalGeneratedCvsAndTotalUsers()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<ApiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApiContext(dbOptions);
        db.CvGenerations.AddRange(
            new CvGenerated { Id = Guid.NewGuid(), UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"), CreatedAt = DateTime.UtcNow },
            new CvGenerated { Id = Guid.NewGuid(), UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"), CreatedAt = DateTime.UtcNow },
            new CvGenerated { Id = Guid.NewGuid(), UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"), CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var controller = new CVController(new FakeCvService(), db, new CVExtractionRunner());

        // Act
        var action = await controller.GetGeneratedStats();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(action);
        var totalGeneratedCvs = (int)(ok.Value!.GetType().GetProperty("totalGeneratedCvs")!.GetValue(ok.Value)!);
        var totalUsers = (int)(ok.Value!.GetType().GetProperty("totalUsers")!.GetValue(ok.Value)!);

        Assert.Equal(3, totalGeneratedCvs);
        Assert.Equal(2, totalUsers);
    }

    [Fact]
    public async Task GetGeneratedStats_ReturnsZeros_WhenNoRowsExist()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<ApiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApiContext(dbOptions);

        var controller = new CVController(new FakeCvService(), db, new CVExtractionRunner());

        // Act
        var action = await controller.GetGeneratedStats();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(action);
        var totalGeneratedCvs = (int)(ok.Value!.GetType().GetProperty("totalGeneratedCvs")!.GetValue(ok.Value)!);
        var totalUsers = (int)(ok.Value!.GetType().GetProperty("totalUsers")!.GetValue(ok.Value)!);

        Assert.Equal(0, totalGeneratedCvs);
        Assert.Equal(0, totalUsers);
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
