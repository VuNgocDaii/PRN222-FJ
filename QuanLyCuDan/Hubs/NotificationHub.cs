using QuanLyCuDan.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuanLyCuDan.Services;
namespace QuanLyCuDan.Hubs;
[Authorize(AuthenticationSchemes = "Cookies")]
public class NotificationHub(ISessionService sessions, INotificationSubscriptionService subscriptions) : Hub
{
    public static string FeedbackUserGroup(string userId) => $"feedback:user:{userId}";
    public static string FeedbackStaffGroup(long groupId) => $"feedback:staff:{groupId}";
    public override async Task OnConnectedAsync()
    {
        if (!await sessions.IsValidAsync(Context.User!))
        {
            Context.Abort();
            return;
        }
        foreach (var group in await subscriptions.GetGroupsAsync(Context.User!))
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        await base.OnConnectedAsync();
    }
}
