using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class PermissionRepository(MyStockContext db) : IPermissionRepository
{
    public Task<List<VwEffectivePermission>> GetGrantsAsync(string userId, string permissionCode) =>
        db.VwEffectivePermissions.AsNoTracking().Where(x => x.UserId == userId && x.PermissionCode == permissionCode).ToListAsync();
    public async Task<HashSet<string>> GetSystemCodesAsync(string userId) =>
        (await db.VwEffectivePermissions.AsNoTracking().Where(x => x.UserId == userId && x.ScopeType == "SYSTEM").Select(x => x.PermissionCode).ToListAsync()).ToHashSet();

    public Task<bool> HasAsync(string userId, string permissionCode, params string[] scopeTypes) =>
        db.VwEffectivePermissions.AsNoTracking().AnyAsync(x => x.UserId == userId && x.PermissionCode == permissionCode && scopeTypes.Contains(x.ScopeType));

    public Task<bool> HasAnyCodeAsync(string userId, IEnumerable<string> permissionCodes, string scopeType)
    {
        var codes = permissionCodes.ToArray();
        return db.VwEffectivePermissions.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ScopeType == scopeType && codes.Contains(x.PermissionCode));
    }
    public Task<bool> HasAnyAsync(string userId, string permissionCode) =>
        db.VwEffectivePermissions.AsNoTracking().AnyAsync(x => x.UserId == userId && x.PermissionCode == permissionCode);
    public Task<bool> CanAccessGroupAsync(string userId, string permissionCode, long groupId) =>
        db.VwEffectivePermissions.AsNoTracking().AnyAsync(x => x.UserId == userId && x.PermissionCode == permissionCode &&
            ((x.ScopeType == "GROUP" && x.ResidentialGroupId == groupId) ||
             (x.ScopeType == "AREA" && db.ResidentialUnits.Any(g => g.Id == groupId && g.IsActive && g.ParentUnitId == x.ResidentialAreaId))));

    public async Task<List<long>> GetGroupIdsAsync(string userId, string permissionCode)
    {
        var grants = await GetGrantsAsync(userId, permissionCode);
        var direct = grants.Where(x => x.ScopeType == "GROUP" && x.ResidentialGroupId != null).Select(x => x.ResidentialGroupId!.Value).Distinct().ToList();
        var areas = grants.Where(x => x.ScopeType == "AREA" && x.ResidentialAreaId != null).Select(x => x.ResidentialAreaId!.Value).Distinct().ToList();
        return await db.ResidentialUnits.AsNoTracking()
            .Where(g => g.IsActive && g.UnitType == "GROUP" && (direct.Contains(g.Id) || (g.ParentUnitId != null && areas.Contains(g.ParentUnitId.Value))))
            .Select(g => g.Id).ToListAsync();
    }
    public async Task<HashSet<string>> GetSelfCodesAsync(string userId) =>
        (await db.VwEffectivePermissions.AsNoTracking().Where(x => x.UserId == userId && x.ScopeType == "SELF").Select(x => x.PermissionCode).ToListAsync()).ToHashSet();
}
