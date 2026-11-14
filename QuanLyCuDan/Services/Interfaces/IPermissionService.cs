using QuanLyCuDan.Auth;
using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IPermissionService
{
    Task<List<Permission>> ListAsync(ClaimsPrincipal actor);
    Task<int> CreateAsync(ClaimsPrincipal actor, PermissionCreateInput input);
    Task UpdateAsync(ClaimsPrincipal actor, int id, PermissionUpdateInput input);
    Task DeleteAsync(ClaimsPrincipal actor, int id, PermissionDeleteInput input);
}
