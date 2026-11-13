using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using Microsoft.EntityFrameworkCore;

namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class AdminController(MyStockContext context) : BaseController(context)
{
    public async Task<IActionResult> Index(string? tab, string? search, int page = 1, string? userId = null, string? roleId = null, int? permissionId = null)
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
        var access = (await Effective(CurrentUserId(User)).Where(x => x.ScopeType == "SYSTEM").Select(x => x.PermissionCode).ToListAsync()).ToHashSet();
        ViewData["Permissions"] = access;
        ViewData["AdminTabs"] = true;
        var available = new List<string>();
        if (access.Contains("accounts.read")) available.Add("users");
        if (access.Contains("roles.read")) available.Add("roles");
        if (access.Contains("permissions.read")) available.Add("permissions");
        if (available.Count == 0) return RedirectToAction("Index", "RoleAssignments");
        var model = new AdminViewModel { Tab = available.Contains(tab ?? "") ? tab! : available[0], Search = search?.Trim() };
        model.CanAssign = access.Contains("accounts.read") && access.Contains("roles.read") && access.Contains("permissions.read") && access.Contains("roles.assign");
        if (model.Tab == "users")
        {
            var query = db.AspNetUsers.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(model.Search)) query = query.Where(u => u.UserName.Contains(model.Search) || u.FullName.Contains(model.Search));
            model.Total = await query.CountAsync();
            model.Page = Math.Clamp(page, 1, Math.Max(1, (model.Total + 29) / 30));
            model.Users = await query.OrderBy(u => u.UserName).Skip((model.Page - 1) * 30).Take(30).ToListAsync();
            if (model.CanAssign)
            {
                model.SelectedUser = userId == null ? model.Users.FirstOrDefault() : await db.AspNetUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
                model.Units = await db.ResidentialUnits.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync();
                if (model.SelectedUser != null) model.Assignments = await db.UserRoleAssignments.AsNoTracking().Include(a => a.Role)
                    .Where(a => a.UserId == model.SelectedUser.Id).OrderByDescending(a => a.AssignedAt).Take(200).ToListAsync();
            }
        }
        if (model.Tab == "roles" || model.CanAssign && model.Tab == "users")
        {
            model.Roles = await db.AspNetRoles.AsNoTracking().Include(r => r.Permissions).ThenInclude(p => p.PermissionAllowedScopes).OrderBy(r => r.Name).ToListAsync();
            model.SelectedRole = model.Roles.FirstOrDefault(r => r.Id == roleId) ?? model.Roles.FirstOrDefault();
            model.OwnRoleIds = (await db.UserRoleAssignments.Where(a => a.UserId == CurrentUserId(User) && a.RevokedAt == null && (a.ValidUntil == null || a.ValidUntil > DateTime.UtcNow)).Select(a => a.RoleId).ToListAsync()).ToHashSet();
        }
        if (model.Tab == "permissions" || model.Tab == "roles" && access.Contains("permissions.read"))
        {
            model.Permissions = await db.Permissions.AsNoTracking().Include(p => p.PermissionAllowedScopes).Include(p => p.Roles).AsSplitQuery().OrderBy(p => p.Module).ThenBy(p => p.Code).ToListAsync();
            if (model.Tab == "permissions" && permissionId.HasValue && access.Contains("permissions.update")) model.EditingPermission = model.Permissions.FirstOrDefault(p => p.Id == permissionId);
        }
        return View(model);
    }
}
