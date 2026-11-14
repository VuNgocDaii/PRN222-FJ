using System.Data;
using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class OrganizationService(
    IPermissionChecker checker,
    IOrganizationRepository organizations,
    IResidentialUnitRepository units,
    IAuditRepository audit,
    IUnitOfWork unitOfWork) : IOrganizationService
{
    private async Task<(bool All, long[] Groups, long[] Own)> ScopeAsync(ClaimsPrincipal actor, string permission)
    {
        var grants = await checker.GrantsAsync(actor, permission);
        return (grants.Any(p => p.ScopeType == "SYSTEM"),
            grants.Where(p => p.ScopeType == "GROUP" && p.OrganizationId == null).Select(p => p.ResidentialGroupId!.Value).ToArray(),
            grants.Where(p => p.ScopeType == "GROUP" && p.OrganizationId != null).Select(p => p.OrganizationId!.Value).ToArray());
    }

    public async Task<OrganizationPage> GetAsync(ClaimsPrincipal actor, long? id)
    {
        var read = await ScopeAsync(actor, "organizations.read");
        var list = await organizations.GetAccessibleAsync(read.All, read.Groups, read.Own);
        var canManage = await checker.HasSystemPermissionAsync(actor, "organizations.manage");
        if (list.Count == 0 && !canManage)
            throw new AppFault(403, "Bạn chưa được cấp quyền xem tổ chức.");
        var selected = id == null ? list.FirstOrDefault() : list.SingleOrDefault(o => o.Id == id);
        if (id != null && selected == null)
            throw new AppFault(404, "Không tìm thấy tổ chức.");
        var page = new OrganizationPage { Organizations = list, SelectedId = selected?.Id, CanManage = canManage };
        if (canManage)
            page.Groups = await units.GetActiveGroupsAsync();
        if (selected != null)
        {
            var manage = await ScopeAsync(actor, "organizations.members.manage");
            page.CanManageMembers = selected.IsActive &&
                await organizations.FindActiveAccessibleAsync(manage.All, manage.Groups, manage.Own, selected.Id) != null;
            page.Members = await organizations.GetMembersAsync(selected.Id);
            if (page.CanManageMembers)
                page.Candidates = await organizations.GetCandidatesAsync(selected);
        }
        return page;
    }

    public async Task CreateAsync(ClaimsPrincipal actor, OrganizationInput input)
    {
        if (!await checker.HasSystemPermissionAsync(actor, "organizations.manage"))
            throw new AppFault(403, "Bạn không có quyền thực hiện thao tác này.");
        var code = input.Code?.Trim() ?? "";
        var name = input.Name?.Trim() ?? "";
        if (code.Length == 0 || name.Length == 0)
            throw new AppFault(400, "Nhập mã và tên tổ chức.");
        if (!await units.IsActiveGroupAsync(input.ResidentialGroupId))
            throw new AppFault(400, "Chọn thôn đang hoạt động.");
        if (await organizations.CodeExistsAsync(code))
            throw new AppFault(409, "Mã tổ chức đã tồn tại.");
        organizations.Add(new Organization { Code = code, Name = name, ResidentialGroupId = input.ResidentialGroupId });
        audit.Add(checker.UserId(actor), "ORGANIZATION_CREATED", "Organization", code);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task SetMemberAsync(ClaimsPrincipal actor, long id, long residentId, bool active, string? rowVersion)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var scope = await ScopeAsync(actor, "organizations.members.manage");
        var org = await organizations.FindActiveAccessibleAsync(scope.All, scope.Groups, scope.Own, id)
            ?? throw new AppFault(403, "Bạn không có quyền quản lý thành viên tổ chức này.");
        var member = await organizations.FindMemberAsync(id, residentId);
        if (active)
        {
            if (!await organizations.ResidentInGroupAsync(residentId, org.ResidentialGroupId))
                throw new AppFault(400, "Thành viên phải có hồ sơ cư dân trong thôn của tổ chức.");
            if (member == null)
                organizations.AddMember(new OrganizationMember { OrganizationId = id, ResidentId = residentId, JoinedAt = DateTime.UtcNow });
            else if (member.LeftAt != null)
            {
                member.JoinedAt = DateTime.UtcNow;
                member.LeftAt = null;
            }
        }
        else
        {
            if (member == null)
                throw new AppFault(404, "Không tìm thấy thành viên.");
            unitOfWork.SetRowVersion(member, rowVersion ?? "");
            member.LeftAt ??= DateTime.UtcNow;
        }
        audit.Add(checker.UserId(actor), active ? "ORGANIZATION_MEMBER_ADDED" : "ORGANIZATION_MEMBER_REMOVED", "Organization", id.ToString());
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
