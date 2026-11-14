using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class FeedbackService(
    IPermissionChecker checker,
    IPermissionRepository permissions,
    IFeedbackRepository feedback,
    IFeedbackNotifier notifier,
    IUnitOfWork unitOfWork) : IFeedbackService
{
    public async Task<FeedbackPageViewModel> GetAsync(ClaimsPrincipal actor, long? id, string? notice)
    {
        var userId = checker.UserId(actor);
        var staff = await permissions.HasAnyAsync(userId, "feedback.read");
        var residentId = await feedback.GetResidentIdAsync(userId);
        string? submittedBy = null;
        IReadOnlyCollection<long>? groups = null;
        if (!staff)
        {
            if (residentId is null || !await permissions.HasAnyAsync(userId, "feedback.self.read"))
                return new FeedbackPageViewModel();
            submittedBy = userId;
        }
        else
            groups = await permissions.GetGroupIdsAsync(userId, "feedback.read");

        var rows = await feedback.ListAsync(submittedBy, groups);
        FeedbackConversation? selected = null;
        var selectedId = id ?? rows.FirstOrDefault()?.Id;
        if (selectedId is not null)
        {
            var request = await feedback.FindInScopeWithSubmitterAsync(selectedId.Value, submittedBy, groups);
            if (request is not null)
                selected = new FeedbackConversation(request.Id, request.Code, request.Subject, request.Category ?? "OTHER", request.Status,
                    request.SubmittedAt, request.SubmittedByUser.FullName, request.Body, await feedback.GetMessagesAsync(request.Id));
        }
        var canSubmit = residentId is not null && await permissions.HasAnyAsync(userId, "feedback.self.create");
        var canManage = staff && (await permissions.HasAnyAsync(userId, "feedback.process") || await permissions.HasAnyAsync(userId, "feedback.reply"));
        return new FeedbackPageViewModel { CanSubmit = canSubmit, CanManage = canManage, Notice = notice, Items = rows, Selected = selected };
    }

    public async Task<long> CreateAsync(ClaimsPrincipal actor, string subject, string category, string body)
    {
        subject = subject.Trim();
        body = body.Trim();
        if (subject.Length is < 3 or > 200 || body.Length is < 5 or > 4000 || !new[] { "ENVIRONMENT", "INFRASTRUCTURE", "OTHER" }.Contains(category))
            throw new AppFault(400, "Vui lòng kiểm tra tiêu đề, nội dung và loại góp ý.");
        var userId = checker.UserId(actor);
        var resident = await feedback.GetResidentWithHouseholdAsync(userId);
        if (resident?.HouseholdId is null || !await permissions.HasAnyAsync(userId, "feedback.self.create"))
            throw new AppFault(403, "Bạn không có quyền truy cập góp ý này.");
        var groupId = await feedback.GetHouseholdGroupIdAsync(resident.HouseholdId.Value);
        var now = DateTime.UtcNow;
        var request = new Request
        {
            Code = $"PA-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), RequestKind = "FEEDBACK", Source = "RESIDENT", SubmittedByUserId = userId, ResidentId = resident.Id,
            AssignedGroupId = groupId, RequestType = "FEEDBACK", Category = category, Subject = subject, Body = body, Status = "PENDING",
            ClientRequestKey = Guid.NewGuid().ToString("N"), SubmittedAt = now
        };
        feedback.Add(request);
        await unitOfWork.SaveChangesAsync();
        feedback.AddActivity(new Models.Activity { ActivityType = "REQUEST_EVENT", RequestId = request.Id, ActorUserId = userId, EventCode = "SUBMITTED", ToStatus = "PENDING", OccurredAt = now });
        await unitOfWork.SaveChangesAsync();
        await notifier.ToStaffAsync(groupId, request.Id, "Có góp ý mới cần tiếp nhận.");
        return request.Id;
    }

    public async Task ProcessAsync(ClaimsPrincipal actor, long id)
    {
        var userId = checker.UserId(actor);
        var item = await feedback.FindFeedbackAsync(id) ?? throw new AppFault(404, "Không tìm thấy góp ý.");
        if (!await permissions.CanAccessGroupAsync(userId, "feedback.process", item.AssignedGroupId))
            throw new AppFault(403, "Bạn không có quyền truy cập góp ý này.");
        if (item.Status is "PENDING")
        {
            item.Status = "PROCESSING";
            item.ProcessedByUserId = userId;
            item.ProcessedAt = DateTime.UtcNow;
            feedback.AddActivity(new Models.Activity { ActivityType = "REQUEST_EVENT", RequestId = id, ActorUserId = userId, EventCode = "ACCEPTED", FromStatus = "PENDING", ToStatus = "PROCESSING", OccurredAt = DateTime.UtcNow });
            await unitOfWork.SaveChangesAsync();
            await notifier.ToUserAsync(item.SubmittedByUserId, id, "Góp ý của bạn đã được tiếp nhận.");
        }
    }

    public async Task CompleteAsync(ClaimsPrincipal actor, long id)
    {
        var userId = checker.UserId(actor);
        var item = await feedback.FindFeedbackAsync(id) ?? throw new AppFault(404, "Không tìm thấy góp ý.");
        if (!await permissions.CanAccessGroupAsync(userId, "feedback.process", item.AssignedGroupId))
            throw new AppFault(403, "Bạn không có quyền xử lý góp ý này.");
        if (item.Status is not ("PROCESSING" or "RESPONDED"))
            throw new AppFault(409, "Chỉ có thể hoàn tất phản ánh đang xử lý hoặc đã phản hồi.");
        var previousStatus = item.Status;
        item.Status = "CLOSED";
        feedback.AddActivity(new Models.Activity { ActivityType = "REQUEST_EVENT", RequestId = id, ActorUserId = userId, EventCode = "COMPLETED", FromStatus = previousStatus, ToStatus = "CLOSED", OccurredAt = DateTime.UtcNow });
        await unitOfWork.SaveChangesAsync();
        await notifier.ToUserAsync(item.SubmittedByUserId, id, "Phản ánh của bạn đã hoàn tất.");
    }

    public async Task ReplyAsync(ClaimsPrincipal actor, long id, string body)
    {
        body = body.Trim();
        if (body.Length is < 1 or > 4000)
            throw new AppFault(400, "Nội dung phản hồi phải từ 1 đến 4000 ký tự.");
        var userId = checker.UserId(actor);
        var item = await feedback.FindFeedbackAsync(id) ?? throw new AppFault(404, "Không tìm thấy góp ý.");
        var staff = await permissions.CanAccessGroupAsync(userId, "feedback.reply", item.AssignedGroupId);
        var self = item.SubmittedByUserId == userId && await permissions.HasAnyAsync(userId, "feedback.self.reply");
        if (!staff && !self)
            throw new AppFault(403, "Bạn không có quyền truy cập góp ý này.");
        if (item.Status is "CLOSED" or "OUT_OF_SCOPE")
            throw new AppFault(409, "Góp ý đã đóng.");
        var now = DateTime.UtcNow;
        feedback.AddActivity(new Models.Activity { ActivityType = "MESSAGE", RequestId = id, ActorUserId = userId, EventCode = staff ? "OFFICIAL_REPLY" : "RESIDENT_REPLY", Body = body, ClientMessageKey = Guid.NewGuid().ToString("N"), OccurredAt = now });
        if (staff)
        {
            item.Status = "RESPONDED";
            item.ProcessedByUserId = userId;
            item.ProcessedAt = now;
        }
        else
            item.Status = "PROCESSING";
        await unitOfWork.SaveChangesAsync();
        await notifier.ToUserAsync(item.SubmittedByUserId, id, "Góp ý có cập nhật mới.");
        await notifier.ToStaffAsync(item.AssignedGroupId, id, "Góp ý có cập nhật mới.");
    }
}
