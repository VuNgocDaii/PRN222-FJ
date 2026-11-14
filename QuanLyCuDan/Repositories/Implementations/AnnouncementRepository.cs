using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class AnnouncementRepository(MyStockContext db) : IAnnouncementRepository
{
    // Thông báo người dùng được đọc: trong thôn được cấp quyền và (xem toàn thôn | thuộc tổ chức mình | do mình soạn | đã đăng toàn thôn).
    private IQueryable<Announcement> Readable(AnnouncementScope scope)
    {
        var groups = scope.GroupIds;
        var open = scope.UnrestrictedGroupIds;
        var organizations = scope.OrganizationIds;
        var user = scope.UserId;
        return db.Announcements.Where(a => groups.Contains(a.ResidentialGroupId) &&
            (open.Contains(a.ResidentialGroupId) ||
             a.OrganizationId != null && organizations.Contains(a.OrganizationId.Value) ||
             a.CreatedByUserId == user ||
             a.OrganizationId == null && a.Status == "PUBLISHED"));
    }

    private IQueryable<Models.Activity> Notifications(long id)
    {
        var key = id.ToString();
        return db.Activities.Where(a => a.ActivityType == "NOTIFICATION" && a.EventCode == "ANNOUNCEMENT_PUBLISHED" && a.TargetType == "Announcement" && a.TargetId == key);
    }

    public async Task<AnnouncementPage> SearchAsync(AnnouncementScope scope, string? search, string filter, long? groupId, int page, int pageSize)
    {
        var query = Readable(scope).AsNoTracking();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(a => a.Title.Contains(search) || a.Body.Contains(search));
        if (groupId != null)
            query = query.Where(a => a.ResidentialGroupId == groupId);
        if (filter is "PUBLISHED" or "UNPUBLISHED")
            query = query.Where(a => a.Status == filter);
        else if (filter is "DRAFT" or "PENDING" or "APPROVED" or "REJECTED")
            query = query.Where(a => a.Status != "PUBLISHED" && a.ReviewStatus == filter);
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var items = await query.Include(a => a.ResidentialGroup)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new AnnouncementPage(items, total, page);
    }

    public Task<Announcement?> FindAsync(AnnouncementScope scope, long id) =>
        Readable(scope).AsTracking().Include(a => a.Organization).Include(a => a.ResidentialGroup)
            .Include(a => a.CreatedByUser).Include(a => a.PublishedByUser).SingleOrDefaultAsync(a => a.Id == id);

    public async Task AddAsync(Announcement item) => await db.Announcements.AddAsync(item);

    public Task<int> CountRecipientsAsync(long id) => Notifications(id).CountAsync();

    public Task<int> CountReadAsync(long id) => Notifications(id).CountAsync(a => a.ReadAt != null);

    public Task<List<AnnouncementHistoryItem>> GetHistoryAsync(long id)
    {
        var key = id.ToString();
        return db.Activities.AsNoTracking()
            .Where(a => a.ActivityType == "AUDIT" && a.TargetType == "Announcement" && a.TargetId == key)
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(50)
            .Select(a => new AnnouncementHistoryItem(a.EventCode, a.ActorUser == null ? null : a.ActorUser.FullName, a.OccurredAt)).ToListAsync();
    }

    public Task<List<string>> GetRecipientUserIdsAsync(Announcement item, DateTime now)
    {
        var groupId = item.ResidentialGroupId;
        var organizationId = item.OrganizationId;
        return db.AspNetUsers.Where(u => u.IsActive && u.VerificationStatus == "APPROVED" && u.Resident != null && u.Resident.Household != null && !u.Resident.Household.IsArchived &&
            u.Resident.Household.ResidentialGroupId == groupId &&
            (organizationId == null || db.OrganizationMembers.Any(m => m.OrganizationId == organizationId && m.ResidentId == u.ResidentId && m.LeftAt == null && m.JoinedAt <= now && m.Organization.IsActive)) &&
            db.VwEffectivePermissions.Any(p => p.UserId == u.Id && p.ScopeType == "SELF" && p.PermissionCode == "announcements.audience.read"))
            .Select(u => u.Id).ToListAsync();
    }

    public Task<List<Models.Activity>> GetPublishNotificationsAsync(long id) => Notifications(id).ToListAsync();

    public void AddNotifications(IEnumerable<Models.Activity> items) => db.Activities.AddRange(items);
}
