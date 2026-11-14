using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services;

public sealed class OrganizationService(MyStockContext context) : ApplicationService(context)
{
    private async Task<IQueryable<Organization>> AccessibleAsync(ClaimsPrincipal actor, string permission)
    {
        var grants = await Effective(CurrentUserId(actor)).Where(p => p.PermissionCode == permission).ToListAsync();
        if (grants.Any(p => p.ScopeType == "SYSTEM")) return db.Organizations;
        var groups = grants.Where(p => p.ScopeType == "GROUP" && p.OrganizationId == null).Select(p => p.ResidentialGroupId!.Value).ToArray();
        var own = grants.Where(p => p.ScopeType == "GROUP" && p.OrganizationId != null).Select(p => p.OrganizationId!.Value).ToArray();
        return db.Organizations.Where(o => groups.Contains(o.ResidentialGroupId) || own.Contains(o.Id));
    }

    public async Task<OrganizationPage> GetAsync(ClaimsPrincipal actor, long? id)
    {
        var organizations = await (await AccessibleAsync(actor, "organizations.read")).AsNoTracking().Include(o => o.ResidentialGroup).OrderBy(o => o.Name).ToListAsync();
        var canManage = await Effective(CurrentUserId(actor)).AnyAsync(p => p.PermissionCode == "organizations.manage" && p.ScopeType == "SYSTEM");
        if (organizations.Count == 0 && !canManage) throw new AppFault(403, "Bạn chưa được cấp quyền xem tổ chức.");
        var selected = id == null ? organizations.FirstOrDefault() : organizations.SingleOrDefault(o => o.Id == id);
        if (id != null && selected == null) throw new AppFault(404, "Không tìm thấy tổ chức.");
        var page = new OrganizationPage { Organizations = organizations, SelectedId = selected?.Id, CanManage = canManage };
        if (canManage)
            page.Groups = await db.ResidentialUnits.AsNoTracking().Where(g => g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.IsActive).OrderBy(g => g.Name).ToListAsync();
        if (selected != null)
        {
            page.CanManageMembers = selected.IsActive && await (await AccessibleAsync(actor, "organizations.members.manage")).AnyAsync(o => o.Id == selected.Id);
            page.Members = await db.OrganizationMembers.AsNoTracking().Include(m => m.Resident).Where(m => m.OrganizationId == selected.Id && m.LeftAt == null).OrderBy(m => m.Resident.FullName).ToListAsync();
            if (page.CanManageMembers)
                page.Candidates = await db.Residents.AsNoTracking().Where(r => r.Household != null && !r.Household.IsArchived && r.Household.ResidentialGroupId == selected.ResidentialGroupId && !db.OrganizationMembers.Any(m => m.OrganizationId == selected.Id && m.ResidentId == r.Id && m.LeftAt == null)).OrderBy(r => r.FullName).ToListAsync();
        }
        return page;
    }

    public async Task CreateAsync(ClaimsPrincipal actor, OrganizationInput input)
    {
        await RequirePermission(actor, "organizations.manage");
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) throw new AppFault(400, "Nhập mã và tên tổ chức.");
        if (!await db.ResidentialUnits.AnyAsync(g => g.Id == input.ResidentialGroupId && g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.IsActive))
            throw new AppFault(400, "Chọn thôn đang hoạt động.");
        if (await db.Organizations.AnyAsync(o => o.Code == input.Code.Trim())) throw new AppFault(409, "Mã tổ chức đã tồn tại.");
        db.Organizations.Add(new Organization { Code = input.Code.Trim(), Name = input.Name.Trim(), ResidentialGroupId = input.ResidentialGroupId });
        Audit(CurrentUserId(actor), "ORGANIZATION_CREATED", "Organization", input.Code.Trim());
        await db.SaveChangesAsync();
    }

    public async Task SetMemberAsync(ClaimsPrincipal actor, long id, long residentId, bool active, string? rowVersion)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var org = await (await AccessibleAsync(actor, "organizations.members.manage")).SingleOrDefaultAsync(o => o.Id == id && o.IsActive)
            ?? throw new AppFault(403, "Bạn không có quyền quản lý thành viên tổ chức này.");
        var member = await db.OrganizationMembers.SingleOrDefaultAsync(m => m.OrganizationId == id && m.ResidentId == residentId);
        if (active)
        {
            if (!await db.Residents.AnyAsync(r => r.Id == residentId && r.Household != null && !r.Household.IsArchived && r.Household.ResidentialGroupId == org.ResidentialGroupId))
                throw new AppFault(400, "Thành viên phải có hồ sơ cư dân trong thôn của tổ chức.");
            if (member == null)
                db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = id, ResidentId = residentId, JoinedAt = DateTime.UtcNow });
            else if (member.LeftAt != null)
            {
                member.JoinedAt = DateTime.UtcNow;
                member.LeftAt = null;
            }
        }
        else
        {
            if (member == null) throw new AppFault(404, "Không tìm thấy thành viên.");
            SetVersion(member, rowVersion ?? "");
            member.LeftAt ??= DateTime.UtcNow;
        }
        Audit(CurrentUserId(actor), active ? "ORGANIZATION_MEMBER_ADDED" : "ORGANIZATION_MEMBER_REMOVED", "Organization", id.ToString());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
