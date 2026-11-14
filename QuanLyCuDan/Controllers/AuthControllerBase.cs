using QuanLyCuDan.Services.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using QuanLyCuDan.Models;
using QuanLyCuDan.Services;
namespace QuanLyCuDan.Controllers;

public abstract class AuthControllerBase(IAuthService authentication, INavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    protected IAuthService Authentication { get; } = authentication;

    protected async Task SignInCore(HttpContext http, AspNetUser user, UserSession session)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.UserName), new Claim("sid", session.Id.ToString()) }, "Cookies");
        await http.SignInAsync("Cookies", new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false, ExpiresUtc = new DateTimeOffset(DateTime.SpecifyKind(session.AbsoluteExpiresAt, DateTimeKind.Utc)) });
    }

    protected async Task LogoutCore(HttpContext http)
    {
        await Authentication.RevokeCurrentSessionAsync(http.User);
        await http.SignOutAsync("Cookies");
    }
}
