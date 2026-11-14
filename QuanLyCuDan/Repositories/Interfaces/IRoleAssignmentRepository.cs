using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IRoleAssignmentRepository
{
    Task<List<ResidentialUnit>> ActiveUnitsAsync();
    Task<List<Organization>> ActiveOrganizationsAsync();
    Task<List<UserRoleAssignment>> ListForUserAsync(string? userId);
    Task<bool> UnitValidAsync(long? id, string scope);
    Task<bool> OrganizationInGroupAsync(long? organizationId, long? groupId);
    Task<List<UserRoleAssignment>> ExpiredAsync(string userId);
    Task<bool> HasActiveAsync(string userId);
    void Add(UserRoleAssignment assignment);
    Task<UserRoleAssignment?> FindWithRoleAsync(string userId, long id);
    Task<HashSet<string>> OwnActiveRoleIdsAsync(string userId);
}
