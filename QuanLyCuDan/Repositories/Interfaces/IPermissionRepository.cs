using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IPermissionRepository
{
    // Các quyền hiệu lực (còn hạn, chưa thu hồi) của một tài khoản cho một mã quyền.
    Task<List<VwEffectivePermission>> GetGrantsAsync(string userId, string permissionCode);
    Task<HashSet<string>> GetSystemCodesAsync(string userId);
    Task<bool> HasAsync(string userId, string permissionCode, params string[] scopeTypes);
    Task<bool> HasAnyCodeAsync(string userId, IEnumerable<string> permissionCodes, string scopeType);
    Task<bool> HasAnyAsync(string userId, string permissionCode);
    Task<bool> CanAccessGroupAsync(string userId, string permissionCode, long groupId);
    // Các thôn (GROUP đang hoạt động) mà tài khoản được cấp quyền này, gồm cả thôn nằm trong khu dân cư được cấp.
    Task<List<long>> GetGroupIdsAsync(string userId, string permissionCode);
    Task<HashSet<string>> GetSelfCodesAsync(string userId);
}
