using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class UserService(MyStockContext context) : ApplicationService(context)
{
    public async Task<UserListResult> ListAsync(ClaimsPrincipal actor, int page = 1, string? search = null)
    {
        await RequirePermission(actor, "accounts.read");
        var query = db.AspNetUsers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(u => u.UserName.Contains(search) || u.FullName.Contains(search));
        return new(await query.OrderBy(u => u.UserName).Skip((Math.Max(1, page)-1)*30).Take(30).ToListAsync(), await query.CountAsync(), Math.Max(1,page), search);
    }
    public async Task<string> CreateAsync(ClaimsPrincipal actor, UserInput input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "accounts.create");
        var normalized = input.UserName.Trim().ToUpperInvariant();
        if (normalized.Length == 0 || string.IsNullOrWhiteSpace(input.FullName))
            throw new AppFault(400, "Tên tài khoản và họ tên không được để trống.");
        if (await db.AspNetUsers.AnyAsync(x => x.NormalizedUserName == normalized))
            throw new AppFault(409, "Tên tài khoản đã tồn tại.");
        var user = new AspNetUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = input.UserName.Trim(),
            NormalizedUserName = normalized,
            FullName = input.FullName.Trim(),
            IsActive = true,
            VerificationStatus = "PENDING",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true,
            PermissionVersion = 1,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = HashPassword(user, input.Password);
        db.AspNetUsers.Add(user);
        var basic = await db.AspNetRoles.SingleAsync(x => x.Name == "ACCOUNT" && x.IsActive);
        db.UserRoleAssignments.Add(new UserRoleAssignment { UserId = user.Id, RoleId = basic.Id, ScopeType = "SELF", AssignedByUserId = CurrentUserId(actor), AssignedAt = DateTime.UtcNow, ValidFrom = DateTime.UtcNow });
        Audit(CurrentUserId(actor), "ACCOUNT_CREATED", "User", user.Id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return user.Id;
    }

    public async Task UpdateAsync(ClaimsPrincipal actor, string id, UserEdit input)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "accounts.update");
        var user = await db.AspNetUsers.SingleOrDefaultAsync(x => x.Id == id) ?? throw new AppFault(404, "Không tìm thấy tài khoản.");
        if (user.IsActive != input.IsActive)
        {
            await RequirePermission(actor, "accounts.lock");
            if (id == CurrentUserId(actor))
                throw new AppFault(400, "Không thể khóa chính tài khoản đang sử dụng.");
            if (!input.IsActive)
                await ProtectLastAdmin(id, null);
        }

        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new AppFault(400, "Họ tên không được để trống.");
        SetVersion(user, input.RowVersion);
        user.FullName = input.FullName.Trim();
        user.IsActive = input.IsActive;
        if (!user.IsActive)
            await db.UserSessions.Where(s => s.UserId == id && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow).SetProperty(x => x.RevocationReason, "ACCOUNT_DISABLED"));
        Audit(CurrentUserId(actor), "ACCOUNT_UPDATED", "User", id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
