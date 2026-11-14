using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

// Truy vấn cho cổng cư dân (hồ sơ, hộ, yêu cầu/phản ánh của chính cư dân, thông báo).
public interface IResidentRepository
{
    Task<AspNetUser> GetAccountAsync(string userId);
    Task<List<Resident>> GetHouseholdMembersAsync(long householdId);
    Task<List<Payment>> GetPaymentsAsync(long householdId);
    Task<bool> ResidentInHouseholdAsync(long? residentId, long householdId);
    Task<long?> FindTargetHouseholdIdAsync(string? code, long groupId);

    Task<int> CountRequestsAsync(string userId, string kind);
    Task<List<Models.Request>> ListRequestsAsync(string userId, string kind, int page, int size);
    Task<bool> HasOpenResidenceRequestAsync(string userId);
    Task<Models.Request?> FindByClientKeyAsync(string userId, string? clientKey);
    Task<Models.Request?> FindMineAsync(string userId, string kind, long id);
    Task<Models.Request?> FindOwnedAsync(string userId, long id);
    Task<Models.Request?> FindOwnedNoTrackingAsync(string userId, long id);
    Task<Models.Request?> FindDetailAsync(string userId, long id);
    Task<List<Models.Activity>> GetTimelineAsync(long requestId);
    Task<Models.Activity?> FindMessageByKeyAsync(string userId, string clientKey);
    void Add(Models.Request request);
    void AddActivity(Models.Activity activity);

    IQueryable<Announcement> VisibleAnnouncements(long groupId, long? residentId);

    Task<int> CountNotificationsAsync(string userId);
    Task<List<Models.Activity>> ListNotificationsAsync(string userId, int page, int size);
    Task<Models.Activity?> FindNotificationAsync(long id, string userId);
}
