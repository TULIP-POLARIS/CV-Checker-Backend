using CVApi.Auth;
using DAL.Api;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CVApi.Controllers;

[Authorize]
[ApiController]
[Route("api/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly ApiContext _db;

    public ProfileController(ApiContext db)
    {
        _db = db;
    }

    [HttpGet("personal")]
    public async Task<ActionResult<PersonalOut>> GetPersonal()
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var row = await _db.PersonalInfos.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        if (row == null)
            return Ok(new PersonalOut());

        return Ok(PersonalOut.From(row));
    }

    [HttpPut("personal")]
    public async Task<ActionResult> PutPersonal([FromBody] PersonalIn body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var now = DateTime.UtcNow;
        var row = await _db.PersonalInfos.FirstOrDefaultAsync(p => p.UserId == userId);

        if (row == null)
        {
            row = new PersonalInfo { Id = Guid.NewGuid(), UserId = userId.Value, CreatedAt = now, UpdatedAt = now };
            _db.PersonalInfos.Add(row);
        }

        row.FirstName = body.FirstName ?? "";
        row.LastName = body.LastName ?? "";
        row.DateOfBirth = string.IsNullOrWhiteSpace(body.DateOfBirth)
            ? null
            : DateOnly.TryParse(body.DateOfBirth, out var dob) ? dob : null;
        row.Gender = body.Gender;
        row.Nationality = body.Nationality;
        row.Address = body.Address;
        row.CountryOfResidence = body.CountryOfResidence;
        row.PhoneNumber = body.PhoneNumber;
        row.UpdatedAt = now;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("education")]
    public async Task<ActionResult<List<EducationOut>>> ListEducation()
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var list = await _db.Educations.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.StartDate)
            .Select(e => EducationOut.From(e))
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("education")]
    public async Task<ActionResult<EducationOut>> PostEducation([FromBody] EducationIn body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var now = DateTime.UtcNow;
        var e = new Education
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Degree = body.Degree ?? "",
            FieldOfStudy = body.FieldOfStudy ?? "",
            Institution = body.Institution ?? "",
            StartDate = body.StartDate ?? "",
            EndDate = body.EndDate,
            Description = body.Description,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Educations.Add(e);
        await _db.SaveChangesAsync();
        return Ok(EducationOut.From(e));
    }

    [HttpPut("education/{id:guid}")]
    public async Task<ActionResult> PutEducation(Guid id, [FromBody] EducationIn body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var e = await _db.Educations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (e == null) return NotFound();

        e.Degree = body.Degree ?? "";
        e.FieldOfStudy = body.FieldOfStudy ?? "";
        e.Institution = body.Institution ?? "";
        e.StartDate = body.StartDate ?? "";
        e.EndDate = body.EndDate;
        e.Description = body.Description;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("education/{id:guid}")]
    public async Task<ActionResult> DeleteEducation(Guid id)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var e = await _db.Educations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (e == null) return NotFound();
        _db.Educations.Remove(e);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("work")]
    public async Task<ActionResult<List<WorkOut>>> ListWork()
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var list = await _db.WorkExperiences.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.StartDate)
            .Select(w => WorkOut.From(w))
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("work")]
    public async Task<ActionResult<WorkOut>> PostWork([FromBody] WorkIn body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var now = DateTime.UtcNow;
        var w = new WorkExperience
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            JobTitle = body.JobTitle ?? "",
            Company = body.Company ?? "",
            Location = body.Location,
            StartDate = body.StartDate ?? "",
            EndDate = body.EndDate,
            CurrentlyWorking = body.CurrentlyWorking,
            Description = body.Description,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.WorkExperiences.Add(w);
        await _db.SaveChangesAsync();
        return Ok(WorkOut.From(w));
    }

    [HttpPut("work/{id:guid}")]
    public async Task<ActionResult> PutWork(Guid id, [FromBody] WorkIn body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var w = await _db.WorkExperiences.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (w == null) return NotFound();

        w.JobTitle = body.JobTitle ?? "";
        w.Company = body.Company ?? "";
        w.Location = body.Location;
        w.StartDate = body.StartDate ?? "";
        w.EndDate = body.EndDate;
        w.CurrentlyWorking = body.CurrentlyWorking;
        w.Description = body.Description;
        w.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("work/{id:guid}")]
    public async Task<ActionResult> DeleteWork(Guid id)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var w = await _db.WorkExperiences.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (w == null) return NotFound();
        _db.WorkExperiences.Remove(w);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("skills")]
    public async Task<ActionResult<List<SkillOut>>> ListSkills()
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var list = await _db.Skills.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Name)
            .Select(s => SkillOut.From(s))
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("skills")]
    public async Task<ActionResult<SkillOut>> PostSkill([FromBody] SkillIn body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var now = DateTime.UtcNow;
        var s = new Skill
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = body.Name ?? "",
            Level = body.Level ?? "",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Skills.Add(s);
        await _db.SaveChangesAsync();
        return Ok(SkillOut.From(s));
    }

    [HttpPut("skills/{id:guid}")]
    public async Task<ActionResult> PutSkill(Guid id, [FromBody] SkillIn body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var s = await _db.Skills.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s == null) return NotFound();

        s.Name = body.Name ?? "";
        s.Level = body.Level ?? "";
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("skills/{id:guid}")]
    public async Task<ActionResult> DeleteSkill(Guid id)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var s = await _db.Skills.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s == null) return NotFound();
        _db.Skills.Remove(s);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("languages")]
    public async Task<ActionResult<List<LanguageOut>>> ListLanguages()
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();

        var list = await _db.Languages.AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Name)
            .Select(l => LanguageOut.From(l))
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("languages")]
    public async Task<ActionResult<LanguageOut>> PostLanguage([FromBody] LanguageIn body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var now = DateTime.UtcNow;
        var l = new Language
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = body.Name ?? "",
            Level = body.Level ?? "",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Languages.Add(l);
        await _db.SaveChangesAsync();
        return Ok(LanguageOut.From(l));
    }

    [HttpPut("languages/{id:guid}")]
    public async Task<ActionResult> PutLanguage(Guid id, [FromBody] LanguageIn body)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var l = await _db.Languages.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (l == null) return NotFound();

        l.Name = body.Name ?? "";
        l.Level = body.Level ?? "";
        l.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("languages/{id:guid}")]
    public async Task<ActionResult> DeleteLanguage(Guid id)
    {
        var userId = HttpUser.GetUserId(User);
        if (userId == null) return Unauthorized();
        var l = await _db.Languages.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (l == null) return NotFound();
        _db.Languages.Remove(l);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public sealed class PersonalIn
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? Address { get; set; }
    public string? CountryOfResidence { get; set; }
    public string? PhoneNumber { get; set; }
}

