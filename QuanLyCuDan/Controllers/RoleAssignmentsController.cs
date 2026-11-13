using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class RoleAssignmentsController(MyStockContext context) : BaseController(context)
{
    [HttpGet]
    public async Task<IActionResult> Index(string? userId = null)
    {
        await RequirePermission(User, "roles.assign");
        ViewData["Accounts"] = await db.AspNetUsers.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        ViewData["Roles"] = await db.AspNetRoles.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync();
        ViewData["Units"] = await db.ResidentialUnits.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync();
        ViewData["UserId"] = userId;
        return View("Index", await db.UserRoleAssignments.AsNoTracking().Include(a => a.Role).Where(a => a.UserId == userId).OrderByDescending(a => a.AssignedAt).Take(200).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Assign(string userId, AssignmentInput input) => SubmitForm(async () => { await AssignCore(User, userId, input); }, () => Index(userId), () => RedirectToAction(nameof(Index), new { userId }));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Revoke(string userId, long id, RevokeInput input) => SubmitForm(async () => { await RevokeCore(User, userId, id, input); }, () => Index(userId), () => RedirectToAction(nameof(Index), new { userId }));

    private async Task AssignCore(ClaimsPrincipal actor, string userId, AssignmentInput input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "roles.assign");
        var actorId = CurrentUserId(actor);
        if (actorId == userId)
            throw new AppFault(403, "Không thể tự gán vai trò cho chính mình.");
        if (!await db.AspNetUsers.AnyAsync(x => x.Id == userId && x.IsActive))
            throw new AppFault(404, "Tài khoản không tồn tại hoặc đã khóa.");
        var role = await db.AspNetRoles.Include(x => x.Permissions).ThenInclude(p => p.PermissionAllowedScopes).SingleOrDefaultAsync(x => x.Id == input.RoleId && x.IsActive) ?? throw new AppFault(400, "Vai trò không hợp lệ.");
        var scope = input.ScopeType.ToUpperInvariant();
        if (!new[]
        {
            "SYSTEM",
            "SELF",
            "AREA",
            "GROUP"
        }.Contains(scope) || role.Permissions.Count == 0 || role.Permissions.Any(p => !p.PermissionAllowedScopes.Any(s => s.ScopeType == scope)))
            throw new AppFault(400, "Phạm vi không tương thích với vai trò.");
        if (scope is "AREA" or "GROUP")
        {
            if (!await db.ResidentialUnits.AnyAsync(x => x.Id == input.UnitId && x.UnitType == scope && x.IsActive && (scope == "AREA" || x.ParentUnit != null && x.ParentUnit.IsActive)))
                throw new AppFault(400, "Địa bàn không hợp lệ.");
        }
        else if (input.UnitId != null)
            throw new AppFault(400, "SYSTEM/SELF không được có địa bàn.");
        if (scope != "SYSTEM" && await IsAdmin(userId))
            throw new AppFault(403, "Không gán vai trò nghiệp vụ cho tài khoản ADMIN.");
        var from = input.ValidFrom.ToUniversalTime();
        var until = input.ValidUntil?.ToUniversalTime();
        if (until != null && (until <= from || until <= DateTime.UtcNow))
            throw new AppFault(400, "Thời hạn không hợp lệ.");
        var area = scope == "AREA" ? input.UnitId : null;
        var group = scope == "GROUP" ? input.UnitId : null;
        if (await db.UserRoleAssignments.AnyAsync(a => a.UserId == userId && a.RoleId == role.Id && a.ScopeType == scope && a.ResidentialAreaId == area && a.ResidentialGroupId == group && a.RevokedAt == null && (a.ValidUntil == null || a.ValidUntil > from) && (until == null || a.ValidFrom < until)))
            throw new AppFault(409, "Đã có lần gán trùng khoảng hiệu lực.");
        db.UserRoleAssignments.Add(new UserRoleAssignment { UserId = userId, RoleId = role.Id, ScopeType = scope, ResidentialAreaId = area, ResidentialGroupId = group, ValidFrom = from, ValidUntil = until, AssignedByUserId = actorId, AssignedAt = DateTime.UtcNow });
        Audit(actorId, "ROLE_ASSIGNED", "User", userId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task RevokeCore(ClaimsPrincipal actor, string userId, long assignmentId, RevokeInput input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "roles.assign");
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new AppFault(400, "Cần nhập lý do thu hồi.");
        var assignment = await db.UserRoleAssignments.Include(x => x.Role).SingleOrDefaultAsync(a => a.Id == assignmentId && a.UserId == userId) ?? throw new AppFault(404, "Không tìm thấy lần gán.");
        if (assignment.RevokedAt != null)
            throw new AppFault(409, "Lần gán đã bị thu hồi.");
        if (assignment.Role.Name == "ADMIN")
            await ProtectLastAdmin(userId, assignmentId);
        SetVersion(assignment, input.RowVersion);
        assignment.RevokedAt = DateTime.UtcNow;
        assignment.RevocationReason = input.Reason.Trim();
        Audit(CurrentUserId(actor), "ROLE_REVOKED", "Assignment", assignmentId.ToString());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
