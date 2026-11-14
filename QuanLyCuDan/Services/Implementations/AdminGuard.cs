using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class AdminGuard(IPermissionChecker checker, IUserRepository users) : IAdminGuard
{
    public async Task RequireAsync(ClaimsPrincipal actor, string permission)
    {
        if (!await checker.HasSystemPermissionAsync(actor, permission))
            throw new AppFault(403, "Bạn không có quyền thực hiện thao tác này.");
    }

    public async Task EnsureNotLastAdminAsync(string userId, long? assignmentId)
    {
        if (await users.IsAdminAsync(userId) && !await users.AnotherAdminExistsAsync(userId, assignmentId))
            throw new AppFault(409, "Không được khóa hoặc thu hồi ADMIN cuối cùng.");
    }
}
