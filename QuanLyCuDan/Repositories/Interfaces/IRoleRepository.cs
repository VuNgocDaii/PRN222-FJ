using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IRoleRepository
{
    Task<List<AspNetRole>> ListWithPermissionsAsync();
    Task<List<AspNetRole>> ListActiveWithScopesAsync();
    Task<List<Permission>> CatalogAsync();
    Task<bool> NameExistsAsync(string normalizedName);
    void Add(AspNetRole role);
    Task<AspNetRole?> FindWithPermissionsAsync(string id);
    Task<AspNetRole?> FindActiveWithScopesAsync(string id);
    Task<List<Permission>> FindPermissionsAsync(int[] ids);
    Task<List<string>> ActiveAssignmentScopesAsync(string roleId);
    Task<List<AspNetRole>> ListAllWithScopesAsync();
}
