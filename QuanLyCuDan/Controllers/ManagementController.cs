using QuanLyCuDan.Services.Interfaces;
using QuanLyCuDan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;

[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class ManagementController(INavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet("/Management/{module=overview}")]
    public IActionResult Index(string module)
    {
        if (module == "announcements") return RedirectToAction("Index", "Announcements");
        var selected = ManagementModule.All.FirstOrDefault(x => x.Key == module);
        if (selected is null) return NotFound();
        return View(selected);
    }
}
