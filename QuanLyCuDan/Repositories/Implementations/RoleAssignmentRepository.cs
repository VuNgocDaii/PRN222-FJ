using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class RoleAssignmentRepository(MyStockContext db) : IRoleAssignmentRepository
{
    public Task<List<ResidentialUnit>> ActiveUnitsAsync() => db.ResidentialUnits.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync();
    public Task<List<Organization>> ActiveOrganizationsAsync() => db.Organizations.AsNoTracking().Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync();
    public Task<List<UserRoleAssignment>> ListForUserAsync(string? userId) => db.UserRoleAssignments.AsNoTracking().Include(a => a.Role).Where(a => a.UserId == userId).OrderByDescending(a => a.AssignedAt).Take(200).ToListAsync();
    public Task<bool> UnitValidAsync(long? id, string scope) => db.ResidentialUnits.AnyAsync(x => x.Id == id && x.UnitType == scope && x.IsActive && (scope == "AREA" || x.ParentUnit != null && x.ParentUnit.IsActive));
    public Task<bool> OrganizationInGroupAsync(long? organizationId, long? groupId) => db.Organizations.AnyAsync(o => o.Id == organizationId && o.IsActive && o.ResidentialGroupId == groupId);
    public Task<List<UserRoleAssignment>> ExpiredAsync(string userId) => db.UserRoleAssignments.Where(a => a.UserId == userId && a.RevokedAt == null && a.ValidUntil <= DateTime.UtcNow).ToListAsync();
    public Task<bool> HasActiveAsync(string userId) => db.UserRoleAssignments.AnyAsync(a => a.UserId == userId && a.RevokedAt == null);
    public void Add(UserRoleAssignment assignment) => db.UserRoleAssignments.Add(assignment);
    public Task<UserRoleAssignment?> FindWithRoleAsync(string userId, long id) => db.UserRoleAssignments.Include(x => x.Role).SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    public async Task<HashSet<string>> OwnActiveRoleIdsAsync(string userId) =>
        (await db.UserRoleAssignments.Where(a => a.UserId == userId && a.RevokedAt == null && (a.ValidUntil == null || a.ValidUntil > DateTime.UtcNow)).Select(a => a.RoleId).ToListAsync()).ToHashSet();
}
