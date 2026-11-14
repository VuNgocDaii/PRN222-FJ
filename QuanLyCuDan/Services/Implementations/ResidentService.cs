using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using RequestEntity = QuanLyCuDan.Models.Request;
namespace QuanLyCuDan.Services;

public sealed class ResidentService(MyStockContext context, IWebHostEnvironment environment) : ApplicationService(context)
{
    private static readonly Dictionary<string, string> Sections = new() {
        ["overview"] = "profile.self.read", ["profile"] = "profile.self.read", ["household"] = "households.self.read",
        ["requests"] = "residence_requests.self.read", ["feedback"] = "feedback.self.read",
        ["fees"] = "fees.self.read", ["announcements"] = "announcements.audience.read", ["notifications"] = "notifications.self.read"
    };
    public async Task Check(ClaimsPrincipal actor, string permission)
    {
        if (!await Effective(CurrentUserId(actor)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == permission))
            throw new AppFault(403, "Bạn chưa được cấp quyền sử dụng chức năng này.");
    }
    private async Task<AspNetUser> Account(ClaimsPrincipal actor) => await db.AspNetUsers.Include(u => u.Resident)!
        .ThenInclude(r => r!.Household)!.ThenInclude(h => h!.ResidentialGroup).ThenInclude(g => g.ParentUnit)
        .SingleAsync(u => u.Id == CurrentUserId(actor));
    private static Resident Linked(AspNetUser account) => account.Resident ?? throw new AppFault(403, "Tài khoản chưa được liên kết với hồ sơ cư dân. Vui lòng liên hệ ban quản lý.");
    private static Household CurrentHousehold(AspNetUser account)
    {
        var h = Linked(account).Household;
        if (h == null || h.IsArchived || !h.ResidentialGroup.IsActive || h.ResidentialGroup.UnitType != "GROUP" ||
            h.ResidentialGroup.ParentUnit is not { IsActive: true, UnitType: "AREA" })
            throw new AppFault(403, "Bạn chưa có hộ hiện tại trong tổ dân phố đang hoạt động.");
        return h;
    }
    private IQueryable<RequestEntity> Mine(ClaimsPrincipal actor, string kind) => db.Requests.Where(r => r.SubmittedByUserId == CurrentUserId(actor) && r.Source == "RESIDENT" && r.RequestKind == kind);

