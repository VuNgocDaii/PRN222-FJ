using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
[ApiController, Route("api/me")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class MeController(MyStockContext context) : BaseController(context)
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            id = CurrentUserId(User),
            userName = User.Identity!.Name
        });
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> Permissions()
    {
        return Ok(await Effective(CurrentUserId(User)).ToListAsync());
    }
}
