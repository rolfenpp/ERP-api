using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // AnyAsync
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.WebUtilities; // for WebEncoders
using System.Text; // for Encoding

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
        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            companyId = user.CompanyId,
            roles
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

        var roles = (model.Roles?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? new[] { "Employee" });
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

        var roles = (model.Roles?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? new[] { "Employee" });
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

    // Keep existing simple register (first user Admin), mainly for dev
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        var isFirstUser = !await _userManager.Users.AnyAsync();

        var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        var role = isFirstUser ? "Admin" : "Employee";
        await _userManager.AddToRoleAsync(user, role);

        return Ok("User registered successfully.");
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

    // Optional (defaults to ["Employee"])
    public IEnumerable<string>? Roles { get; set; }
}

public class CreateUserBasicModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    // Optional (defaults to ["Employee"])
    public IEnumerable<string>? Roles { get; set; }
}

public class ActivateAccountModel
{
    [Required] public string UserId { get; set; } = string.Empty; // from invite URL
    [Required] public string EmailToken { get; set; } = string.Empty; // c=...
    [Required] public string ResetToken { get; set; } = string.Empty; // r=...
    [Required] public string Password { get; set; } = string.Empty;
}