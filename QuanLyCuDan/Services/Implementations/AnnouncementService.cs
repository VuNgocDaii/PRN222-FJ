using System.Data;
using System.Security.Claims;
using System.Text.Json;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class AnnouncementService(
    IAnnouncementAccessService access,
    IAnnouncementRepository repository,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    IPermissionChecker checker) : IAnnouncementService
{
    private const int PageSize = 12;

    public async Task<AnnouncementListViewModel> ListAsync(ClaimsPrincipal actor, string? search, string filter = "ALL", long? groupId = null, int page = 1)
    {
        var groups = await access.ChoicesAsync(actor, "announcements.read");
        if (groups.Count == 0)
            throw new AppFault(403, "Bạn chưa được cấp quyền xem thông báo trong địa bàn.");
        search = search?.Trim();
        if (search?.Length > 200)
            search = search[..200];
        if (filter is not ("PUBLISHED" or "UNPUBLISHED" or "DRAFT" or "PENDING" or "APPROVED" or "REJECTED"))
            filter = "ALL";
        var result = await repository.SearchAsync(await access.ScopeAsync(actor), search, filter, groupId, page, PageSize);
        return new AnnouncementListViewModel
        {
            Groups = groups,
            Search = search,
            Filter = filter,
            GroupId = groupId,
            Page = result.Page,
            Total = result.Total,
            Items = result.Items,
            CanCreate = (await access.ChoicesAsync(actor, "announcements.create")).Count > 0
        };
    }

    public async Task<AnnouncementEditViewModel> GetCreateModelAsync(ClaimsPrincipal actor, AnnouncementInput? input = null)
    {
        if (input != null)
            await access.RequireAsync(actor, "announcements.create", input.ResidentialGroupId);
        var groups = await access.ChoicesAsync(actor, "announcements.create");
        if (groups.Count == 0)
            throw new AppFault(403, "Bạn chưa được cấp quyền soạn thông báo.");
        return new AnnouncementEditViewModel
        {
            Groups = groups,
            Organizations = await access.OrganizationChoicesAsync(actor),
            Input = input ?? new AnnouncementInput { ResidentialGroupId = groups[0].Id }
        };
    }

    public async Task<long> CreateAsync(ClaimsPrincipal actor, AnnouncementInput input)
    {
        await access.RequireAsync(actor, "announcements.create", input.ResidentialGroupId);
        EnsureContent(input);
        await access.RequireAudienceAsync(actor, input);
        var userId = checker.UserId(actor);
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
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = ToUtc(input.ExpiresAtLocal)
        };
        await using var tx = await unitOfWork.BeginTransactionAsync();
        await repository.AddAsync(item);
        await unitOfWork.SaveChangesAsync();
        audit.Add(userId, "ANNOUNCEMENT_CREATED", "Announcement", item.Id.ToString());
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return item.Id;
    }

    public async Task<AnnouncementDetailViewModel?> GetDetailAsync(ClaimsPrincipal actor, long id)
    {
        var item = await access.FindReadableAsync(actor, id);
        if (item == null)
            return null;
        var update = await access.CanUpdateAsync(actor, item);
        var statistics = await access.CanSeeStatisticsAsync(actor, item);
        var canApprove = await access.CanApproveAsync(actor, item);
        return new AnnouncementDetailViewModel
        {
            Item = item,
            RecipientCount = statistics ? await repository.CountRecipientsAsync(id) : null,
            ReadCount = statistics ? await repository.CountReadAsync(id) : null,
            CanEdit = IsEditable(item) && update,
            CanSubmit = item.Status != "PUBLISHED" && item.ReviewStatus is "DRAFT" or "REJECTED" && update,
            PublishesOnSubmit = canApprove,
            CanReview = item.Status != "PUBLISHED" && item.ReviewStatus == "PENDING" && canApprove,
            CanUnpublish = item.Status == "PUBLISHED" && await access.CanUnpublishAsync(actor, item),
            CanWithdraw = item.Status != "PUBLISHED" && item.ReviewStatus == "PENDING" && update,
            History = await repository.GetHistoryAsync(id)
        };
    }

    public async Task<AnnouncementEditViewModel?> GetEditModelAsync(ClaimsPrincipal actor, long id, AnnouncementInput? input = null)
    {
        var item = await access.FindReadableAsync(actor, id);
        if (item == null)
            return null;
        await access.RequireUpdateAsync(actor, item);
        EnsureEditable(item, input);
        return new AnnouncementEditViewModel
        {
            Id = id,
            Groups = [item.ResidentialGroup],
            Organizations = await access.OrganizationChoicesAsync(actor),
            Input = input ?? new AnnouncementInput
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
        var item = await access.FindReadableAsync(actor, id) ?? throw new AppFault(404, "Không tìm thấy thông báo.");
        await access.RequireUpdateAsync(actor, item);
        EnsureEditable(item, input);
        EnsureContent(input);
        await access.RequireAudienceAsync(actor, input);
        unitOfWork.SetRowVersion(item, input.RowVersion ?? "");
        item.Kind = input.Kind;
        item.DueAt = ToUtc(input.DueAtLocal);
        item.Title = input.Title.Trim();
        item.Body = input.Body.Trim();
        item.Audience = input.Audience;
        item.OrganizationId = input.OrganizationId;
        item.ExpiresAt = ToUtc(input.ExpiresAtLocal);
        ResetReview(item);
        audit.Add(checker.UserId(actor), "ANNOUNCEMENT_UPDATED", "Announcement", id.ToString());
        await unitOfWork.SaveChangesAsync();
    }

    // Gửi duyệt. Nếu người gửi chính là người có quyền duyệt bài này (Trưởng thôn/Bí thư,
    // hoặc Trưởng tổ chức với bài của tổ chức mình) thì đăng luôn, không chờ ai duyệt.
    public Task<string> SubmitAsync(ClaimsPrincipal actor, long id, string rowVersion) => ChangeAsync(actor, id, rowVersion,
        item => access.RequireUpdateAsync(actor, item), async item =>
        {
            if (item.Status == "PUBLISHED" || item.ReviewStatus is not ("DRAFT" or "REJECTED"))
                throw new AppFault(409, "Chỉ gửi duyệt thông báo nháp hoặc bị từ chối.");
            CheckExpiry(item);
            EnsureDirective(item);
            var now = DateTime.UtcNow;
            item.SubmittedAt = now;
            item.ReviewNote = null;
            if (await access.CanApproveAsync(actor, item))
            {
                item.ReviewStatus = "APPROVED";
                item.ReviewedByUserId = checker.UserId(actor);
                item.ReviewedAt = now;
                await PublishApprovedAsync(actor, item, writeAudit: false);
                return "ANNOUNCEMENT_SELF_PUBLISHED";
            }
            item.ReviewStatus = "PENDING";
            item.ReviewedAt = null;
            item.ReviewedByUserId = null;
            return "ANNOUNCEMENT_SUBMITTED";
        });

    public Task<string> WithdrawAsync(ClaimsPrincipal actor, long id, string rowVersion) => ChangeAsync(actor, id, rowVersion,
        item => access.RequireUpdateAsync(actor, item), item =>
        {
            if (item.Status == "PUBLISHED" || item.ReviewStatus != "PENDING")
                throw new AppFault(409, "Thông báo không ở trạng thái chờ duyệt.");
            item.ReviewStatus = "DRAFT";
            item.SubmittedAt = null;
            return Task.FromResult("ANNOUNCEMENT_WITHDRAWN");
        });

    public Task<string> ReviewAsync(ClaimsPrincipal actor, long id, string rowVersion, bool approve, string? note) => ChangeAsync(actor, id, rowVersion,
        async item =>
        {
            if (!await access.CanApproveAsync(actor, item))
                throw new AppFault(403, "Bạn không có quyền duyệt thông báo này.");
        }, async item =>
        {
            if (item.Status == "PUBLISHED" || item.ReviewStatus != "PENDING")
                throw new AppFault(409, "Chỉ được duyệt thông báo đang chờ duyệt.");
            if (note?.Length > 1000 || !approve && string.IsNullOrWhiteSpace(note))
                throw new AppFault(400, "Nhập lý do từ chối; ghi chú tối đa 1.000 ký tự.");
            if (approve)
                CheckExpiry(item);
            item.ReviewStatus = approve ? "APPROVED" : "REJECTED";
            item.ReviewedByUserId = checker.UserId(actor);
            item.ReviewedAt = DateTime.UtcNow;
            item.ReviewNote = note?.Trim();
            if (approve)
                await PublishApprovedAsync(actor, item, writeAudit: true);
            return approve ? "ANNOUNCEMENT_APPROVED" : "ANNOUNCEMENT_REJECTED";
        });

    public Task<string> UnpublishAsync(ClaimsPrincipal actor, long id, string rowVersion, string? reason) => ChangeAsync(actor, id, rowVersion,
        async item =>
        {
            if (!await access.CanUnpublishAsync(actor, item))
                throw new AppFault(403, "Bạn không có quyền gỡ thông báo này.");
        }, item =>
        {
            if (item.Status != "PUBLISHED")
                throw new AppFault(409, "Chỉ được gỡ thông báo đang đăng.");
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
                throw new AppFault(400, "Nhập lý do gỡ thông báo, tối đa 1.000 ký tự.");
            item.Status = "UNPUBLISHED";
            item.UnpublishReason = reason.Trim();
            return Task.FromResult("ANNOUNCEMENT_UNPUBLISHED");
        });

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
        unitOfWork.SetRowVersion(item, rowVersion);
        item.DocumentFileName = Path.GetFileName(fileName);
        if (item.DocumentFileName.Length > 255)
            item.DocumentFileName = "van-ban.pdf";
        item.DocumentContent = bytes;
        ResetReview(item);
        audit.Add(checker.UserId(actor), "ANNOUNCEMENT_DOCUMENT_UPDATED", "Announcement", id.ToString());
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<DocumentDownload?> DownloadDocumentAsync(ClaimsPrincipal actor, long id)
    {
        var item = await access.FindReadableAsync(actor, id);
        if (item?.DocumentContent == null)
            return null;
        return new DocumentDownload(item.DocumentContent, "application/pdf", item.DocumentFileName ?? "van-ban.pdf");
    }

    public IReadOnlyDictionary<string, string> ValidateContent(AnnouncementInput input)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Body))
            errors[""] = "Nhập tiêu đề và nội dung thông báo.";
        if (input.ExpiresAtLocal != null && input.ExpiresAtLocal <= DateTime.UtcNow.AddHours(7))
            errors[nameof(input.ExpiresAtLocal)] = "Thời điểm hết hạn phải ở tương lai (giờ Việt Nam).";
        if (input.Kind == "DIRECTIVE" && (input.DueAtLocal == null || input.DueAtLocal <= DateTime.UtcNow.AddHours(7)))
            errors[nameof(input.DueAtLocal)] = "Chọn hạn thực hiện chỉ thị trong tương lai.";
        return errors;
    }

    // Khung chung cho mọi thao tác đổi trạng thái: transaction Serializable + kiểm tra RowVersion + ghi nhật ký.
    private async Task<string> ChangeAsync(ClaimsPrincipal actor, long id, string rowVersion, Func<Announcement, Task> authorize, Func<Announcement, Task<string>> apply)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await access.FindReadableAsync(actor, id) ?? throw new AppFault(404, "Không tìm thấy thông báo.");
        await authorize(item);
        unitOfWork.SetRowVersion(item, rowVersion ?? "");
        var previousStatus = item.Status;
        var previousReview = item.ReviewStatus;
        var eventCode = await apply(item);
        var note = eventCode == "ANNOUNCEMENT_UNPUBLISHED" ? item.UnpublishReason
            : eventCode is "ANNOUNCEMENT_APPROVED" or "ANNOUNCEMENT_REJECTED" ? item.ReviewNote : null;
        audit.Add(checker.UserId(actor), eventCode, "Announcement", id.ToString(), JsonSerializer.Serialize(new
        {
            previousStatus,
            previousReview,
            status = item.Status,
            reviewStatus = item.ReviewStatus,
            notePreview = note == null ? null : note[..Math.Min(note.Length, 250)]
        }));
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return eventCode;
    }

    private async Task PublishApprovedAsync(ClaimsPrincipal actor, Announcement item, bool writeAudit)
    {
        CheckExpiry(item);
        EnsureDirective(item);
        var now = DateTime.UtcNow;
        item.Status = "PUBLISHED";
        item.PublishedByUserId = checker.UserId(actor);
        item.PublishedAt = now;
        item.UnpublishReason = null;
        var recipients = await repository.GetRecipientUserIdsAsync(item, now);
        var existing = await repository.GetPublishNotificationsAsync(item.Id);
        foreach (var notification in existing.Where(a => recipients.Contains(a.RecipientUserId!)))
        {
            notification.ReadAt = null;
            notification.OccurredAt = now;
            notification.DispatchedAt = null;
            notification.AttemptCount = 0;
            notification.NextAttemptAt = null;
        }
        var notified = existing.Select(a => a.RecipientUserId).ToHashSet();
        repository.AddNotifications(recipients.Where(recipient => !notified.Contains(recipient)).Select(recipient => new Models.Activity
        {
            ActivityType = "NOTIFICATION",
            RecipientUserId = recipient,
            EventCode = "ANNOUNCEMENT_PUBLISHED",
            TargetType = "Announcement",
            TargetId = item.Id.ToString(),
            OccurredAt = now
        }));
        if (writeAudit)
            audit.Add(checker.UserId(actor), "ANNOUNCEMENT_PUBLISHED", "Announcement", item.Id.ToString());
    }

    private static void ResetReview(Announcement item)
    {
        item.ReviewStatus = "DRAFT";
        item.SubmittedAt = null;
        item.ReviewedAt = null;
        item.ReviewedByUserId = null;
        item.ReviewNote = null;
    }

    private static bool IsEditable(Announcement item) => item.Status != "PUBLISHED" && item.ReviewStatus != "PENDING";

    private static void EnsureEditable(Announcement item, AnnouncementInput? input)
    {
        if (!IsEditable(item))
            throw new AppFault(409, "Gỡ thông báo hoặc rút yêu cầu duyệt trước khi chỉnh sửa.");
        if (input != null && input.ResidentialGroupId != item.ResidentialGroupId)
            throw new AppFault(400, "Không thể đổi thôn của thông báo. Hãy soạn thông báo mới.");
    }

    private void EnsureContent(AnnouncementInput input)
    {
        var errors = ValidateContent(input);
        if (errors.Count > 0)
            throw new AppFault(400, errors.First().Value);
    }

    private static void EnsureDirective(Announcement item)
    {
        if (item.Kind == "DIRECTIVE" && (item.DocumentContent == null || item.DueAt == null || item.DueAt <= DateTime.UtcNow))
            throw new AppFault(400, "Chỉ thị cần văn bản PDF gốc và hạn thực hiện trong tương lai trước khi gửi duyệt.");
    }

    private static void CheckExpiry(Announcement item)
    {
        if (item.ExpiresAt != null && item.ExpiresAt <= DateTime.UtcNow)
            throw new AppFault(409, "Thông báo đã hết hạn. Hãy chỉnh lại hạn trước khi gửi duyệt hoặc đăng.");
    }

    private static DateTime? ToUtc(DateTime? value) =>
        value == null ? null : DateTime.SpecifyKind(DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified).AddHours(-7), DateTimeKind.Utc);
}
