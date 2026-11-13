using QuanLyCuDan.Services;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class UsersController(UserService service, NavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? search = null)
    {
        var model = await service.ListAsync(User, page, search);
        ViewData["Search"] = model.Search; ViewData["Page"] = model.Page; ViewData["Total"] = model.Total;
        return View("Index", model.Items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(UserInput input) => SubmitForm(async () => { await service.CreateAsync(User, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Update(string id, UserEdit input) => SubmitForm(async () => { await service.UpdateAsync(User, id, input); }, () => Index(), () => RedirectToAction(nameof(Index)));

}
