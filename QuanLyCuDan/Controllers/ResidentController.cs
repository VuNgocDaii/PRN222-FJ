using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Portal;
using RequestEntity = QuanLyCuDan.Models.Request;
namespace QuanLyCuDan.Controllers;

[Authorize(AuthenticationSchemes = "Cookies")]
[Route("Resident")]
public sealed class ResidentController(MyStockContext context, IWebHostEnvironment environment, IConfiguration configuration) : BaseController(context)
{
    private readonly PortalScope portal = new(context, configuration);
    private readonly OrganizationReader organizations = new(context);
    private PortalLocality locality = null!;
    private AspNetUser? portalAccount;
    private bool isOnboarding;
    private static readonly HashSet<string> OnboardingPermissions = ["profile.self.read", "profile.self.update_contact",
        "residence_requests.self.read", "residence_requests.self.create", "residence_requests.self.cancel", "attachments.self.read", "attachments.self.upload"];

    // Runs for every Resident GET/POST, including attachment downloads and mutations addressed directly by ID.
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        locality = await portal.GetAsync();
        ViewData["PortalLocality"] = locality;
        try
        {
            var account = await Account();
            if (!account.IsActive || account.VerificationStatus != "APPROVED") throw OutsidePortal();
            var resident = Linked(account);
            if (resident.HouseholdId != null)
            {
                CurrentHousehold(account);
            }
            else
            {
                // No household does not establish membership. Reuse only the existing verified registration/MOVE_IN flow.
                var profile = RegistrationProfileInput.Read(account.RegistrationProfile);
                if (resident.ResidenceStatus != "MOVED_OUT" || account.ReviewedAt == null || account.ReviewedByUserId == null ||
                    profile == null || profile.GroupId != locality.Id || !System.ComponentModel.DataAnnotations.Validator.TryValidateObject(profile,
                        new System.ComponentModel.DataAnnotations.ValidationContext(profile), new List<System.ComponentModel.DataAnnotations.ValidationResult>(), true))
                    throw OutsidePortal();
                isOnboarding = true;
            }
        }
        catch (AppFault error) when (error.Status == 403 && HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            var denied = View("~/Views/Resident/AccessDenied.cshtml");
            denied.StatusCode = 403;
            context.Result = denied;
            return;
        }
        await base.OnActionExecutionAsync(context, next);
    }
    private static AppFault OutsidePortal() => new(403, "Tài khoản chưa đủ điều kiện sử dụng cổng cư dân của địa bàn phục vụ. Vui lòng liên hệ ban quản lý để xác minh hồ sơ.");
    private static readonly Dictionary<string, string> Sections = new() {
        ["overview"] = "profile.self.read", ["profile"] = "profile.self.read", ["household"] = "households.self.read",
        ["requests"] = "residence_requests.self.read", ["feedback"] = "feedback.self.read",
        ["fees"] = "fees.self.read", ["announcements"] = "announcements.audience.read", ["notifications"] = "notifications.self.read",
        ["documents"] = "attachments.self.read", ["groups"] = "profile.self.read", ["my-groups"] = "profile.self.read"
    };
    private async Task Check(string permission)
    {
        if (isOnboarding && !OnboardingPermissions.Contains(permission)) throw OutsidePortal();
        if (!await Effective(CurrentUserId(User)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == permission))
            throw new AppFault(403, "Bạn chưa được cấp quyền sử dụng chức năng này.");
    }
    private async Task<AspNetUser> Account() => portalAccount ??= await db.AspNetUsers.Include(u => u.Resident)!
        .ThenInclude(r => r!.Household)!.ThenInclude(h => h!.ResidentialGroup).ThenInclude(g => g.ParentUnit)
        .SingleAsync(u => u.Id == CurrentUserId(User));
    private static Resident Linked(AspNetUser account) => account.Resident ?? throw new AppFault(403, "Tài khoản chưa được liên kết với hồ sơ cư dân. Vui lòng liên hệ ban quản lý.");
    private Household CurrentHousehold(AspNetUser account)
    {
        var h = Linked(account).Household;
        if (h == null || h.ResidentialGroupId != locality.Id || h.IsArchived || !h.ResidentialGroup.IsActive || h.ResidentialGroup.UnitType != "GROUP" ||
            h.ResidentialGroup.ParentUnit is not { IsActive: true, UnitType: "AREA" })
            throw OutsidePortal();
        return h;
    }
    private IQueryable<RequestEntity> OwnedRequests() => db.Requests.Where(r => r.SubmittedByUserId == CurrentUserId(User) &&
        r.Source == "RESIDENT" && r.AssignedGroupId == locality.Id &&
        (!isOnboarding || r.RequestKind == "RESIDENCE" && (r.RequestType == "MOVE_IN" || r.RequestType == "ADD_MEMBER")));
    private IQueryable<RequestEntity> Mine(string kind) => OwnedRequests().Where(r => r.RequestKind == kind);
    private IQueryable<Activity> MyNotifications()
    {
        var userId = CurrentUserId(User);
        var householdId = portalAccount!.Resident!.HouseholdId;
        return db.Activities.Where(a => a.ActivityType == "NOTIFICATION" && a.RecipientUserId == userId &&
            (a.TargetType != "Request" && a.TargetType != "REQUEST" || db.Requests.Any(r => r.Id.ToString() == a.TargetId &&
                r.AssignedGroupId == locality.Id && r.SubmittedByUserId == userId && r.Source == "RESIDENT")) &&
            (a.TargetType != "Announcement" || db.Announcements.Any(n => n.Id.ToString() == a.TargetId && n.ResidentialGroupId == locality.Id)) &&
            (a.TargetType != "Payment" || db.Payments.Any(p => p.Id.ToString() == a.TargetId && p.HouseholdId == householdId && p.Campaign.ResidentialGroupId == locality.Id)));
    }

    [HttpGet("{section?}")]
    public async Task<IActionResult> Index(string section = "overview", int page = 1)
    {
        if (!Sections.ContainsKey(section)) return NotFound();
        await Check(Sections[section]);
        return View(await Load(section, page));
    }
    private async Task<ResidentPortalViewModel> Load(string section, int page = 1)
    {
        await Check(Sections[section]);
        var user = await Account();
        var m = new ResidentPortalViewModel { Section = section, Account = user, IsOnboarding = isOnboarding,
            Access = (await Effective(user.Id).Where(p => p.ScopeType == "SELF").Select(p => p.PermissionCode).ToListAsync()).ToHashSet() };
        if (isOnboarding) m.Access.IntersectWith(OnboardingPermissions);
        var resident = Linked(user);
        if (section is "groups" or "my-groups")
        {
            CurrentHousehold(user); // Householdless onboarding does not grant internal organization access.
            if (section == "groups") m.Organizations = await organizations.Directory(locality.Id, publicOnly: false);
            else m.MyOrganizations = await organizations.Mine(resident.Id, locality.Id);
        }
        if (section == "documents")
        {
            var query = OwnedRequests().AsNoTracking().Where(r => r.AttachmentStorageKey != null);
            m.Total = await query.CountAsync(); m.Page = ClampPage(page, m.Total);
            m.Requests = await query.OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id).Skip((m.Page - 1) * 10).Take(10).ToListAsync();
        }
        if (section is "household" || (section == "requests" && resident.Household != null))
        {
            var h = CurrentHousehold(user);
            m.Members = await db.Residents.AsNoTracking().Where(r => r.HouseholdId == h.Id).OrderBy(r => r.Id).ToListAsync();
        }
        if (section is "requests" or "feedback")
        {
            var query = Mine(section == "requests" ? "RESIDENCE" : "FEEDBACK").AsNoTracking();
            m.Total = await query.CountAsync(); m.Page = ClampPage(page, m.Total);
            m.Requests = await query.OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id).Skip((m.Page - 1) * 10).Take(10).ToListAsync();
            m.Input.Kind = section == "feedback" ? "FEEDBACK" : "RESIDENCE";
            m.Input.Type = section == "feedback" ? "OTHER" : isOnboarding ? "MOVE_IN" : "TEMPORARY_ABSENCE";
        }
        if (section == "fees")
        {
            await Check("payments.self.read");
            var h = CurrentHousehold(user);
            if (h.HeadResidentId == resident.Id)
                m.Payments = await db.Payments.AsNoTracking().Include(p => p.Campaign).Where(p => p.HouseholdId == h.Id && p.Campaign.ResidentialGroupId == locality.Id).OrderByDescending(p => p.CreatedAt).ToListAsync();
        }
        if (section == "announcements")
        {
            var h = CurrentHousehold(user);
            var query = db.Announcements.AsNoTracking().Where(a => a.ResidentialGroupId == h.ResidentialGroupId &&
                (a.Audience == "PUBLIC" || a.Audience == "RESIDENTS") && a.Status == "PUBLISHED" && a.ReviewStatus == "APPROVED" &&
                a.PublishedAt <= DateTime.UtcNow && (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow));
            m.Total = await query.CountAsync(); m.Page = ClampPage(page, m.Total);
            m.Announcements = await query.OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id).Skip((m.Page - 1) * 10).Take(10).ToListAsync();
        }
        if (section == "notifications")
        {
            var query = MyNotifications().AsNoTracking();
            m.Total = await query.CountAsync(); m.Page = ClampPage(page, m.Total);
            m.Notifications = await query.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((m.Page - 1) * 10).Take(10).ToListAsync();
        }
        return m;
    }
    private static int ClampPage(int page, int total) => Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 10d)));

    [HttpGet("organization/{id:long}")]
    public async Task<IActionResult> Organization(long id)
    {
        await Check("profile.self.read");
        CurrentHousehold(await Account());
        var model = await organizations.Detail(id, locality.Id, publicOnly: false);
        return model == null ? NotFound() : View("~/Views/Community/Detail.cshtml", model);
    }

    [HttpGet("announcement/{id:long}")]
    public async Task<IActionResult> Announcement(long id)
    {
        await Check("announcements.audience.read");
        var groupId = CurrentHousehold(await Account()).ResidentialGroupId;
        var item = await db.Announcements.AsNoTracking().Include(a => a.ResidentialGroup).SingleOrDefaultAsync(a => a.Id == id && a.ResidentialGroupId == groupId &&
            a.Status == "PUBLISHED" && a.ReviewStatus == "APPROVED" && (a.Audience == "PUBLIC" || a.Audience == "RESIDENTS") &&
            a.PublishedAt <= DateTime.UtcNow && (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow));
        return item == null ? NotFound() : View(item);
    }

    [HttpPost("contact"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactInput input)
    {
        await Check("profile.self.update_contact");
        if (!ModelState.IsValid) return View("Index", await Load("profile"));
        var account = await Account(); var resident = Linked(account);
        SetVersion(account, input.RowVersion); SetVersion(resident, input.ResidentVersion);
        account.PhoneNumber = resident.PhoneNumber = input.PhoneNumber;
        account.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
        account.NormalizedEmail = account.Email?.ToUpperInvariant();
        account.EmailConfirmed = false; account.PhoneNumberConfirmed = false;
        Audit(account.Id, "CONTACT_UPDATED", "RESIDENT", resident.Id.ToString());
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Thông tin đã thay đổi. Hãy tải lại và thử lại."; return RedirectToAction(nameof(Index), new { section = "profile" }); }
        TempData["Success"] = "Đã cập nhật thông tin liên hệ.";
        return RedirectToAction(nameof(Index), new { section = "profile" });
    }

    [HttpPost("requests"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] ResidentRequestInput input)
    {
        var feedback = input.Kind == "FEEDBACK";
        await Check(feedback ? "feedback.self.create" : "residence_requests.self.create");
        var section = feedback ? "feedback" : "requests";
        if (ModelState.IsValid)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var account = await Account(); var resident = Linked(account);
                var groupId = (await portal.GetAsync()).Id;
                if (groupId != locality.Id) throw new AppFault(409, "Địa bàn phục vụ vừa thay đổi. Hãy tải lại biểu mẫu.");
                if (isOnboarding && (feedback || input.Type is not ("MOVE_IN" or "ADD_MEMBER")))
                    throw new AppFault(400, "Bạn chưa có hộ tại địa bàn phục vụ. Chỉ có thể gửi yêu cầu chuyển đến hoặc đăng ký vào hộ để được xác minh.");
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
                var existing = await OwnedRequests().Include(r => r.Activities).SingleOrDefaultAsync(r => r.ClientRequestKey == input.ClientKey);
                if (existing != null)
                {
                    if (!existing.Activities.Any(a => a.ActivityType == "REQUEST_EVENT" && a.EventCode == "SUBMITTED" && a.SafeMetadata == "{\"payloadHash\":\"" + hash + "\"}"))
                        throw new AppFault(409, "Mã gửi đã được dùng với nội dung khác. Hãy tải lại biểu mẫu.");
                    return RedirectToAction(nameof(Detail), new { id = existing.Id });
                }
                if (!feedback && await db.Requests.AnyAsync(r => r.SubmittedByUserId == account.Id && r.Source == "RESIDENT" && r.RequestKind == "RESIDENCE" && (r.Status == "PENDING" || r.Status == "PROCESSING")))
                    throw new AppFault(409, "Bạn đang có yêu cầu cư trú chờ xử lý. Vui lòng chờ kết quả hoặc hủy yêu cầu đang chờ trước khi gửi mới.");
                var request = new RequestEntity { Code = "YC-" + Guid.NewGuid().ToString("N")[..24], RequestKind = input.Kind,
                    Source = "RESIDENT", SubmittedByUserId = account.Id, ResidentId = resident.Id, AssignedGroupId = groupId,
                    RequestType = feedback ? "FEEDBACK" : input.Type, Category = feedback ? input.Type : null,
                    Subject = input.Subject.Trim(), Body = input.Body.Trim(), StartDate = feedback ? null : input.StartDate,
                    EndDate = feedback ? null : input.EndDate, OriginalHouseholdId = feedback ? null : resident.HouseholdId,
                    TargetHouseholdId = targetId, NewHeadResidentId = feedback ? null : input.NewHeadResidentId,
                    Status = "PENDING", ClientRequestKey = input.ClientKey, SubmittedAt = DateTime.UtcNow };
                request.Activities.Add(new Models.Activity { ActivityType = "REQUEST_EVENT", ActorUserId = account.Id,
                    EventCode = "SUBMITTED", ToStatus = "PENDING", OccurredAt = DateTime.UtcNow, SafeMetadata = "{\"payloadHash\":\"" + hash + "\"}" });
                db.Requests.Add(request); await db.SaveChangesAsync(); await transaction.CommitAsync();
                TempData["Success"] = "Đã gửi yêu cầu. Bạn có thể theo dõi tiến độ tại đây.";
                return RedirectToAction(nameof(Detail), new { id = request.Id });
            }
            catch (AppFault e) when (e.Status is 400 or 409) { ModelState.AddModelError("", e.Message); }
        }
        var model = await Load(section); model.Input = input;
        return View("Index", model);
    }

    [HttpGet("request/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var request = await OwnedRequests().AsNoTracking().Include(r => r.AssignedGroup).SingleOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        await Check(request.RequestKind == "FEEDBACK" ? "feedback.self.read" : "residence_requests.self.read");
        return View(new ResidentRequestDetailViewModel {
            Request = request,
            Timeline = await db.Activities.AsNoTracking().Where(a => a.RequestId == id && (a.ActivityType == "MESSAGE" || a.ActivityType == "REQUEST_EVENT")).OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).ToListAsync(),
            CanCancel = request.RequestKind == "RESIDENCE" && request.Status == "PENDING" && await Effective(CurrentUserId(User)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == "residence_requests.self.cancel"),
            CanReply = request.RequestKind == "FEEDBACK" && request.Status is "PENDING" or "PROCESSING" or "RESPONDED" && await Effective(CurrentUserId(User)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == "feedback.self.reply"),
            CanUpload = request.Status == "PENDING" && request.AttachmentStorageKey == null && await Effective(CurrentUserId(User)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == "attachments.self.upload"),
            CanDownload = request.AttachmentStorageKey != null && await Effective(CurrentUserId(User)).AnyAsync(p => p.ScopeType == "SELF" && p.PermissionCode == "attachments.self.read")
        });
    }
    [HttpPost("request/{id:long}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, string rowVersion)
    {
        await Check("residence_requests.self.cancel");
        var request = await Mine("RESIDENCE").SingleOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != "PENDING") throw new AppFault(409, "Chỉ có thể hủy yêu cầu đang chờ tiếp nhận.");
        SetVersion(request, rowVersion ?? ""); request.Status = "CANCELLED";
        request.Activities.Add(new Models.Activity { ActivityType = "REQUEST_EVENT", ActorUserId = CurrentUserId(User), EventCode = "RESIDENT_CANCELLED", FromStatus = "PENDING", ToStatus = "CANCELLED", OccurredAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); TempData["Success"] = "Đã hủy yêu cầu.";
        return RedirectToAction(nameof(Detail), new { id });
    }
    [HttpPost("request/{id:long}/reply"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(long id, string body, string clientKey)
    {
        await Check("feedback.self.reply");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await Mine("FEEDBACK").SingleOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status is not ("PENDING" or "PROCESSING" or "RESPONDED")) throw new AppFault(409, "Phản ánh đã kết thúc, không thể gửi thêm trao đổi.");
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 4000 || !Guid.TryParseExact(clientKey, "N", out _))
        { TempData["Error"] = "Nội dung trao đổi phải có 1–4.000 ký tự."; return RedirectToAction(nameof(Detail), new { id }); }
        var existing = await db.Activities.SingleOrDefaultAsync(a => a.ActorUserId == CurrentUserId(User) && a.ClientMessageKey == clientKey);
        if (existing != null)
        {
            if (existing.RequestId != id || existing.ActivityType != "MESSAGE" || existing.Body != body.Trim()) throw new AppFault(409, "Mã gửi đã dùng với nội dung khác.");
            return RedirectToAction(nameof(Detail), new { id });
        }
        db.Activities.Add(new Models.Activity { ActivityType = "MESSAGE", RequestId = id, ActorUserId = CurrentUserId(User),
            EventCode = "RESIDENT_MESSAGE", Body = body.Trim(), ClientMessageKey = clientKey, OccurredAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        TempData["Success"] = "Đã gửi trao đổi.";
        return RedirectToAction(nameof(Detail), new { id });
    }
    [HttpPost("notifications/{id:long}/read"), ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long id)
    {
        await Check("notifications.self.mark_read");
        var notification = await MyNotifications().SingleOrDefaultAsync(a => a.Id == id);
        if (notification == null) return NotFound();
        notification.ReadAt ??= DateTime.UtcNow; await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { section = "notifications" });
    }

    [HttpPost("request/{id:long}/attachment"), ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(long id, IFormFile? file, string rowVersion)
    {
        await Check("attachments.self.upload");
        var request = await OwnedRequests().SingleOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != "PENDING" || request.AttachmentStorageKey != null) throw new AppFault(409, "Chỉ được bổ sung một giấy tờ khi hồ sơ đang chờ tiếp nhận.");
        if (file == null || file.Length is < 1 or > 5242880)
        { TempData["Error"] = "Chọn một tệp PDF, JPEG hoặc PNG tối đa 5 MB."; return RedirectToAction(nameof(Detail), new { id }); }
        using var memory = new MemoryStream(); await file.CopyToAsync(memory);
        var bytes = memory.ToArray();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        string? mime = null;
        if (ext == ".pdf" && bytes.AsSpan().StartsWith("%PDF-"u8)) mime = "application/pdf";
        if (ext is ".jpg" or ".jpeg" && bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217) mime = "image/jpeg";
        if (ext == ".png" && bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) mime = "image/png";
        if (mime == null)
        { TempData["Error"] = "Định dạng hoặc nội dung tệp không hợp lệ. Chỉ nhận PDF, JPEG, PNG."; return RedirectToAction(nameof(Detail), new { id }); }
        SetVersion(request, rowVersion ?? "");
        var key = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(environment.ContentRootPath, ".local", "attachments");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, key);
        request.AttachmentStorageKey = key; request.AttachmentFileName = Path.GetFileName(file.FileName);
        if (request.AttachmentFileName.Length > 255) request.AttachmentFileName = "giay-to" + ext;
        request.AttachmentContentType = mime; request.AttachmentSizeBytes = bytes.Length;
        request.AttachmentHash = System.Security.Cryptography.SHA256.HashData(bytes);
        request.Activities.Add(new Models.Activity { ActivityType = "REQUEST_EVENT", ActorUserId = CurrentUserId(User), EventCode = "ATTACHMENT_UPLOADED", OccurredAt = DateTime.UtcNow });
        try { await System.IO.File.WriteAllBytesAsync(path, bytes); await db.SaveChangesAsync(); }
        catch { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); throw; }
        TempData["Success"] = "Đã bổ sung giấy tờ vào hồ sơ.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("request/{id:long}/attachment")]
    public async Task<IActionResult> Download(long id)
    {
        await Check("attachments.self.read");
        var request = await OwnedRequests().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
        if (request?.AttachmentStorageKey == null || !Guid.TryParseExact(request.AttachmentStorageKey, "N", out _)) return NotFound();
        var path = Path.Combine(environment.ContentRootPath, ".local", "attachments", request.AttachmentStorageKey);
        if (!System.IO.File.Exists(path)) return NotFound();
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        if (request.AttachmentHash == null || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(request.AttachmentHash, System.Security.Cryptography.SHA256.HashData(bytes)))
            throw new AppFault(409, "Giấy tờ không khớp dữ liệu đã lưu. Vui lòng liên hệ ban quản lý.");
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox";
        return File(bytes, request.AttachmentContentType ?? "application/octet-stream", request.AttachmentFileName ?? "giay-to");
    }
}
