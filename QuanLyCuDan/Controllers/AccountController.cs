using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Portal;

namespace QuanLyCuDan.Controllers;
public sealed class AccountController(MyStockContext context, TokenIssuer tokens, IWebHostEnvironment environment, IConfiguration configuration) : AuthControllerBase(context, tokens, environment)
{
    private readonly PortalScope portal = new(context, configuration);
    private const string ProfileFields = "DateOfBirth,Gender,CitizenId,Occupation,Address,ResidenceStatus";

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Register()
    {
        await RegistrationLocality();
        return View(new RegisterInput());
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> Register([Bind(ProfileFields + ",UserName,FullName,PhoneNumber,Password,ConfirmPassword")] RegisterInput input)
    {
        await RegistrationLocality(input);
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
            var isResident = await Effective(result.User.Id).AnyAsync(x => x.PermissionCode == "profile.self.read" && x.ScopeType == "SELF");
            var hasAnnouncements = await Effective(result.User.Id).AnyAsync(x => x.PermissionCode == "announcements.read" && (x.ScopeType == "GROUP" || x.ScopeType == "AREA"));
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : isManager ? "/Admin" : isResident ? "/Resident" : hasAnnouncements ? "/Announcements" : "/Account/Profile");
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
        var user = await db.AspNetUsers.AsNoTracking().SingleAsync(u => u.Id == CurrentUserId(User));
        ViewData["CanSubmitResidentProfile"] = user.ResidentId == null && user.VerificationStatus == "APPROVED" && !await IsAdmin(user.Id);
        return View();
    }

    [Authorize(AuthenticationSchemes = "Cookies"), HttpGet]
    public async Task<IActionResult> ResidentRegistration()
    {
        var user = await ApplicantAccount();
        await RegistrationLocality();
        ViewData["ApplicantName"] = user.FullName;
        ViewData["ApplicantVersion"] = Convert.ToBase64String(user.RowVersion);
        return View(RegistrationProfileInput.Read(user.RegistrationProfile) ?? new RegistrationProfileInput());
    }

    [Authorize(AuthenticationSchemes = "Cookies"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResidentRegistration([Bind(ProfileFields)] RegistrationProfileInput input, string rowVersion)
    {
        var user = await ApplicantAccount();
        await RegistrationLocality(input);
        ViewData["ApplicantName"] = user.FullName;
        ViewData["ApplicantVersion"] = Convert.ToBase64String(user.RowVersion);
        if (!ModelState.IsValid) return View(input);
        SetVersion(user, rowVersion);
        user.RegistrationProfile = System.Text.Json.JsonSerializer.Serialize(input);
        user.VerificationStatus = "PENDING";
        user.ReviewedByUserId = null; user.ReviewedAt = null; user.RejectionReason = null;
        user.PermissionVersion++;
        Audit(user.Id, "REGISTRATION_PROFILE_SUBMITTED", "User", user.Id);
        await db.SaveChangesAsync();
        await LogoutCore(HttpContext);
        TempData["RegistrationMessage"] = "Đã gửi thông tin cư dân. Vui lòng chờ quản trị viên xác minh và phê duyệt trước khi đăng nhập lại.";
        return RedirectToAction(nameof(Login));
    }

    private async Task<AspNetUser> ApplicantAccount()
    {
        var user = await db.AspNetUsers.SingleAsync(u => u.Id == CurrentUserId(User));
        if (user.ResidentId != null || user.VerificationStatus != "APPROVED" || await IsAdmin(user.Id))
            throw new AppFault(403, "Chỉ tài khoản đã duyệt nhưng chưa có hồ sơ cư dân được bổ sung thông tin đăng ký.");
        return user;
    }

    private async Task RegisterCore(RegisterInput input)
    {
        string normalizedName = input.UserName.Trim().ToUpperInvariant();
        if (await db.AspNetUsers.AnyAsync(u => u.NormalizedUserName == normalizedName))
            throw new AppFault(409, "Tên đăng nhập đã được sử dụng.");
        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new AppFault(400, "Nhập họ và tên.");
        input.GroupId = (await portal.GetAsync()).Id;
        input.Address = input.Address.Trim();
        input.CitizenId = string.IsNullOrWhiteSpace(input.CitizenId) ? null : input.CitizenId.Trim();
        input.Occupation = string.IsNullOrWhiteSpace(input.Occupation) ? null : input.Occupation.Trim();

        var user = new AspNetUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = input.UserName.Trim(),
            NormalizedUserName = normalizedName,
            FullName = input.FullName.Trim(),
            PhoneNumber = input.PhoneNumber.Trim(),
            IsActive = true,
            VerificationStatus = "PENDING",
            RegistrationProfile = System.Text.Json.JsonSerializer.Serialize<RegistrationProfileInput>(input),
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

    private async Task RegistrationLocality(RegistrationProfileInput? input = null)
    {
        var locality = await portal.GetAsync();
        ViewData["PortalLocality"] = locality;
        if (input != null)
        {
            // GroupId remains in the saved JSON contract used by independent registration review.
            // It is excluded from browser binding, supplied by the server and validated against the database above.
            input.GroupId = locality.Id;
            ModelState.Remove(nameof(input.GroupId));
            // MVC may skip IValidatableObject when the excluded, required GroupId is initially empty.
            // Revalidate the completed server-owned profile without discarding any model-binding errors.
            var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            System.ComponentModel.DataAnnotations.Validator.TryValidateObject(input,
                new System.ComponentModel.DataAnnotations.ValidationContext(input), errors, true);
            foreach (var error in errors)
                foreach (var member in error.MemberNames.DefaultIfEmpty(""))
                    if (!ModelState.TryGetValue(member, out var state) || state.Errors.Count == 0)
                        ModelState.AddModelError(member, error.ErrorMessage ?? "Thông tin đăng ký không hợp lệ.");
        }
    }
}
