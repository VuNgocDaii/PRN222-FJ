using QuanLyCuDan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuanLyCuDan.Controllers;

[Authorize(AuthenticationSchemes = "Cookies")]
public class SignalRController(MyStockContext context) : BaseController(context)
{
    public IActionResult Index()
    {
        return View();
    }
}
