using System.Security.Claims;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class SessionService(IPermissionChecker checker, ISessionRepository sessions) : ISessionService
{
    public Task<bool> IsValidAsync(ClaimsPrincipal actor)
    {
        var id = actor.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == null || !Guid.TryParse(actor.FindFirstValue("sid"), out var sid))
            return Task.FromResult(false);
        return sessions.IsValidAsync(id, sid);
    }

    public Task<UserSession> GetForSignInAsync(ClaimsPrincipal actor) =>
        sessions.GetActiveWithUserAsync(checker.UserId(actor), Guid.Parse(actor.FindFirstValue("sid")!));
}
