using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface ISessionService
{
    Task<bool> IsValidAsync(ClaimsPrincipal actor);
    Task<UserSession> GetForSignInAsync(ClaimsPrincipal actor);
}
