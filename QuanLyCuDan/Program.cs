using QuanLyCuDan.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using Microsoft.AspNetCore.DataProtection;
using QuanLyCuDan.Hubs;
using QuanLyCuDan.Controllers;

using QuanLyCuDan.Repositories.Implementations;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Implementations;
using QuanLyCuDan.Services.Interfaces;
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
var localDevelopment = builder.Environment.IsDevelopment();
var cookiePolicy = localDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
if (localDevelopment)
    builder.Services.AddDataProtection().SetApplicationName("QuanLyCuDan").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".local", "keys")));
// Add services to the container.
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.ResponseCacheAttribute { NoStore = true, Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.None });
    o.ModelMetadataDetailsProviders.Add(new QuanLyCuDan.Auth.VietnameseMetadataProvider());
    QuanLyCuDan.Auth.VietnameseMetadataProvider.Apply(o.ModelBindingMessageProvider);
});
builder.Services.AddDbContext<MyStockContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("MyStock")));
builder.Services.AddSingleton<TokenIssuer>();
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<PersistenceSession>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IPublicPortalService, PublicPortalService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IResidentService, ResidentService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();
builder.Services.AddScoped<IResidentialUnitRepository, ResidentialUnitRepository>();
builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IPermissionChecker, PermissionChecker>();
builder.Services.AddScoped<IAnnouncementAccessService, AnnouncementAccessService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<INotificationSubscriptionService, NotificationSubscriptionService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IPublicPortalRepository, PublicPortalRepository>();
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IResidentRepository, ResidentRepository>();
builder.Services.AddScoped<IAttachmentStorage, LocalAttachmentStorage>();
builder.Services.AddScoped<IFeedbackNotifier, FeedbackNotifier>();
builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
builder.Services.AddScoped<IPermissionCatalogRepository, PermissionCatalogRepository>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IAdminGuard, AdminGuard>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRoleAssignmentService, RoleAssignmentService>();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.SecurePolicy = cookiePolicy;
    o.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddAuthentication("Cookies").AddScheme<AuthenticationSchemeOptions, JwtCookieHandler>("Cookies", _ => { });
builder.Services.AddAuthorization();
// SignalR dùng cookie đăng nhập sẵn có của website.
builder.Services.AddSignalR();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
static IResult ErrorPage(int status, string message)
{
    var safeMessage = System.Net.WebUtility.HtmlEncode(message);
    return Results.Content($"<!doctype html><html lang='vi'><head><meta charset='utf-8'><title>Lỗi {status}</title></head><body><h1>Không thể thực hiện thao tác</h1><p>{safeMessage}</p><a href='/'>Về trang chủ</a></body></html>", "text/html; charset=utf-8", statusCode: status);
}

var app = builder.Build();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (AppFault e)
    {
        await ErrorPage(e.Status, e.Message).ExecuteAsync(context);
    }
    catch (DbUpdateConcurrencyException)
    {
        await ErrorPage(409, "Dữ liệu đã thay đổi. Hãy tải lại.").ExecuteAsync(context);
    }
    catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627 || sql.Number == 1205))
    {
        await ErrorPage(409, "Dữ liệu bị trùng hoặc có thao tác đồng thời. Hãy tải lại.").ExecuteAsync(context);
    }
    catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 1205)
    {
        await ErrorPage(409, "Có thao tác đồng thời. Hãy thử lại.").ExecuteAsync(context);
    }
    catch (Exception e)
    {
        app.Logger.LogError(e, "Request failed");
        await ErrorPage(500, "Không thể xử lý yêu cầu. Kiểm tra kết nối cơ sở dữ liệu hoặc log máy chủ.").ExecuteAsync(context);
    }
});
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!localDevelopment)
    app.UseHttpsRedirection();
app.UseStaticFiles();
// Trang lỗi tiếng Việt cho các mã 401/403/404... không có nội dung.
app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    if (response.ContentType != null)
        return;
    var message = response.StatusCode switch
    {
        401 => "Vui lòng đăng nhập để tiếp tục.",
        403 => "Bạn chưa được cấp quyền sử dụng chức năng này.",
        404 => "Không tìm thấy trang bạn yêu cầu.",
        _ => "Đã xảy ra lỗi. Vui lòng thử lại."
    };
    response.ContentType = "text/html; charset=utf-8";
    await response.WriteAsync("<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" + message + "</title></head><body style=\"font-family:Segoe UI,sans-serif;text-align:center;padding:3rem 1rem;font-size:1.2rem\"><h1>" + message + "</h1><p><a href=\"/\">Về trang chủ</a></p></body></html>");
});
app.UseRouting();
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method) && context.Request.Path.StartsWithSegments("/Account") && context.Request.Headers.TryGetValue("Origin", out var origin) && origin.ToString() != context.Request.Scheme + "://" + context.Request.Host)
    {
        context.Response.StatusCode = 400;
        return;
    }

    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications", options =>
{
    options.CloseOnAuthenticationExpiration = true;
});
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
