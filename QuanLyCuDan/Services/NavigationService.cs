using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed record NavigationAccess(HashSet<string> Permissions, bool CanUseResident, bool CanManageAnnouncements, bool CanReviewRegistrations, bool CanManageOrganizations);
public sealed class NavigationService(MyStockContext context) : ApplicationService(context)
{
    public async Task<NavigationAccess> GetAsync(ClaimsPrincipal actor)
    {
        var id = CurrentUserId(actor);
        var access = (await Effective(id).Where(x => x.ScopeType == "SYSTEM").Select(x => x.PermissionCode).ToListAsync()).ToHashSet();
        return new(access,
            await Effective(id).AnyAsync(x => x.ScopeType == "SELF" && x.PermissionCode == "profile.self.read"),
            await Effective(id).AnyAsync(x => (x.ScopeType == "GROUP" || x.ScopeType == "AREA") && x.PermissionCode == "announcements.read"),
            access.Contains("accounts.read") && access.Contains("accounts.update") && await IsAdmin(id),
            await Effective(id).AnyAsync(p => p.PermissionCode == "organizations.read"));
    }
}
