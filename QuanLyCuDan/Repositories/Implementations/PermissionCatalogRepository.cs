using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class PermissionCatalogRepository(MyStockContext db) : IPermissionCatalogRepository
{
    public Task<List<Permission>> ListAsync() => db.Permissions.AsNoTracking().Include(p => p.PermissionAllowedScopes).Include(p => p.Roles).OrderBy(p => p.Module).ThenBy(p => p.Code).ToListAsync();
    public Task<bool> CodeExistsAsync(string code) => db.Permissions.AnyAsync(item => item.Code == code);
    public void Add(Permission permission) => db.Permissions.Add(permission);
    public Task<Permission?> FindWithDetailsAsync(int id) => db.Permissions.Include(i => i.PermissionAllowedScopes).Include(i => i.Roles).SingleOrDefaultAsync(i => i.Id == id);
    public void RemoveScopes(IEnumerable<PermissionAllowedScope> scopes) => db.PermissionAllowedScopes.RemoveRange(scopes);
    public void Remove(Permission permission) => db.Permissions.Remove(permission);
    public Task<List<Permission>> ListSplitAsync() => db.Permissions.AsNoTracking().Include(p => p.PermissionAllowedScopes).Include(p => p.Roles).AsSplitQuery().OrderBy(p => p.Module).ThenBy(p => p.Code).ToListAsync();
}
