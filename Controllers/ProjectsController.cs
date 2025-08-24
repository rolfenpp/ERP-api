using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Linq;
using System.Collections.Generic;
using Recipt_api;

[ApiController]
[Route("projects")]
[Authorize] // require JWT for all
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProjectsController(ApplicationDbContext db) => _db = db;

    private int GetCompanyId()
    {
        var claim = User.FindFirst("companyId")?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    // List projects
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetAll()
    {
        var companyId = GetCompanyId();

        var items = await _db.Projects
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.CreatedUtc)
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                CreatedUtc = p.CreatedUtc,
                UpdatedUtc = p.UpdatedUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    // Get single project
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDto>> GetById(int id)
    {
        var companyId = GetCompanyId();

        var p = await _db.Projects
            .Where(x => x.Id == id && x.CompanyId == companyId)
            .Select(x => new ProjectDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                CreatedUtc = x.CreatedUtc,
                UpdatedUtc = x.UpdatedUtc
            })
            .FirstOrDefaultAsync();

        if (p == null) return NotFound();
        return Ok(p);
    }

    // Create project (Admin only)
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProjectDto>> Create([FromBody] CreateProjectDto dto)
    {
        var companyId = GetCompanyId();

        var entity = new Project
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            CompanyId = companyId,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Projects.Add(entity);
        await _db.SaveChangesAsync();

        var result = new ProjectDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            CreatedUtc = entity.CreatedUtc,
            UpdatedUtc = entity.UpdatedUtc
        };

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, result);
    }

    // Update project (Admin only)
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProjectDto dto)
    {
        var companyId = GetCompanyId();

        var entity = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId);
        if (entity == null) return NotFound();

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Delete project (Admin only)
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var companyId = GetCompanyId();

        var entity = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId);
        if (entity == null) return NotFound();

        _db.Projects.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class ProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}

public class CreateProjectDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class UpdateProjectDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}