using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class UserRepository(MyStockContext db) : IUserRepository
{
    public async Task<(List<AspNetUser> Items, int Total)> SearchAsync(string? search, int page, int size)
    {
        var query = db.AspNetUsers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.UserName.Contains(search) || u.FullName.Contains(search));
        var items = await query.OrderBy(u => u.UserName).Skip((Math.Max(1, page) - 1) * size).Take(size).ToListAsync();
        return (items, await query.CountAsync());
    }

    public Task<List<AspNetUser>> GetAllAsync() => db.AspNetUsers.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
    public Task<AspNetUser?> FindAsync(string id) => db.AspNetUsers.SingleOrDefaultAsync(x => x.Id == id);
    public Task<bool> UserNameExistsAsync(string normalizedUserName) => db.AspNetUsers.AnyAsync(x => x.NormalizedUserName == normalizedUserName);
    public Task<bool> ActiveUserExistsAsync(string id) => db.AspNetUsers.AnyAsync(x => x.Id == id && x.IsActive);
    public Task<AspNetRole> GetActiveRoleByNameAsync(string name) => db.AspNetRoles.SingleAsync(x => x.Name == name && x.IsActive);
    public void Add(AspNetUser user) => db.AspNetUsers.Add(user);
    public void AddAssignment(UserRoleAssignment assignment) => db.UserRoleAssignments.Add(assignment);

    public Task<bool> IsAdminAsync(string userId)
    {
        var now = DateTime.UtcNow;
        return db.UserRoleAssignments.AnyAsync(a => a.UserId == userId && a.Role.Name == "ADMIN" && a.Role.IsActive && a.ScopeType == "SYSTEM" && a.User.IsActive && a.RevokedAt == null && a.ValidFrom <= now && (a.ValidUntil == null || a.ValidUntil > now));
    }

    public Task<bool> AnotherAdminExistsAsync(string userId, long? assignmentId)
    {
        var now = DateTime.UtcNow;
        return db.UserRoleAssignments.AnyAsync(a => (assignmentId == null ? a.UserId != userId : a.Id != assignmentId) && a.Role.Name == "ADMIN" && a.Role.IsActive && a.ScopeType == "SYSTEM" && a.User.IsActive && (a.User.LockoutEnd == null || a.User.LockoutEnd <= DateTimeOffset.UtcNow) && a.RevokedAt == null && a.ValidFrom <= now && (a.ValidUntil == null || a.ValidUntil > now));
    }

    public Task RevokeSessionsAsync(string userId, string reason) =>
        db.UserSessions.Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow).SetProperty(x => x.RevocationReason, reason));
    public Task<AspNetUser?> FindByNormalizedNameAsync(string normalizedUserName) => db.AspNetUsers.SingleOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName);
    public Task<AspNetRole?> FindActiveRoleByNameAsync(string name) => db.AspNetRoles.SingleOrDefaultAsync(r => r.IsActive && r.Name == name);
    public Task<UserRoleAssignment?> GetCurrentAssignmentAsync(string userId) => db.UserRoleAssignments.Include(a => a.Role).SingleOrDefaultAsync(a => a.UserId == userId && a.RevokedAt == null);
    private IQueryable<AspNetUser> Filter(string? search)
    {
        var query = db.AspNetUsers.AsNoTracking();
        return string.IsNullOrWhiteSpace(search) ? query : query.Where(u => u.UserName.Contains(search) || u.FullName.Contains(search));
    }

    public Task<int> CountAsync(string? search) => Filter(search).CountAsync();
    public Task<List<AspNetUser>> ListPageAsync(string? search, int page, int size) => Filter(search).OrderBy(u => u.UserName).Skip((page - 1) * size).Take(size).ToListAsync();
    public Task<AspNetUser?> FindNoTrackingAsync(string id) => db.AspNetUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
}
