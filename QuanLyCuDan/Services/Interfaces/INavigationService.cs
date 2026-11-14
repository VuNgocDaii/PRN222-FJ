using System.Security.Claims;

namespace QuanLyCuDan.Services.Interfaces;

public sealed record NavigationAccess(HashSet<string> Permissions, bool CanUseResident, bool CanManageAnnouncements, bool CanReviewRegistrations, bool CanManageOrganizations);

public interface INavigationService
{
    Task<NavigationAccess> GetAsync(ClaimsPrincipal actor);
}
