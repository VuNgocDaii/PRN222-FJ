using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;
using RequestEntity = QuanLyCuDan.Models.Request;

namespace QuanLyCuDan.Services.Implementations;

public sealed class ResidentService(
    IPermissionChecker checker,
    IPermissionRepository permissions,
    IResidentRepository residents,
    IResidentialUnitRepository units,
    IAuditRepository audit,
    IAttachmentStorage storage,
    IUnitOfWork unitOfWork) : IResidentService
{
    private const int PageSize = 10;
    private static readonly Dictionary<string, string> Sections = new()
    {
        ["overview"] = "profile.self.read", ["profile"] = "profile.self.read", ["household"] = "households.self.read",
        ["feedback"] = "feedback.self.read",
        ["announcements"] = "announcements.audience.read", ["notifications"] = "notifications.self.read"
    };

    public async Task Check(ClaimsPrincipal actor, string permission)
    {
        if (!await permissions.HasAsync(checker.UserId(actor), permission, "SELF"))
            throw new AppFault(403, "Bạn chưa được cấp quyền sử dụng chức năng này.");
    }

    private static Resident Linked(AspNetUser account) => account.Resident ?? throw new AppFault(403, "Tài khoản chưa được liên kết với hồ sơ cư dân. Vui lòng liên hệ ban quản lý.");

    private static Household CurrentHousehold(AspNetUser account)
    {
        var h = Linked(account).Household;
        if (h == null || h.IsArchived || !h.ResidentialGroup.IsActive || h.ResidentialGroup.UnitType != "GROUP" ||
            h.ResidentialGroup.ParentUnit is not { IsActive: true, UnitType: "AREA" })
            throw new AppFault(403, "Bạn chưa có hộ hiện tại trong thôn đang hoạt động.");
        return h;
    }

    private static int ClampPage(int page, int total) => Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)PageSize)));

    public async Task<ResidentPortalViewModel> GetPortalAsync(ClaimsPrincipal actor, string section, int page = 1)
    {
        if (!Sections.ContainsKey(section))
            throw new AppFault(404, "Không tìm thấy chức năng cư dân.");
        await Check(actor, Sections[section]);
        var user = await residents.GetAccountAsync(checker.UserId(actor));
        var m = new ResidentPortalViewModel { Section = section, Account = user, Access = await permissions.GetSelfCodesAsync(user.Id) };
        var resident = Linked(user);
        if (section is "household" || (section == "requests" && resident.Household != null))
            m.Members = await residents.GetHouseholdMembersAsync(CurrentHousehold(user).Id);
        if (section is "requests" or "feedback")
        {
            var kind = section == "requests" ? "RESIDENCE" : "FEEDBACK";
            m.Total = await residents.CountRequestsAsync(user.Id, kind);
            m.Page = ClampPage(page, m.Total);
            m.Requests = await residents.ListRequestsAsync(user.Id, kind, m.Page, PageSize);
            m.Input.Kind = kind;
            m.Input.Type = section == "feedback" ? "OTHER" : "TEMPORARY_ABSENCE";
            m.Groups = await units.GetRegistrableGroupsAsync();
        }
        if (section == "fees")
        {
            await Check(actor, "payments.self.read");
            var h = CurrentHousehold(user);
            if (h.HeadResidentId == resident.Id)
                m.Payments = await residents.GetPaymentsAsync(h.Id);
        }
        if (section == "announcements")
        {
            var query = residents.VisibleAnnouncements(CurrentHousehold(user).ResidentialGroupId, user.ResidentId);
            m.Total = await query.CountAsync();
            m.Page = ClampPage(page, m.Total);
            m.Announcements = await query.OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id).Skip((m.Page - 1) * PageSize).Take(PageSize).ToListAsync();
        }
        if (section == "notifications")
        {
            m.Total = await residents.CountNotificationsAsync(user.Id);
            m.Page = ClampPage(page, m.Total);
            m.Notifications = await residents.ListNotificationsAsync(user.Id, m.Page, PageSize);
        }
        return m;
    }

    public async Task<Announcement?> GetAnnouncementAsync(ClaimsPrincipal actor, long id)
    {
        await Check(actor, "announcements.audience.read");
        var user = await residents.GetAccountAsync(checker.UserId(actor));
        return await residents.VisibleAnnouncements(CurrentHousehold(user).ResidentialGroupId, user.ResidentId)
            .Include(a => a.ResidentialGroup).SingleOrDefaultAsync(a => a.Id == id);
    }

    public async Task UpdateContactAsync(ClaimsPrincipal actor, ContactInput input)
    {
        await Check(actor, "profile.self.update_contact");
        var account = await residents.GetAccountAsync(checker.UserId(actor));
        var resident = Linked(account);
        unitOfWork.SetRowVersion(account, input.RowVersion);
        unitOfWork.SetRowVersion(resident, input.ResidentVersion);
        account.PhoneNumber = resident.PhoneNumber = input.PhoneNumber;
        account.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
        account.NormalizedEmail = account.Email?.ToUpperInvariant();
        account.EmailConfirmed = false;
        account.PhoneNumberConfirmed = false;
        audit.Add(account.Id, "CONTACT_UPDATED", "RESIDENT", resident.Id.ToString());
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<long> CreateRequestAsync(ClaimsPrincipal actor, ResidentRequestInput input)
    {
        var feedback = input.Kind == "FEEDBACK";
        if (!feedback)
            throw new AppFault(404, "Chức năng yêu cầu cư trú hiện không còn được sử dụng.");
        await Check(actor, feedback ? "feedback.self.create" : "residence_requests.self.create");
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var account = await residents.GetAccountAsync(checker.UserId(actor));
        var resident = Linked(account);
        var groupId = resident.Household != null ? CurrentHousehold(account).ResidentialGroupId : input.GroupId;
        if (groupId == null || !await units.IsRegistrableGroupAsync(groupId))
            throw new AppFault(400, "Chọn thôn đang hoạt động để tiếp nhận.");
        if (string.IsNullOrWhiteSpace(input.Subject) || string.IsNullOrWhiteSpace(input.Body))
            throw new AppFault(400, "Nhập tiêu đề và nội dung yêu cầu.");
        long? targetId = null;
        if (feedback)
        {
            if (input.Type is not ("ENVIRONMENT" or "INFRASTRUCTURE" or "OTHER"))
                throw new AppFault(400, "Lĩnh vực phản ánh không hợp lệ.");
            if (input.StartDate != null || input.EndDate != null || input.TargetHouseholdCode != null || input.NewHeadResidentId != null)
                throw new AppFault(400, "Phản ánh không dùng thông tin thay đổi cư trú.");
        }
        else
        {
            if (input.Type is not ("MOVE_IN" or "MOVE_OUT" or "TEMPORARY_RESIDENCE" or "TEMPORARY_ABSENCE" or "ADD_MEMBER" or "CHANGE_HEAD"))
                throw new AppFault(400, "Loại yêu cầu không hợp lệ.");
            if (input.StartDate == null || input.EndDate < input.StartDate)
                throw new AppFault(400, "Chọn ngày bắt đầu và ngày kết thúc không trước ngày bắt đầu.");
            if (input.Type != "CHANGE_HEAD" && input.NewHeadResidentId != null)
                throw new AppFault(400, "Loại yêu cầu này không thay đổi chủ hộ.");
            if (input.Type is "MOVE_IN" or "ADD_MEMBER")
            {
                targetId = await residents.FindTargetHouseholdIdAsync(input.TargetHouseholdCode, groupId.Value);
                if (targetId == null)
                    throw new AppFault(400, "Mã hộ tiếp nhận không hợp lệ trong tổ đã chọn.");
            }
            else
            {
                var h = CurrentHousehold(account);
                targetId = input.Type == "MOVE_OUT" ? null : h.Id;
                if (input.Type == "CHANGE_HEAD" && (h.HeadResidentId != resident.Id || input.NewHeadResidentId == resident.Id || !await residents.ResidentInHouseholdAsync(input.NewHeadResidentId, h.Id)))
                    throw new AppFault(400, "Chỉ chủ hộ hiện tại được đề nghị thay chủ hộ bằng một thành viên khác trong hộ.");
            }
        }
        var fingerprint = JsonSerializer.Serialize(new { input.Kind, input.Type, Subject = input.Subject.Trim(), Body = input.Body.Trim(), input.StartDate, input.EndDate, Group = groupId, input.TargetHouseholdCode, input.NewHeadResidentId });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
        var existing = await residents.FindByClientKeyAsync(account.Id, input.ClientKey);
        if (existing != null)
        {
            if (!existing.Activities.Any(a => a.ActivityType == "REQUEST_EVENT" && a.EventCode == "SUBMITTED" && a.SafeMetadata == "{\"payloadHash\":\"" + hash + "\"}"))
                throw new AppFault(409, "Mã gửi đã được dùng với nội dung khác. Hãy tải lại biểu mẫu.");
            return existing.Id;
        }
        if (!feedback && await residents.HasOpenResidenceRequestAsync(account.Id))
            throw new AppFault(409, "Bạn đang có yêu cầu cư trú chờ xử lý. Vui lòng chờ kết quả hoặc hủy yêu cầu đang chờ trước khi gửi mới.");
        var request = new RequestEntity
        {
            Code = "YC-" + Guid.NewGuid().ToString("N")[..24], RequestKind = input.Kind,
            Source = "RESIDENT", SubmittedByUserId = account.Id, ResidentId = resident.Id, AssignedGroupId = groupId.Value,
            RequestType = feedback ? "FEEDBACK" : input.Type, Category = feedback ? input.Type : null,
            Subject = input.Subject.Trim(), Body = input.Body.Trim(), StartDate = feedback ? null : input.StartDate,
            EndDate = feedback ? null : input.EndDate, OriginalHouseholdId = feedback ? null : resident.HouseholdId,
            TargetHouseholdId = targetId, NewHeadResidentId = feedback ? null : input.NewHeadResidentId,
            Status = "PENDING", ClientRequestKey = input.ClientKey, SubmittedAt = DateTime.UtcNow
        };
        request.Activities.Add(new Models.Activity
        {
            ActivityType = "REQUEST_EVENT", ActorUserId = account.Id, EventCode = "SUBMITTED", ToStatus = "PENDING",
            OccurredAt = DateTime.UtcNow, SafeMetadata = "{\"payloadHash\":\"" + hash + "\"}"
        });
        residents.Add(request);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return request.Id;
    }

    public async Task<ResidentRequestDetailViewModel?> GetDetailAsync(ClaimsPrincipal actor, long id)
    {
        var userId = checker.UserId(actor);
        var request = await residents.FindDetailAsync(userId, id);
        if (request == null)
            return null;
        await Check(actor, request.RequestKind == "FEEDBACK" ? "feedback.self.read" : "residence_requests.self.read");
        return new ResidentRequestDetailViewModel
        {
            Request = request,
            Timeline = await residents.GetTimelineAsync(id),
            CanCancel = request.RequestKind == "RESIDENCE" && request.Status == "PENDING" && await permissions.HasAsync(userId, "residence_requests.self.cancel", "SELF"),
            CanReply = request.RequestKind == "FEEDBACK" && request.Status is "PENDING" or "PROCESSING" or "RESPONDED" && await permissions.HasAsync(userId, "feedback.self.reply", "SELF"),
            CanUpload = request.Status == "PENDING" && request.AttachmentStorageKey == null && await permissions.HasAsync(userId, "attachments.self.upload", "SELF"),
            CanDownload = request.AttachmentStorageKey != null && await permissions.HasAsync(userId, "attachments.self.read", "SELF")
        };
    }

    public async Task CancelAsync(ClaimsPrincipal actor, long id, string rowVersion)
    {
        await Check(actor, "residence_requests.self.cancel");
        var userId = checker.UserId(actor);
        var request = await residents.FindMineAsync(userId, "RESIDENCE", id) ?? throw new AppFault(404, "Không tìm thấy yêu cầu.");
        if (request.Status != "PENDING")
            throw new AppFault(409, "Chỉ có thể hủy yêu cầu đang chờ tiếp nhận.");
        unitOfWork.SetRowVersion(request, rowVersion ?? "");
        request.Status = "CANCELLED";
        request.Activities.Add(new Models.Activity { ActivityType = "REQUEST_EVENT", ActorUserId = userId, EventCode = "RESIDENT_CANCELLED", FromStatus = "PENDING", ToStatus = "CANCELLED", OccurredAt = DateTime.UtcNow });
        await unitOfWork.SaveChangesAsync();
    }

    public async Task ReplyAsync(ClaimsPrincipal actor, long id, string body, string clientKey)
    {
        await Check(actor, "feedback.self.reply");
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var userId = checker.UserId(actor);
        var request = await residents.FindMineAsync(userId, "FEEDBACK", id) ?? throw new AppFault(404, "Không tìm thấy yêu cầu.");
        if (request.Status is not ("PENDING" or "PROCESSING" or "RESPONDED"))
            throw new AppFault(409, "Phản ánh đã kết thúc, không thể gửi thêm trao đổi.");
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 4000 || !Guid.TryParseExact(clientKey, "N", out _))
            throw new AppFault(400, "Nội dung trao đổi phải có 1–4.000 ký tự.");
        var existing = await residents.FindMessageByKeyAsync(userId, clientKey);
        if (existing != null)
        {
            if (existing.RequestId != id || existing.ActivityType != "MESSAGE" || existing.Body != body.Trim())
                throw new AppFault(409, "Mã gửi đã dùng với nội dung khác.");
            return;
        }
        residents.AddActivity(new Models.Activity { ActivityType = "MESSAGE", RequestId = id, ActorUserId = userId, EventCode = "RESIDENT_MESSAGE", Body = body.Trim(), ClientMessageKey = clientKey, OccurredAt = DateTime.UtcNow });
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task MarkReadAsync(ClaimsPrincipal actor, long id)
    {
        await Check(actor, "notifications.self.mark_read");
        var notification = await residents.FindNotificationAsync(id, checker.UserId(actor)) ?? throw new AppFault(404, "Không tìm thấy thông báo.");
        notification.ReadAt ??= DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync();
    }

    public async Task UploadAsync(ClaimsPrincipal actor, long id, Stream? content, long length, string fileName, string rowVersion)
    {
        await Check(actor, "attachments.self.upload");
        var request = await residents.FindOwnedAsync(checker.UserId(actor), id) ?? throw new AppFault(404, "Không tìm thấy yêu cầu.");
        if (request.Status != "PENDING" || request.AttachmentStorageKey != null)
            throw new AppFault(409, "Chỉ được bổ sung một giấy tờ khi hồ sơ đang chờ tiếp nhận.");
        if (content == null || length is < 1 or > 5242880)
            throw new AppFault(400, "Chọn một tệp PDF, JPEG hoặc PNG tối đa 5 MB.");
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory);
        var bytes = memory.ToArray();
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        string? mime = null;
        if (ext == ".pdf" && bytes.AsSpan().StartsWith("%PDF-"u8)) mime = "application/pdf";
        if (ext is ".jpg" or ".jpeg" && bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217) mime = "image/jpeg";
        if (ext == ".png" && bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) mime = "image/png";
        if (mime == null)
            throw new AppFault(400, "Định dạng hoặc nội dung tệp không hợp lệ. Chỉ nhận PDF, JPEG, PNG.");
        unitOfWork.SetRowVersion(request, rowVersion ?? "");
        var key = Guid.NewGuid().ToString("N");
        request.AttachmentStorageKey = key;
        request.AttachmentFileName = Path.GetFileName(fileName);
        if (request.AttachmentFileName.Length > 255)
            request.AttachmentFileName = "giay-to" + ext;
        request.AttachmentContentType = mime;
        request.AttachmentSizeBytes = bytes.Length;
        request.AttachmentHash = SHA256.HashData(bytes);
        request.Activities.Add(new Models.Activity { ActivityType = "REQUEST_EVENT", ActorUserId = checker.UserId(actor), EventCode = "ATTACHMENT_UPLOADED", OccurredAt = DateTime.UtcNow });
        try
        {
            await storage.SaveAsync(key, bytes);
            await unitOfWork.SaveChangesAsync();
        }
        catch
        {
            storage.Delete(key);
            throw;
        }
    }

    public async Task<DocumentDownload?> DownloadAsync(ClaimsPrincipal actor, long id)
    {
        await Check(actor, "attachments.self.read");
        var request = await residents.FindOwnedNoTrackingAsync(checker.UserId(actor), id);
        if (request?.AttachmentStorageKey == null || !Guid.TryParseExact(request.AttachmentStorageKey, "N", out _))
            return null;
        var bytes = await storage.ReadAsync(request.AttachmentStorageKey);
        if (bytes == null)
            return null;
        if (request.AttachmentHash == null || !CryptographicOperations.FixedTimeEquals(request.AttachmentHash, SHA256.HashData(bytes)))
            throw new AppFault(409, "Giấy tờ không khớp dữ liệu đã lưu. Vui lòng liên hệ ban quản lý.");
        return new DocumentDownload(bytes, request.AttachmentContentType ?? "application/octet-stream", request.AttachmentFileName ?? "giay-to");
    }
}
