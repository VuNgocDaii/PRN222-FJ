using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IPermissionCatalogRepository
{
    Task<List<Permission>> ListAsync();
    Task<bool> CodeExistsAsync(string code);
    void Add(Permission permission);
    Task<Permission?> FindWithDetailsAsync(int id);
    void RemoveScopes(IEnumerable<PermissionAllowedScope> scopes);
    void Remove(Permission permission);
    Task<List<Permission>> ListSplitAsync();
}
