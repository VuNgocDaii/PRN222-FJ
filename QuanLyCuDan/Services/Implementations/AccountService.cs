using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class AccountService(MyStockContext context) : ApplicationService(context)
{
    public async Task<AspNetUser> GetApplicantAsync(ClaimsPrincipal actor)
    {
        var user = await db.AspNetUsers.SingleAsync(u => u.Id == CurrentUserId(actor));
        if (user.ResidentId != null || user.VerificationStatus != "APPROVED" || await IsAdmin(user.Id))
            throw new AppFault(403, "Chỉ tài khoản đã duyệt nhưng chưa có hồ sơ cư dân được bổ sung thông tin đăng ký.");
        return user;
    }

    public async Task RegisterAsync(RegisterInput input)
    {
        string normalizedName = input.UserName.Trim().ToUpperInvariant();
        if (await db.AspNetUsers.AnyAsync(u => u.NormalizedUserName == normalizedName))
            throw new AppFault(409, "Tên đăng nhập đã được sử dụng.");
        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new AppFault(400, "Nhập họ và tên.");
        if (!await db.ResidentialUnits.AnyAsync(g => g.Id == input.GroupId && g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA"))
            throw new AppFault(400, "Tổ dân phố không còn hoạt động. Vui lòng chọn lại.");
        input.Address = input.Address.Trim();
        input.CitizenId = string.IsNullOrWhiteSpace(input.CitizenId) ? null : input.CitizenId.Trim();
        input.Occupation = string.IsNullOrWhiteSpace(input.Occupation) ? null : input.Occupation.Trim();

        var user = new AspNetUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = input.UserName.Trim(),
            NormalizedUserName = normalizedName,
            FullName = input.FullName.Trim(),
            PhoneNumber = input.PhoneNumber.Trim(),
            IsActive = true,
            VerificationStatus = "PENDING",
            RegistrationProfile = System.Text.Json.JsonSerializer.Serialize<RegistrationProfileInput>(input),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true,
            PermissionVersion = 1,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = HashPassword(user, input.Password);
        // Chưa cấp role và chưa liên kết hồ sơ cư dân khi tự đăng ký.
        db.AspNetUsers.Add(user);
        await db.SaveChangesAsync();
    }

    public Task<List<ResidentialUnit>> GetGroupsAsync() => db.ResidentialUnits.AsNoTracking().Where(g => g.IsActive && g.UnitType == "GROUP" && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA").OrderBy(g => g.Name).ToListAsync();
    public async Task<bool> CanSubmitProfileAsync(ClaimsPrincipal actor)
    {
        var user = await db.AspNetUsers.AsNoTracking().SingleAsync(u => u.Id == CurrentUserId(actor));
        return user.ResidentId == null && user.VerificationStatus == "APPROVED" && !await IsAdmin(user.Id);
    }
    public async Task SubmitProfileAsync(ClaimsPrincipal actor, RegistrationProfileInput input, string rowVersion)
    {
        var user = await GetApplicantAsync(actor);
        if (!await db.ResidentialUnits.AnyAsync(g => g.Id == input.GroupId && g.IsActive && g.UnitType == "GROUP" && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA"))
        throw new AppFault(400, "Chọn tổ dân phố đang hoạt động.");
        SetVersion(user, rowVersion);
        user.RegistrationProfile = System.Text.Json.JsonSerializer.Serialize(input);
        user.VerificationStatus = "PENDING";
        user.ReviewedByUserId = null; user.ReviewedAt = null; user.RejectionReason = null;
        user.PermissionVersion++;
        Audit(user.Id, "REGISTRATION_PROFILE_SUBMITTED", "User", user.Id);
        await db.SaveChangesAsync();
    }
}
