using QuanLyCuDan.Auth;
using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IUserService
{
    Task<UserListResult> ListAsync(ClaimsPrincipal actor, int page = 1, string? search = null);
    Task<string> CreateAsync(ClaimsPrincipal actor, UserInput input);
    Task UpdateAsync(ClaimsPrincipal actor, string id, UserEdit input);
}
