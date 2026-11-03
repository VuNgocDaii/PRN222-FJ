using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
[ApiController, Route("api/auth")]
public sealed class AuthController(MyStockContext context, TokenIssuer tokens, IWebHostEnvironment environment) : AuthControllerBase(context, tokens, environment)
{
    [AllowAnonymous, HttpGet("csrf")]
    public IActionResult Csrf([FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery af)
    {
        return Ok(new
        {
            token = af.GetAndStoreTokens(HttpContext).RequestToken
        });
    }

    [AllowAnonymous, HttpPost("login"), ValidateAntiForgeryToken, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var r = await LoginCore(input, Request.Headers.UserAgent.ToString());
        return Ok(await CreateTokensCore(HttpContext, r.Session));
    }

    [AllowAnonymous, HttpPost("refresh"), ValidateAntiForgeryToken, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("refresh")]
    public async Task<IActionResult> Refresh()
    {
        return Ok(await RefreshCore(HttpContext));
    }

    [Authorize(AuthenticationSchemes = "Bearer"), HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await LogoutCore(HttpContext);
        return NoContent();
    }

    [Authorize(AuthenticationSchemes = "Bearer"), HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(PasswordInput input)
    {
        await ChangePasswordCore(User, input);
        return NoContent();
    }
}
