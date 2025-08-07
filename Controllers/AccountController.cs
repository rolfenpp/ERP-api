using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

[ApiController]
[Route("[controller]")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenHelper _jwtTokenHelper;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenHelper jwtTokenHelper)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenHelper = jwtTokenHelper;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
            return Ok("User registered successfully.");

        return BadRequest(result.Errors);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return Unauthorized("Invalid login attempt.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);

        if (result.Succeeded)
        {
            var token = _jwtTokenHelper.GenerateToken(user.Id, user.Email);
            return Ok(new { token });
        }

        return Unauthorized("Invalid login attempt.");
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