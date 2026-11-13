using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace QuanLyCuDan.Controllers;

[Authorize(AuthenticationSchemes = "Cookies")]
public class RegistrationsController(MyStockContext context) : BaseController(context)
{
    [HttpGet]
    public async Task<IActionResult> Index(string status = "PENDING")
    {
        return View(await ListRegistrations(User, status));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Index(RegistrationReviewInput input, string status = "PENDING")
    {
        return Review(input, status);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(RegistrationReviewInput input, string status = "PENDING")
    {
        await RequireReviewer(User);
        if (!input.Approve && string.IsNullOrWhiteSpace(input.Reason))
            input.Reason = "Đăng ký đã bị từ chối.";
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu không hợp lệ. Lý do tối đa 1000 ký tự. Hãy tải lại danh sách.";
            return RedirectToAction(nameof(Index), new { status });
        }
        try
        {
            await ReviewRegistration(User, input);
            TempData["Message"] = input.Approve ? "Đã liên kết hồ sơ và cấp quyền cư dân." : "Đã từ chối đăng ký.";
        }
        catch (AppFault error) when (error.Status == 400 || error.Status == 404 || error.Status == 409)
        {
            TempData["Error"] = error.Message;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Dữ liệu đã thay đổi. Hãy kiểm tra lại danh sách.";
        }
        return RedirectToAction(nameof(Index), new { status });
    }

    private async Task RequireReviewer(ClaimsPrincipal actor)
    {
        await RequirePermission(actor, "accounts.read");
        await RequirePermission(actor, "accounts.update");
        if (!await IsAdmin(CurrentUserId(actor)))
            throw new AppFault(403, "Chỉ quản trị viên được duyệt đăng ký.");
    }

    private async Task<RegistrationListViewModel> ListRegistrations(ClaimsPrincipal actor, string status)
    {
        await RequireReviewer(actor);
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

    private async Task ReviewRegistration(ClaimsPrincipal actor, RegistrationReviewInput input)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequireReviewer(actor);
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
        {
            if (!input.Verified)
                throw new AppFault(400, "Cần xác nhận đã đối chiếu đúng thông tin cư dân trước khi cấp quyền.");
            await RequirePermission(actor, "roles.assign");
            if (await IsAdmin(user.Id))
                throw new AppFault(403, "Không liên kết hồ sơ cư dân cho tài khoản quản trị hệ thống.");
            if (input.CreateResident)
                throw new AppFault(400, "Hãy nhờ cán bộ tạo hồ sơ cư dân trước khi duyệt tài khoản.");
            var profile = RegistrationProfileInput.Read(user.RegistrationProfile);
            var citizenId = profile?.CitizenId?.Trim();
            if (string.IsNullOrEmpty(citizenId))
                throw new AppFault(400, "Đăng ký chưa có CCCD. Hãy bổ sung CCCD trước khi duyệt.");
            var resident = await db.Residents.SingleOrDefaultAsync(r => r.CitizenId == citizenId);
            if (resident == null)
                throw new AppFault(400, "Chưa có hồ sơ trùng CCCD. Hãy nhờ cán bộ tạo hồ sơ trước rồi tải lại danh sách.");
            if (input.ResidentId.HasValue && input.ResidentId != resident.Id)
                throw new AppFault(400, "Hồ sơ xác nhận không khớp CCCD đăng ký. Hãy tải lại danh sách.");
            if (await db.AspNetUsers.AnyAsync(u => u.ResidentId == resident.Id && u.Id != user.Id))
                throw new AppFault(409, "Hồ sơ cư dân này đã liên kết tài khoản khác.");
            user.ResidentId = resident.Id;
            user.VerificationStatus = "APPROVED";
            user.RejectionReason = null;

            var roles = await db.AspNetRoles.Where(r => r.IsActive && (r.Name == "ACCOUNT" || r.Name == "RESIDENT")).ToListAsync();
            if (roles.Count != 2)
                throw new AppFault(400, "Thiếu role ACCOUNT hoặc RESIDENT. Kiểm tra dữ liệu seed.");
            foreach (var role in roles)
            {
                bool assigned = await db.UserRoleAssignments.AnyAsync(a => a.UserId == user.Id && a.RoleId == role.Id && a.ScopeType == "SELF" && a.RevokedAt == null && (a.ValidUntil == null || a.ValidUntil > DateTime.UtcNow));
                if (!assigned)
                    db.UserRoleAssignments.Add(new UserRoleAssignment
                    {
                        UserId = user.Id, RoleId = role.Id, ScopeType = "SELF",
                        AssignedByUserId = reviewerId, AssignedAt = DateTime.UtcNow, ValidFrom = DateTime.UtcNow
                    });
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(input.Reason))
                throw new AppFault(400, "Nhập lý do từ chối.");
            user.VerificationStatus = "REJECTED";
            user.RejectionReason = input.Reason.Trim();
        }

        user.ReviewedByUserId = reviewerId;
        user.ReviewedAt = DateTime.UtcNow;
        user.PermissionVersion++;
        Audit(reviewerId, completingLink ? "RESIDENT_LINK_COMPLETED" : input.Approve ? "REGISTRATION_APPROVED" : "REGISTRATION_REJECTED", "User", user.Id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

}
