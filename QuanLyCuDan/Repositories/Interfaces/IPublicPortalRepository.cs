using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IPublicPortalRepository
{
    // Thông báo công khai đang đăng; page được chỉnh lại cho nằm trong khoảng hợp lệ.
    Task<(List<Announcement> Items, int Total, int Page)> SearchPublishedAsync(string? search, long? groupId, int page, int size);
    Task<Announcement?> FindPublishedAsync(long id);
}
