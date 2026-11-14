using System.Security.Claims;

namespace QuanLyCuDan.Services.Interfaces;

public interface INotificationSubscriptionService
{
    Task<IReadOnlyList<string>> GetGroupsAsync(ClaimsPrincipal actor);
}
