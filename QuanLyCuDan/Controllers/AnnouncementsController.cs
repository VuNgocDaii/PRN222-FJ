using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Controllers;

[Authorize(AuthenticationSchemes = "Cookies")]
[Route("Announcements")]
public sealed class AnnouncementsController(MyStockContext context) : BaseController(context)
{
    private IQueryable<ResidentialUnit> Groups(string permission)
    {
        var grants = Effective(CurrentUserId(User)).Where(p => p.PermissionCode == permission);
        return db.ResidentialUnits.Where(g => g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.UnitType == "AREA" && g.ParentUnit.IsActive &&
            grants.Any(p => p.ScopeType == "GROUP" && p.ResidentialGroupId == g.Id || p.ScopeType == "AREA" && p.ResidentialAreaId == g.ParentUnitId));
    }
    private async Task Check(string permission, long groupId)
    {
        if (!await Groups(permission).AnyAsync(g => g.Id == groupId)) throw new AppFault(403, "Bạn không có quyền thực hiện thao tác này trong tổ dân phố đã chọn.");
    }
    private async Task<Announcement?> Find(long id)
    {
        // Materialize grants separately: the permission view's AsNoTracking must not detach the editable announcement.
        var readable = await Groups("announcements.read").Select(g => g.Id).ToArrayAsync();
        return await db.Announcements.AsTracking().Include(a => a.ResidentialGroup).Include(a => a.CreatedByUser).Include(a => a.PublishedByUser)
            .SingleOrDefaultAsync(a => a.Id == id && readable.Contains(a.ResidentialGroupId));
    }
    private async Task<List<ResidentialUnit>> Choices(string permission) => await Groups(permission).AsNoTracking().OrderBy(g => g.Name).ToListAsync();

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string filter = "ALL", long? groupId = null, int page = 1)
    {
        var groups = await Choices("announcements.read");
        if (groups.Count == 0) throw new AppFault(403, "Bạn chưa được cấp quyền xem thông báo trong địa bàn.");
        var ids = groups.Select(g => g.Id).ToArray();
        var query = db.Announcements.AsNoTracking().Where(a => ids.Contains(a.ResidentialGroupId));
        search = search?.Trim(); if (search?.Length > 200) search = search[..200];
        if (!string.IsNullOrEmpty(search)) query = query.Where(a => a.Title.Contains(search) || a.Body.Contains(search));
        if (groupId != null) query = query.Where(a => a.ResidentialGroupId == groupId);
        if (filter is "PUBLISHED" or "UNPUBLISHED") query = query.Where(a => a.Status == filter);
        else if (filter is "DRAFT" or "PENDING" or "APPROVED" or "REJECTED") query = query.Where(a => a.Status != "PUBLISHED" && a.ReviewStatus == filter);
        else filter = "ALL";
        var total = await query.CountAsync(); page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 12d)));
        return View(new AnnouncementListViewModel { Groups = groups, Search = search, Filter = filter, GroupId = groupId, Page = page, Total = total,
            Items = await query.Include(a => a.ResidentialGroup).OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Skip((page - 1) * 12).Take(12).ToListAsync(),
            CanCreate = await Groups("announcements.create").AnyAsync() });
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var groups = await Choices("announcements.create");
        if (groups.Count == 0) throw new AppFault(403, "Bạn chưa được cấp quyền soạn thông báo.");
        return View("Edit", new AnnouncementEditViewModel { Groups = groups, Input = new AnnouncementInput { ResidentialGroupId = groups[0].Id } });
    }
    [HttpPost("Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] AnnouncementInput input)
    {
        await Check("announcements.create", input.ResidentialGroupId);
        ValidateContent(input);
        if (!ModelState.IsValid) return View("Edit", new AnnouncementEditViewModel { Input = input, Groups = await Choices("announcements.create") });
        var item = new Announcement { Title = input.Title.Trim(), Body = input.Body.Trim(), Audience = input.Audience,
            ResidentialGroupId = input.ResidentialGroupId, Status = "DRAFT", ReviewStatus = "DRAFT", CreatedByUserId = CurrentUserId(User),
            CreatedAt = DateTime.UtcNow, ExpiresAt = ToUtc(input.ExpiresAtLocal) };
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Announcements.Add(item); await db.SaveChangesAsync();
        Audit(CurrentUserId(User), "ANNOUNCEMENT_CREATED", "Announcement", item.Id.ToString());
        await db.SaveChangesAsync(); await tx.CommitAsync();
        TempData["AnnouncementMessage"] = "Đã lưu nháp. Gửi duyệt khi nội dung đã sẵn sàng.";
        return RedirectToAction(nameof(Detail), new { id = item.Id });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var item = await Find(id); if (item == null) return NotFound();
        var editable = item.Status != "PUBLISHED" && item.ReviewStatus != "PENDING";
        var update = await Groups("announcements.update").AnyAsync(g => g.Id == item.ResidentialGroupId);
        return View(new AnnouncementDetailViewModel {
            Item = item, CanEdit = editable && update,
            CanSubmit = item.Status != "PUBLISHED" && item.ReviewStatus is "DRAFT" or "REJECTED" && update,
            CanReview = item.Status != "PUBLISHED" && item.ReviewStatus == "PENDING" && await Groups("announcements.approve").AnyAsync(g => g.Id == item.ResidentialGroupId),
            CanPublish = item.Status != "PUBLISHED" && item.ReviewStatus == "APPROVED" && await Groups("announcements.publish").AnyAsync(g => g.Id == item.ResidentialGroupId),
            CanUnpublish = item.Status == "PUBLISHED" && await Groups("announcements.unpublish").AnyAsync(g => g.Id == item.ResidentialGroupId),
            CanWithdraw = item.Status != "PUBLISHED" && item.ReviewStatus == "PENDING" && update,
            History = await db.Activities.AsNoTracking().Where(a => a.ActivityType == "AUDIT" && a.TargetType == "Announcement" && a.TargetId == id.ToString())
                .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(50)
                .Select(a => new AnnouncementHistoryItem(a.EventCode, a.ActorUser == null ? null : a.ActorUser.FullName, a.OccurredAt)).ToListAsync()
        });
    }
    [HttpGet("{id:long}/Edit")]
    public async Task<IActionResult> Edit(long id)
    {
        var item = await Find(id); if (item == null) return NotFound();
        await Check("announcements.update", item.ResidentialGroupId);
        if (item.Status == "PUBLISHED" || item.ReviewStatus == "PENDING") throw new AppFault(409, "Gỡ thông báo hoặc rút yêu cầu duyệt trước khi chỉnh sửa.");
        return View(new AnnouncementEditViewModel { Id = id, Groups = [item.ResidentialGroup], Input = new AnnouncementInput {
            Title = item.Title, Body = item.Body, Audience = item.Audience, ResidentialGroupId = item.ResidentialGroupId,
            ExpiresAtLocal = item.ExpiresAt?.AddHours(7), RowVersion = Convert.ToBase64String(item.RowVersion) } });
    }
    [HttpPost("{id:long}/Edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, [Bind(Prefix = "Input")] AnnouncementInput input)
    {
        var item = await Find(id); if (item == null) return NotFound();
        await Check("announcements.update", item.ResidentialGroupId);
        if (item.Status == "PUBLISHED" || item.ReviewStatus == "PENDING") throw new AppFault(409, "Gỡ thông báo hoặc rút yêu cầu duyệt trước khi chỉnh sửa.");
        if (input.ResidentialGroupId != item.ResidentialGroupId) throw new AppFault(400, "Không thể đổi tổ dân phố của thông báo. Hãy soạn thông báo mới.");
        ValidateContent(input);
        if (!ModelState.IsValid) return View(new AnnouncementEditViewModel { Id = id, Input = input, Groups = [item.ResidentialGroup] });
        SetVersion(item, input.RowVersion ?? "");
        item.Title = input.Title.Trim(); item.Body = input.Body.Trim(); item.Audience = input.Audience; item.ExpiresAt = ToUtc(input.ExpiresAtLocal);
        item.ReviewStatus = "DRAFT"; item.SubmittedAt = null; item.ReviewedAt = null; item.ReviewedByUserId = null; item.ReviewNote = null;
        Audit(CurrentUserId(User), "ANNOUNCEMENT_UPDATED", "Announcement", id.ToString());
        try { await db.SaveChangesAsync(); TempData["AnnouncementMessage"] = "Đã lưu thay đổi. Nội dung cần được gửi duyệt lại trước khi đăng."; }
        catch (DbUpdateConcurrencyException) { TempData["AnnouncementError"] = "Thông báo đã thay đổi. Hãy tải lại và kiểm tra nội dung mới."; }
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:long}/Submit"), ValidateAntiForgeryToken]
    public Task<IActionResult> Submit(long id, string rowVersion) => Change(id, rowVersion, "announcements.update", "ANNOUNCEMENT_SUBMITTED", item => {
        if (item.Status == "PUBLISHED" || item.ReviewStatus is not ("DRAFT" or "REJECTED")) throw new AppFault(409, "Chỉ gửi duyệt thông báo nháp hoặc bị từ chối.");
        CheckExpiry(item); item.ReviewStatus = "PENDING"; item.SubmittedAt = DateTime.UtcNow; item.ReviewNote = null; item.ReviewedAt = null; item.ReviewedByUserId = null;
        return Task.CompletedTask;
    });
    [HttpPost("{id:long}/Withdraw"), ValidateAntiForgeryToken]
    public Task<IActionResult> Withdraw(long id, string rowVersion) => Change(id, rowVersion, "announcements.update", "ANNOUNCEMENT_WITHDRAWN", item => {
        if (item.Status == "PUBLISHED" || item.ReviewStatus != "PENDING") throw new AppFault(409, "Thông báo không ở trạng thái chờ duyệt.");
        item.ReviewStatus = "DRAFT"; item.SubmittedAt = null; return Task.CompletedTask;
    });
    [HttpPost("{id:long}/Review"), ValidateAntiForgeryToken]
    public Task<IActionResult> Review(long id, string rowVersion, bool approve, string? note) => Change(id, rowVersion, "announcements.approve", approve ? "ANNOUNCEMENT_APPROVED" : "ANNOUNCEMENT_REJECTED", item => {
        if (item.Status == "PUBLISHED" || item.ReviewStatus != "PENDING") throw new AppFault(409, "Chỉ được duyệt thông báo đang chờ duyệt.");
        if (note?.Length > 1000 || !approve && string.IsNullOrWhiteSpace(note)) throw new AppFault(400, "Nhập lý do từ chối; ghi chú tối đa 1.000 ký tự.");
        if (approve) CheckExpiry(item);
        item.ReviewStatus = approve ? "APPROVED" : "REJECTED"; item.ReviewedByUserId = CurrentUserId(User); item.ReviewedAt = DateTime.UtcNow; item.ReviewNote = note?.Trim();
        return Task.CompletedTask;
    });
    [HttpPost("{id:long}/Publish"), ValidateAntiForgeryToken]
    public Task<IActionResult> Publish(long id, string rowVersion) => Change(id, rowVersion, "announcements.publish", "ANNOUNCEMENT_PUBLISHED", async item => {
        if (item.Status == "PUBLISHED" || item.ReviewStatus != "APPROVED") throw new AppFault(409, "Chỉ được đăng nội dung đã duyệt và chưa được đăng.");
        CheckExpiry(item); var now = DateTime.UtcNow;
        item.Status = "PUBLISHED"; item.PublishedByUserId = CurrentUserId(User); item.PublishedAt = now; item.UnpublishReason = null;
        var recipients = await db.AspNetUsers.Where(u => u.IsActive && u.VerificationStatus == "APPROVED" && u.Resident != null && u.Resident.Household != null && !u.Resident.Household.IsArchived &&
            u.Resident.Household.ResidentialGroupId == item.ResidentialGroupId && db.VwEffectivePermissions.Any(p => p.UserId == u.Id && p.ScopeType == "SELF" && p.PermissionCode == "announcements.audience.read")).Select(u => u.Id).ToListAsync();
        db.Activities.AddRange(recipients.Select(recipient => new Models.Activity { ActivityType = "NOTIFICATION", RecipientUserId = recipient,
            EventCode = "ANNOUNCEMENT_PUBLISHED", TargetType = "Announcement", TargetId = item.Id.ToString(), OccurredAt = now }));
    });
    [HttpPost("{id:long}/Unpublish"), ValidateAntiForgeryToken]
    public Task<IActionResult> Unpublish(long id, string rowVersion, string? reason) => Change(id, rowVersion, "announcements.unpublish", "ANNOUNCEMENT_UNPUBLISHED", item => {
        if (item.Status != "PUBLISHED") throw new AppFault(409, "Chỉ được gỡ thông báo đang đăng.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new AppFault(400, "Nhập lý do gỡ thông báo, tối đa 1.000 ký tự.");
        item.Status = "UNPUBLISHED"; item.UnpublishReason = reason.Trim(); return Task.CompletedTask;
    });

    private async Task<IActionResult> Change(long id, string rowVersion, string permission, string eventCode, Func<Announcement, Task> apply)
    {
        try
        {
            if (!ModelState.IsValid) throw new AppFault(400, "Dữ liệu thao tác không hợp lệ. Hãy tải lại biểu mẫu.");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var item = await Find(id); if (item == null) return NotFound();
            await Check(permission, item.ResidentialGroupId); SetVersion(item, rowVersion ?? "");
            var previousStatus = item.Status; var previousReview = item.ReviewStatus;
            await apply(item);
            var note = eventCode == "ANNOUNCEMENT_UNPUBLISHED" ? item.UnpublishReason : eventCode is "ANNOUNCEMENT_APPROVED" or "ANNOUNCEMENT_REJECTED" ? item.ReviewNote : null;
            db.Activities.Add(new Models.Activity { ActivityType = "AUDIT", ActorUserId = CurrentUserId(User), EventCode = eventCode,
                TargetType = "Announcement", TargetId = id.ToString(), OccurredAt = DateTime.UtcNow,
                SafeMetadata = System.Text.Json.JsonSerializer.Serialize(new { previousStatus, previousReview, status = item.Status, reviewStatus = item.ReviewStatus,
                    notePreview = note == null ? null : note[..Math.Min(note.Length, 250)] }) });
            await db.SaveChangesAsync(); await tx.CommitAsync();
            TempData["AnnouncementMessage"] = AnnouncementLabels.Event(eventCode) + " thành công.";
        }
        catch (AppFault error) when (error.Status is 400 or 409) { TempData["AnnouncementError"] = error.Message; }
        catch (DbUpdateConcurrencyException) { TempData["AnnouncementError"] = "Thông báo đã thay đổi. Hãy tải lại trước khi thao tác."; }
        return RedirectToAction(nameof(Detail), new { id });
    }
    private void ValidateContent(AnnouncementInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Body)) ModelState.AddModelError("", "Nhập tiêu đề và nội dung thông báo.");
        if (input.ExpiresAtLocal != null && input.ExpiresAtLocal <= DateTime.UtcNow.AddHours(7)) ModelState.AddModelError("Input.ExpiresAtLocal", "Thời điểm hết hạn phải ở tương lai (giờ Việt Nam).");
    }
    private static void CheckExpiry(Announcement item)
    {
        if (item.ExpiresAt != null && item.ExpiresAt <= DateTime.UtcNow) throw new AppFault(409, "Thông báo đã hết hạn. Hãy chỉnh lại hạn trước khi gửi duyệt hoặc đăng.");
    }
    private static DateTime? ToUtc(DateTime? value) => value == null ? null : DateTime.SpecifyKind(DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified).AddHours(-7), DateTimeKind.Utc);
}
