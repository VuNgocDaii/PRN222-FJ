using System.Data;
using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class RegistrationService(
    IAdminGuard guard,
    IPermissionChecker checker,
    IUserRepository users,
    IRegistrationRepository registrations,
    IResidentialUnitRepository units,
    IAuditRepository audit,
    IUnitOfWork unitOfWork) : IRegistrationService
{
    public async Task EnsureReviewerAsync(ClaimsPrincipal actor)
    {
        await guard.RequireAsync(actor, "accounts.read");
        await guard.RequireAsync(actor, "accounts.update");
        if (!await users.IsAdminAsync(checker.UserId(actor)))
            throw new AppFault(403, "Chỉ quản trị viên được duyệt đăng ký.");
    }

    public async Task<RegistrationListViewModel> ListAsync(ClaimsPrincipal actor, string status)
    {
        await EnsureReviewerAsync(actor);
        if (status != "APPROVED" && status != "REJECTED")
            status = "PENDING";
        var residents = await registrations.GetUnlinkedResidentsAsync();
        return new RegistrationListViewModel
        {
            Status = status,
            Users = await registrations.ListByStatusAsync(status, 100),
            Groups = await units.GetRegistrableGroupsAsync(),
            Residents = residents,
            HeadNames = await registrations.GetHeadNamesAsync(residents)
        };
    }

    public async Task ReviewAsync(ClaimsPrincipal actor, RegistrationReviewInput input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await EnsureReviewerAsync(actor);
        var reviewerId = checker.UserId(actor);
        if (reviewerId == input.UserId)
            throw new AppFault(403, "Không thể tự duyệt tài khoản của mình.");
        var user = await users.FindAsync(input.UserId) ?? throw new AppFault(404, "Không tìm thấy tài khoản.");
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
        audit.Add(reviewerId, completingLink ? "RESIDENT_LINK_COMPLETED" : input.Approve ? "REGISTRATION_APPROVED" : "REGISTRATION_REJECTED", "User", user.Id);
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task ApproveAsync(ClaimsPrincipal actor, AspNetUser user, RegistrationReviewInput input, bool completingLink, string reviewerId)
    {
        await guard.RequireAsync(actor, "roles.assign");
        if (await users.IsAdminAsync(user.Id))
            throw new AppFault(403, "Không liên kết hồ sơ cư dân cho tài khoản quản trị hệ thống.");
        if (input.CreateResident)
            throw new AppFault(400, "Hãy nhờ cán bộ tạo hồ sơ cư dân trước khi duyệt tài khoản.");
        var profile = RegistrationProfileInput.Read(user.RegistrationProfile);
        var citizenId = profile?.CitizenId?.Trim();
        var resident = string.IsNullOrEmpty(citizenId) ? null : await registrations.FindLinkableResidentAsync(citizenId, user.Id);
        if (input.ResidentId.HasValue && input.ResidentId != resident?.Id)
            throw new AppFault(400, "Hồ sơ không khớp hoặc đã liên kết tài khoản khác. Hãy tải lại danh sách.");
        if (completingLink && resident == null)
            throw new AppFault(400, "Tài khoản đã được duyệt. Chưa có hồ sơ phù hợp để liên kết thêm.");
        user.ResidentId = resident?.Id;
        user.VerificationStatus = "APPROVED";
        user.RejectionReason = null;
        await EnsureResidentRoleAsync(user, reviewerId);
    }

    private async Task EnsureResidentRoleAsync(AspNetUser user, string reviewerId)
    {
        var roleName = user.ResidentId == null ? "ACCOUNT" : "RESIDENT";
        var role = await users.FindActiveRoleByNameAsync(roleName)
            ?? throw new AppFault(400, "Thiếu vai trò tài khoản/cư dân. Kiểm tra dữ liệu khởi tạo.");
        var current = await users.GetCurrentAssignmentAsync(user.Id);
        if (current != null && (current.RoleId == role.Id || current.Role.Name is not ("ACCOUNT" or "RESIDENT")))
            return;
        if (current != null)
        {
            current.RevokedAt = DateTime.UtcNow;
            current.RevocationReason = "Chuyển vai trò sau xác minh hồ sơ cư dân.";
            await unitOfWork.SaveChangesAsync();
        }
        users.AddAssignment(new UserRoleAssignment
        {
            UserId = user.Id, RoleId = role.Id, ScopeType = "SELF",
            AssignedByUserId = reviewerId, AssignedAt = DateTime.UtcNow, ValidFrom = DateTime.UtcNow
        });
    }

    private static void Reject(AspNetUser user, string? reason)
    {
        user.VerificationStatus = "REJECTED";
        user.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Đăng ký đã bị từ chối." : reason.Trim();
    }
}
