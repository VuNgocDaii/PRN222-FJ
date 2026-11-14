using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class RoleRepository(MyStockContext db) : IRoleRepository
{
    public Task<List<AspNetRole>> ListWithPermissionsAsync() => db.AspNetRoles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync();
    public Task<List<AspNetRole>> ListActiveWithScopesAsync() => db.AspNetRoles.AsNoTracking().Include(r => r.Permissions).ThenInclude(p => p.PermissionAllowedScopes).Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync();
    public Task<List<Permission>> CatalogAsync() => db.Permissions.AsNoTracking().Include(p => p.PermissionAllowedScopes).OrderBy(p => p.Code).ToListAsync();
    public Task<bool> NameExistsAsync(string normalizedName) => db.AspNetRoles.AnyAsync(x => x.NormalizedName == normalizedName);
    public void Add(AspNetRole role) => db.AspNetRoles.Add(role);
    public Task<AspNetRole?> FindWithPermissionsAsync(string id) => db.AspNetRoles.Include(x => x.Permissions).SingleOrDefaultAsync(x => x.Id == id);
    public Task<AspNetRole?> FindActiveWithScopesAsync(string id) => db.AspNetRoles.Include(x => x.Permissions).ThenInclude(p => p.PermissionAllowedScopes).SingleOrDefaultAsync(x => x.Id == id && x.IsActive);
    public Task<List<Permission>> FindPermissionsAsync(int[] ids) => db.Permissions.Include(x => x.PermissionAllowedScopes).Where(x => ids.Contains(x.Id)).ToListAsync();
    public Task<List<string>> ActiveAssignmentScopesAsync(string roleId) => db.UserRoleAssignments.Where(a => a.RoleId == roleId && a.RevokedAt == null).Select(a => a.ScopeType).Distinct().ToListAsync();
    public Task<List<AspNetRole>> ListAllWithScopesAsync() => db.AspNetRoles.AsNoTracking().Include(r => r.Permissions).ThenInclude(p => p.PermissionAllowedScopes).OrderBy(r => r.Name).ToListAsync();
}
