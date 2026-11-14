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
public sealed class RoleAssignmentsController(IRoleAssignmentService service, INavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet]
    public async Task<IActionResult> Index(string? userId = null)
    {
        var model = await service.ListAsync(User, userId);
        ViewData["Accounts"] = model.Accounts; ViewData["Roles"] = model.Roles; ViewData["Units"] = model.Units; ViewData["UserId"] = model.UserId;
        ViewData["Organizations"] = model.Organizations;
        return View("Index", model.Assignments);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Assign(string userId, AssignmentInput input) => SubmitForm(async () => { await service.AssignAsync(User, userId, input); }, () => Index(userId), () => RedirectToAction(nameof(Index), new { userId }));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Revoke(string userId, long id, RevokeInput input) => SubmitForm(async () => { await service.RevokeAsync(User, userId, id, input); }, () => Index(userId), () => RedirectToAction(nameof(Index), new { userId }));

}
