using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

// Phạm vi đọc của một người dùng: thôn được xem, thôn xem toàn bộ, tổ chức được xem, và chính họ.
public sealed record AnnouncementScope(string UserId, long[] GroupIds, long[] UnrestrictedGroupIds, long[] OrganizationIds);
public sealed record AnnouncementPage(List<Announcement> Items, int Total, int Page);

public interface IAnnouncementRepository
{
    Task<AnnouncementPage> SearchAsync(AnnouncementScope scope, string? search, string filter, long? groupId, int page, int pageSize);
    Task<Announcement?> FindAsync(AnnouncementScope scope, long id);
    Task AddAsync(Announcement item);
    Task<int> CountRecipientsAsync(long id);
    Task<int> CountReadAsync(long id);
    Task<List<AnnouncementHistoryItem>> GetHistoryAsync(long id);
    Task<List<string>> GetRecipientUserIdsAsync(Announcement item, DateTime now);
    Task<List<Models.Activity>> GetPublishNotificationsAsync(long id);
    void AddNotifications(IEnumerable<Models.Activity> items);
}
