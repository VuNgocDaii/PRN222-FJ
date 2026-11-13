using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Controllers;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Hubs;

// Chỉ người đã đăng nhập mới kết nối được.
[Authorize(AuthenticationSchemes = "Cookies")]
public class NotificationHub : Hub
{
    private readonly MyStockContext _db;

    public NotificationHub(MyStockContext db)
    {
        _db = db;
    }

    public static string FeedbackUserGroup(string userId) => $"feedback:user:{userId}";
    public static string FeedbackStaffGroup(long groupId) => $"feedback:staff:{groupId}";

    public override async Task OnConnectedAsync()
    {
        if (!await BaseController.ValidSession(_db, Context.User!))
        {
            Context.Abort();
            return;
        }

        var userId = Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await Groups.AddToGroupAsync(Context.ConnectionId, FeedbackUserGroup(userId));
        var grants = await _db.VwEffectivePermissions.AsNoTracking()
            .Where(x => x.UserId == userId && x.PermissionCode == "feedback.read")
            .Select(x => new { x.ScopeType, x.ResidentialGroupId, x.ResidentialAreaId }).ToListAsync();
        foreach (var grant in grants.Where(x => x.ScopeType == "GROUP" && x.ResidentialGroupId != null))
            await Groups.AddToGroupAsync(Context.ConnectionId, FeedbackStaffGroup(grant.ResidentialGroupId!.Value));
        var areas = grants.Where(x => x.ScopeType == "AREA" && x.ResidentialAreaId != null).Select(x => x.ResidentialAreaId!.Value).Distinct().ToList();
        var groupIds = await _db.ResidentialUnits.AsNoTracking().Where(x => x.IsActive && x.ParentUnitId != null && areas.Contains(x.ParentUnitId.Value)).Select(x => x.Id).ToListAsync();
        foreach (var groupId in groupIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, FeedbackStaffGroup(groupId));
        await base.OnConnectedAsync();
    }

}
