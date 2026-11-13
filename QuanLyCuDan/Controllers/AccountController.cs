using QuanLyCuDan.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;
public sealed class AccountController(AccountService accounts, AuthService authentication, NavigationService navigation, PersistenceSession persistence) : AuthControllerBase(authentication, navigation, persistence)
{
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Register()
    {
        ViewData["RegistrationGroups"] = await accounts.GetGroupsAsync();
        return View(new RegisterInput());
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> Register(RegisterInput input)
    {
        ViewData["RegistrationGroups"] = await accounts.GetGroupsAsync();
        if (!ModelState.IsValid)
            return View(input);
        try
        {
            await accounts.RegisterAsync(input);
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
            var result = await Authentication.LoginAsync(input, Request.Headers.UserAgent.ToString());
            await SignInCore(HttpContext, result.User, result.Session);
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : await Authentication.GetDestinationAsync(result.User.Id));
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
    public async Task<IActionResult> Profile()
    {
        ViewData["CanSubmitResidentProfile"] = await accounts.CanSubmitProfileAsync(User);
        return View("Profile");
    }

    [Authorize(AuthenticationSchemes = "Cookies"), HttpGet]
    public async Task<IActionResult> ResidentRegistration()
    {
        var user = await accounts.GetApplicantAsync(User);
        await SetApplicantViewDataAsync(user);
        return View(RegistrationProfileInput.Read(user.RegistrationProfile) ?? new RegistrationProfileInput());
    }

    [Authorize(AuthenticationSchemes = "Cookies"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResidentRegistration(RegistrationProfileInput input, string rowVersion)
    {
        var user = await accounts.GetApplicantAsync(User);
        await SetApplicantViewDataAsync(user);
        if (!ModelState.IsValid)
            return View(input);
        try
        {
            await accounts.SubmitProfileAsync(User, input, rowVersion);
        }
        catch (AppFault error) when (error.Status == 400) { ModelState.AddModelError("", error.Message); return View(input); }
        await LogoutCore(HttpContext);
        TempData["RegistrationMessage"] = "Đã gửi thông tin cư dân. Vui lòng chờ quản trị viên xác minh và phê duyệt trước khi đăng nhập lại.";
        return RedirectToAction(nameof(Login));
    }


    [Authorize(AuthenticationSchemes = "Cookies"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(PasswordInput input, string? confirmPassword)
    {
        if (input.NewPassword != confirmPassword)
            ModelState.AddModelError("", "Mật khẩu xác nhận không khớp.");
        if (ModelState.IsValid)
        {
            try
            {
                await Authentication.ChangePasswordAsync(User, input);
                await HttpContext.SignOutAsync("Cookies");
                TempData["RegistrationMessage"] = "Đã đổi mật khẩu. Hãy đăng nhập lại.";
                return RedirectToAction(nameof(Login));
            }
            catch (AppFault error) { ModelState.AddModelError("", error.Message); }
        }
        return await Profile();
    }

    private async Task SetApplicantViewDataAsync(AspNetUser user)
    {
        ViewData["RegistrationGroups"] = await accounts.GetGroupsAsync();
        ViewData["ApplicantName"] = user.FullName;
        ViewData["ApplicantVersion"] = Convert.ToBase64String(user.RowVersion);
    }

}
