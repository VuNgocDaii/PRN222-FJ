using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class AnnouncementService(MyStockContext context, AnnouncementAccessService access) : ApplicationService(context)
{
    public async Task<AnnouncementListViewModel> ListAsync(ClaimsPrincipal actor, string? search, string filter = "ALL", long? groupId = null, int page = 1)
    {
        var groups = await access.ChoicesAsync(actor, "announcements.read");
        if (groups.Count == 0)
            throw new AppFault(403, "Bạn chưa được cấp quyền xem thông báo trong địa bàn.");
        var ids = groups.Select(g => g.Id).ToArray();
        var query = (await access.ReadableAsync(actor)).AsNoTracking();
        search = search?.Trim();
        if (search?.Length > 200)
            search = search[..200];
        if (!string.IsNullOrEmpty(search))
            query = query.Where(a => a.Title.Contains(search) || a.Body.Contains(search));
        if (groupId != null)
            query = query.Where(a => a.ResidentialGroupId == groupId);
        if (filter is "PUBLISHED" or "UNPUBLISHED")
            query = query.Where(a => a.Status == filter);
        else if (filter is "DRAFT" or "PENDING" or "APPROVED" or "REJECTED")
            query = query.Where(a => a.Status != "PUBLISHED" && a.ReviewStatus == filter);
        else
            filter = "ALL";
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 12d)));
        return new AnnouncementListViewModel
        {
            Groups = groups,
            Search = search,
            Filter = filter,
            GroupId = groupId,
            Page = page,
            Total = total,
            Items = await query.Include(a => a.ResidentialGroup).OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Skip((page - 1) * 12).Take(12).ToListAsync(),
            CanCreate = await access.Groups(actor, "announcements.create").AnyAsync()
        };
    }
    public async Task<AnnouncementEditViewModel> GetCreateModelAsync(ClaimsPrincipal actor, AnnouncementInput? input = null)
    {
        if (input != null)
            await access.RequireAsync(actor, "announcements.create", input.ResidentialGroupId);
        var groups = await access.ChoicesAsync(actor, "announcements.create");
        if (groups.Count == 0)
            throw new AppFault(403, "Bạn chưa được cấp quyền soạn thông báo.");
        return new AnnouncementEditViewModel { Groups = groups, Organizations = await access.OrganizationChoicesAsync(actor), Input = input ?? new AnnouncementInput { ResidentialGroupId = groups[0].Id } };
    }
    public async Task<long> CreateAsync(ClaimsPrincipal actor, AnnouncementInput input)
    {
        await access.RequireAsync(actor, "announcements.create", input.ResidentialGroupId);
        EnsureContent(input);
        await access.RequireAudienceAsync(actor, input);
        var item = new Announcement
        {
            Kind = input.Kind,
            DueAt = ToUtc(input.DueAtLocal),
            Title = input.Title.Trim(),
            Body = input.Body.Trim(),
            Audience = input.Audience,
            OrganizationId = input.OrganizationId,
            ResidentialGroupId = input.ResidentialGroupId,
            Status = "DRAFT",
            ReviewStatus = "DRAFT",
            CreatedByUserId = CurrentUserId(actor),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = ToUtc(input.ExpiresAtLocal)
        };
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Announcements.Add(item);
        await db.SaveChangesAsync();
        Audit(CurrentUserId(actor), "ANNOUNCEMENT_CREATED", "Announcement", item.Id.ToString());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return item.Id;
    }
    public async Task<AnnouncementDetailViewModel?> GetDetailAsync(ClaimsPrincipal actor, long id)
    {
        var item = await access.FindReadableAsync(actor, id);
        if (item == null)
            return null;
        var editable = IsEditable(item);
        var update = await access.CanUpdateAsync(actor, item);
        var statistics = await access.CanSeeStatisticsAsync(actor, item);
        return new AnnouncementDetailViewModel
        {
            Item = item,
            RecipientCount = statistics ? await db.Activities.CountAsync(a => a.ActivityType == "NOTIFICATION" && a.EventCode == "ANNOUNCEMENT_PUBLISHED" && a.TargetType == "Announcement" && a.TargetId == id.ToString()) : null,
            ReadCount = statistics ? await db.Activities.CountAsync(a => a.ActivityType == "NOTIFICATION" && a.EventCode == "ANNOUNCEMENT_PUBLISHED" && a.TargetType == "Announcement" && a.TargetId == id.ToString() && a.ReadAt != null) : null,
            CanEdit = editable && update,
            CanSubmit = item.Status != "PUBLISHED" && item.ReviewStatus is "DRAFT" or "REJECTED" && update,
            CanReview = item.Status != "PUBLISHED" && item.ReviewStatus == "PENDING" && await access.Groups(actor, "announcements.approve").AnyAsync(g => g.Id == item.ResidentialGroupId),
            CanUnpublish = item.Status == "PUBLISHED" && await access.Groups(actor, "announcements.unpublish").AnyAsync(g => g.Id == item.ResidentialGroupId),
            CanWithdraw = item.Status != "PUBLISHED" && item.ReviewStatus == "PENDING" && update,
            History = await db.Activities.AsNoTracking().Where(a => a.ActivityType == "AUDIT" && a.TargetType == "Announcement" && a.TargetId == id.ToString())
                .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(50)
                .Select(a => new AnnouncementHistoryItem(a.EventCode, a.ActorUser == null ? null : a.ActorUser.FullName, a.OccurredAt)).ToListAsync()
        };
    }
    public async Task<AnnouncementEditViewModel?> GetEditModelAsync(ClaimsPrincipal actor, long id, AnnouncementInput? input = null)
    {
        var item = await access.FindReadableAsync(actor, id);
        if (item == null)
            return null;
        await access.RequireUpdateAsync(actor, item);
        EnsureEditable(item, input);
        if (input != null)
            return new AnnouncementEditViewModel { Id = id, Groups = [item.ResidentialGroup], Organizations = await access.OrganizationChoicesAsync(actor), Input = input };
        return new AnnouncementEditViewModel
        {
            Id = id,
            Groups = [item.ResidentialGroup],
            Organizations = await access.OrganizationChoicesAsync(actor),
            Input = new AnnouncementInput
            {
                Kind = item.Kind,
                DueAtLocal = item.DueAt?.AddHours(7),
                Title = item.Title,
                Body = item.Body,
                Audience = item.Audience,
                OrganizationId = item.OrganizationId,
                ResidentialGroupId = item.ResidentialGroupId,
                ExpiresAtLocal = item.ExpiresAt?.AddHours(7),
                RowVersion = Convert.ToBase64String(item.RowVersion)
            }
        };
    }
    public async Task UpdateAsync(ClaimsPrincipal actor, long id, AnnouncementInput input)
    {
        var item = await access.FindReadableAsync(actor, id);
        if (item == null)
            throw new AppFault(404, "Không tìm thấy thông báo.");
        await access.RequireUpdateAsync(actor, item);
        EnsureEditable(item, input);
        EnsureContent(input);
        await access.RequireAudienceAsync(actor, input);
        SetVersion(item, input.RowVersion ?? "");
        item.Kind = input.Kind;
        item.DueAt = ToUtc(input.DueAtLocal);
        item.Title = input.Title.Trim();
        item.Body = input.Body.Trim();
        item.Audience = input.Audience;
        item.OrganizationId = input.OrganizationId;
        item.ExpiresAt = ToUtc(input.ExpiresAtLocal);
        item.ReviewStatus = "DRAFT";
        item.SubmittedAt = null;
        item.ReviewedAt = null;
        item.ReviewedByUserId = null;
        item.ReviewNote = null;
        Audit(CurrentUserId(actor), "ANNOUNCEMENT_UPDATED", "Announcement", id.ToString());
        await db.SaveChangesAsync();
    }
    public Task<string> SubmitAsync(ClaimsPrincipal actor, long id, string rowVersion) => ChangeAsync(actor, id, rowVersion, "announcements.update", "ANNOUNCEMENT_SUBMITTED", item =>
    {
        if (item.Status == "PUBLISHED" || item.ReviewStatus is not ("DRAFT" or "REJECTED"))
            throw new AppFault(409, "Chỉ gửi duyệt thông báo nháp hoặc bị từ chối.");
        CheckExpiry(item);
        EnsureDirective(item);
        item.ReviewStatus = "PENDING";
        item.SubmittedAt = DateTime.UtcNow;
        item.ReviewNote = null;
        item.ReviewedAt = null;
        item.ReviewedByUserId = null;
        return Task.CompletedTask;
    });
    public Task<string> WithdrawAsync(ClaimsPrincipal actor, long id, string rowVersion) => ChangeAsync(actor, id, rowVersion, "announcements.update", "ANNOUNCEMENT_WITHDRAWN", item =>
    {
        if (item.Status == "PUBLISHED" || item.ReviewStatus != "PENDING")
            throw new AppFault(409, "Thông báo không ở trạng thái chờ duyệt.");
        item.ReviewStatus = "DRAFT";
        item.SubmittedAt = null;
        return Task.CompletedTask;
    });
    public Task<string> ReviewAsync(ClaimsPrincipal actor, long id, string rowVersion, bool approve, string? note) => ChangeAsync(actor, id, rowVersion, "announcements.approve", approve ? "ANNOUNCEMENT_APPROVED" : "ANNOUNCEMENT_REJECTED", async item =>
    {
        if (item.Status == "PUBLISHED" || item.ReviewStatus != "PENDING")
            throw new AppFault(409, "Chỉ được duyệt thông báo đang chờ duyệt.");
        if (note?.Length > 1000 || !approve && string.IsNullOrWhiteSpace(note))
            throw new AppFault(400, "Nhập lý do từ chối; ghi chú tối đa 1.000 ký tự.");
        if (approve)
            CheckExpiry(item);
        item.ReviewStatus = approve ? "APPROVED" : "REJECTED";
        item.ReviewedByUserId = CurrentUserId(actor);
        item.ReviewedAt = DateTime.UtcNow;
        item.ReviewNote = note?.Trim();
        if (approve)
            await PublishApprovedAsync(actor, item);
    });
    private async Task PublishApprovedAsync(ClaimsPrincipal actor, Announcement item)
    {
        CheckExpiry(item);
        EnsureDirective(item);
        var now = DateTime.UtcNow;
        item.Status = "PUBLISHED";
        item.PublishedByUserId = CurrentUserId(actor);
        item.PublishedAt = now;
        item.UnpublishReason = null;
        var recipients = await db.AspNetUsers.Where(u => u.IsActive && u.VerificationStatus == "APPROVED" && u.Resident != null && u.Resident.Household != null && !u.Resident.Household.IsArchived &&
            u.Resident.Household.ResidentialGroupId == item.ResidentialGroupId &&
            (item.OrganizationId == null || db.OrganizationMembers.Any(m => m.OrganizationId == item.OrganizationId && m.ResidentId == u.ResidentId && m.LeftAt == null && m.JoinedAt <= now && m.Organization.IsActive)) &&
            db.VwEffectivePermissions.Any(p => p.UserId == u.Id && p.ScopeType == "SELF" && p.PermissionCode == "announcements.audience.read")).Select(u => u.Id).ToListAsync();
        var existing = await db.Activities.Where(a => a.ActivityType == "NOTIFICATION" && a.EventCode == "ANNOUNCEMENT_PUBLISHED" && a.TargetType == "Announcement" && a.TargetId == item.Id.ToString()).ToListAsync();
        foreach (var notification in existing.Where(a => recipients.Contains(a.RecipientUserId!)))
        {
            notification.ReadAt = null;
            notification.OccurredAt = now;
            notification.DispatchedAt = null;
            notification.AttemptCount = 0;
            notification.NextAttemptAt = null;
        }
        var notified = existing.Select(a => a.RecipientUserId).ToHashSet();
        db.Activities.AddRange(recipients.Where(recipient => !notified.Contains(recipient)).Select(recipient => new Models.Activity
        {
            ActivityType = "NOTIFICATION",
            RecipientUserId = recipient,
            EventCode = "ANNOUNCEMENT_PUBLISHED",
            TargetType = "Announcement",
            TargetId = item.Id.ToString(),
            OccurredAt = now
        }));
        Audit(CurrentUserId(actor), "ANNOUNCEMENT_PUBLISHED", "Announcement", item.Id.ToString());
    }
    public Task<string> UnpublishAsync(ClaimsPrincipal actor, long id, string rowVersion, string? reason) => ChangeAsync(actor, id, rowVersion, "announcements.unpublish", "ANNOUNCEMENT_UNPUBLISHED", item =>
    {
        if (item.Status != "PUBLISHED")
            throw new AppFault(409, "Chỉ được gỡ thông báo đang đăng.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new AppFault(400, "Nhập lý do gỡ thông báo, tối đa 1.000 ký tự.");
        item.Status = "UNPUBLISHED";
        item.UnpublishReason = reason.Trim();
        return Task.CompletedTask;
    });

    private async Task<string> ChangeAsync(ClaimsPrincipal actor, long id, string rowVersion, string permission, string eventCode, Func<Announcement, Task> apply)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await access.FindReadableAsync(actor, id);
        if (item == null)
            throw new AppFault(404, "Không tìm thấy thông báo.");
        await access.RequireAsync(actor, permission, item.ResidentialGroupId);
        if (permission == "announcements.update")
            await access.RequireUpdateAsync(actor, item);
        SetVersion(item, rowVersion ?? "");
        var previousStatus = item.Status;
        var previousReview = item.ReviewStatus;
        await apply(item);
        var note = eventCode == "ANNOUNCEMENT_UNPUBLISHED" ? item.UnpublishReason : eventCode is "ANNOUNCEMENT_APPROVED" or "ANNOUNCEMENT_REJECTED" ? item.ReviewNote : null;
        db.Activities.Add(new Models.Activity
        {
            ActivityType = "AUDIT",
            ActorUserId = CurrentUserId(actor),
            EventCode = eventCode,
            TargetType = "Announcement",
            TargetId = id.ToString(),
            OccurredAt = DateTime.UtcNow,
            SafeMetadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                previousStatus,
                previousReview,
                status = item.Status,
                reviewStatus = item.ReviewStatus,
                notePreview = note == null ? null : note[..Math.Min(note.Length, 250)]
            })
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return eventCode;
    }
    private static void EnsureDirective(Announcement item)
    {
        if (item.Kind == "DIRECTIVE" && (item.DocumentContent == null || item.DueAt == null || item.DueAt <= DateTime.UtcNow))
            throw new AppFault(400, "Chỉ thị cần văn bản PDF gốc và hạn thực hiện trong tương lai trước khi gửi duyệt.");
    }

    public async Task UploadDocumentAsync(ClaimsPrincipal actor, long id, Stream? content, long length, string fileName, string rowVersion)
    {
        var item = await access.FindReadableAsync(actor, id) ?? throw new AppFault(404, "Không tìm thấy thông báo.");
        await access.RequireUpdateAsync(actor, item);
        EnsureEditable(item, null);
        if (content == null || length is < 5 or > 5242880 || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new AppFault(400, "Chọn văn bản PDF tối đa 5 MB.");
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory);
        var bytes = memory.ToArray();
        if (bytes.Length is < 5 or > 5242880 || !bytes.AsSpan().StartsWith("%PDF-"u8))
            throw new AppFault(400, "Nội dung tệp PDF không hợp lệ.");
        SetVersion(item, rowVersion);
        item.DocumentFileName = Path.GetFileName(fileName);
        if (item.DocumentFileName.Length > 255) item.DocumentFileName = "van-ban.pdf";
        item.DocumentContent = bytes;
        item.ReviewStatus = "DRAFT";
        item.SubmittedAt = null;
        item.ReviewedAt = null;
        item.ReviewedByUserId = null;
        item.ReviewNote = null;
        Audit(CurrentUserId(actor), "ANNOUNCEMENT_DOCUMENT_UPDATED", "Announcement", id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task<DocumentDownload?> DownloadDocumentAsync(ClaimsPrincipal actor, long id)
    {
        var item = await access.FindReadableAsync(actor, id);
        if (item?.DocumentContent == null) return null;
        return new DocumentDownload(item.DocumentContent, "application/pdf", item.DocumentFileName ?? "van-ban.pdf");
    }

    private static bool IsEditable(Announcement item) =>
        item.Status != "PUBLISHED" && item.ReviewStatus != "PENDING";

    private static void EnsureEditable(Announcement item, AnnouncementInput? input)
    {
        if (!IsEditable(item))
            throw new AppFault(409, "Gỡ thông báo hoặc rút yêu cầu duyệt trước khi chỉnh sửa.");
        if (input != null && input.ResidentialGroupId != item.ResidentialGroupId)
            throw new AppFault(400, "Không thể đổi tổ dân phố của thông báo. Hãy soạn thông báo mới.");
    }

    public static IReadOnlyDictionary<string, string> ValidateContent(AnnouncementInput input)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Body))
            errors[""] = "Nhập tiêu đề và nội dung thông báo.";
        if (input.ExpiresAtLocal != null && input.ExpiresAtLocal <= DateTime.UtcNow.AddHours(7))
            errors[nameof(input.ExpiresAtLocal)] = "Thời điểm hết hạn phải ở tương lai (giờ Việt Nam).";
        if (input.Kind == "DIRECTIVE" && (input.DueAtLocal == null || input.DueAtLocal <= DateTime.UtcNow.AddHours(7))) errors[nameof(input.DueAtLocal)] = "Chọn hạn thực hiện chỉ thị trong tương lai.";
        return errors;
    }
    private static void EnsureContent(AnnouncementInput input)
    {
        var errors = ValidateContent(input);
        if (errors.Count > 0)
            throw new AppFault(400, errors.First().Value);
    }
    private static void CheckExpiry(Announcement item)
    {
        if (item.ExpiresAt != null && item.ExpiresAt <= DateTime.UtcNow)
            throw new AppFault(409, "Thông báo đã hết hạn. Hãy chỉnh lại hạn trước khi gửi duyệt hoặc đăng.");
    }
    private static DateTime? ToUtc(DateTime? value) => value == null ? null : DateTime.SpecifyKind(DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified).AddHours(-7), DateTimeKind.Utc);
}
