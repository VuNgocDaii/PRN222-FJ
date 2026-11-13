using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;

// Shared authentication code for MVC form actions.
public abstract class AuthControllerBase(MyStockContext context) : BaseController(context)
{

    protected async Task<(AspNetUser User, UserSession Session)> LoginCore(LoginInput input, string? device)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await db.AspNetUsers.SingleOrDefaultAsync(x => x.NormalizedUserName == input.UserName.Trim().ToUpperInvariant());
        if (user == null || !user.IsActive || user.LockoutEnd > DateTimeOffset.UtcNow)
            throw new AppFault(401, "Tài khoản hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.");
        if (!VerifyPassword(user, input.Password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
                user.AccessFailedCount = 0;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            throw new AppFault(401, "Tài khoản hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.");
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        // Chỉ thông báo trạng thái sau khi đã kiểm tra đúng mật khẩu.
        if (user.VerificationStatus is "PENDING" or "REJECTED")
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            string message = user.VerificationStatus == "PENDING"
                ? "Tài khoản đang chờ quản trị viên phê duyệt."
                : "Đăng ký bị từ chối. Lý do: " + user.RejectionReason;
            throw new AppFault(403, message);
        }
        // Upgrade SQL demo hashes to the built-in Identity PBKDF2 format after successful login.
        if (user.PasswordHash!.StartsWith("SHA256$"))
            user.PasswordHash = HashPassword(user, input.Password);
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            DeviceLabel = device?[..Math.Min(device.Length, 200)],
            CreatedAt = DateTime.UtcNow,
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (user, session);
    }

    protected async Task SignInCore(HttpContext http, AspNetUser user, UserSession session)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.UserName), new Claim("sid", session.Id.ToString()) }, "Cookies");
        await http.SignInAsync("Cookies", new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false, ExpiresUtc = new DateTimeOffset(DateTime.SpecifyKind(session.AbsoluteExpiresAt, DateTimeKind.Utc)) });
    }

    protected async Task LogoutCore(HttpContext http)
    {
        var id = CurrentUserId(http.User);
        var sid = Guid.Parse(http.User.FindFirstValue("sid")!);
        await db.UserSessions.Where(s => s.UserId == id && s.Id == sid && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow).SetProperty(x => x.RevocationReason, "LOGOUT"));
        await http.SignOutAsync("Cookies");
    }

    protected async Task ChangePasswordCore(ClaimsPrincipal principal, PasswordInput input)
    {
        var user = await db.AspNetUsers.SingleAsync(x => x.Id == CurrentUserId(principal));
        if (!VerifyPassword(user, input.CurrentPassword))
            throw new AppFault(400, "Mật khẩu hiện tại không đúng.");
        await using var tx = await db.Database.BeginTransactionAsync();
        user.PasswordHash = HashPassword(user, input.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        await db.UserSessions.Where(s => s.UserId == user.Id && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow).SetProperty(x => x.RevocationReason, "PASSWORD_CHANGED"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
