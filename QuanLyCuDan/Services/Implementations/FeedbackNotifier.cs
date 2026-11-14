using Microsoft.AspNetCore.SignalR;
using QuanLyCuDan.Hubs;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class FeedbackNotifier(IHubContext<NotificationHub> hub) : IFeedbackNotifier
{
    public Task ToStaffAsync(long groupId, long requestId, string message) =>
        hub.Clients.Group(NotificationHub.FeedbackStaffGroup(groupId)).SendAsync("FeedbackChanged", new { requestId, message });

    public Task ToUserAsync(string userId, long requestId, string message) =>
        hub.Clients.Group(NotificationHub.FeedbackUserGroup(userId)).SendAsync("FeedbackChanged", new { requestId, message });
}
