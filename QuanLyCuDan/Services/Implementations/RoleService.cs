using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class RoleService(MyStockContext context) : ApplicationService(context)
{
    public async Task<RoleListResult> ListAsync(ClaimsPrincipal actor)
    {
        await RequirePermission(actor, "roles.read");
        return new(await db.AspNetRoles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync(), await db.Permissions.AsNoTracking().Include(p => p.PermissionAllowedScopes).OrderBy(p => p.Code).ToListAsync());
    }
    public async Task<string> CreateAsync(ClaimsPrincipal actor, RoleInput input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "roles.create");
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            throw new AppFault(400, "Tên hiển thị không được để trống.");
        if (await db.AspNetRoles.AnyAsync(x => x.NormalizedName == input.Name.ToUpperInvariant()))
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
        db.AspNetRoles.Add(role);
        Audit(CurrentUserId(actor), "ROLE_CREATED", "Role", role.Id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return role.Id;
    }

    private async Task<AspNetRole> EditableRoleCore(string id, string stamp)
    {
        var role = await db.AspNetRoles.Include(x => x.Permissions).SingleOrDefaultAsync(x => x.Id == id) ?? throw new AppFault(404, "Không tìm thấy vai trò.");
        if (role.ConcurrencyStamp != stamp)
            throw new AppFault(409, "Vai trò đã thay đổi. Hãy tải lại.");
        role.ConcurrencyStamp = Guid.NewGuid().ToString();
        return role;
    }

    public async Task UpdateAsync(ClaimsPrincipal actor, string id, RoleEdit input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "roles.update");
        var role = await EditableRoleCore(id, input.ConcurrencyStamp);
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            throw new AppFault(400, "Tên hiển thị không được để trống.");
        role.DisplayName = input.DisplayName.Trim();
        role.Description = input.Description;
        role.IsActive = input.IsActive;
        Audit(CurrentUserId(actor), "ROLE_UPDATED", "Role", id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task SetPermissionsAsync(ClaimsPrincipal actor, string roleId, PermissionEdit input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "roles.permissions.update");
        var role = await EditableRoleCore(roleId, input.ConcurrencyStamp);
        var ids = (input.PermissionIds ?? Array.Empty<int>()).Distinct().ToArray();
        var selected = await db.Permissions.Include(x => x.PermissionAllowedScopes).Where(x => ids.Contains(x.Id)).ToListAsync();
        if (selected.Count != ids.Length)
            throw new AppFault(400, "Danh mục quyền không hợp lệ.");
        if (selected.Count > 0 && !new[] { "SELF", "GROUP", "AREA", "SYSTEM" }.Any(scope => RoleScopeRules.Supports(selected, scope)))
            throw new AppFault(400, "Các quyền nghiệp vụ phải có phạm vi tương thích; quyền cá nhân được áp dụng riêng.");
        var assignmentScopes = await db.UserRoleAssignments.Where(a => a.RoleId == roleId && a.RevokedAt == null)
            .Select(a => a.ScopeType).Distinct().ToListAsync();
        if (selected.Count > 0 && assignmentScopes.Any(scope => !RoleScopeRules.Supports(selected, scope)))
            throw new AppFault(400, "Quyền mới không tương thích với địa bàn đang gán.");
        if (role.Name is not ("VILLAGE_HEAD" or "PARTY_SECRETARY") && selected.Any(p => p.Code is "announcements.approve" or "announcements.unpublish" or "announcements.publish"))
            throw new AppFault(400, "Chỉ Trưởng thôn/Bí thư được xét duyệt và gỡ; thông báo tự đăng sau duyệt.");
        role.Permissions.Clear();
        foreach (var permission in selected)
            role.Permissions.Add(permission);
        Audit(CurrentUserId(actor), "ROLE_PERMISSIONS_UPDATED", "Role", roleId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
