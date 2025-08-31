using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

[ApiController]
[Route("[controller]")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenHelper _jwtTokenHelper;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _config;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenHelper jwtTokenHelper,
        RoleManager<IdentityRole> roleManager,
        IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenHelper = jwtTokenHelper;
        _roleManager = roleManager;
        _config = config;
    }

    // Who am I? (dashboard bootstrap)
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        var perms = claims.Where(c => c.Type == "perm").Select(c => c.Value).ToArray();

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            companyId = user.CompanyId,
            roles,
            permissions = perms
        });
    }

    // Admin creates a user without password (cannot login yet). Assign roles, tie to admin's company.
    [Authorize(Roles = "Admin")]
    [HttpPost("users/basic")]
    public async Task<IActionResult> CreateUserBasic([FromBody] CreateUserBasicModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();

        var admin = await _userManager.FindByIdAsync(adminId);
        if (admin is null) return Unauthorized();

        var email = model.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null) return Conflict("User with this email already exists.");

        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            CompanyId = admin.CompanyId,
            EmailConfirmed = false
        };

        var createResult = await _userManager.CreateAsync(newUser); // no password
        if (!createResult.Succeeded) return BadRequest(createResult.Errors);

        var roles = (model.Roles?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? new[] { "User" });
        foreach (var r in roles)
        {
            if (!await _roleManager.RoleExistsAsync(r))
                return BadRequest($"Role '{r}' does not exist.");
        }

        var roleResult = await _userManager.AddToRolesAsync(newUser, roles);
        if (!roleResult.Succeeded) return BadRequest(roleResult.Errors);

        return Created(string.Empty, new
        {
            id = newUser.Id,
            email = newUser.Email,
            companyId = newUser.CompanyId,
            roles
        });
    }

    // Admin creates a user with password (still requires activation/confirmation to login)
    [Authorize(Roles = "Admin")]
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();

        var admin = await _userManager.FindByIdAsync(adminId);
        if (admin is null) return Unauthorized();

        var email = model.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null) return Conflict("User with this email already exists.");

        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            CompanyId = admin.CompanyId,
            EmailConfirmed = false
        };

        var createResult = await _userManager.CreateAsync(newUser, model.Password);
        if (!createResult.Succeeded) return BadRequest(createResult.Errors);

        var roles = (model.Roles?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? new[] { "User" });
        foreach (var r in roles)
        {
            if (!await _roleManager.RoleExistsAsync(r))
                return BadRequest($"Role '{r}' does not exist.");
        }

        var roleResult = await _userManager.AddToRolesAsync(newUser, roles);
        if (!roleResult.Succeeded) return BadRequest(roleResult.Errors);

        return Created(string.Empty, new
        {
            id = newUser.Id,
            email = newUser.Email,
            companyId = newUser.CompanyId,
            roles
        });
    }

    // Admin: get a user's permissions (same company)
    [Authorize(Roles = "Admin")]
    [HttpGet("users/{userId}/permissions")]
    public async Task<IActionResult> GetUserPermissions(string userId)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();

        var admin = await _userManager.FindByIdAsync(adminId);
        if (admin is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound("User not found.");
        if (user.CompanyId != admin.CompanyId) return Forbid();

        var claims = await _userManager.GetClaimsAsync(user);
        var perms = claims.Where(c => c.Type == "perm").Select(c => c.Value).ToArray();
        return Ok(new { userId = user.Id, permissions = perms });
    }

    public class SetPermissionsRequest
    {
        [Required] public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();
    }

    // Admin: replace a user's permissions completely (same company)
    [Authorize(Roles = "Admin")]
    [HttpPut("users/{userId}/permissions")]
    public async Task<IActionResult> SetUserPermissions(string userId, [FromBody] SetPermissionsRequest req)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();

        var admin = await _userManager.FindByIdAsync(adminId);
        if (admin is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound("User not found.");
        if (user.CompanyId != admin.CompanyId) return Forbid();

        var requested = req.Permissions?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();

        // Validate against whitelist
        var invalid = requested.Except(Permissions.All, StringComparer.OrdinalIgnoreCase).ToArray();
        if (invalid.Length > 0)
            return BadRequest($"Invalid permission(s): {string.Join(", ", invalid)}");

        var existingClaims = (await _userManager.GetClaimsAsync(user))
            .Where(c => c.Type == "perm")
            .ToArray();

        if (existingClaims.Length > 0)
        {
            var remove = await _userManager.RemoveClaimsAsync(user, existingClaims);
            if (!remove.Succeeded) return BadRequest(remove.Errors);
        }

        var newClaims = requested.Select(p => new Claim("perm", p)).ToArray();
        if (newClaims.Length > 0)
        {
            var add = await _userManager.AddClaimsAsync(user, newClaims);
            if (!add.Succeeded) return BadRequest(add.Errors);
        }

        // Note: user must re-authenticate or receive a new token to see updated permissions.
        return NoContent();
    }

    // Admin generates a one-time activation (email confirm + password reset) link for a user in their company
    [Authorize(Roles = "Admin")]
    [HttpPost("users/{userId}/invite")]
    public async Task<IActionResult> SendInvite(string userId)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();

        var admin = await _userManager.FindByIdAsync(adminId);
        if (admin is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound("User not found.");

        if (user.CompanyId != admin.CompanyId) return Forbid();
        if (user.EmailConfirmed) return BadRequest("User is already activated.");

        var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        static string Encode(string t) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(t));

        var emailTokenEnc = Encode(emailToken);
        var resetTokenEnc = Encode(resetToken);

        // Optional: configure in appsettings.json: { "App": { "BaseUrl": "http://localhost:5173" } }
        var appBaseUrl = _config["App:BaseUrl"]?.TrimEnd('/');
        var activationUrl = appBaseUrl is null
            ? null
            : $"{appBaseUrl}/activate?userId={Uri.EscapeDataString(user.Id)}&c={emailTokenEnc}&r={resetTokenEnc}";

        // TODO: send email with activationUrl. For dev, we return it.
        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            activationUrl,
            emailToken = emailTokenEnc,
            resetToken = resetTokenEnc
        });
    }

    // Public: activate account using tokens from invite and set a password
    [AllowAnonymous]
    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateAccountModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null) return BadRequest("Invalid activation request.");

        static string Decode(string t) => Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(t));

        if (!user.EmailConfirmed)
        {
            var confirm = await _userManager.ConfirmEmailAsync(user, Decode(model.EmailToken));
            if (!confirm.Succeeded) return BadRequest("Invalid or expired email token.");
        }

        var reset = await _userManager.ResetPasswordAsync(user, Decode(model.ResetToken), model.Password);
        if (!reset.Succeeded) return BadRequest("Invalid or expired reset token.");

        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        var jwt = _jwtTokenHelper.GenerateToken(user, roles, claims);

        return Ok(new { token = jwt });
    }

    // Keep existing simple register (first user Admin)
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        // Disable bare registration to enforce company-first onboarding.
        // Use POST /companies/register to create a company and its Admin, then Admin invites users.
        return Forbid();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return Unauthorized("Invalid login attempt.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
        if (!result.Succeeded)
            return Unauthorized("Invalid login attempt.");

        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        var token = _jwtTokenHelper.GenerateToken(user, roles, claims);
        return Ok(new { token });
    }
}

public class RegisterModel
{
    public string Email { get; set; }
    public string Password { get; set; }
}

public class LoginModel
{
    public string Email { get; set; }
    public string Password { get; set; }
}

public class CreateUserModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    // Optional (defaults to ["User"])
    public IEnumerable<string>? Roles { get; set; }
}

public class CreateUserBasicModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    // Optional (defaults to ["User"])
    public IEnumerable<string>? Roles { get; set; }
}

public class ActivateAccountModel
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required] public string EmailToken { get; set; } = string.Empty;
    [Required] public string ResetToken { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}