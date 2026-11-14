using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface ISessionRepository
{
    Task<bool> IsValidAsync(string userId, Guid sessionId);
    Task<UserSession> GetActiveWithUserAsync(string userId, Guid sessionId);
    void Add(UserSession session);
    Task RevokeAsync(string userId, Guid sessionId, string reason);
}
