using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuanLyCuDan.Controllers;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Auth;

// MVC forms and SignalR send the signed JWT in an HttpOnly cookie.
public sealed class JwtCookieHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, TokenIssuer tokens, MyStockContext db,
    IWebHostEnvironment environment) : SignInAuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string CookieName = "qlcd.auth";
    private CookieOptions CookieSettings() => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict, Path = "/"
    };

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var raw)) return AuthenticateResult.NoResult();
        try
        {
            var principal = tokens.Validate(raw);
            if (!await BaseController.ValidSession(db, principal))
                return AuthenticateResult.Fail("Session revoked or expired.");
            var properties = new AuthenticationProperties
            {
                ExpiresUtc = DateTimeOffset.FromUnixTimeSeconds(long.Parse(principal.FindFirstValue("exp")!))
            };
            return AuthenticateResult.Success(new AuthenticationTicket(principal, properties, Scheme.Name));
        }
        catch (Exception error) when (error is SecurityTokenException or ArgumentException)
        {
            return AuthenticateResult.Fail("Invalid session JWT.");
        }
    }

    protected override async Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = Guid.Parse(user.FindFirstValue("sid")!);
        var session = await db.UserSessions.Include(s => s.User)
            .SingleAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null);
        Response.Cookies.Append(CookieName, tokens.Issue(session.User, session), CookieSettings());
    }

    protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
    {
        Response.Cookies.Delete(CookieName, CookieSettings());
        return Task.CompletedTask;
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var returnUrl = Request.PathBase + Request.Path + Request.QueryString;
        Response.Redirect("/Account/Login?returnUrl=" + Uri.EscapeDataString(returnUrl));
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
