using Microsoft.AspNetCore.SignalR;
using QuanLyCuDan.Hubs;
using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class FeedbackService(MyStockContext context, IHubContext<NotificationHub> hub) : ApplicationService(context)
{
    public async Task<FeedbackPageViewModel> GetAsync(ClaimsPrincipal actor, long? id, string? notice)
    {
        var userId = CurrentUserId(actor);
        var staff = await Effective(userId).AnyAsync(x => x.PermissionCode == "feedback.read");
        var resident = await db.AspNetUsers.Where(x => x.Id == userId).Select(x => x.ResidentId).SingleAsync();
        var query = db.Requests.AsNoTracking().Where(x => x.RequestKind == "FEEDBACK");
        if (!staff)
        {
            if (resident is null || !await Effective(userId).AnyAsync(x => x.PermissionCode == "feedback.self.read"))
                return new FeedbackPageViewModel();
            query = query.Where(x => x.SubmittedByUserId == userId);
        }
        else
        {
            var grants = await Effective(userId).Where(x => x.PermissionCode == "feedback.read")
                .Select(x => new { x.ScopeType, x.ResidentialGroupId, x.ResidentialAreaId }).ToListAsync();
            var directGroups = grants.Where(x => x.ScopeType == "GROUP" && x.ResidentialGroupId != null).Select(x => x.ResidentialGroupId!.Value).ToList();
            var areas = grants.Where(x => x.ScopeType == "AREA" && x.ResidentialAreaId != null).Select(x => x.ResidentialAreaId!.Value).Distinct().ToList();
            var areaGroups = await db.ResidentialUnits.AsNoTracking().Where(g => g.IsActive && g.ParentUnitId != null && areas.Contains(g.ParentUnitId.Value)).Select(g => g.Id).ToListAsync();
            var groups = directGroups.Concat(areaGroups).Distinct().ToList();
            query = query.Where(x => groups.Contains(x.AssignedGroupId));
        }

        var rows = await query.OrderByDescending(x => x.SubmittedAt).Select(x => new FeedbackListItem(
            x.Id, x.Code, x.Subject, x.Status, x.SubmittedAt, x.SubmittedByUser.FullName)).ToListAsync();
        FeedbackConversation? selected = null;
        var selectedId = id ?? rows.FirstOrDefault()?.Id;
        if (selectedId is not null)
        {
            var request = await query.Include(x => x.SubmittedByUser).FirstOrDefaultAsync(x => x.Id == selectedId.Value);
            if (request is not null)
            {
                var messages = await db.Activities.AsNoTracking().Where(a => a.RequestId == request.Id && a.ActivityType == "MESSAGE")
                    .OrderBy(a => a.OccurredAt).Select(a => new FeedbackMessage(a.ActorUser!.FullName, a.Body!, a.OccurredAt, a.EventCode == "OFFICIAL_REPLY")).ToListAsync();
                selected = new FeedbackConversation(request.Id, request.Code, request.Subject, request.Category ?? "OTHER", request.Status,
                    request.SubmittedAt, request.SubmittedByUser.FullName, request.Body, messages);
            }
        }

        var canSubmit = resident is not null && await Effective(userId).AnyAsync(x => x.PermissionCode == "feedback.self.create");
        var canManage = staff && await Effective(userId).AnyAsync(x => x.PermissionCode == "feedback.process" || x.PermissionCode == "feedback.reply");
        return new FeedbackPageViewModel { CanSubmit = canSubmit, CanManage = canManage, Notice = notice, Items = rows, Selected = selected };
    }
    public async Task<long> CreateAsync(ClaimsPrincipal actor, string subject, string category, string body)
    {
        subject = subject.Trim(); body = body.Trim();
        if (subject.Length is < 3 or > 200 || body.Length is < 5 or > 4000 || !new[] { "ENVIRONMENT", "INFRASTRUCTURE", "OTHER" }.Contains(category))
            throw new AppFault(400, "Vui lòng kiểm tra tiêu đề, nội dung và loại góp ý.");
        var userId = CurrentUserId(actor);
        var resident = await db.AspNetUsers.Where(x => x.Id == userId && x.ResidentId != null).Select(x => x.Resident).SingleOrDefaultAsync();
        if (resident?.HouseholdId is null || !await Effective(userId).AnyAsync(x => x.PermissionCode == "feedback.self.create")) throw new AppFault(403, "Bạn không có quyền truy cập góp ý này.");
        var groupId = await db.Households.Where(h => h.Id == resident.HouseholdId && !h.IsArchived).Select(h => h.ResidentialGroupId).SingleAsync();
        var now = DateTime.UtcNow;
        var request = new Request { Code = $"PA-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), RequestKind = "FEEDBACK", Source = "RESIDENT", SubmittedByUserId = userId, ResidentId = resident.Id,
            AssignedGroupId = groupId, RequestType = "FEEDBACK", Category = category, Subject = subject, Body = body, Status = "PENDING",
            ClientRequestKey = Guid.NewGuid().ToString("N"), SubmittedAt = now };
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        db.Activities.Add(new Activity { ActivityType = "REQUEST_EVENT", RequestId = request.Id, ActorUserId = userId, EventCode = "SUBMITTED", ToStatus = "PENDING", OccurredAt = now });
        await db.SaveChangesAsync();
        await hub.Clients.Group(NotificationHub.FeedbackStaffGroup(groupId)).SendAsync("FeedbackChanged", new { requestId = request.Id, message = "Có góp ý mới cần tiếp nhận." });
        return request.Id;
    }
    public async Task ProcessAsync(ClaimsPrincipal actor, long id)
    {
        var userId = CurrentUserId(actor);
        var item = await db.Requests.SingleOrDefaultAsync(x => x.Id == id && x.RequestKind == "FEEDBACK");
        if (item is null) throw new AppFault(404, "Không tìm thấy góp ý.");
        if (!await CanAccessGroup(userId, "feedback.process", item.AssignedGroupId)) throw new AppFault(403, "Bạn không có quyền truy cập góp ý này.");
        if (item.Status is "PENDING")
        {
            item.Status = "PROCESSING"; item.ProcessedByUserId = userId; item.ProcessedAt = DateTime.UtcNow;
            db.Activities.Add(new Activity { ActivityType = "REQUEST_EVENT", RequestId = id, ActorUserId = userId, EventCode = "ACCEPTED", FromStatus = "PENDING", ToStatus = "PROCESSING", OccurredAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            await hub.Clients.Group(NotificationHub.FeedbackUserGroup(item.SubmittedByUserId)).SendAsync("FeedbackChanged", new { requestId = id, message = "Góp ý của bạn đã được tiếp nhận." });
        }

    }
    public async Task CompleteAsync(ClaimsPrincipal actor, long id)
    {
        var userId = CurrentUserId(actor);
        var item = await db.Requests.SingleOrDefaultAsync(x => x.Id == id && x.RequestKind == "FEEDBACK");
        if (item is null) throw new AppFault(404, "Không tìm thấy góp ý.");
        if (!await CanAccessGroup(userId, "feedback.process", item.AssignedGroupId)) throw new AppFault(403, "Bạn không có quyền xử lý góp ý này.");
        if (item.Status is not ("PROCESSING" or "RESPONDED")) throw new AppFault(409, "Chỉ có thể hoàn tất phản ánh đang xử lý hoặc đã phản hồi.");

        var now = DateTime.UtcNow;
        var previousStatus = item.Status;
        item.Status = "CLOSED";
        db.Activities.Add(new Activity { ActivityType = "REQUEST_EVENT", RequestId = id, ActorUserId = userId,
            EventCode = "COMPLETED", FromStatus = previousStatus, ToStatus = "CLOSED", OccurredAt = now });
        await db.SaveChangesAsync();
        await hub.Clients.Group(NotificationHub.FeedbackUserGroup(item.SubmittedByUserId))
            .SendAsync("FeedbackChanged", new { requestId = id, message = "Phản ánh của bạn đã hoàn tất." });
    }
    public async Task ReplyAsync(ClaimsPrincipal actor, long id, string body)
    {
        body = body.Trim();
        if (body.Length is < 1 or > 4000) throw new AppFault(400, "Nội dung phản hồi phải từ 1 đến 4000 ký tự.");
        var userId = CurrentUserId(actor);
        var item = await db.Requests.SingleOrDefaultAsync(x => x.Id == id && x.RequestKind == "FEEDBACK");
        if (item is null) throw new AppFault(404, "Không tìm thấy góp ý.");
        var staff = await CanAccessGroup(userId, "feedback.reply", item.AssignedGroupId);
        var self = item.SubmittedByUserId == userId && await Effective(userId).AnyAsync(x => x.PermissionCode == "feedback.self.reply");
        if (!staff && !self) throw new AppFault(403, "Bạn không có quyền truy cập góp ý này.");
        if (item.Status is "CLOSED" or "OUT_OF_SCOPE") throw new AppFault(409, "Góp ý đã đóng.");
        var now = DateTime.UtcNow;
        var official = staff;
        db.Activities.Add(new Activity { ActivityType = "MESSAGE", RequestId = id, ActorUserId = userId,
            EventCode = official ? "OFFICIAL_REPLY" : "RESIDENT_REPLY", Body = body, ClientMessageKey = Guid.NewGuid().ToString("N"), OccurredAt = now });
        if (official) { item.Status = "RESPONDED"; item.ProcessedByUserId = userId; item.ProcessedAt = now; }
        else item.Status = "PROCESSING";
        await db.SaveChangesAsync();
        await hub.Clients.Group(NotificationHub.FeedbackUserGroup(item.SubmittedByUserId)).SendAsync("FeedbackChanged", new { requestId = id, message = "Góp ý có cập nhật mới." });
        await hub.Clients.Group(NotificationHub.FeedbackStaffGroup(item.AssignedGroupId)).SendAsync("FeedbackChanged", new { requestId = id, message = "Góp ý có cập nhật mới." });

    }
}
