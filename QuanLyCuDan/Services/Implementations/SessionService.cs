using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class SessionService(MyStockContext context) : ApplicationService(context)
{
    public Task<bool> IsValidAsync(ClaimsPrincipal actor) => ValidSession(db, actor);
    public Task<UserSession> GetForSignInAsync(ClaimsPrincipal actor)
    {
        var id = CurrentUserId(actor); var sid = Guid.Parse(actor.FindFirstValue("sid")!);
        return db.UserSessions.Include(s => s.User).SingleAsync(s => s.Id == sid && s.UserId == id && s.RevokedAt == null);
    }
}