    public async Task<ResidentPortalViewModel> GetPortalAsync(ClaimsPrincipal actor, string section, int page = 1)
    {
        if (!Sections.ContainsKey(section)) throw new AppFault(404, "Không tìm thấy chức năng cư dân.");
        await Check(actor, Sections[section]);
        var user = await Account(actor);
        var m = new ResidentPortalViewModel { Section = section, Account = user,
            Access = (await Effective(user.Id).Where(p => p.ScopeType == "SELF").Select(p => p.PermissionCode).ToListAsync()).ToHashSet() };
        var resident = Linked(user);
        if (section is "household" || (section == "requests" && resident.Household != null))
        {
            var h = CurrentHousehold(user);
            m.Members = await db.Residents.AsNoTracking().Where(r => r.HouseholdId == h.Id).OrderBy(r => r.Id).ToListAsync();
        }
        if (section is "requests" or "feedback")
        {
            var query = Mine(actor, section == "requests" ? "RESIDENCE" : "FEEDBACK").AsNoTracking();
            m.Total = await query.CountAsync(); m.Page = ClampPage(page, m.Total);
            m.Requests = await query.OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id).Skip((m.Page - 1) * 10).Take(10).ToListAsync();
            m.Input.Kind = section == "feedback" ? "FEEDBACK" : "RESIDENCE";
            m.Input.Type = section == "feedback" ? "OTHER" : "TEMPORARY_ABSENCE";
            m.Groups = await db.ResidentialUnits.AsNoTracking().Where(g => g.IsActive && g.UnitType == "GROUP" && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA").OrderBy(g => g.Name).ToListAsync();
        }
        if (section == "fees")
        {
            await Check(actor, "payments.self.read");
            var h = CurrentHousehold(user);
            if (h.HeadResidentId == resident.Id)
                m.Payments = await db.Payments.AsNoTracking().Include(p => p.Campaign).Where(p => p.HouseholdId == h.Id).OrderByDescending(p => p.CreatedAt).ToListAsync();
        }
        if (section == "announcements")
        {
            var h = CurrentHousehold(user);
            var query = VisibleAnnouncements(user);
            m.Total = await query.CountAsync(); m.Page = ClampPage(page, m.Total);
            m.Announcements = await query.OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id).Skip((m.Page - 1) * 10).Take(10).ToListAsync();
        }
        if (section == "notifications")
        {
            var query = db.Activities.AsNoTracking().Where(a => a.ActivityType == "NOTIFICATION" && a.RecipientUserId == user.Id);
            m.Total = await query.CountAsync(); m.Page = ClampPage(page, m.Total);
            m.Notifications = await query.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((m.Page - 1) * 10).Take(10).ToListAsync();
        }
        return m;
    }
    private static int ClampPage(int page, int total) => Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 10d)));

    private IQueryable<Announcement> VisibleAnnouncements(AspNetUser user)
    {
        var groupId = CurrentHousehold(user).ResidentialGroupId;
        return db.Announcements.AsNoTracking().Where(a => a.ResidentialGroupId == groupId &&
            a.Status == "PUBLISHED" && a.ReviewStatus == "APPROVED" && a.PublishedAt <= DateTime.UtcNow &&
            (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow) &&
            (a.Audience == "PUBLIC" || a.Audience == "RESIDENTS" || a.Audience == "ORGANIZATION" &&
             db.OrganizationMembers.Any(m => m.OrganizationId == a.OrganizationId && m.ResidentId == user.ResidentId &&
                m.LeftAt == null && m.JoinedAt <= DateTime.UtcNow && m.Organization.IsActive)));
    }

    public async Task<Announcement?> GetAnnouncementAsync(ClaimsPrincipal actor, long id)
    {
        await Check(actor, "announcements.audience.read");
        var user = await Account(actor);
        var groupId = CurrentHousehold(user).ResidentialGroupId;
        var item = await VisibleAnnouncements(user).Include(a => a.ResidentialGroup).SingleOrDefaultAsync(a => a.Id == id);
        return item;
    }
    public async Task UpdateContactAsync(ClaimsPrincipal actor, ContactInput input)
    {
        await Check(actor, "profile.self.update_contact");
        var account = await Account(actor); var resident = Linked(account);
        SetVersion(account, input.RowVersion); SetVersion(resident, input.ResidentVersion);
        account.PhoneNumber = resident.PhoneNumber = input.PhoneNumber;
        account.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
        account.NormalizedEmail = account.Email?.ToUpperInvariant();
        account.EmailConfirmed = false; account.PhoneNumberConfirmed = false;
        Audit(account.Id, "CONTACT_UPDATED", "RESIDENT", resident.Id.ToString());
        await db.SaveChangesAsync();
    }
    public async Task<long> CreateRequestAsync(ClaimsPrincipal actor, ResidentRequestInput input)
    {
        var feedback = input.Kind == "FEEDBACK";
        await Check(actor, feedback ? "feedback.self.create" : "residence_requests.self.create");
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var account = await Account(actor); var resident = Linked(account);
                var groupId = resident.Household != null ? CurrentHousehold(account).ResidentialGroupId : input.GroupId;
                if (groupId == null || !await db.ResidentialUnits.AnyAsync(g => g.Id == groupId && g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null && g.ParentUnit.IsActive && g.ParentUnit.UnitType == "AREA"))
                    throw new AppFault(400, "Chọn tổ dân phố đang hoạt động để tiếp nhận.");
                if (string.IsNullOrWhiteSpace(input.Subject) || string.IsNullOrWhiteSpace(input.Body))
                    throw new AppFault(400, "Nhập tiêu đề và nội dung yêu cầu.");
                long? targetId = null;
                if (feedback)
                {
                    if (input.Type is not ("ENVIRONMENT" or "INFRASTRUCTURE" or "OTHER")) throw new AppFault(400, "Lĩnh vực phản ánh không hợp lệ.");
                    if (input.StartDate != null || input.EndDate != null || input.TargetHouseholdCode != null || input.NewHeadResidentId != null)
                        throw new AppFault(400, "Phản ánh không dùng thông tin thay đổi cư trú.");
                }
                else
                {
                    if (input.Type is not ("MOVE_IN" or "MOVE_OUT" or "TEMPORARY_RESIDENCE" or "TEMPORARY_ABSENCE" or "ADD_MEMBER" or "CHANGE_HEAD"))
                        throw new AppFault(400, "Loại yêu cầu không hợp lệ.");
                    if (input.StartDate == null || input.EndDate < input.StartDate) throw new AppFault(400, "Chọn ngày bắt đầu và ngày kết thúc không trước ngày bắt đầu.");
                    if (input.Type != "CHANGE_HEAD" && input.NewHeadResidentId != null) throw new AppFault(400, "Loại yêu cầu này không thay đổi chủ hộ.");
                    if (input.Type is "MOVE_IN" or "ADD_MEMBER")
                    {
                        targetId = await db.Households.Where(h => h.Code == input.TargetHouseholdCode && !h.IsArchived && h.ResidentialGroupId == groupId).Select(h => (long?)h.Id).SingleOrDefaultAsync();
                        if (targetId == null) throw new AppFault(400, "Mã hộ tiếp nhận không hợp lệ trong tổ đã chọn.");
                    }
                    else
                    {
                        var h = CurrentHousehold(account);
                        targetId = input.Type == "MOVE_OUT" ? null : h.Id;
                        if (input.Type == "CHANGE_HEAD" && (h.HeadResidentId != resident.Id || input.NewHeadResidentId == resident.Id || !await db.Residents.AnyAsync(r => r.Id == input.NewHeadResidentId && r.HouseholdId == h.Id)))
                            throw new AppFault(400, "Chỉ chủ hộ hiện tại được đề nghị thay chủ hộ bằng một thành viên khác trong hộ.");
                    }
                }
                var fingerprint = System.Text.Json.JsonSerializer.Serialize(new { input.Kind, input.Type, Subject = input.Subject.Trim(), Body = input.Body.Trim(), input.StartDate, input.EndDate, Group = groupId, input.TargetHouseholdCode, input.NewHeadResidentId });
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fingerprint)));
                var existing = await db.Requests.Include(r => r.Activities).SingleOrDefaultAsync(r => r.SubmittedByUserId == account.Id && r.ClientRequestKey == input.ClientKey);
                if (existing != null)
                {
                    if (!existing.Activities.Any(a => a.ActivityType == "REQUEST_EVENT" && a.EventCode == "SUBMITTED" && a.SafeMetadata == "{\"payloadHash\":\"" + hash + "\"}"))
                        throw new AppFault(409, "Mã gửi đã được dùng với nội dung khác. Hãy tải lại biểu mẫu.");
                    return existing.Id;
                }
                if (!feedback && await Mine(actor, "RESIDENCE").AnyAsync(r => r.Status == "PENDING" || r.Status == "PROCESSING"))
                    throw new AppFault(409, "Bạn đang có yêu cầu cư trú chờ xử lý. Vui lòng chờ kết quả hoặc hủy yêu cầu đang chờ trước khi gửi mới.");
                var request = new RequestEntity { Code = "YC-" + Guid.NewGuid().ToString("N")[..24], RequestKind = input.Kind,
                    Source = "RESIDENT", SubmittedByUserId = account.Id, ResidentId = resident.Id, AssignedGroupId = groupId.Value,
                    RequestType = feedback ? "FEEDBACK" : input.Type, Category = feedback ? input.Type : null,
                    Subject = input.Subject.Trim(), Body = input.Body.Trim(), StartDate = feedback ? null : input.StartDate,
                    EndDate = feedback ? null : input.EndDate, OriginalHouseholdId = feedback ? null : resident.HouseholdId,
                    TargetHouseholdId = targetId, NewHeadResidentId = feedback ? null : input.NewHeadResidentId,
                    Status = "PENDING", ClientRequestKey = input.ClientKey, SubmittedAt = DateTime.UtcNow };
                request.Activities.Add(new Models.Activity { ActivityType = "REQUEST_EVENT", ActorUserId = account.Id,
                    EventCode = "SUBMITTED", ToStatus = "PENDING", OccurredAt = DateTime.UtcNow, SafeMetadata = "{\"payloadHash\":\"" + hash + "\"}" });
                db.Requests.Add(request); await db.SaveChangesAsync(); await transaction.CommitAsync();
                return request.Id;
    }
    public async Task<ResidentRequestDetailViewModel?> GetDetailAsync(ClaimsPrincipal actor, long id)
    {
        var request = await db.Requests.AsNoTracking().Include(r => r.AssignedGroup).SingleOrDefaultAsync(r => r.Id == id && r.SubmittedByUserId == CurrentUserId(actor) && r.Source == "RESIDENT");
        if (request == null) return null;
        await Check(actor, request.RequestKind == "FEEDBACK" ? "feedback.self.read" : "residence_requests.self.read");
        return new ResidentRequestDetailViewModel {
            Request = request,
            Timeline = await db.Activities.AsNoTracking().Where(a => a.RequestId == id && (a.ActivityType == "MESSAGE" || a.ActivityType == "REQUEST_EVENT")).OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).ToListAsync(),
            CanCancel = request.RequestKind == "RESIDENCE" && request.Status == "PENDING" && await Effective(CurrentUserId(actor)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == "residence_requests.self.cancel"),
            CanReply = request.RequestKind == "FEEDBACK" && request.Status is "PENDING" or "PROCESSING" or "RESPONDED" && await Effective(CurrentUserId(actor)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == "feedback.self.reply"),
            CanUpload = request.Status == "PENDING" && request.AttachmentStorageKey == null && await Effective(CurrentUserId(actor)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == "attachments.self.upload"),
            CanDownload = request.AttachmentStorageKey != null && await Effective(CurrentUserId(actor)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == "attachments.self.read")
        };
    }
    public async Task CancelAsync(ClaimsPrincipal actor, long id, string rowVersion)
    {
        await Check(actor, "residence_requests.self.cancel");
        var request = await Mine(actor, "RESIDENCE").SingleOrDefaultAsync(r => r.Id == id);
        if (request == null) throw new AppFault(404, "Không tìm thấy yêu cầu.");
        if (request.Status != "PENDING") throw new AppFault(409, "Chỉ có thể hủy yêu cầu đang chờ tiếp nhận.");
        SetVersion(request, rowVersion ?? ""); request.Status = "CANCELLED";
        request.Activities.Add(new Models.Activity { ActivityType = "REQUEST_EVENT", ActorUserId = CurrentUserId(actor), EventCode = "RESIDENT_CANCELLED", FromStatus = "PENDING", ToStatus = "CANCELLED", OccurredAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

    }
    public async Task ReplyAsync(ClaimsPrincipal actor, long id, string body, string clientKey)
    {
        await Check(actor, "feedback.self.reply");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await Mine(actor, "FEEDBACK").SingleOrDefaultAsync(r => r.Id == id);
        if (request == null) throw new AppFault(404, "Không tìm thấy yêu cầu.");
        if (request.Status is not ("PENDING" or "PROCESSING" or "RESPONDED")) throw new AppFault(409, "Phản ánh đã kết thúc, không thể gửi thêm trao đổi.");
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 4000 || !Guid.TryParseExact(clientKey, "N", out _))
        throw new AppFault(400, "Nội dung trao đổi phải có 1–4.000 ký tự.");
        var existing = await db.Activities.SingleOrDefaultAsync(a => a.ActorUserId == CurrentUserId(actor) && a.ClientMessageKey == clientKey);
        if (existing != null)
        {
            if (existing.RequestId != id || existing.ActivityType != "MESSAGE" || existing.Body != body.Trim()) throw new AppFault(409, "Mã gửi đã dùng với nội dung khác.");
            return;
        }
        db.Activities.Add(new Models.Activity { ActivityType = "MESSAGE", RequestId = id, ActorUserId = CurrentUserId(actor),
            EventCode = "RESIDENT_MESSAGE", Body = body.Trim(), ClientMessageKey = clientKey, OccurredAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return;
    }
    public async Task MarkReadAsync(ClaimsPrincipal actor, long id)
    {
        await Check(actor, "notifications.self.mark_read");
        var notification = await db.Activities.SingleOrDefaultAsync(a => a.Id == id && a.ActivityType == "NOTIFICATION" && a.RecipientUserId == CurrentUserId(actor));
        if (notification == null) throw new AppFault(404, "Không tìm thấy thông báo.");
        notification.ReadAt ??= DateTime.UtcNow; await db.SaveChangesAsync();

    }
    public async Task UploadAsync(ClaimsPrincipal actor, long id, Stream? content, long length, string fileName, string rowVersion)
    {
        await Check(actor, "attachments.self.upload");
        var request = await db.Requests.SingleOrDefaultAsync(r => r.Id == id && r.Source == "RESIDENT" && r.SubmittedByUserId == CurrentUserId(actor));
        if (request == null) throw new AppFault(404, "Không tìm thấy yêu cầu.");
        if (request.Status != "PENDING" || request.AttachmentStorageKey != null) throw new AppFault(409, "Chỉ được bổ sung một giấy tờ khi hồ sơ đang chờ tiếp nhận.");
        if (content == null || length is < 1 or > 5242880)
        throw new AppFault(400, "Chọn một tệp PDF, JPEG hoặc PNG tối đa 5 MB.");
        using var memory = new MemoryStream(); await content.CopyToAsync(memory);
        var bytes = memory.ToArray();
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        string? mime = null;
        if (ext == ".pdf" && bytes.AsSpan().StartsWith("%PDF-"u8)) mime = "application/pdf";
        if (ext is ".jpg" or ".jpeg" && bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217) mime = "image/jpeg";
        if (ext == ".png" && bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) mime = "image/png";
        if (mime == null)
        throw new AppFault(400, "Định dạng hoặc nội dung tệp không hợp lệ. Chỉ nhận PDF, JPEG, PNG.");
        SetVersion(request, rowVersion ?? "");
        var key = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(environment.ContentRootPath, ".local", "attachments");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, key);
        request.AttachmentStorageKey = key; request.AttachmentFileName = Path.GetFileName(fileName);
        if (request.AttachmentFileName.Length > 255) request.AttachmentFileName = "giay-to" + ext;
        request.AttachmentContentType = mime; request.AttachmentSizeBytes = bytes.Length;
        request.AttachmentHash = System.Security.Cryptography.SHA256.HashData(bytes);
        request.Activities.Add(new Models.Activity { ActivityType = "REQUEST_EVENT", ActorUserId = CurrentUserId(actor), EventCode = "ATTACHMENT_UPLOADED", OccurredAt = DateTime.UtcNow });
        try { await System.IO.File.WriteAllBytesAsync(path, bytes); await db.SaveChangesAsync(); }
        catch { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); throw; }

    }
    public async Task<DocumentDownload?> DownloadAsync(ClaimsPrincipal actor, long id)
    {
        await Check(actor, "attachments.self.read");
        var request = await db.Requests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.Source == "RESIDENT" && r.SubmittedByUserId == CurrentUserId(actor));
        if (request?.AttachmentStorageKey == null || !Guid.TryParseExact(request.AttachmentStorageKey, "N", out _)) return null;
        var path = Path.Combine(environment.ContentRootPath, ".local", "attachments", request.AttachmentStorageKey);
        if (!System.IO.File.Exists(path)) return null;
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        if (request.AttachmentHash == null || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(request.AttachmentHash, System.Security.Cryptography.SHA256.HashData(bytes)))
            throw new AppFault(409, "Giấy tờ không khớp dữ liệu đã lưu. Vui lòng liên hệ ban quản lý.");
        return new DocumentDownload(bytes, request.AttachmentContentType ?? "application/octet-stream", request.AttachmentFileName ?? "giay-to");
    }
}
