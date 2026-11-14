using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class PermissionChecker(IPermissionRepository permissions) : IPermissionChecker
{
    public string UserId(ClaimsPrincipal actor) =>
        actor.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new AppFault(401, "Bạn cần đăng nhập.");

    public Task<List<VwEffectivePermission>> GrantsAsync(ClaimsPrincipal actor, string permission) =>
        permissions.GetGrantsAsync(UserId(actor), permission);

    public async Task<bool> HasSystemPermissionAsync(ClaimsPrincipal actor, string permission) =>
        (await GrantsAsync(actor, permission)).Any(g => g.ScopeType == "SYSTEM");
}
