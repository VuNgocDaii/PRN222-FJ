using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IPublicPortalService
{
    Task<PublicHomeViewModel> GetHomeAsync(string? search, long? groupId, int page = 1);
    Task<Announcement?> GetAnnouncementAsync(long id);
}
