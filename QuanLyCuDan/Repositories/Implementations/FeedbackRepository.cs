using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class FeedbackRepository(MyStockContext db) : IFeedbackRepository
{
    private IQueryable<Request> Scoped(string? submittedBy, IReadOnlyCollection<long>? groupIds)
    {
        var query = db.Requests.AsNoTracking().Where(x => x.RequestKind == "FEEDBACK");
        if (submittedBy != null)
            query = query.Where(x => x.SubmittedByUserId == submittedBy);
        if (groupIds != null)
            query = query.Where(x => groupIds.Contains(x.AssignedGroupId));
        return query;
    }

    public Task<long?> GetResidentIdAsync(string userId) => db.AspNetUsers.Where(x => x.Id == userId).Select(x => x.ResidentId).SingleAsync();

    public Task<List<FeedbackListItem>> ListAsync(string? submittedBy, IReadOnlyCollection<long>? groupIds) =>
        Scoped(submittedBy, groupIds).OrderByDescending(x => x.SubmittedAt)
            .Select(x => new FeedbackListItem(x.Id, x.Code, x.Subject, x.Status, x.SubmittedAt, x.SubmittedByUser.FullName)).ToListAsync();

    public Task<Request?> FindInScopeWithSubmitterAsync(long id, string? submittedBy, IReadOnlyCollection<long>? groupIds) =>
        Scoped(submittedBy, groupIds).Include(x => x.SubmittedByUser).FirstOrDefaultAsync(x => x.Id == id);

    public Task<Request?> FindFeedbackInGroupsAsync(long id, IReadOnlyCollection<long> groupIds) =>
        db.Requests.AsNoTracking().Where(x => x.RequestKind == "FEEDBACK" && x.Id == id && groupIds.Contains(x.AssignedGroupId)).FirstOrDefaultAsync();

    public Task<Request?> FindFeedbackBySubmitterAsync(long id, string userId) =>
        db.Requests.AsNoTracking().Where(x => x.RequestKind == "FEEDBACK" && x.Id == id && x.SubmittedByUserId == userId).FirstOrDefaultAsync();

    public Task<List<FeedbackMessage>> GetMessagesAsync(long requestId) =>
        db.Activities.AsNoTracking().Where(a => a.RequestId == requestId && a.ActivityType == "MESSAGE").OrderBy(a => a.OccurredAt)
            .Select(a => new FeedbackMessage(a.ActorUser!.FullName, a.Body!, a.OccurredAt, a.EventCode == "OFFICIAL_REPLY")).ToListAsync();

    public Task<Resident?> GetResidentWithHouseholdAsync(string userId) =>
        db.AspNetUsers.Where(x => x.Id == userId && x.ResidentId != null).Select(x => x.Resident).SingleOrDefaultAsync();

    public Task<long> GetHouseholdGroupIdAsync(long householdId) =>
        db.Households.Where(h => h.Id == householdId && !h.IsArchived).Select(h => h.ResidentialGroupId).SingleAsync();

    public Task<Request?> FindFeedbackAsync(long id) => db.Requests.SingleOrDefaultAsync(x => x.Id == id && x.RequestKind == "FEEDBACK");
    public void Add(Request request) => db.Requests.Add(request);
    public void AddActivity(Models.Activity activity) => db.Activities.Add(activity);

    public Task<int> CountPendingAsync(IReadOnlyCollection<long> groupIds) =>
        db.Requests.AsNoTracking().CountAsync(x => x.RequestKind == "FEEDBACK" && x.Status == "PENDING" && !EF.Functions.Like(x.Code, "PA-DEMO-%") && groupIds.Contains(x.AssignedGroupId));
}
