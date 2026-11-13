using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class RegistrationService(MyStockContext context) : ApplicationService(context)
{
    public async Task EnsureReviewerAsync(ClaimsPrincipal actor)
    {
        await RequirePermission(actor, "accounts.read");
        await RequirePermission(actor, "accounts.update");
        if (!await IsAdmin(CurrentUserId(actor)))
            throw new AppFault(403, "Chỉ quản trị viên được duyệt đăng ký.");
    }

    public async Task<RegistrationListViewModel> ListAsync(ClaimsPrincipal actor, string status)
    {
        await EnsureReviewerAsync(actor);
        if (status != "APPROVED" && status != "REJECTED")
            status = "PENDING";
        return new RegistrationListViewModel
        {
            Status = status,
            Users = await db.AspNetUsers.AsNoTracking().Where(u => u.VerificationStatus == status)
                .OrderByDescending(u => u.CreatedAt).Take(100).ToListAsync(),
            Groups = await db.ResidentialUnits.AsNoTracking().Where(g => g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA").OrderBy(g => g.Name).ToListAsync(),
            Residents = await db.Residents.AsNoTracking()
                .Where(r => !db.AspNetUsers.Any(u => u.ResidentId == r.Id))
                .OrderBy(r => r.FullName).ToListAsync()
        };
    }

    public async Task ReviewAsync(ClaimsPrincipal actor, RegistrationReviewInput input)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await EnsureReviewerAsync(actor);
        string reviewerId = CurrentUserId(actor);
        if (reviewerId == input.UserId)
            throw new AppFault(403, "Không thể tự duyệt tài khoản của mình.");

        var user = await db.AspNetUsers.SingleOrDefaultAsync(u => u.Id == input.UserId);
        if (user == null)
            throw new AppFault(404, "Không tìm thấy tài khoản.");
        var completingLink = user.VerificationStatus == "APPROVED" && user.ResidentId == null && input.Approve;
        if (user.VerificationStatus != "PENDING" && !completingLink)
            throw new AppFault(409, "Tài khoản đã được xét duyệt. Hãy tải lại danh sách.");
        if (Convert.ToBase64String(user.RowVersion) != input.RowVersion)
            throw new AppFault(409, "Tài khoản đã thay đổi. Hãy tải lại danh sách.");
        if (!user.IsActive)
            throw new AppFault(400, "Tài khoản đang bị khóa.");

        if (input.Approve)
            await ApproveAsync(actor, user, input, completingLink, reviewerId);
        else
            Reject(user, input.Reason);

        user.ReviewedByUserId = reviewerId;
        user.ReviewedAt = DateTime.UtcNow;
        user.PermissionVersion++;
        Audit(reviewerId, completingLink ? "RESIDENT_LINK_COMPLETED" : input.Approve ? "REGISTRATION_APPROVED" : "REGISTRATION_REJECTED", "User", user.Id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task ApproveAsync(ClaimsPrincipal actor, AspNetUser user,
        RegistrationReviewInput input, bool completingLink, string reviewerId)
    {
        await RequirePermission(actor, "roles.assign");
        if (await IsAdmin(user.Id))
            throw new AppFault(403, "Không liên kết hồ sơ cư dân cho tài khoản quản trị hệ thống.");
        if (input.CreateResident)
            throw new AppFault(400, "Hãy nhờ cán bộ tạo hồ sơ cư dân trước khi duyệt tài khoản.");
        var profile = RegistrationProfileInput.Read(user.RegistrationProfile);
        var citizenId = profile?.CitizenId?.Trim();
        var resident = string.IsNullOrEmpty(citizenId) ? null
            : await db.Residents.SingleOrDefaultAsync(r => r.CitizenId == citizenId
                && !db.AspNetUsers.Any(u => u.ResidentId == r.Id && u.Id != user.Id));
        if (input.ResidentId.HasValue && input.ResidentId != resident?.Id)
            throw new AppFault(400, "Hồ sơ không khớp hoặc đã liên kết tài khoản khác. Hãy tải lại danh sách.");
        if (completingLink && resident == null)
            throw new AppFault(400, "Tài khoản đã được duyệt. Chưa có hồ sơ phù hợp để liên kết thêm.");
        user.ResidentId = resident?.Id;
        user.VerificationStatus = "APPROVED";
        user.RejectionReason = null;

        await EnsureResidentRolesAsync(user, reviewerId);
    }

    private async Task EnsureResidentRolesAsync(AspNetUser user, string reviewerId)
    {
        var roleName = user.ResidentId == null ? "ACCOUNT" : "RESIDENT";
        var role = await db.AspNetRoles.SingleOrDefaultAsync(r => r.IsActive && r.Name == roleName)
            ?? throw new AppFault(400, "Thiếu vai trò tài khoản/cư dân. Kiểm tra dữ liệu khởi tạo.");
        var current = await db.UserRoleAssignments.Include(a => a.Role)
            .SingleOrDefaultAsync(a => a.UserId == user.Id && a.RevokedAt == null);
        if (current != null && (current.RoleId == role.Id || current.Role.Name is not ("ACCOUNT" or "RESIDENT")))
            return;
        if (current != null)
        {
            current.RevokedAt = DateTime.UtcNow;
            current.RevocationReason = "Chuyển vai trò sau xác minh hồ sơ cư dân.";
            await db.SaveChangesAsync();
        }
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            UserId = user.Id, RoleId = role.Id, ScopeType = "SELF",
            AssignedByUserId = reviewerId, AssignedAt = DateTime.UtcNow, ValidFrom = DateTime.UtcNow
        });
    }

    private static void Reject(AspNetUser user, string? reason)
    {
        user.VerificationStatus = "REJECTED";
        user.RejectionReason = string.IsNullOrWhiteSpace(reason)
            ? "Đăng ký đã bị từ chối."
            : reason.Trim();
    }

}
