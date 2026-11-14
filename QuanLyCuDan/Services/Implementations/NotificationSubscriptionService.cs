using System.Security.Claims;
using QuanLyCuDan.Hubs;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class NotificationSubscriptionService(IPermissionChecker checker, IPermissionRepository permissions, IResidentialUnitRepository units) : INotificationSubscriptionService
{
    public async Task<IReadOnlyList<string>> GetGroupsAsync(ClaimsPrincipal actor)
    {
        var userId = checker.UserId(actor);
        var result = new List<string> { NotificationHub.FeedbackUserGroup(userId) };
        var grants = await permissions.GetGrantsAsync(userId, "feedback.read");
        foreach (var grant in grants.Where(x => x.ScopeType == "GROUP" && x.ResidentialGroupId != null))
            result.Add(NotificationHub.FeedbackStaffGroup(grant.ResidentialGroupId!.Value));
        var areas = grants.Where(x => x.ScopeType == "AREA" && x.ResidentialAreaId != null).Select(x => x.ResidentialAreaId!.Value).Distinct().ToList();
        foreach (var groupId in await units.GetActiveGroupIdsUnderAreasAsync(areas))
            result.Add(NotificationHub.FeedbackStaffGroup(groupId));
        return result.Distinct().ToArray();
    }
}
