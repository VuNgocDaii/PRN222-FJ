using QuanLyCuDan.Models;

namespace QuanLyCuDan.Repositories.Interfaces;

public interface IFeedbackRepository
{
    Task<long?> GetResidentIdAsync(string userId);
    // submittedBy != null: chỉ góp ý của người đó (cư dân); groupIds != null: góp ý thuộc các thôn đó (cán bộ).
    Task<List<FeedbackListItem>> ListAsync(string? submittedBy, IReadOnlyCollection<long>? groupIds);
    Task<Request?> FindInScopeWithSubmitterAsync(long id, string? submittedBy, IReadOnlyCollection<long>? groupIds);
    Task<List<FeedbackMessage>> GetMessagesAsync(long requestId);
    Task<Resident?> GetResidentWithHouseholdAsync(string userId);
    Task<long> GetHouseholdGroupIdAsync(long householdId);
    Task<Request?> FindFeedbackAsync(long id);
    void Add(Request request);
    void AddActivity(Models.Activity activity);
    Task<int> CountPendingAsync(IReadOnlyCollection<long> groupIds);
}
