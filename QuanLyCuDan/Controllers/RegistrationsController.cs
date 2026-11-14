using QuanLyCuDan.Services.Interfaces;
using QuanLyCuDan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using Microsoft.EntityFrameworkCore;

namespace QuanLyCuDan.Controllers;

[Authorize(AuthenticationSchemes = "Cookies")]
public class RegistrationsController(IRegistrationService service, INavigationService navigation, PersistenceSession persistence) : BaseController(navigation, persistence)
{
    [HttpGet]
    public async Task<IActionResult> Index(string status = "PENDING")
    {
        return View(await service.ListAsync(User, status));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Index(RegistrationReviewInput input, string status = "PENDING")
    {
        return Review(input, status);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(RegistrationReviewInput input, string status = "PENDING")
    {
        await service.EnsureReviewerAsync(User);
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu không hợp lệ. Lý do tối đa 1000 ký tự. Hãy tải lại danh sách.";
            return RedirectToAction(nameof(Index), new
            {
                status
            });
        }
        try
        {
            await service.ReviewAsync(User, input);
            TempData["Message"] = input.Approve ? "Đã duyệt tài khoản. Người dùng có thể đăng nhập." : "Đã từ chối đăng ký.";
        }
        catch (AppFault error) when (error.Status == 400 || error.Status == 404 || error.Status == 409)
        {
            TempData["Error"] = error.Message;
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Dữ liệu đã thay đổi. Hãy kiểm tra lại danh sách.";
        }
        return RedirectToAction(nameof(Index), new
        {
            status
        });
    }

}
