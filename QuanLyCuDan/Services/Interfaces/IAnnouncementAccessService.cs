using System.Security.Claims;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Services.Interfaces;

// Quy tắc phân quyền và duyệt theo cấp của thông báo.
public interface IAnnouncementAccessService
{
    Task<List<ResidentialUnit>> ChoicesAsync(ClaimsPrincipal actor, string permission);
    Task RequireAsync(ClaimsPrincipal actor, string permission, long groupId);
    Task<AnnouncementScope> ScopeAsync(ClaimsPrincipal actor);
    Task<Announcement?> FindReadableAsync(ClaimsPrincipal actor, long id);
    Task<List<Organization>> OrganizationChoicesAsync(ClaimsPrincipal actor);
    Task RequireAudienceAsync(ClaimsPrincipal actor, AnnouncementInput input);
    Task<bool> CanUpdateAsync(ClaimsPrincipal actor, Announcement item);
    Task RequireUpdateAsync(ClaimsPrincipal actor, Announcement item);
    Task<bool> CanSeeStatisticsAsync(ClaimsPrincipal actor, Announcement item);
    // Người này có quyền duyệt (và vì vậy tự đăng không cần chờ duyệt) bài này không.
    Task<bool> CanApproveAsync(ClaimsPrincipal actor, Announcement item);
    Task<bool> CanUnpublishAsync(ClaimsPrincipal actor, Announcement item);
}
