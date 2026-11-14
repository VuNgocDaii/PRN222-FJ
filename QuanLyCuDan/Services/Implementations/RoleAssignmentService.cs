using System.Data;
using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class RoleAssignmentService(
    IAdminGuard guard,
    IPermissionChecker checker,
    IUserRepository users,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    IAuditRepository audit,
    IUnitOfWork unitOfWork) : IRoleAssignmentService
{
    public async Task<AssignmentListResult> ListAsync(ClaimsPrincipal actor, string? userId)
    {
        await guard.RequireAsync(actor, "roles.assign");
        return new(await users.GetAllAsync(), await roles.ListActiveWithScopesAsync(), await assignments.ActiveUnitsAsync(),
            await assignments.ListForUserAsync(userId), userId, await assignments.ActiveOrganizationsAsync());
    }

    public async Task AssignAsync(ClaimsPrincipal actor, string userId, AssignmentInput input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "roles.assign");
        var actorId = checker.UserId(actor);
        if (actorId == userId)
            throw new AppFault(403, "Không thể tự gán vai trò cho chính mình.");
        if (!await users.ActiveUserExistsAsync(userId))
            throw new AppFault(404, "Tài khoản không tồn tại hoặc đã khóa.");
        var role = await roles.FindActiveWithScopesAsync(input.RoleId) ?? throw new AppFault(400, "Vai trò không hợp lệ.");
        var scope = input.ScopeType.ToUpperInvariant();
        if (!new[] { "SYSTEM", "SELF", "AREA", "GROUP" }.Contains(scope) || !RoleScopeRules.Supports(role.Permissions, scope))
            throw new AppFault(400, "Phạm vi không tương thích với vai trò.");
        if (scope is "AREA" or "GROUP")
        {
            if (!await assignments.UnitValidAsync(input.UnitId, scope))
                throw new AppFault(400, "Địa bàn không hợp lệ.");
        }
        else if (input.UnitId != null)
            throw new AppFault(400, "SYSTEM/SELF không được có địa bàn.");
        if (scope != "SYSTEM" && await users.IsAdminAsync(userId))
            throw new AppFault(403, "Không gán vai trò nghiệp vụ cho tài khoản ADMIN.");
        var from = input.ValidFrom.ToUniversalTime();
        var until = input.ValidUntil?.ToUniversalTime();
        if (until != null && (until <= from || until <= DateTime.UtcNow))
            throw new AppFault(400, "Thời hạn không hợp lệ.");
        var area = scope == "AREA" ? input.UnitId : null;
        var group = scope == "GROUP" ? input.UnitId : null;
        if (input.OrganizationId != null && (scope != "GROUP" || !await assignments.OrganizationInGroupAsync(input.OrganizationId, group)))
            throw new AppFault(400, "Tổ chức không thuộc thôn đã chọn hoặc không còn hoạt động.");
        if (role.Name is "VILLAGE_HEAD" or "PARTY_SECRETARY" && (scope != "GROUP" || input.OrganizationId != null))
            throw new AppFault(400, "Trưởng thôn/Bí thư phải được gán phạm vi thôn, không giới hạn tổ chức.");
        if (role.Name == "ORGANIZATION_HEAD" && input.OrganizationId == null)
            throw new AppFault(400, "Chọn tổ chức do tài khoản quản lý.");
        foreach (var expired in await assignments.ExpiredAsync(userId))
        {
            expired.RevokedAt = DateTime.UtcNow;
            expired.RevocationReason = "Hết hạn trước lần gán mới.";
        }
        await unitOfWork.SaveChangesAsync();
        if (await assignments.HasActiveAsync(userId))
            throw new AppFault(409, "Tài khoản chỉ có một vai trò. Hãy thu hồi vai trò hiện tại trước khi gán vai trò mới.");
        assignments.Add(new UserRoleAssignment { UserId = userId, RoleId = role.Id, ScopeType = scope, OrganizationId = input.OrganizationId, ResidentialAreaId = area, ResidentialGroupId = group, ValidFrom = from, ValidUntil = until, AssignedByUserId = actorId, AssignedAt = DateTime.UtcNow });
        audit.Add(actorId, "ROLE_ASSIGNED", "User", userId);
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task RevokeAsync(ClaimsPrincipal actor, string userId, long assignmentId, RevokeInput input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "roles.assign");
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new AppFault(400, "Cần nhập lý do thu hồi.");
        var assignment = await assignments.FindWithRoleAsync(userId, assignmentId) ?? throw new AppFault(404, "Không tìm thấy lần gán.");
        if (assignment.RevokedAt != null)
            throw new AppFault(409, "Lần gán đã bị thu hồi.");
        if (assignment.Role.Name == "ADMIN")
            await guard.EnsureNotLastAdminAsync(userId, assignmentId);
        unitOfWork.SetRowVersion(assignment, input.RowVersion);
        assignment.RevokedAt = DateTime.UtcNow;
        assignment.RevocationReason = input.Reason.Trim();
        audit.Add(checker.UserId(actor), "ROLE_REVOKED", "Assignment", assignmentId.ToString());
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
