using QuanLyCuDan.Services;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class PermissionsController(PermissionService service, NavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet]
    public async Task<IActionResult> Index() => View("Index", await service.ListAsync(User));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(PermissionCreateInput input) => SubmitForm(async () => { await service.CreateAsync(User, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Update(int id, PermissionUpdateInput input) => SubmitForm(async () => { await service.UpdateAsync(User, id, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Delete(int id, PermissionDeleteInput input) => SubmitForm(async () => { await service.DeleteAsync(User, id, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

}
