using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class PublicPortalRepository(MyStockContext db) : IPublicPortalRepository
{
    private IQueryable<Announcement> Published() => db.Announcements.AsNoTracking().Where(a =>
        a.Audience == "PUBLIC" && a.Status == "PUBLISHED" && a.ReviewStatus == "APPROVED" && a.PublishedAt <= DateTime.UtcNow &&
        (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow) && a.ResidentialGroup.IsActive && a.ResidentialGroup.UnitType == "GROUP" &&
        a.ResidentialGroup.ParentUnit != null && a.ResidentialGroup.ParentUnit.IsActive && a.ResidentialGroup.ParentUnit.UnitType == "AREA");

    public async Task<(List<Announcement> Items, int Total, int Page)> SearchPublishedAsync(string? search, long? groupId, int page, int size)
    {
        var query = Published();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(a => a.Title.Contains(search) || a.Body.Contains(search));
        if (groupId.HasValue)
            query = query.Where(a => a.ResidentialGroupId == groupId);
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)size)));
        var items = await query.Include(a => a.ResidentialGroup).OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total, page);
    }

    public Task<Announcement?> FindPublishedAsync(long id) => Published().Include(a => a.ResidentialGroup).SingleOrDefaultAsync(a => a.Id == id);
}
