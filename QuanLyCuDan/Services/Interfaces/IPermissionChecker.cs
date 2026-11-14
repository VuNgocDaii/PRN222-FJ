using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

// Lấy danh tính và quyền hiệu lực của người đang đăng nhập.
public interface IPermissionChecker
{
    string UserId(ClaimsPrincipal actor);
    Task<List<VwEffectivePermission>> GrantsAsync(ClaimsPrincipal actor, string permission);
    Task<bool> HasSystemPermissionAsync(ClaimsPrincipal actor, string permission);
}
