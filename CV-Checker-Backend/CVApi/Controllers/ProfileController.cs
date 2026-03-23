using System.Security.Claims;
using CVApi.Contracts.Profile;
using DAL.Api;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CVApi.Controllers;

[Authorize]
[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly ApiContext _db;

    // Allowed values
    private static readonly HashSet<string> SkillLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Beginner", "Intermediate", "Advanced", "Expert"
    };

    private static readonly HashSet<string> LanguageLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "A1", "A2", "B1", "B2", "C1", "C2", "Fluent"
    };

    public ProfileController(ApiContext db)
    {
        _db = db;
    }

    // Read user id from JWT
    private bool TryGetUserId(out Guid userId)
    {
        userId = default;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return sub != null && Guid.TryParse(sub, out userId);
    }

    [HttpGet("personal")]
    public async Task<ActionResult<PersonalInfoResponse>> GetPersonal()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var row = await _db.PersonalInfos.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        if (row == null)
            return NotFound();

        return Ok(ToDto(row));
    }

    [HttpPut("personal")]
    public async Task<ActionResult<PersonalInfoResponse>> PutPersonal([FromBody] PersonalInfoPutRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var now = DateTime.UtcNow;
        var row = await _db.PersonalInfos.FirstOrDefaultAsync(p => p.UserId == userId);
        if (row == null)
        {
            row = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = now
            };
            _db.PersonalInfos.Add(row);
        }

        row.FirstName = body.FirstName ?? "";
        row.LastName = body.LastName ?? "";
        row.DateOfBirth = body.DateOfBirth;
        row.Gender = body.Gender;
        row.Nationality = body.Nationality;
        row.Address = body.Address;
        row.CountryOfResidence = body.CountryOfResidence;
        row.PhoneNumber = body.PhoneNumber;
        row.UpdatedAt = now;

        await _db.SaveChangesAsync();
        return Ok(ToDto(row));
    }

    private static PersonalInfoResponse ToDto(PersonalInfo p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        FirstName = p.FirstName,
        LastName = p.LastName,
        DateOfBirth = p.DateOfBirth,
        Gender = p.Gender,
        Nationality = p.Nationality,
        Address = p.Address,
        CountryOfResidence = p.CountryOfResidence,
        PhoneNumber = p.PhoneNumber,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    // ---- education (basic CRUD) ----

    [HttpGet("education")]
    public async Task<ActionResult<List<EducationResponse>>> GetEducation()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var list = await _db.Educations.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();

        var result = list.Select(e => new EducationResponse
        {
            Id = e.Id,
            UserId = e.UserId,
            Degree = e.Degree,
            FieldOfStudy = e.FieldOfStudy,
            Institution = e.Institution,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Description = e.Description,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        }).ToList();

        return Ok(result);
    }

    [HttpPost("education")]
    public async Task<ActionResult<EducationResponse>> PostEducation([FromBody] EducationRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Degree) || string.IsNullOrWhiteSpace(body.Institution))
            return BadRequest("degree and institution are required");

        var now = DateTime.UtcNow;
        var e = new Education
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Degree = body.Degree.Trim(),
            FieldOfStudy = body.FieldOfStudy?.Trim() ?? "",
            Institution = body.Institution.Trim(),
            StartDate = body.StartDate?.Trim() ?? "",
            EndDate = string.IsNullOrWhiteSpace(body.EndDate) ? null : body.EndDate.Trim(),
            Description = body.Description,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Educations.Add(e);
        await _db.SaveChangesAsync();

        return Ok(new EducationResponse
        {
            Id = e.Id,
            UserId = e.UserId,
            Degree = e.Degree,
            FieldOfStudy = e.FieldOfStudy,
            Institution = e.Institution,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Description = e.Description,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        });
    }

    [HttpPut("education/{id:guid}")]
    public async Task<ActionResult<EducationResponse>> PutEducation(Guid id, [FromBody] EducationRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var e = await _db.Educations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (e == null)
            return NotFound();

        e.Degree = body.Degree ?? e.Degree;
        e.FieldOfStudy = body.FieldOfStudy ?? "";
        e.Institution = body.Institution ?? e.Institution;
        e.StartDate = body.StartDate ?? e.StartDate;
        e.EndDate = string.IsNullOrWhiteSpace(body.EndDate) ? null : body.EndDate;
        e.Description = body.Description;
        e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new EducationResponse
        {
            Id = e.Id,
            UserId = e.UserId,
            Degree = e.Degree,
            FieldOfStudy = e.FieldOfStudy,
            Institution = e.Institution,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Description = e.Description,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        });
    }

    [HttpDelete("education/{id:guid}")]
    public async Task<IActionResult> DeleteEducation(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var e = await _db.Educations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (e == null)
            return NotFound();

        _db.Educations.Remove(e);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // work experience

    [HttpGet("work")]
    public async Task<ActionResult<List<WorkExperienceResponse>>> GetWork()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var list = await _db.WorkExperiences.AsNoTracking()
            .Where(w => w.UserId == userId)
            .ToListAsync();

        var result = list.Select(w => new WorkExperienceResponse
        {
            Id = w.Id,
            UserId = w.UserId,
            JobTitle = w.JobTitle,
            Company = w.Company,
            Location = w.Location,
            StartDate = w.StartDate,
            EndDate = w.EndDate,
            CurrentlyWorking = w.CurrentlyWorking,
            Description = w.Description,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt
        }).ToList();

        return Ok(result);
    }

    [HttpPost("work")]
    public async Task<ActionResult<WorkExperienceResponse>> PostWork([FromBody] WorkExperienceRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.JobTitle) || string.IsNullOrWhiteSpace(body.Company))
            return BadRequest("jobTitle and company are required");

        var now = DateTime.UtcNow;
        var w = new WorkExperience
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobTitle = body.JobTitle.Trim(),
            Company = body.Company.Trim(),
            Location = body.Location,
            StartDate = body.StartDate?.Trim() ?? "",
            EndDate = string.IsNullOrWhiteSpace(body.EndDate) ? null : body.EndDate.Trim(),
            CurrentlyWorking = body.CurrentlyWorking,
            Description = body.Description,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.WorkExperiences.Add(w);
        await _db.SaveChangesAsync();

        return Ok(new WorkExperienceResponse
        {
            Id = w.Id,
            UserId = w.UserId,
            JobTitle = w.JobTitle,
            Company = w.Company,
            Location = w.Location,
            StartDate = w.StartDate,
            EndDate = w.EndDate,
            CurrentlyWorking = w.CurrentlyWorking,
            Description = w.Description,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt
        });
    }

    [HttpPut("work/{id:guid}")]
    public async Task<ActionResult<WorkExperienceResponse>> PutWork(Guid id, [FromBody] WorkExperienceRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var w = await _db.WorkExperiences.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (w == null)
            return NotFound();

        w.JobTitle = body.JobTitle ?? w.JobTitle;
        w.Company = body.Company ?? w.Company;
        w.Location = body.Location;
        w.StartDate = body.StartDate ?? w.StartDate;
        w.EndDate = string.IsNullOrWhiteSpace(body.EndDate) ? null : body.EndDate;
        w.CurrentlyWorking = body.CurrentlyWorking;
        w.Description = body.Description;
        w.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new WorkExperienceResponse
        {
            Id = w.Id,
            UserId = w.UserId,
            JobTitle = w.JobTitle,
            Company = w.Company,
            Location = w.Location,
            StartDate = w.StartDate,
            EndDate = w.EndDate,
            CurrentlyWorking = w.CurrentlyWorking,
            Description = w.Description,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt
        });
    }

    [HttpDelete("work/{id:guid}")]
    public async Task<IActionResult> DeleteWork(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var w = await _db.WorkExperiences.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (w == null)
            return NotFound();

        _db.WorkExperiences.Remove(w);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // skills

    [HttpGet("skills")]
    public async Task<ActionResult<List<SkillResponse>>> GetSkills()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var list = await _db.Skills.AsNoTracking().Where(s => s.UserId == userId).ToListAsync();
        return Ok(list.Select(s => new SkillResponse
        {
            Id = s.Id,
            UserId = s.UserId,
            Name = s.Name,
            Level = s.Level,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList());
    }

    [HttpPost("skills")]
    public async Task<ActionResult<SkillResponse>> PostSkill([FromBody] SkillRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("name is required");
        if (string.IsNullOrWhiteSpace(body.Level) || !SkillLevels.Contains(body.Level))
            return BadRequest("level must be Beginner, Intermediate, Advanced, or Expert");

        var now = DateTime.UtcNow;
        var s = new Skill
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = body.Name.Trim(),
            Level = body.Level,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Skills.Add(s);
        await _db.SaveChangesAsync();

        return Ok(new SkillResponse
        {
            Id = s.Id,
            UserId = s.UserId,
            Name = s.Name,
            Level = s.Level,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        });
    }

    [HttpPut("skills/{id:guid}")]
    public async Task<ActionResult<SkillResponse>> PutSkill(Guid id, [FromBody] SkillRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Level) || !SkillLevels.Contains(body.Level))
            return BadRequest("invalid level");

        var s = await _db.Skills.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s == null)
            return NotFound();

        s.Name = body.Name ?? s.Name;
        s.Level = body.Level;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new SkillResponse
        {
            Id = s.Id,
            UserId = s.UserId,
            Name = s.Name,
            Level = s.Level,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        });
    }

    [HttpDelete("skills/{id:guid}")]
    public async Task<IActionResult> DeleteSkill(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var s = await _db.Skills.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s == null)
            return NotFound();

        _db.Skills.Remove(s);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // languages

    [HttpGet("languages")]
    public async Task<ActionResult<List<LanguageResponse>>> GetLanguages()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var list = await _db.Languages.AsNoTracking().Where(l => l.UserId == userId).ToListAsync();
        return Ok(list.Select(l => new LanguageResponse
        {
            Id = l.Id,
            UserId = l.UserId,
            Name = l.Name,
            Level = l.Level,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        }).ToList());
    }

    [HttpPost("languages")]
    public async Task<ActionResult<LanguageResponse>> PostLanguage([FromBody] LanguageRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("name is required");
        if (string.IsNullOrWhiteSpace(body.Level) || !LanguageLevels.Contains(body.Level))
            return BadRequest("level must be A1, A2, B1, B2, C1, C2, or Native");

        var now = DateTime.UtcNow;
        var l = new Language
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = body.Name.Trim(),
            Level = body.Level,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Languages.Add(l);
        await _db.SaveChangesAsync();

        return Ok(new LanguageResponse
        {
            Id = l.Id,
            UserId = l.UserId,
            Name = l.Name,
            Level = l.Level,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        });
    }

    [HttpPut("languages/{id:guid}")]
    public async Task<ActionResult<LanguageResponse>> PutLanguage(Guid id, [FromBody] LanguageRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Level) || !LanguageLevels.Contains(body.Level))
            return BadRequest("invalid level");

        var l = await _db.Languages.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (l == null)
            return NotFound();

        l.Name = body.Name ?? l.Name;
        l.Level = body.Level;
        l.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new LanguageResponse
        {
            Id = l.Id,
            UserId = l.UserId,
            Name = l.Name,
            Level = l.Level,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        });
    }

    [HttpDelete("languages/{id:guid}")]
    public async Task<IActionResult> DeleteLanguage(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var l = await _db.Languages.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (l == null)
            return NotFound();

        _db.Languages.Remove(l);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
