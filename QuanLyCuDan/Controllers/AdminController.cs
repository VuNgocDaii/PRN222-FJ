using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class AdminController(MyStockContext context) : BaseController(context)
{
    public async Task<IActionResult> Index()
    {
        var management = new[]
        {
            "accounts.read",
            "roles.read",
            "roles.assign",
            "permissions.read"
        };
        if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(Effective(CurrentUserId(User)), x => x.ScopeType == "SYSTEM" && management.Contains(x.PermissionCode)))
            throw new AppFault(403, "Bạn không có quyền quản trị hệ thống.");
        ViewData["Permissions"] = (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(Effective(CurrentUserId(User)).Where(x => x.ScopeType == "SYSTEM"))).Select(x => x.PermissionCode).ToHashSet();
        return View();
    }
}
