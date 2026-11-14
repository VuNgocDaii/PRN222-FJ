using QuanLyCuDan.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Services;
namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies"), Route("Resident")]
public sealed class ResidentController(IResidentService service, INavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet("{section?}")]
    public async Task<IActionResult> Index(string section = "overview", int page = 1) => View(await service.GetPortalAsync(User, section, page));
    [HttpGet("announcement/{id:long}")]
    public async Task<IActionResult> Announcement(long id)
    {
        var model = await service.GetAnnouncementAsync(User, id); return model == null ? NotFound() : View(model);
    }
    [HttpGet("announcement/{id:long}/Document")]
    public async Task<IActionResult> AnnouncementDocument(long id)
    {
        var item = await service.GetAnnouncementAsync(User, id);
        return item?.DocumentContent == null ? NotFound() : File(item.DocumentContent, "application/pdf", item.DocumentFileName ?? "van-ban.pdf");
    }
    [HttpPost("contact"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactInput input)
    {
        await service.Check(User, "profile.self.update_contact");
        if (!ModelState.IsValid) return View("Index", await service.GetPortalAsync(User, "profile"));
        try { await service.UpdateContactAsync(User, input); TempData["Success"] = "Đã cập nhật thông tin liên hệ."; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Thông tin đã thay đổi. Hãy tải lại và thử lại."; }
        return RedirectToAction(nameof(Index), new { section = "profile" });
    }
    [HttpPost("requests"), ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] ResidentRequestInput input, IFormFile? file)
    {
        var feedback = input.Kind == "FEEDBACK";
        await service.Check(User, feedback ? "feedback.self.create" : "residence_requests.self.create");
        if (ModelState.IsValid)
        {
            try { using var stream = file?.OpenReadStream(); var id = await service.CreateRequestAsync(User, input, stream, file?.Length ?? 0, file?.FileName ?? ""); TempData["Success"] = "Đã gửi yêu cầu. Bạn có thể theo dõi tiến độ tại đây."; return RedirectToAction(nameof(Detail), new { id }); }
            catch (AppFault error) when (error.Status is 400 or 409) { ModelState.AddModelError("", error.Message); }
        }
        var model = await service.GetPortalAsync(User, feedback ? "feedback" : "requests"); model.Input = input;
        return View("Index", model);
    }
    [HttpGet("request/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var model = await service.GetDetailAsync(User, id); return model == null ? NotFound() : View(model);
    }
    [HttpPost("request/{id:long}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, string rowVersion)
    {
        await service.CancelAsync(User, id, rowVersion); TempData["Success"] = "Đã hủy yêu cầu."; return RedirectToAction(nameof(Detail), new { id });
    }
    [HttpPost("request/{id:long}/reply"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(long id, string body, string clientKey)
    {
        try { await service.ReplyAsync(User, id, body, clientKey); TempData["Success"] = "Đã gửi trao đổi."; }
        catch (AppFault error) when (error.Status == 400) { TempData["Error"] = error.Message; }
        return RedirectToAction(nameof(Detail), new { id });
    }
    [HttpPost("notifications/{id:long}/read"), ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long id)
    {
        await service.MarkReadAsync(User, id); return RedirectToAction(nameof(Index), new { section = "notifications" });
    }
    [HttpPost("request/{id:long}/attachment"), ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(long id, IFormFile? file, string rowVersion)
    {
        try { using var stream = file?.OpenReadStream(); await service.UploadAsync(User, id, stream, file?.Length ?? 0, file?.FileName ?? "", rowVersion); TempData["Success"] = "Đã bổ sung giấy tờ vào hồ sơ."; }
        catch (AppFault error) when (error.Status == 400) { TempData["Error"] = error.Message; }
        return RedirectToAction(nameof(Detail), new { id });
    }
    [HttpGet("request/{id:long}/attachment")]
    public async Task<IActionResult> Download(long id)
    {
        var file = await service.DownloadAsync(User, id); if (file == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff"; Response.Headers.ContentSecurityPolicy = "sandbox";
        return File(file.Content, file.ContentType, file.FileName);
    }
}