public sealed class PersonalOut
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? Address { get; set; }
    public string? CountryOfResidence { get; set; }
    public string? PhoneNumber { get; set; }

    public static PersonalOut From(PersonalInfo p) => new()
    {
        FirstName = p.FirstName,
        LastName = p.LastName,
        DateOfBirth = p.DateOfBirth?.ToString("yyyy-MM-dd"),
        Gender = p.Gender,
        Nationality = p.Nationality,
        Address = p.Address,
        CountryOfResidence = p.CountryOfResidence,
        PhoneNumber = p.PhoneNumber
    };
}

public sealed class EducationIn
{
    public string? Degree { get; set; }
    public string? FieldOfStudy { get; set; }
    public string? Institution { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Description { get; set; }
}

public sealed class EducationOut
{
    public Guid Id { get; set; }
    public string Degree { get; set; } = "";
    public string FieldOfStudy { get; set; } = "";
    public string Institution { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public string? Description { get; set; }

    public static EducationOut From(Education e) => new()
    {
        Id = e.Id,
        Degree = e.Degree,
        FieldOfStudy = e.FieldOfStudy,
        Institution = e.Institution,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        Description = e.Description
    };
}

public sealed class WorkIn
{
    public string? JobTitle { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool CurrentlyWorking { get; set; }
    public string? Description { get; set; }
}

public sealed class WorkOut
{
    public Guid Id { get; set; }
    public string JobTitle { get; set; } = "";
    public string Company { get; set; } = "";
    public string? Location { get; set; }
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public bool CurrentlyWorking { get; set; }
    public string? Description { get; set; }

    public static WorkOut From(WorkExperience w) => new()
    {
        Id = w.Id,
        JobTitle = w.JobTitle,
        Company = w.Company,
        Location = w.Location,
        StartDate = w.StartDate,
        EndDate = w.EndDate,
        CurrentlyWorking = w.CurrentlyWorking,
        Description = w.Description
    };
}

public sealed class SkillIn
{
    public string? Name { get; set; }
    public string? Level { get; set; }
}

public sealed class SkillOut
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";

    public static SkillOut From(Skill s) => new() { Id = s.Id, Name = s.Name, Level = s.Level };
}

public sealed class LanguageIn
{
    public string? Name { get; set; }
    public string? Level { get; set; }
}

public sealed class LanguageOut
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";

    public static LanguageOut From(Language l) => new() { Id = l.Id, Name = l.Name, Level = l.Level };
}
