using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class SessionRepository(MyStockContext db) : ISessionRepository
{
    public Task<bool> IsValidAsync(string userId, Guid sessionId) =>
        db.UserSessions.AsNoTracking().AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null && s.AbsoluteExpiresAt > DateTime.UtcNow && s.User.IsActive && s.User.VerificationStatus != "PENDING" && s.User.VerificationStatus != "REJECTED" && (s.User.LockoutEnd == null || s.User.LockoutEnd <= DateTimeOffset.UtcNow));

    public Task<UserSession> GetActiveWithUserAsync(string userId, Guid sessionId) =>
        db.UserSessions.Include(s => s.User).SingleAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null);

    public void Add(UserSession session) => db.UserSessions.Add(session);

    public Task RevokeAsync(string userId, Guid sessionId, string reason) =>
        db.UserSessions.Where(s => s.UserId == userId && s.Id == sessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow).SetProperty(x => x.RevocationReason, reason));
}
