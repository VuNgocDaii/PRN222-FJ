using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Services;
namespace QuanLyCuDan.Controllers;

public abstract class BaseController(NavigationService navigation, PersistenceSession persistence) : Controller
{
    protected async Task<IActionResult> SubmitForm(Func<Task> save, Func<Task<IActionResult>> invalid, Func<IActionResult> success)
    {
        var returnUrl = Request.HasFormContentType ? Request.Form["returnUrl"].ToString() : "";
        var returnToAdmin = Url.IsLocalUrl(returnUrl) && (returnUrl == "/Admin" || returnUrl.StartsWith("/Admin?", StringComparison.Ordinal));
        if (ModelState.IsValid)
        {
            try { await save(); TempData["Message"] = "Đã lưu thay đổi."; return returnToAdmin ? LocalRedirect(returnUrl) : success(); }
            catch (AppFault error) when (error.Status is 400 or 404 or 409) { ModelState.AddModelError("", error.Message); }
            catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "Dữ liệu đã thay đổi. Hãy tải lại trang."); }
            catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627 or 1205)
            { ModelState.AddModelError("", "Dữ liệu bị trùng hoặc đã thay đổi. Hãy kiểm tra lại."); }
        }
        persistence.ClearTracking();
        if (returnToAdmin)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Dữ liệu không hợp lệ." : e.ErrorMessage).Distinct());
            return LocalRedirect(returnUrl);
        }
        return await invalid();
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var access = await navigation.GetAsync(User);
            ViewData["Permissions"] = access.Permissions;
            ViewData["CanManageOrganizations"] = access.CanManageOrganizations;
            ViewData["CanUseResident"] = access.CanUseResident;
            ViewData["CanManageAnnouncements"] = access.CanManageAnnouncements;
            ViewData["CanReviewRegistrations"] = access.CanReviewRegistrations;
        }
        await next();
    }
}
