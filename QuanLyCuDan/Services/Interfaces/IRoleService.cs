using QuanLyCuDan.Auth;
using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IRoleService
{
    Task<RoleListResult> ListAsync(ClaimsPrincipal actor);
    Task<string> CreateAsync(ClaimsPrincipal actor, RoleInput input);
    Task UpdateAsync(ClaimsPrincipal actor, string id, RoleEdit input);
    Task SetPermissionsAsync(ClaimsPrincipal actor, string roleId, PermissionEdit input);
}
