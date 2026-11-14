using System.Data;
using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class RoleService(
    IAdminGuard guard,
    IPermissionChecker checker,
    IRoleRepository roles,
    IAuditRepository audit,
    IUnitOfWork unitOfWork) : IRoleService
{
    public async Task<RoleListResult> ListAsync(ClaimsPrincipal actor)
    {
        await guard.RequireAsync(actor, "roles.read");
        return new(await roles.ListWithPermissionsAsync(), await roles.CatalogAsync());
    }

    public async Task<string> CreateAsync(ClaimsPrincipal actor, RoleInput input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "roles.create");
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            throw new AppFault(400, "Tên hiển thị không được để trống.");
        if (await roles.NameExistsAsync(input.Name.ToUpperInvariant()))
            throw new AppFault(409, "Mã vai trò đã tồn tại.");
        var role = new AspNetRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = input.Name.ToUpperInvariant(),
            NormalizedName = input.Name.ToUpperInvariant(),
            DisplayName = input.DisplayName.Trim(),
            Description = input.Description,
            IsActive = true,
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        roles.Add(role);
        audit.Add(checker.UserId(actor), "ROLE_CREATED", "Role", role.Id);
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return role.Id;
    }

    public async Task UpdateAsync(ClaimsPrincipal actor, string id, RoleEdit input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "roles.update");
        var role = await EditableRoleAsync(id, input.ConcurrencyStamp);
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            throw new AppFault(400, "Tên hiển thị không được để trống.");
        role.DisplayName = input.DisplayName.Trim();
        role.Description = input.Description;
        role.IsActive = input.IsActive;
        audit.Add(checker.UserId(actor), "ROLE_UPDATED", "Role", id);
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task SetPermissionsAsync(ClaimsPrincipal actor, string roleId, PermissionEdit input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "roles.permissions.update");
        var role = await EditableRoleAsync(roleId, input.ConcurrencyStamp);
        var ids = (input.PermissionIds ?? Array.Empty<int>()).Distinct().ToArray();
        var selected = await roles.FindPermissionsAsync(ids);
        if (selected.Count != ids.Length)
            throw new AppFault(400, "Danh mục quyền không hợp lệ.");
        if (selected.Count > 0 && !new[] { "SELF", "GROUP", "AREA", "SYSTEM" }.Any(scope => RoleScopeRules.Supports(selected, scope)))
            throw new AppFault(400, "Các quyền nghiệp vụ phải có phạm vi tương thích; quyền cá nhân được áp dụng riêng.");
        var assignmentScopes = await roles.ActiveAssignmentScopesAsync(roleId);
        if (selected.Count > 0 && assignmentScopes.Any(scope => !RoleScopeRules.Supports(selected, scope)))
            throw new AppFault(400, "Quyền mới không tương thích với địa bàn đang gán.");
        if (role.Name is not ("VILLAGE_HEAD" or "PARTY_SECRETARY" or "ORGANIZATION_HEAD") && selected.Any(p => p.Code is "announcements.approve" or "announcements.unpublish" or "announcements.publish"))
            throw new AppFault(400, "Chỉ Trưởng thôn/Bí thư/Trưởng tổ chức được xét duyệt và gỡ; thông báo tự đăng sau duyệt.");
        role.Permissions.Clear();
        foreach (var permission in selected)
            role.Permissions.Add(permission);
        audit.Add(checker.UserId(actor), "ROLE_PERMISSIONS_UPDATED", "Role", roleId);
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<AspNetRole> EditableRoleAsync(string id, string stamp)
    {
        var role = await roles.FindWithPermissionsAsync(id) ?? throw new AppFault(404, "Không tìm thấy vai trò.");
        if (role.ConcurrencyStamp != stamp)
            throw new AppFault(409, "Vai trò đã thay đổi. Hãy tải lại.");
        role.ConcurrencyStamp = Guid.NewGuid().ToString();
        return role;
    }
}
