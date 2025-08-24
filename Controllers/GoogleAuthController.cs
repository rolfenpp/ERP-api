using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("account")]
public class GoogleAccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public GoogleAccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet("google")]
    [AllowAnonymous]
    public IActionResult GoogleLogin([FromQuery] string? returnUrl = null)
    {
        var callbackUrl = Url.Action(nameof(GoogleCallback), null, new { returnUrl }, Request.Scheme)!;
        var props = _signInManager.ConfigureExternalAuthenticationProperties("Google", callbackUrl);
        return Challenge(props, "Google");
    }

    [HttpGet("google-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl = null, [FromQuery] string? remoteError = null)
    {
        if (!string.IsNullOrEmpty(remoteError))
            return BadRequest($"Google error: {remoteError}");

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
            return BadRequest("No external login info.");

        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (signInResult.Succeeded)
            return Ok("Login successful with Google.");

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            return BadRequest("Google account has no email.");

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser { UserName = email, Email = email };
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded) return BadRequest(createResult.Errors);
        }

        var addLoginResult = await _userManager.AddLoginAsync(user, info);
        if (!addLoginResult.Succeeded) return BadRequest(addLoginResult.Errors);

        await _signInManager.SignInAsync(user, isPersistent: false);

        return Ok("User created/linked and logged in with Google.");
    }
}
