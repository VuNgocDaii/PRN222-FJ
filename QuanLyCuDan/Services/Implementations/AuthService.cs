using System.Data;
using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class AuthService(
    IPermissionChecker checker,
    IPermissionRepository permissions,
    IUserRepository users,
    ISessionRepository sessions,
    IPasswordService passwords,
    IUnitOfWork unitOfWork) : IAuthService
{
    private const string BadLogin = "Tài khoản hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.";

    public async Task<(AspNetUser User, UserSession Session)> LoginAsync(LoginInput input, string? device)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await users.FindByNormalizedNameAsync(input.UserName.Trim().ToUpperInvariant());
        if (user == null || !user.IsActive || user.LockoutEnd > DateTimeOffset.UtcNow)
            throw new AppFault(401, BadLogin);
        if (!passwords.Verify(user, input.Password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
                user.AccessFailedCount = 0;
            }
            await unitOfWork.SaveChangesAsync();
            await tx.CommitAsync();
            throw new AppFault(401, BadLogin);
        }
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        // Chỉ thông báo trạng thái sau khi đã kiểm tra đúng mật khẩu.
        if (user.VerificationStatus is "PENDING" or "REJECTED")
        {
            await unitOfWork.SaveChangesAsync();
            await tx.CommitAsync();
            throw new AppFault(403, user.VerificationStatus == "PENDING"
                ? "Tài khoản đang chờ quản trị viên phê duyệt."
                : "Đăng ký bị từ chối. Lý do: " + user.RejectionReason);
        }
        // Nâng cấp hash SHA256 của dữ liệu demo lên định dạng PBKDF2 của Identity.
        if (user.PasswordHash!.StartsWith("SHA256$"))
            user.PasswordHash = passwords.Hash(user, input.Password);
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            DeviceLabel = device?[..Math.Min(device.Length, 200)],
            CreatedAt = DateTime.UtcNow,
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        sessions.Add(session);
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return (user, session);
    }

    public async Task ChangePasswordAsync(ClaimsPrincipal principal, PasswordInput input)
    {
        var user = await users.FindAsync(checker.UserId(principal)) ?? throw new AppFault(404, "Không tìm thấy tài khoản.");
        if (!passwords.Verify(user, input.CurrentPassword))
            throw new AppFault(400, "Mật khẩu hiện tại không đúng.");
        await using var tx = await unitOfWork.BeginTransactionAsync();
        user.PasswordHash = passwords.Hash(user, input.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        await users.RevokeSessionsAsync(user.Id, "PASSWORD_CHANGED");
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public Task RevokeCurrentSessionAsync(ClaimsPrincipal actor) =>
        sessions.RevokeAsync(checker.UserId(actor), Guid.Parse(actor.FindFirstValue("sid")!), "LOGOUT");

    public async Task<string> GetDestinationAsync(string id)
    {
        if (await permissions.HasAsync(id, "accounts.read", "SYSTEM")) return "/Admin";
        if (await permissions.HasAsync(id, "announcements.read", "GROUP", "AREA")) return "/Announcements";
        if (await permissions.HasAsync(id, "profile.self.read", "SELF")) return "/Resident";
        return "/Account/Profile";
    }
}
