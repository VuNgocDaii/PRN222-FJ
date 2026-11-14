using System.Security.Claims;
using System.Text.Json;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class AccountService(
    IPermissionChecker checker,
    IUserRepository users,
    IResidentialUnitRepository units,
    IPasswordService passwords,
    IAuditRepository audit,
    IUnitOfWork unitOfWork) : IAccountService
{
    public async Task<AspNetUser> GetApplicantAsync(ClaimsPrincipal actor)
    {
        var user = await users.FindAsync(checker.UserId(actor)) ?? throw new AppFault(404, "Không tìm thấy tài khoản.");
        if (user.ResidentId != null || user.VerificationStatus != "APPROVED" || await users.IsAdminAsync(user.Id))
            throw new AppFault(403, "Chỉ tài khoản đã duyệt nhưng chưa có hồ sơ cư dân được bổ sung thông tin đăng ký.");
        return user;
    }

    public async Task RegisterAsync(RegisterInput input)
    {
        var normalizedName = input.UserName.Trim().ToUpperInvariant();
        if (await users.UserNameExistsAsync(normalizedName))
            throw new AppFault(409, "Tên đăng nhập đã được sử dụng.");
        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new AppFault(400, "Nhập họ và tên.");
        if (!await units.IsRegistrableGroupAsync(input.GroupId))
            throw new AppFault(400, "Thôn không còn hoạt động. Vui lòng chọn lại.");
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
            RegistrationProfile = JsonSerializer.Serialize<RegistrationProfileInput>(input),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true,
            PermissionVersion = 1,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwords.Hash(user, input.Password);
        // Chưa cấp role và chưa liên kết hồ sơ cư dân khi tự đăng ký.
        users.Add(user);
        await unitOfWork.SaveChangesAsync();
    }

    public Task<List<ResidentialUnit>> GetGroupsAsync() => units.GetRegistrableGroupsAsync();

    public async Task<bool> CanSubmitProfileAsync(ClaimsPrincipal actor)
    {
        var user = await users.FindAsync(checker.UserId(actor)) ?? throw new AppFault(404, "Không tìm thấy tài khoản.");
        return user.ResidentId == null && user.VerificationStatus == "APPROVED" && !await users.IsAdminAsync(user.Id);
    }

    public async Task SubmitProfileAsync(ClaimsPrincipal actor, RegistrationProfileInput input, string rowVersion)
    {
        var user = await GetApplicantAsync(actor);
        if (!await units.IsRegistrableGroupAsync(input.GroupId))
            throw new AppFault(400, "Chọn thôn đang hoạt động.");
        unitOfWork.SetRowVersion(user, rowVersion);
        user.RegistrationProfile = JsonSerializer.Serialize(input);
        user.VerificationStatus = "PENDING";
        user.ReviewedByUserId = null;
        user.ReviewedAt = null;
        user.RejectionReason = null;
        user.PermissionVersion++;
        audit.Add(user.Id, "REGISTRATION_PROFILE_SUBMITTED", "User", user.Id);
        await unitOfWork.SaveChangesAsync();
    }
}
