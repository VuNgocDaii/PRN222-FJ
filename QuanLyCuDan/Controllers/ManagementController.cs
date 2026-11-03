using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;

[Authorize(AuthenticationSchemes = "Cookies")]
public sealed class ManagementController(MyStockContext context) : BaseController(context)
{
    [HttpGet("/Management/{module=overview}")]
    public IActionResult Index(string module)
    {
        var selected = ManagementModule.All.FirstOrDefault(x => x.Key == module);
        if (selected is null) return NotFound();
        return View(selected);
    }
}
