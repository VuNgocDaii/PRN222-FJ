using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Models;
using QuanLyCuDan.Services;
using QuanLyCuDan.Services.Interfaces;
namespace QuanLyCuDan.Controllers;
[AllowAnonymous]
public sealed class HomeController(IPublicPortalService service) : Controller
{
    public async Task<IActionResult> Index(string? search, long? groupId, int page = 1) => View(await service.GetHomeAsync(search, groupId, page));
    [HttpGet("/thong-bao/{id:long}")]
    public async Task<IActionResult> Announcement(long id)
    {
        var item = await service.GetAnnouncementAsync(id); return item == null ? NotFound() : View(item);
    }
    [HttpGet("/thong-bao/{id:long}/Document")]
    public async Task<IActionResult> AnnouncementDocument(long id)
    {
        var item = await service.GetAnnouncementAsync(id);
        return item?.DocumentContent == null ? NotFound() : File(item.DocumentContent, "application/pdf", item.DocumentFileName ?? "van-ban.pdf");
    }
    public IActionResult Privacy() => View();
    public IActionResult Error() => View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
}
