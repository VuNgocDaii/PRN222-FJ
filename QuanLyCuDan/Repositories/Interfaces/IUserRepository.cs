using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IUserRepository
{
    Task<(List<AspNetUser> Items, int Total)> SearchAsync(string? search, int page, int size);
    Task<List<AspNetUser>> GetAllAsync();
    Task<AspNetUser?> FindAsync(string id);
    Task<bool> UserNameExistsAsync(string normalizedUserName);
    Task<bool> ActiveUserExistsAsync(string id);
    Task<AspNetRole> GetActiveRoleByNameAsync(string name);
    void Add(AspNetUser user);
    void AddAssignment(UserRoleAssignment assignment);
    Task<bool> IsAdminAsync(string userId);
    Task<bool> AnotherAdminExistsAsync(string userId, long? assignmentId);
    Task RevokeSessionsAsync(string userId, string reason);
    Task<AspNetUser?> FindByNormalizedNameAsync(string normalizedUserName);
    Task<AspNetRole?> FindActiveRoleByNameAsync(string name);
    Task<UserRoleAssignment?> GetCurrentAssignmentAsync(string userId);
    Task<int> CountAsync(string? search);
    Task<List<AspNetUser>> ListPageAsync(string? search, int page, int size);
    Task<AspNetUser?> FindNoTrackingAsync(string id);
}
