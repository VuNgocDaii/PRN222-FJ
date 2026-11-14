using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class AdminService(
    IPermissionChecker checker,
    IPermissionRepository permissions,
    IUserRepository users,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    IPermissionCatalogRepository catalog) : IAdminService
{
    private const int PageSize = 30;

    public async Task<AdminPageResult> GetAsync(ClaimsPrincipal actor, string? tab, string? search, int page = 1, string? userId = null, string? roleId = null, int? permissionId = null)
    {
        var me = checker.UserId(actor);
        if (!await permissions.HasAnyCodeAsync(me, ["accounts.read", "roles.read", "roles.assign", "permissions.read"], "SYSTEM"))
            throw new AppFault(403, "Bạn không có quyền quản trị hệ thống.");
        var access = await permissions.GetSystemCodesAsync(me);
        var available = new List<string>();
        if (access.Contains("accounts.read")) available.Add("users");
        if (access.Contains("roles.read")) available.Add("roles");
        if (access.Contains("permissions.read")) available.Add("permissions");
        if (available.Count == 0) return new(null, access);
        var model = new AdminViewModel { Tab = available.Contains(tab ?? "") ? tab! : available[0], Search = search?.Trim() };
        model.CanAssign = access.Contains("accounts.read") && access.Contains("roles.read") && access.Contains("permissions.read") && access.Contains("roles.assign");
        if (model.Tab == "users")
        {
            model.Total = await users.CountAsync(model.Search);
            model.Page = Math.Clamp(page, 1, Math.Max(1, (model.Total + PageSize - 1) / PageSize));
            model.Users = await users.ListPageAsync(model.Search, model.Page, PageSize);
            if (model.CanAssign)
            {
                model.SelectedUser = userId == null ? model.Users.FirstOrDefault() : await users.FindNoTrackingAsync(userId);
                model.Organizations = await assignments.ActiveOrganizationsAsync();
                model.Units = await assignments.ActiveUnitsAsync();
                if (model.SelectedUser != null)
                    model.Assignments = await assignments.ListForUserAsync(model.SelectedUser.Id);
            }
        }
        if (model.Tab == "roles" || model.CanAssign && model.Tab == "users")
        {
            model.Roles = await roles.ListAllWithScopesAsync();
            model.SelectedRole = model.Roles.FirstOrDefault(r => r.Id == roleId) ?? model.Roles.FirstOrDefault();
            model.OwnRoleIds = await assignments.OwnActiveRoleIdsAsync(me);
        }
        if (model.Tab == "permissions" || model.Tab == "roles" && access.Contains("permissions.read"))
        {
            model.Permissions = await catalog.ListSplitAsync();
            if (model.Tab == "permissions" && permissionId.HasValue && access.Contains("permissions.update"))
                model.EditingPermission = model.Permissions.FirstOrDefault(p => p.Id == permissionId);
        }
        return new(model, access);
    }
}
