using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.ViewComponents;

public sealed class FeedbackCountViewComponent(IPermissionRepository permissions, IFeedbackRepository feedback) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = ViewContext.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !await permissions.HasAnyAsync(userId, "feedback.read"))
            return Content(string.Empty);
        var groups = await permissions.GetGroupIdsAsync(userId, "feedback.read");
        return View(await feedback.CountPendingAsync(groups));
    }
}
