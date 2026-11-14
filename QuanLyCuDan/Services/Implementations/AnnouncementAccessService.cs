using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services;

public sealed class AnnouncementAccessService(MyStockContext context) : ApplicationService(context)
{
    public IQueryable<ResidentialUnit> Groups(ClaimsPrincipal actor, string permission)
    {
        var grants = Effective(CurrentUserId(actor)).Where(p => p.PermissionCode == permission);
        return db.ResidentialUnits.Where(g => g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.UnitType == "AREA" && g.ParentUnit.IsActive &&
            grants.Any(p => p.ScopeType == "GROUP" && p.ResidentialGroupId == g.Id || p.ScopeType == "AREA" && p.ResidentialAreaId == g.ParentUnitId));
    }

    public async Task RequireAsync(ClaimsPrincipal actor, string permission, long groupId)
    {
        if (!await Groups(actor, permission).AnyAsync(g => g.Id == groupId))
            throw new AppFault(403, "Bạn không có quyền thực hiện thao tác này trong thôn đã chọn.");
    }

    public async Task<IQueryable<Announcement>> ReadableAsync(ClaimsPrincipal actor)
    {
        var userId = CurrentUserId(actor);
        // Materialize permission grants before joining tracked editable entities.
        var grants = await Effective(userId).Where(p => p.PermissionCode == "announcements.read").ToListAsync();
        var groups = await Groups(actor, "announcements.read").Select(g => g.Id).ToArrayAsync();
        var unrestricted = grants.Where(p => p.OrganizationId == null && p.ScopeType == "GROUP").Select(p => p.ResidentialGroupId!.Value).ToArray();
        var areas = grants.Where(p => p.OrganizationId == null && p.ScopeType == "AREA").Select(p => p.ResidentialAreaId!.Value).ToArray();
        var organizations = grants.Where(p => p.OrganizationId != null).Select(p => p.OrganizationId!.Value).ToArray();
        return db.Announcements.Where(a => groups.Contains(a.ResidentialGroupId) &&
            (unrestricted.Contains(a.ResidentialGroupId) || areas.Contains(a.ResidentialGroup.ParentUnitId!.Value) ||
             a.OrganizationId != null && organizations.Contains(a.OrganizationId.Value) ||
             a.CreatedByUserId == userId || a.OrganizationId == null && a.Status == "PUBLISHED"));
    }

    public async Task<Announcement?> FindReadableAsync(ClaimsPrincipal actor, long id) =>
        await (await ReadableAsync(actor)).AsTracking().Include(a => a.Organization).Include(a => a.ResidentialGroup)
            .Include(a => a.CreatedByUser).Include(a => a.PublishedByUser).SingleOrDefaultAsync(a => a.Id == id);

    public Task<List<ResidentialUnit>> ChoicesAsync(ClaimsPrincipal actor, string permission) =>
        Groups(actor, permission).AsNoTracking().OrderBy(g => g.Name).ToListAsync();

    public async Task<List<Organization>> OrganizationChoicesAsync(ClaimsPrincipal actor)
    {
        var grants = await Effective(CurrentUserId(actor)).Where(p => p.PermissionCode == "announcements.create").ToListAsync();
        var whole = grants.Where(p => p.ScopeType == "GROUP" && p.OrganizationId == null).Select(p => p.ResidentialGroupId!.Value).ToArray();
        var own = grants.Where(p => p.OrganizationId != null).Select(p => p.OrganizationId!.Value).ToArray();
        return await db.Organizations.AsNoTracking().Where(o => o.IsActive && (whole.Contains(o.ResidentialGroupId) || own.Contains(o.Id))).OrderBy(o => o.Name).ToListAsync();
    }

    public async Task RequireAudienceAsync(ClaimsPrincipal actor, AnnouncementInput input)
    {
        if (input.Kind == "DIRECTIVE" && !await Effective(CurrentUserId(actor)).AnyAsync(p => p.PermissionCode == "announcements.create" && (p.RoleName == "COMMUNE_OFFICER" || p.RoleName == "VILLAGE_HEAD" || p.RoleName == "PARTY_SECRETARY")))
            throw new AppFault(403, "Chỉ cán bộ xã, Trưởng thôn hoặc Bí thư được nhập chỉ thị xã.");
        if (input.Kind == "DIRECTIVE" && input.Audience == "ORGANIZATION")
            throw new AppFault(400, "Chỉ thị xã phải gửi tới thôn, không giới hạn trong tổ chức.");
        if (input.Audience == "ORGANIZATION")
        {
            var choices = await OrganizationChoicesAsync(actor);
            if (!choices.Any(o => o.Id == input.OrganizationId && o.ResidentialGroupId == input.ResidentialGroupId))
                throw new AppFault(400, "Chọn tổ chức được phép trong thôn đã chọn.");
        }
        else if (input.Audience is not ("PUBLIC" or "RESIDENTS") || input.OrganizationId != null)
            throw new AppFault(400, "Đối tượng đọc không hợp lệ; thông báo toàn thôn không được giới hạn tổ chức.");
    }

    public Task<bool> CanUpdateAsync(ClaimsPrincipal actor, Announcement item)
    {
        var id = CurrentUserId(actor);
        return Effective(id).AnyAsync(p => p.PermissionCode == "announcements.update" &&
            (p.ScopeType == "GROUP" && p.ResidentialGroupId == item.ResidentialGroupId || p.ScopeType == "AREA" && p.ResidentialAreaId == item.ResidentialGroup.ParentUnitId) &&
            (p.RoleName == "VILLAGE_HEAD" || p.RoleName == "PARTY_SECRETARY" || item.CreatedByUserId == id) &&
            (p.OrganizationId == null || item.OrganizationId == p.OrganizationId || item.CreatedByUserId == id));
    }

    public async Task RequireUpdateAsync(ClaimsPrincipal actor, Announcement item)
    {
        if (!await CanUpdateAsync(actor, item))
            throw new AppFault(403, "Chỉ được sửa/gửi duyệt/rút lại bản soạn của mình trong phạm vi được giao.");
    }

    public Task<bool> CanSeeStatisticsAsync(ClaimsPrincipal actor, Announcement item) =>
        Effective(CurrentUserId(actor)).AnyAsync(p => p.PermissionCode == "announcements.statistics" && (p.ScopeType == "GROUP" && p.ResidentialGroupId == item.ResidentialGroupId || p.ScopeType == "AREA" && p.ResidentialAreaId == item.ResidentialGroup.ParentUnitId) && (p.OrganizationId == null || p.OrganizationId == item.OrganizationId));
}
