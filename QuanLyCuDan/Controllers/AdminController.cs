using QuanLyCuDan.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Services;
namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class AdminController(IAdminService service, INavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    public async Task<IActionResult> Index(string? tab, string? search, int page = 1, string? userId = null, string? roleId = null, int? permissionId = null)
    {
        var result = await service.GetAsync(User, tab, search, page, userId, roleId, permissionId);
        if (result.Model == null) return RedirectToAction("Index", "RoleAssignments");
        ViewData["Permissions"] = result.Permissions; ViewData["AdminTabs"] = true;
        return View(result.Model);
    }
}
