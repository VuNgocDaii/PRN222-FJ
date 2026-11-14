using System.Security.Claims;

namespace QuanLyCuDan.Services.Interfaces;

public interface IAdminService
{
    Task<AdminPageResult> GetAsync(ClaimsPrincipal actor, string? tab, string? search, int page = 1, string? userId = null, string? roleId = null, int? permissionId = null);
}
