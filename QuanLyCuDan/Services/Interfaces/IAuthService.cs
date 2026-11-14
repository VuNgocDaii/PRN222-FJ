using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IAuthService
{
    Task<(AspNetUser User, UserSession Session)> LoginAsync(LoginInput input, string? device);
    Task ChangePasswordAsync(ClaimsPrincipal principal, PasswordInput input);
    Task RevokeCurrentSessionAsync(ClaimsPrincipal actor);
    Task<string> GetDestinationAsync(string userId);
}
