using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

// Quy tắc nghiệp vụ (theo sơ đồ use case):
//  - Trưởng thôn / Bí thư: duyệt mọi bài trong thôn, tự đăng bài của mình, gỡ mọi bài, nhập chỉ thị của xã.
//  - Trưởng tổ chức: duyệt bài của cấp phó trong tổ chức mình, tự đăng bài gửi thành viên tổ chức mình,
//    gỡ bài của tổ chức mình. Bài gửi toàn thôn phải chờ Trưởng thôn / Bí thư duyệt.
//  - Cấp phó: chỉ soạn và gửi duyệt, không tự đăng.
public sealed class AnnouncementAccessService(
    IPermissionChecker checker,
    IResidentialUnitRepository units,
    IOrganizationRepository organizations,
    IAnnouncementRepository announcements) : IAnnouncementAccessService
{
    private static readonly string[] Leaders = ["VILLAGE_HEAD", "PARTY_SECRETARY"];

    public async Task<List<ResidentialUnit>> ChoicesAsync(ClaimsPrincipal actor, string permission)
    {
        var ids = (await checker.GrantsAsync(actor, permission))
            .Where(g => g.ScopeType == "GROUP" && g.ResidentialGroupId != null)
            .Select(g => g.ResidentialGroupId!.Value).Distinct().ToArray();
        return ids.Length == 0 ? new List<ResidentialUnit>() : await units.GetActiveGroupsAsync(ids);
    }

    public async Task RequireAsync(ClaimsPrincipal actor, string permission, long groupId)
    {
        if (!(await ChoicesAsync(actor, permission)).Any(g => g.Id == groupId))
            throw new AppFault(403, "Bạn không có quyền thực hiện thao tác này trong thôn đã chọn.");
    }

    public async Task<AnnouncementScope> ScopeAsync(ClaimsPrincipal actor)
    {
        var grants = await checker.GrantsAsync(actor, "announcements.read");
        var groups = (await ChoicesAsync(actor, "announcements.read")).Select(g => g.Id).ToArray();
        var unrestricted = grants.Where(p => p.OrganizationId == null && p.ScopeType == "GROUP").Select(p => p.ResidentialGroupId!.Value).ToArray();
        var organizationIds = grants.Where(p => p.OrganizationId != null).Select(p => p.OrganizationId!.Value).ToArray();
        return new AnnouncementScope(checker.UserId(actor), groups, unrestricted, organizationIds);
    }

    public async Task<Announcement?> FindReadableAsync(ClaimsPrincipal actor, long id) =>
        await announcements.FindAsync(await ScopeAsync(actor), id);

    public async Task<List<Organization>> OrganizationChoicesAsync(ClaimsPrincipal actor)
    {
        var grants = await checker.GrantsAsync(actor, "announcements.create");
        var whole = grants.Where(p => p.ScopeType == "GROUP" && p.OrganizationId == null).Select(p => p.ResidentialGroupId!.Value).ToArray();
        var own = grants.Where(p => p.OrganizationId != null).Select(p => p.OrganizationId!.Value).ToArray();
        return await organizations.GetActiveChoicesAsync(whole, own);
    }

    public async Task RequireAudienceAsync(ClaimsPrincipal actor, AnnouncementInput input)
    {
        if (input.Kind == "DIRECTIVE")
        {
            var canWrite = (await checker.GrantsAsync(actor, "announcements.create")).Any(p => Leaders.Contains(p.RoleName));
            if (!canWrite)
                throw new AppFault(403, "Chỉ Trưởng thôn hoặc Bí thư được nhập chỉ thị của xã.");
            if (input.Audience == "ORGANIZATION")
                throw new AppFault(400, "Chỉ thị của xã phải gửi tới toàn thôn, không giới hạn trong tổ chức.");
        }
        if (input.Audience == "ORGANIZATION")
        {
            var choices = await OrganizationChoicesAsync(actor);
            if (!choices.Any(o => o.Id == input.OrganizationId && o.ResidentialGroupId == input.ResidentialGroupId))
                throw new AppFault(400, "Chọn tổ chức được phép trong thôn đã chọn.");
        }
        else if (input.Audience is not ("PUBLIC" or "RESIDENTS") || input.OrganizationId != null)
            throw new AppFault(400, "Đối tượng đọc không hợp lệ; thông báo toàn thôn không được giới hạn tổ chức.");
    }

    public async Task<bool> CanUpdateAsync(ClaimsPrincipal actor, Announcement item)
    {
        var id = checker.UserId(actor);
        return (await checker.GrantsAsync(actor, "announcements.update")).Any(p =>
            p.ScopeType == "GROUP" && p.ResidentialGroupId == item.ResidentialGroupId &&
            (Leaders.Contains(p.RoleName) || item.CreatedByUserId == id) &&
            (p.OrganizationId == null || item.OrganizationId == p.OrganizationId || item.CreatedByUserId == id));
    }

    public async Task RequireUpdateAsync(ClaimsPrincipal actor, Announcement item)
    {
        if (!await CanUpdateAsync(actor, item))
            throw new AppFault(403, "Chỉ được sửa/gửi duyệt/rút lại bản soạn của mình trong phạm vi được giao.");
    }

    public async Task<bool> CanSeeStatisticsAsync(ClaimsPrincipal actor, Announcement item) =>
        (await checker.GrantsAsync(actor, "announcements.statistics")).Any(p =>
            p.ScopeType == "GROUP" && p.ResidentialGroupId == item.ResidentialGroupId &&
            (p.OrganizationId == null || p.OrganizationId == item.OrganizationId));

    public Task<bool> CanApproveAsync(ClaimsPrincipal actor, Announcement item) => CoversAsync(actor, "announcements.approve", item);

    public Task<bool> CanUnpublishAsync(ClaimsPrincipal actor, Announcement item) => CoversAsync(actor, "announcements.unpublish", item);

    // Quyền duyệt/gỡ chỉ có hiệu lực với bài nằm trong phạm vi của người đó:
    // Trưởng thôn/Bí thư: cả thôn; Trưởng tổ chức: chỉ bài gửi cho thành viên tổ chức mình.
    private async Task<bool> CoversAsync(ClaimsPrincipal actor, string permission, Announcement item) =>
        (await checker.GrantsAsync(actor, permission)).Any(g =>
            g.ScopeType == "GROUP" && g.ResidentialGroupId == item.ResidentialGroupId &&
            (g.OrganizationId == null
                ? Leaders.Contains(g.RoleName)
                : g.RoleName == "ORGANIZATION_HEAD" && item.Audience == "ORGANIZATION" && item.OrganizationId == g.OrganizationId));
}
