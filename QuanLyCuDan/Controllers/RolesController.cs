using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class RolesController(MyStockContext context) : BaseController(context)
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await RequirePermission(User, "roles.read");
        ViewData["Catalog"] = await db.Permissions.AsNoTracking().Include(p => p.PermissionAllowedScopes).OrderBy(p => p.Code).ToListAsync();
        return View("Index", await db.AspNetRoles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(RoleInput input) => SubmitForm(async () => { await CreateRoleCore(User, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Update(string id, RoleEdit input) => SubmitForm(async () => { await EditRoleCore(User, id, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Permissions(string id, PermissionEdit input) => SubmitForm(async () => { await SetPermissionsCore(User, id, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

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

    private async Task SetPermissionsCore(ClaimsPrincipal actor, string roleId, PermissionEdit input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "roles.permissions.update");
        var role = await EditableRoleCore(actor, roleId, input.ConcurrencyStamp);
        var ids = (input.PermissionIds ?? Array.Empty<int>()).Distinct().ToArray();
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
