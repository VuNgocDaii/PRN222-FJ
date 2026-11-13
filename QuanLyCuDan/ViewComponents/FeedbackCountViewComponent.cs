using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.ViewComponents;

public sealed class FeedbackCountViewComponent(MyStockContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = ViewContext.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Content(string.Empty);

        var grants = await db.VwEffectivePermissions.AsNoTracking()
            .Where(x => x.UserId == userId && x.PermissionCode == "feedback.read")
            .Select(x => new { x.ScopeType, x.ResidentialGroupId, x.ResidentialAreaId })
            .ToListAsync();
        if (grants.Count == 0) return Content(string.Empty);

        var directGroups = grants.Where(x => x.ScopeType == "GROUP" && x.ResidentialGroupId != null)
            .Select(x => x.ResidentialGroupId!.Value).Distinct().ToList();
        var areas = grants.Where(x => x.ScopeType == "AREA" && x.ResidentialAreaId != null)
            .Select(x => x.ResidentialAreaId!.Value).Distinct().ToList();
        var scopedGroups = await db.ResidentialUnits.AsNoTracking()
            .Where(g => g.IsActive && g.UnitType == "GROUP" &&
                (directGroups.Contains(g.Id) || (g.ParentUnitId != null && areas.Contains(g.ParentUnitId.Value))))
            .Select(g => g.Id).ToListAsync();

        var count = await db.Requests.AsNoTracking().CountAsync(x => x.RequestKind == "FEEDBACK" &&
            x.Status == "PENDING" && !EF.Functions.Like(x.Code, "PA-DEMO-%") && scopedGroups.Contains(x.AssignedGroupId));
        return View(count);
    }
}
