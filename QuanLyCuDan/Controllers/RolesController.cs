using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
[ApiController, Route("api/roles")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class RolesController(MyStockContext context) : BaseController(context)
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        return Ok(await RolesCore(User));
    }

    [HttpPost]
    public async Task<IActionResult> Create(RoleInput input)
    {
        return Ok(await CreateRoleCore(User, input));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, RoleEdit input)
    {
        await EditRoleCore(User, id, input);
        return NoContent();
    }

    [HttpPut("{id}/permissions")]
    public async Task<IActionResult> Permissions(string id, PermissionEdit input)
    {
        await SetPermissionsCore(User, id, input);
        return NoContent();
    }

    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetPermissions(string id)
    {
        return Ok(await RolePermissionsCore(User, id));
    }

    private async Task<object> RolesCore(ClaimsPrincipal actor)
    {
        await RequirePermission(actor, "roles.read");
        return await db.AspNetRoles.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.DisplayName, x.Description, x.IsActive, x.IsSystemRole, x.ConcurrencyStamp, permissionIds = x.Permissions.Select(p => p.Id).ToArray() }).ToListAsync();
    }

    private async Task<object> CreateRoleCore(ClaimsPrincipal actor, RoleInput input)
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
        return new
        {
            role.Id
        };
    }

    private async Task<AspNetRole> EditableRoleCore(ClaimsPrincipal actor, string id, string stamp)
    {
        var role = await db.AspNetRoles.Include(x => x.Permissions).SingleOrDefaultAsync(x => x.Id == id) ?? throw new AppFault(404, "Không tìm thấy vai trò.");
        if (role.IsSystemRole)
            throw new AppFault(400, "Vai trò hệ thống được bảo vệ. Hãy tạo vai trò tùy chỉnh.");
        if (role.ConcurrencyStamp != stamp)
            throw new AppFault(409, "Vai trò đã thay đổi. Hãy tải lại.");
        var userId = CurrentUserId(actor);
        if (await db.UserRoleAssignments.AnyAsync(a => a.UserId == userId && a.RoleId == id && a.RevokedAt == null && (a.ValidUntil == null || a.ValidUntil > DateTime.UtcNow)))
            throw new AppFault(403, "Không thể sửa vai trò đang hoặc sẽ được gán cho chính bạn.");
        role.ConcurrencyStamp = Guid.NewGuid().ToString();
        return role;
    }

    private async Task EditRoleCore(ClaimsPrincipal actor, string id, RoleEdit input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "roles.update");
        var role = await EditableRoleCore(actor, id, input.ConcurrencyStamp);
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            throw new AppFault(400, "Tên hiển thị không được để trống.");
        role.DisplayName = input.DisplayName.Trim();
        role.Description = input.Description;
        role.IsActive = input.IsActive;
        Audit(CurrentUserId(actor), "ROLE_UPDATED", "Role", id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<object> RolePermissionsCore(ClaimsPrincipal actor, string roleId)
    {
        await RequirePermission(actor, "roles.read");
        await RequirePermission(actor, "permissions.read");

        var role = await db.AspNetRoles
            .AsNoTracking()
            .Where(item => item.Id == roleId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.DisplayName,
                Permissions = item.Permissions
                    .OrderBy(permission => permission.Code)
                    .Select(permission => new
                    {
                        permission.Id,
                        permission.Code,
                        permission.Name,
                        permission.Module
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync();

        return role ?? throw new AppFault(404, "Không tìm thấy Role.");
    }

    private async Task SetPermissionsCore(ClaimsPrincipal actor, string roleId, PermissionEdit input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "roles.permissions.update");
        var role = await EditableRoleCore(actor, roleId, input.ConcurrencyStamp);
        var ids = input.PermissionIds.Distinct().ToArray();
        var selected = await db.Permissions.Include(x => x.PermissionAllowedScopes).Where(x => ids.Contains(x.Id)).ToListAsync();
        if (selected.Count != ids.Length)
            throw new AppFault(400, "Danh mục quyền không hợp lệ.");
        if (selected.Count > 0 && !selected.Select(p => p.PermissionAllowedScopes.Select(s => s.ScopeType).ToHashSet()).Aggregate((a, b) =>
        {
            a.IntersectWith(b);
            return a;
        }).Any())
            throw new AppFault(400, "Các quyền phải có ít nhất một phạm vi tương thích chung.");
        var assignmentScopes = await db.UserRoleAssignments.Where(a => a.RoleId == roleId && a.RevokedAt == null && (a.ValidUntil == null || a.ValidUntil > DateTime.UtcNow)).Select(a => a.ScopeType).Distinct().ToListAsync();
        if (selected.Any(p => assignmentScopes.Any(s => !p.PermissionAllowedScopes.Any(a => a.ScopeType == s))))
            throw new AppFault(400, "Quyền mới không tương thích với phạm vi đang gán. Hãy thu hồi các lần gán đó trước.");
        role.Permissions.Clear();
        foreach (var permission in selected)
            role.Permissions.Add(permission);
        Audit(CurrentUserId(actor), "ROLE_PERMISSIONS_UPDATED", "Role", roleId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
