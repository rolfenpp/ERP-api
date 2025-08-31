using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

[ApiController]
[Route("companies")]
public class CompaniesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenHelper _jwtTokenHelper;
    private readonly ITenantProvider _tenantProvider;

    public CompaniesController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        JwtTokenHelper jwtTokenHelper,
        ITenantProvider tenantProvider)
    {
        _db = db;
        _userManager = userManager;
        _jwtTokenHelper = jwtTokenHelper;
        _tenantProvider = tenantProvider;
    }

    // Register a company and bootstrap its Admin
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterCompany([FromBody] RegisterCompanyRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var name = req.Name.Trim();
        if (await _db.Companies.AnyAsync(c => c.Name == name))
            return Conflict("A company with this name already exists.");

        var company = new Company { Name = name };
        _db.Companies.Add(company);
        await _db.SaveChangesAsync();

        var email = req.AdminEmail.Trim();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
            return Conflict("A user with this email already exists.");

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            CompanyId = company.Id,
            EmailConfirmed = true // allow immediate login with RequireConfirmedEmail
        };

        var createResult = await _userManager.CreateAsync(admin, req.AdminPassword);
        if (!createResult.Succeeded)
            return BadRequest(createResult.Errors);

        var roleResult = await _userManager.AddToRoleAsync(admin, "Admin");
        if (!roleResult.Succeeded)
            return BadRequest(roleResult.Errors);

        // Issue a JWT for convenience
        var roles = await _userManager.GetRolesAsync(admin);
        var claims = await _userManager.GetClaimsAsync(admin);
        var token = _jwtTokenHelper.GenerateToken(admin, roles, claims);

        return Created(string.Empty, new
        {
            companyId = company.Id,
            companyName = company.Name,
            adminUserId = admin.Id,
            adminEmail = admin.Email,
            token
        });
    }

    // Get current user's company details
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyCompany()
    {
        var companyId = _tenantProvider.CompanyId;
        if (companyId <= 0)
            return NotFound("Company not found.");

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
        if (company == null) return NotFound("Company not found.");

        return Ok(new
        {
            id = company.Id,
            name = company.Name,
            createdUtc = company.CreatedUtc
        });
    }
}

public class RegisterCompanyRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;

    [Required]
    public string AdminPassword { get; set; } = string.Empty;
}