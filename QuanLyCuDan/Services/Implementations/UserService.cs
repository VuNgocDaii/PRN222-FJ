using System.Data;
using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class UserService(
    IAdminGuard guard,
    IPermissionChecker checker,
    IUserRepository users,
    IPasswordService passwords,
    IAuditRepository audit,
    IUnitOfWork unitOfWork) : IUserService
{
    private const int PageSize = 30;

    public async Task<UserListResult> ListAsync(ClaimsPrincipal actor, int page = 1, string? search = null)
    {
        await guard.RequireAsync(actor, "accounts.read");
        var (items, total) = await users.SearchAsync(search, page, PageSize);
        return new(items, total, Math.Max(1, page), search);
    }

    public async Task<string> CreateAsync(ClaimsPrincipal actor, UserInput input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "accounts.create");
        var normalized = input.UserName.Trim().ToUpperInvariant();
        if (normalized.Length == 0 || string.IsNullOrWhiteSpace(input.FullName))
            throw new AppFault(400, "Tên tài khoản và họ tên không được để trống.");
        if (await users.UserNameExistsAsync(normalized))
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
        user.PasswordHash = passwords.Hash(user, input.Password);
        users.Add(user);
        var basic = await users.GetActiveRoleByNameAsync("ACCOUNT");
        users.AddAssignment(new UserRoleAssignment { UserId = user.Id, RoleId = basic.Id, ScopeType = "SELF", AssignedByUserId = checker.UserId(actor), AssignedAt = DateTime.UtcNow, ValidFrom = DateTime.UtcNow });
        audit.Add(checker.UserId(actor), "ACCOUNT_CREATED", "User", user.Id);
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return user.Id;
    }

    public async Task UpdateAsync(ClaimsPrincipal actor, string id, UserEdit input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "accounts.update");
        var user = await users.FindAsync(id) ?? throw new AppFault(404, "Không tìm thấy tài khoản.");
        if (user.IsActive != input.IsActive)
        {
            await guard.RequireAsync(actor, "accounts.lock");
            if (id == checker.UserId(actor))
                throw new AppFault(400, "Không thể khóa chính tài khoản đang sử dụng.");
            if (!input.IsActive)
                await guard.EnsureNotLastAdminAsync(id, null);
        }
        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new AppFault(400, "Họ tên không được để trống.");
        unitOfWork.SetRowVersion(user, input.RowVersion);
        user.FullName = input.FullName.Trim();
        user.IsActive = input.IsActive;
        if (!user.IsActive)
            await users.RevokeSessionsAsync(id, "ACCOUNT_DISABLED");
        audit.Add(checker.UserId(actor), "ACCOUNT_UPDATED", "User", id);
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
