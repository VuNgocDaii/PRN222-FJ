using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Portal;

namespace QuanLyCuDan.Controllers;

[AllowAnonymous]
public sealed class CommunityController(QuanLyCuDan.Models.MyStockContext db, IConfiguration configuration) : Controller
{
    private readonly PortalScope portal = new(db, configuration);
    private readonly OrganizationReader organizations = new(db);

    [HttpGet("/gioi-thieu")]
    public async Task<IActionResult> About()
    {
        var locality = await portal.GetAsync();
        ViewData["PortalLocality"] = locality;
        ViewData["LocalityAddress"] = await db.ResidentialUnits.Where(g => g.Id == locality.Id).Select(g => g.Address).SingleAsync();
        return View(await organizations.Directory(locality.Id));
    }

    [HttpGet("/co-cau-to-chuc")]
    public async Task<IActionResult> Index(string? kind)
    {
        var locality = await portal.GetAsync();
        ViewData["PortalLocality"] = locality;
        if (kind != null && !QuanLyCuDan.Models.OrganizationKinds.Labels.ContainsKey(kind)) return NotFound();
        return View(await organizations.Directory(locality.Id, kind: kind));
    }

    [HttpGet("/hoi-nhom")]
    public async Task<IActionResult> Groups()
    {
        var locality = await portal.GetAsync();
        ViewData["PortalLocality"] = locality;
        var model = await organizations.Directory(locality.Id, associationsOnly: true);
        model.Title = "Hội nhóm & đoàn thể";
        return View("Index", model);
    }

    [HttpGet("/to-chuc/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var locality = await portal.GetAsync();
        ViewData["PortalLocality"] = locality;
        var model = await organizations.Detail(id, locality.Id);
        return model == null ? NotFound() : View(model);
    }
}
