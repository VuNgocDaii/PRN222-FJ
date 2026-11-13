using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Controllers;
[AllowAnonymous]
public sealed class HomeController(MyStockContext db) : Controller
{
    private IQueryable<Announcement> Published() => db.Announcements.AsNoTracking().Where(a =>
        a.Audience == "PUBLIC" && a.Status == "PUBLISHED" && a.ReviewStatus == "APPROVED" && a.PublishedAt <= DateTime.UtcNow &&
        (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow) && a.ResidentialGroup.IsActive && a.ResidentialGroup.UnitType == "GROUP" &&
        a.ResidentialGroup.ParentUnit != null && a.ResidentialGroup.ParentUnit.IsActive && a.ResidentialGroup.ParentUnit.UnitType == "AREA");
    public async Task<IActionResult> Index(string? search, long? groupId, int page = 1)
    {
        search = search?.Trim();
        if (search?.Length > 200) search = search[..200];
        var query = Published();
        if (!string.IsNullOrEmpty(search)) query = query.Where(a => a.Title.Contains(search) || a.Body.Contains(search));
        if (groupId.HasValue) query = query.Where(a => a.ResidentialGroupId == groupId);
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 9d)));
        return View(new PublicHomeViewModel {
            Search = search, GroupId = groupId, Page = page, Total = total,
            Groups = await db.ResidentialUnits.AsNoTracking().Where(g => g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA").OrderBy(g => g.Name).ToListAsync(),
            Announcements = await query.Include(a => a.ResidentialGroup).OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id).Skip((page - 1) * 9).Take(9).ToListAsync()
        });
    }
    [HttpGet("/thong-bao/{id:long}")]
    public async Task<IActionResult> Announcement(long id)
    {
        var item = await Published().Include(a => a.ResidentialGroup).SingleOrDefaultAsync(a => a.Id == id);
        return item == null ? NotFound() : View(item);
    }
    public IActionResult Privacy() => View();
    public IActionResult Error() => View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
}
