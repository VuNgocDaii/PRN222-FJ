using System.Security.Claims;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class NavigationService(IPermissionChecker checker, IPermissionRepository permissions, IUserRepository users) : INavigationService
{
    public async Task<NavigationAccess> GetAsync(ClaimsPrincipal actor)
    {
        var id = checker.UserId(actor);
        var access = await permissions.GetSystemCodesAsync(id);
        return new(access,
            await permissions.HasAsync(id, "profile.self.read", "SELF"),
            await permissions.HasAsync(id, "announcements.read", "GROUP", "AREA"),
            access.Contains("accounts.read") && access.Contains("accounts.update") && await users.IsAdminAsync(id),
            await permissions.HasAnyAsync(id, "organizations.read"));
    }
}
