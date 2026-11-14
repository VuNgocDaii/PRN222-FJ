using QuanLyCuDan.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Services;

namespace QuanLyCuDan.Controllers;

[Authorize(AuthenticationSchemes = "Cookies"), Route("Feedback")]
public sealed class FeedbackController(IFeedbackService service, INavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(long? id, string? notice) => View("Index", await service.GetAsync(User, id, notice));

    [HttpGet("{id:long}/attachment")]
    public async Task<IActionResult> Attachment(long id)
    {
        var file = await service.DownloadAttachmentAsync(User, id);
        if (file == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox";
        return File(file.Content, file.ContentType);
    }

    [HttpPost("Create"), ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create(string subject, string category, string body, IFormFile? file)
    {
        try { using var stream = file?.OpenReadStream(); var id = await service.CreateAsync(User, subject, category, body, stream, file?.Length ?? 0, file?.FileName ?? ""); return RedirectToAction(nameof(Index), new { id, notice = "Đã gửi góp ý." }); }
        catch (AppFault error) when (error.Status == 400) { return RedirectToAction(nameof(Index), new { notice = error.Message }); }
    }

    [HttpPost("{id:long}/Process"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(long id)
    {
        await service.ProcessAsync(User, id);
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost("{id:long}/Complete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(long id)
    {
        try { await service.CompleteAsync(User, id); return RedirectToAction(nameof(Index), new { id }); }
        catch (AppFault error) when (error.Status == 409) { return Conflict(error.Message); }
    }

    [HttpPost("{id:long}/Reply"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(long id, string body)
    {
        try { await service.ReplyAsync(User, id, body); return RedirectToAction(nameof(Index), new { id }); }
        catch (AppFault error) when (error.Status == 400) { return RedirectToAction(nameof(Index), new { id, notice = error.Message }); }
    }
}
