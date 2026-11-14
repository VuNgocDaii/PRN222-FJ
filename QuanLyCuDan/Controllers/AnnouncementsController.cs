using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Services;
using QuanLyCuDan.Services.Interfaces;
namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies"), Route("Announcements")]
public sealed class AnnouncementsController(IAnnouncementService service, INavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string filter = "ALL", long? groupId = null, int page = 1) => View(await service.ListAsync(User, search, filter, groupId, page));
    [HttpGet("Create")]
    public async Task<IActionResult> Create() => View("Edit", await service.GetCreateModelAsync(User));
    [HttpPost("Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] AnnouncementInput input)
    {
        var model = await service.GetCreateModelAsync(User, input);
        AddContentErrors(input);
        if (!ModelState.IsValid)
            return View("Edit", model);
        var id = await service.CreateAsync(User, input);
        TempData["AnnouncementMessage"] = "Đã lưu nháp. Gửi duyệt khi nội dung đã sẵn sàng.";
        return RedirectToAction(nameof(Detail), new
        {
            id
        });
    }
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var model = await service.GetDetailAsync(User, id);
        return model == null ? NotFound() : View(model);
    }
    [HttpGet("{id:long}/Edit")]
    public async Task<IActionResult> Edit(long id)
    {
        var model = await service.GetEditModelAsync(User, id);
        return model == null ? NotFound() : View(model);
    }
    [HttpPost("{id:long}/Edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, [Bind(Prefix = "Input")] AnnouncementInput input)
    {
        var model = await service.GetEditModelAsync(User, id, input);
        if (model == null)
            return NotFound();
        AddContentErrors(input);
        if (!ModelState.IsValid)
            return View(model);
        try
        {
            await service.UpdateAsync(User, id, input);
            TempData["AnnouncementMessage"] = "Đã lưu thay đổi. Nội dung cần được gửi duyệt lại trước khi đăng.";
        }
        catch (DbUpdateConcurrencyException) { TempData["AnnouncementError"] = "Thông báo đã thay đổi. Hãy tải lại và kiểm tra nội dung mới."; }
        return RedirectToAction(nameof(Detail), new
        {
            id
        });
    }
    [HttpPost("{id:long}/Submit"), ValidateAntiForgeryToken]
    public Task<IActionResult> Submit(long id, string rowVersion) => Change(id, () => service.SubmitAsync(User, id, rowVersion));
    [HttpPost("{id:long}/Withdraw"), ValidateAntiForgeryToken]
    public Task<IActionResult> Withdraw(long id, string rowVersion) => Change(id, () => service.WithdrawAsync(User, id, rowVersion));
    [HttpPost("{id:long}/Review"), ValidateAntiForgeryToken]
    public Task<IActionResult> Review(long id, string rowVersion, bool approve, string? note) => Change(id, () => service.ReviewAsync(User, id, rowVersion, approve, note));
    [HttpPost("{id:long}/Unpublish"), ValidateAntiForgeryToken]
    public Task<IActionResult> Unpublish(long id, string rowVersion, string? reason) => Change(id, () => service.UnpublishAsync(User, id, rowVersion, reason));
    [HttpPost("{id:long}/Document"), ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Document(long id, IFormFile? file, string rowVersion)
    {
        using var stream = file?.OpenReadStream();
        return await Change(id, async () =>
        {
            await service.UploadDocumentAsync(User, id, stream, file?.Length ?? 0, file?.FileName ?? "", rowVersion);
            return "ANNOUNCEMENT_DOCUMENT_UPDATED";
        });
    }
    [HttpGet("{id:long}/Document")]
    public async Task<IActionResult> Document(long id)
    {
        var document = await service.DownloadDocumentAsync(User, id);
        return document == null ? NotFound() : File(document.Content, document.ContentType, document.FileName);
    }
    private async Task<IActionResult> Change(long id, Func<Task<string>> apply)
    {
        try
        {
            if (!ModelState.IsValid)
                throw new AppFault(400, "Dữ liệu thao tác không hợp lệ. Hãy tải lại biểu mẫu.");
            TempData["AnnouncementMessage"] = AnnouncementLabels.Event(await apply()) + " thành công.";
        }
        catch (AppFault error) when (error.Status is 400 or 409) { TempData["AnnouncementError"] = error.Message; }
        catch (DbUpdateConcurrencyException) { TempData["AnnouncementError"] = "Thông báo đã thay đổi. Hãy tải lại trước khi thao tác."; }
        return RedirectToAction(nameof(Detail), new
        {
            id
        });
    }
    private void AddContentErrors(AnnouncementInput input)
    {
        foreach (var error in service.ValidateContent(input))
            ModelState.AddModelError(error.Key.Length == 0 ? "" : "Input." + error.Key, error.Value);
    }
}
