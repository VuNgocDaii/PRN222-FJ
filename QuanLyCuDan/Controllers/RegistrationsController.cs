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
            Households = await db.Households.AsNoTracking().Where(h => !h.IsArchived && h.ResidentialGroup.IsActive && h.ResidentialGroup.UnitType == "GROUP" && h.ResidentialGroup.ParentUnit != null && h.ResidentialGroup.ParentUnit.IsActive).OrderBy(h => h.Code).ToListAsync(),
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
            if (input.CreateResident && input.ResidentId != null)
                throw new AppFault(400, "Chỉ chọn một cách: liên kết hồ sơ có sẵn hoặc tạo hồ sơ mới.");
            var resident = input.CreateResident ? await CreateRegisteredResident(user, input, reviewerId)
                : await db.Residents.SingleOrDefaultAsync(r => r.Id == input.ResidentId);
            if (resident == null)
                throw new AppFault(400, "Chọn hồ sơ cư dân để liên kết tài khoản.");
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

    // Called only inside the review transaction. Applicant data never creates official records before approval.
    private async Task<Resident> CreateRegisteredResident(AspNetUser user, RegistrationReviewInput input, string reviewerId)
    {
        var profile = RegistrationProfileInput.Read(user.RegistrationProfile);
        if (profile == null || !System.ComponentModel.DataAnnotations.Validator.TryValidateObject(profile, new System.ComponentModel.DataAnnotations.ValidationContext(profile), new List<System.ComponentModel.DataAnnotations.ValidationResult>(), true))
            throw new AppFault(400, "Đăng ký chưa có đủ thông tin cư dân hợp lệ để tạo hồ sơ. Hãy liên kết hồ sơ đã có hoặc yêu cầu đăng ký đầy đủ.");
        if (!await db.ResidentialUnits.AnyAsync(g => g.Id == profile.GroupId && g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.UnitType == "AREA" && g.ParentUnit.IsActive))
            throw new AppFault(400, "Tổ dân phố đăng ký không còn hoạt động.");
        if (!string.IsNullOrEmpty(profile.CitizenId) && await db.Residents.AnyAsync(r => r.CitizenId == profile.CitizenId))
            throw new AppFault(409, "CCCD đã có trong sổ cư dân. Hãy kiểm tra và liên kết đúng hồ sơ đã có.");
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        Household household;
        var newHousehold = input.HouseholdId == null;
        if (newHousehold)
        {
            household = new Household { Code = "HO-" + Guid.NewGuid().ToString("N")[..24], ResidentialGroupId = profile.GroupId!.Value,
                Address = profile.Address.Trim(), RegisteredAt = today, CreatedByUserId = reviewerId, CreatedAt = now };
            db.Households.Add(household); await db.SaveChangesAsync();
        }
        else
        {
            household = await db.Households.SingleOrDefaultAsync(h => h.Id == input.HouseholdId && !h.IsArchived && h.ResidentialGroupId == profile.GroupId)
                ?? throw new AppFault(400, "Hộ tiếp nhận phải đang hoạt động và thuộc tổ dân phố đã đăng ký.");
            if (string.IsNullOrWhiteSpace(input.RelationshipToHead)) throw new AppFault(400, "Nhập quan hệ với chủ hộ.");
        }
        var resident = new Resident { FullName = user.FullName, DateOfBirth = profile.DateOfBirth!.Value, Gender = profile.Gender,
            CitizenId = profile.CitizenId, PhoneNumber = user.PhoneNumber, Occupation = profile.Occupation,
            HouseholdId = household.Id, RelationshipToHead = newHousehold ? "Chủ hộ" : input.RelationshipToHead!.Trim(),
            ResidenceStatus = profile.ResidenceStatus, MovedInAt = today, CreatedAt = now };
        db.Residents.Add(resident); await db.SaveChangesAsync();
        if (newHousehold) household.HeadResidentId = resident.Id;
        var change = new Models.Request { Code = "YC-" + Guid.NewGuid().ToString("N")[..24], RequestKind = "RESIDENCE", Source = "STAFF",
            SubmittedByUserId = reviewerId, ResidentId = resident.Id, AssignedGroupId = household.ResidentialGroupId,
            RequestType = "ADD_MEMBER", Subject = "Tạo hồ sơ từ đăng ký cư dân đã xác minh", Body = "Quản trị viên xác minh đăng ký và tạo hồ sơ cư dân khi phê duyệt.",
            StartDate = today, TargetHouseholdId = household.Id, OriginalResidenceStatus = "MOVED_OUT", ResultResidenceStatus = resident.ResidenceStatus,
            ResultRelationship = resident.RelationshipToHead, NewHeadResidentId = newHousehold ? resident.Id : null,
            Status = "APPROVED", ProcessedByUserId = reviewerId, ProcessedAt = now, ChangeAppliedAt = now,
            SubmittedAt = now, ClientRequestKey = Guid.NewGuid().ToString("N") };
        change.Activities.Add(new Models.Activity { ActivityType = "REQUEST_EVENT", ActorUserId = reviewerId,
            EventCode = "STAFF_CHANGE_RECORDED", ToStatus = "APPROVED", OccurredAt = now });
        db.Requests.Add(change);
        return resident;
    }
}
