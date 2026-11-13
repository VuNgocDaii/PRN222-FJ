using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Models;
using QuanLyCuDan.Services;

namespace QuanLyCuDan.Controllers;

[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class OrganizationsController(OrganizationService service, NavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet]
    public async Task<IActionResult> Index(long? id) => View("Index", await service.GetAsync(User, id));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(OrganizationInput input) => SubmitForm(
        () => service.CreateAsync(User, input), () => Index(null), () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Member(long id, long residentId, bool active, string? rowVersion) => SubmitForm(
        () => service.SetMemberAsync(User, id, residentId, active, rowVersion), () => Index(id), () => RedirectToAction(nameof(Index), new { id }));
}
