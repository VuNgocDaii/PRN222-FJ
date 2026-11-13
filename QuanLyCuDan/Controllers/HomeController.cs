using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Portal;
namespace QuanLyCuDan.Controllers;
[AllowAnonymous]
public sealed class HomeController(MyStockContext db, IConfiguration configuration) : Controller
{
    private readonly PortalScope portal = new(db, configuration);
    private readonly OrganizationReader organizations = new(db);
    private IQueryable<Announcement> Published(long groupId) => db.Announcements.AsNoTracking().Where(a =>
        a.ResidentialGroupId == groupId && a.Audience == "PUBLIC" && a.Status == "PUBLISHED" && a.ReviewStatus == "APPROVED" && a.PublishedAt <= DateTime.UtcNow &&
        (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow) && a.ResidentialGroup.IsActive && a.ResidentialGroup.UnitType == "GROUP" &&
        a.ResidentialGroup.ParentUnit != null && a.ResidentialGroup.ParentUnit.IsActive && a.ResidentialGroup.ParentUnit.UnitType == "AREA");
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var locality = await portal.GetAsync();
        ViewData["PortalLocality"] = locality;
        search = search?.Trim();
        if (search?.Length > 200) search = search[..200];
        var query = Published(locality.Id);
        if (!string.IsNullOrEmpty(search)) query = query.Where(a => a.Title.Contains(search) || a.Body.Contains(search));
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 9d)));
        return View(new PublicHomeViewModel {
            Search = search, Page = page, Total = total,
            Organizations = await organizations.Directory(locality.Id),
            Announcements = await query.Include(a => a.ResidentialGroup).OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id).Skip((page - 1) * 9).Take(9).ToListAsync()
        });
    }
    [HttpGet("/thong-bao/{id:long}")]
    public async Task<IActionResult> Announcement(long id)
    {
        var locality = await portal.GetAsync();
        ViewData["PortalLocality"] = locality;
        var item = await Published(locality.Id).Include(a => a.ResidentialGroup).SingleOrDefaultAsync(a => a.Id == id);
        return item == null ? NotFound() : View(item);
    }
    public async Task<IActionResult> Privacy()
    {
        ViewData["PortalLocality"] = await portal.GetAsync();
        return View();
    }
    public IActionResult Error() => View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
}
