using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class ResidentRepository(MyStockContext db) : IResidentRepository
{
    private IQueryable<Models.Request> Mine(string userId, string kind) =>
        db.Requests.Where(r => r.SubmittedByUserId == userId && r.Source == "RESIDENT" && r.RequestKind == kind);

    public Task<AspNetUser> GetAccountAsync(string userId) => db.AspNetUsers.Include(u => u.Resident)!
        .ThenInclude(r => r!.Household)!.ThenInclude(h => h!.ResidentialGroup).ThenInclude(g => g.ParentUnit)
        .SingleAsync(u => u.Id == userId);

    public Task<List<Resident>> GetHouseholdMembersAsync(long householdId) =>
        db.Residents.AsNoTracking().Where(r => r.HouseholdId == householdId).OrderBy(r => r.Id).ToListAsync();

    public Task<List<Payment>> GetPaymentsAsync(long householdId) =>
        db.Payments.AsNoTracking().Include(p => p.Campaign).Where(p => p.HouseholdId == householdId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public Task<bool> ResidentInHouseholdAsync(long? residentId, long householdId) => db.Residents.AnyAsync(r => r.Id == residentId && r.HouseholdId == householdId);

    public Task<long?> FindTargetHouseholdIdAsync(string? code, long groupId) =>
        db.Households.Where(h => h.Code == code && !h.IsArchived && h.ResidentialGroupId == groupId).Select(h => (long?)h.Id).SingleOrDefaultAsync();

    public Task<int> CountRequestsAsync(string userId, string kind) => Mine(userId, kind).AsNoTracking().CountAsync();

    public Task<List<Models.Request>> ListRequestsAsync(string userId, string kind, int page, int size) =>
        Mine(userId, kind).AsNoTracking().OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id).Skip((page - 1) * size).Take(size).ToListAsync();

    public Task<bool> HasOpenResidenceRequestAsync(string userId) => Mine(userId, "RESIDENCE").AnyAsync(r => r.Status == "PENDING" || r.Status == "PROCESSING");

    public Task<Models.Request?> FindByClientKeyAsync(string userId, string? clientKey) =>
        db.Requests.Include(r => r.Activities).SingleOrDefaultAsync(r => r.SubmittedByUserId == userId && r.ClientRequestKey == clientKey);

    public Task<Models.Request?> FindMineAsync(string userId, string kind, long id) => Mine(userId, kind).SingleOrDefaultAsync(r => r.Id == id);

    public Task<Models.Request?> FindOwnedAsync(string userId, long id) =>
        db.Requests.SingleOrDefaultAsync(r => r.Id == id && r.Source == "RESIDENT" && r.SubmittedByUserId == userId);

    public Task<Models.Request?> FindOwnedNoTrackingAsync(string userId, long id) =>
        db.Requests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.Source == "RESIDENT" && r.SubmittedByUserId == userId);

    public Task<Models.Request?> FindDetailAsync(string userId, long id) =>
        db.Requests.AsNoTracking().Include(r => r.AssignedGroup).SingleOrDefaultAsync(r => r.Id == id && r.SubmittedByUserId == userId && r.Source == "RESIDENT");

    public Task<List<Models.Activity>> GetTimelineAsync(long requestId) =>
        db.Activities.AsNoTracking().Where(a => a.RequestId == requestId && (a.ActivityType == "MESSAGE" || a.ActivityType == "REQUEST_EVENT")).OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).ToListAsync();

    public Task<Models.Activity?> FindMessageByKeyAsync(string userId, string clientKey) =>
        db.Activities.SingleOrDefaultAsync(a => a.ActorUserId == userId && a.ClientMessageKey == clientKey);

    public void Add(Models.Request request) => db.Requests.Add(request);
    public void AddActivity(Models.Activity activity) => db.Activities.Add(activity);

    public IQueryable<Announcement> VisibleAnnouncements(long groupId, long? residentId) =>
        db.Announcements.AsNoTracking().Where(a => a.ResidentialGroupId == groupId &&
            a.Status == "PUBLISHED" && a.ReviewStatus == "APPROVED" && a.PublishedAt <= DateTime.UtcNow &&
            (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow) &&
            (a.Audience == "PUBLIC" || a.Audience == "RESIDENTS" || a.Audience == "ORGANIZATION" &&
             db.OrganizationMembers.Any(m => m.OrganizationId == a.OrganizationId && m.ResidentId == residentId &&
                m.LeftAt == null && m.JoinedAt <= DateTime.UtcNow && m.Organization.IsActive)));

    private IQueryable<Models.Activity> Notifications(string userId) =>
        db.Activities.AsNoTracking().Where(a => a.ActivityType == "NOTIFICATION" && a.RecipientUserId == userId);

    public Task<int> CountNotificationsAsync(string userId) => Notifications(userId).CountAsync();

    public Task<List<Models.Activity>> ListNotificationsAsync(string userId, int page, int size) =>
        Notifications(userId).OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync();

    public Task<Models.Activity?> FindNotificationAsync(long id, string userId) =>
        db.Activities.SingleOrDefaultAsync(a => a.Id == id && a.ActivityType == "NOTIFICATION" && a.RecipientUserId == userId);
}
