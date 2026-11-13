using QuanLyCuDan.Hubs;
using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class NotificationSubscriptionService(MyStockContext context) : ApplicationService(context)
{
    public async Task<IReadOnlyList<string>> GetGroupsAsync(ClaimsPrincipal actor)
    {
        var userId = actor.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = new List<string> { NotificationHub.FeedbackUserGroup(userId) };
        var grants = await db.VwEffectivePermissions.AsNoTracking()
            .Where(x => x.UserId == userId && x.PermissionCode == "feedback.read")
            .Select(x => new { x.ScopeType, x.ResidentialGroupId, x.ResidentialAreaId }).ToListAsync();
        foreach (var grant in grants.Where(x => x.ScopeType == "GROUP" && x.ResidentialGroupId != null))
            result.Add(NotificationHub.FeedbackStaffGroup(grant.ResidentialGroupId!.Value));
        var areas = grants.Where(x => x.ScopeType == "AREA" && x.ResidentialAreaId != null).Select(x => x.ResidentialAreaId!.Value).Distinct().ToList();
        var groupIds = await db.ResidentialUnits.AsNoTracking().Where(x => x.IsActive && x.ParentUnitId != null && areas.Contains(x.ParentUnitId.Value)).Select(x => x.Id).ToListAsync();
        foreach (var groupId in groupIds)
            result.Add(NotificationHub.FeedbackStaffGroup(groupId));
        return result.Distinct().ToArray();
    }
}
