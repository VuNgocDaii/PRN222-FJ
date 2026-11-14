using QuanLyCuDan.Services;
using QuanLyCuDan.Services.Interfaces;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class RolesController(IRoleService service, INavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await service.ListAsync(User); ViewData["Catalog"] = model.Catalog;
        return View("Index", model.Roles);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(RoleInput input) => SubmitForm(async () => { await service.CreateAsync(User, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Update(string id, RoleEdit input) => SubmitForm(async () => { await service.UpdateAsync(User, id, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Permissions(string id, PermissionEdit input) => SubmitForm(async () => { await service.SetPermissionsAsync(User, id, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

}
