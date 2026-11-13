using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class PublicPortalService(MyStockContext db)
{
    private IQueryable<Announcement> Published() => db.Announcements.AsNoTracking().Where(a =>
        a.Audience == "PUBLIC" && a.Status == "PUBLISHED" && a.ReviewStatus == "APPROVED" && a.PublishedAt <= DateTime.UtcNow &&
        (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow) && a.ResidentialGroup.IsActive && a.ResidentialGroup.UnitType == "GROUP" &&
        a.ResidentialGroup.ParentUnit != null && a.ResidentialGroup.ParentUnit.IsActive && a.ResidentialGroup.ParentUnit.UnitType == "AREA");
    public async Task<PublicHomeViewModel> GetHomeAsync(string? search, long? groupId, int page = 1)
    {
        search = search?.Trim();
        if (search?.Length > 200) search = search[..200];
        var query = Published();
        if (!string.IsNullOrEmpty(search)) query = query.Where(a => a.Title.Contains(search) || a.Body.Contains(search));
        if (groupId.HasValue) query = query.Where(a => a.ResidentialGroupId == groupId);
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 9d)));
        return new PublicHomeViewModel {
            Search = search, GroupId = groupId, Page = page, Total = total,
            Groups = await db.ResidentialUnits.AsNoTracking().Where(g => g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA").OrderBy(g => g.Name).ToListAsync(),
            Announcements = await query.Include(a => a.ResidentialGroup).OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id).Skip((page - 1) * 9).Take(9).ToListAsync()
        };
    }
    public Task<Announcement?> GetAnnouncementAsync(long id) => Published().Include(a => a.ResidentialGroup).SingleOrDefaultAsync(a => a.Id == id);
}
