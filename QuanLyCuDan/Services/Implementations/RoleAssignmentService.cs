using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class RoleAssignmentService(MyStockContext context) : ApplicationService(context)
{
    public async Task<AssignmentListResult> ListAsync(ClaimsPrincipal actor, string? userId)
    {
        await RequirePermission(actor, "roles.assign");
        return new(await db.AspNetUsers.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(),
            await db.AspNetRoles.AsNoTracking().Include(r => r.Permissions).ThenInclude(p => p.PermissionAllowedScopes).Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync(),
            await db.ResidentialUnits.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync(),
            await db.UserRoleAssignments.AsNoTracking().Include(a => a.Role).Where(a => a.UserId == userId).OrderByDescending(a => a.AssignedAt).Take(200).ToListAsync(), userId,
            await db.Organizations.AsNoTracking().Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync());
    }
    public async Task AssignAsync(ClaimsPrincipal actor, string userId, AssignmentInput input)
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
        }.Contains(scope) || !RoleScopeRules.Supports(role.Permissions, scope))
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
        if (input.OrganizationId != null && (scope != "GROUP" ||
            !await db.Organizations.AnyAsync(o => o.Id == input.OrganizationId && o.IsActive && o.ResidentialGroupId == group)))
            throw new AppFault(400, "Tổ chức không thuộc thôn đã chọn hoặc không còn hoạt động.");
        if (role.Name is "VILLAGE_HEAD" or "PARTY_SECRETARY" && (scope != "GROUP" || input.OrganizationId != null))
            throw new AppFault(400, "Trưởng thôn/Bí thư phải được gán phạm vi thôn, không giới hạn tổ chức.");
        if (role.Name == "ORGANIZATION_HEAD" && input.OrganizationId == null)
            throw new AppFault(400, "Chọn tổ chức do tài khoản quản lý.");
        var expired = await db.UserRoleAssignments.Where(a => a.UserId == userId && a.RevokedAt == null && a.ValidUntil <= DateTime.UtcNow).ToListAsync();
        foreach (var assignment in expired)
        {
            assignment.RevokedAt = DateTime.UtcNow;
            assignment.RevocationReason = "Hết hạn trước lần gán mới.";
        }
        await db.SaveChangesAsync();
        if (await db.UserRoleAssignments.AnyAsync(a => a.UserId == userId && a.RevokedAt == null))
            throw new AppFault(409, "Tài khoản chỉ có một vai trò. Hãy thu hồi vai trò hiện tại trước khi gán vai trò mới.");
        db.UserRoleAssignments.Add(new UserRoleAssignment { UserId = userId, RoleId = role.Id, ScopeType = scope, OrganizationId = input.OrganizationId, ResidentialAreaId = area, ResidentialGroupId = group, ValidFrom = from, ValidUntil = until, AssignedByUserId = actorId, AssignedAt = DateTime.UtcNow });
        Audit(actorId, "ROLE_ASSIGNED", "User", userId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task RevokeAsync(ClaimsPrincipal actor, string userId, long assignmentId, RevokeInput input)
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
