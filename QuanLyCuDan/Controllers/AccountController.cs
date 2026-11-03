using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using Microsoft.EntityFrameworkCore;

namespace QuanLyCuDan.Controllers;
public sealed class AccountController(MyStockContext context, TokenIssuer tokens, IWebHostEnvironment environment) : AuthControllerBase(context, tokens, environment)
{
    [AllowAnonymous, HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterInput());
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> Register(RegisterInput input)
    {
        if (!ModelState.IsValid)
            return View(input);
        try
        {
            await RegisterCore(input);
            TempData["RegistrationMessage"] = "Đăng ký thành công. Tài khoản đang chờ quản trị viên phê duyệt.";
            return RedirectToAction(nameof(Login));
        }
        catch (AppFault error)
        {
            ModelState.AddModelError("", error.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException error) when (error.InnerException is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            ModelState.AddModelError("", "Tên đăng nhập đã được sử dụng.");
        }
        return View(input);
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput input, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(input);
        try
        {
            var result = await LoginCore(input, Request.Headers.UserAgent.ToString());
            await CreateTokensCore(HttpContext, result.Session);
            await SignInCore(HttpContext, result.User, result.Session);
            var isManager = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(Effective(result.User.Id), x => x.PermissionCode == "accounts.read" && x.ScopeType == "SYSTEM");
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : isManager ? "/Admin" : "/Account/Profile");
        }
        catch (AppFault e)
        {
            ModelState.AddModelError("", e.Message);
            return View(input);
        }
    }

    [Authorize(AuthenticationSchemes = "Cookies"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await LogoutCore(HttpContext);
        return RedirectToAction(nameof(Login));
    }

    [Authorize(AuthenticationSchemes = "Cookies"), HttpGet]
    public IActionResult Profile()
    {
        return View();
    }

    private async Task RegisterCore(RegisterInput input)
    {
        string normalizedName = input.UserName.Trim().ToUpperInvariant();
        if (await db.AspNetUsers.AnyAsync(u => u.NormalizedUserName == normalizedName))
            throw new AppFault(409, "Tên đăng nhập đã được sử dụng.");
        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new AppFault(400, "Nhập họ và tên.");

        var user = new AspNetUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = input.UserName.Trim(),
            NormalizedUserName = normalizedName,
            FullName = input.FullName.Trim(),
            PhoneNumber = input.PhoneNumber.Trim(),
            IsActive = true,
            VerificationStatus = "PENDING",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true,
            PermissionVersion = 1,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = HashPassword(user, input.Password);
        // Chưa cấp role và chưa liên kết hồ sơ cư dân khi tự đăng ký.
        db.AspNetUsers.Add(user);
        await db.SaveChangesAsync();
    }
}
