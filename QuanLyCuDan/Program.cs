using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using Microsoft.AspNetCore.DataProtection;
using QuanLyCuDan.Hubs;
using QuanLyCuDan.Controllers;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
var localDevelopment = builder.Environment.IsDevelopment();
var cookiePolicy = localDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
if (localDevelopment)
    builder.Services.AddDataProtection().SetApplicationName("QuanLyCuDan").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".local", "keys")));
// Add services to the container.
builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.ResponseCacheAttribute { NoStore = true, Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.None }));
builder.Services.AddDbContext<MyStockContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("MyStock")));
builder.Services.AddSingleton<TokenIssuer>();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.SecurePolicy = cookiePolicy;
    o.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", o =>
{
    o.LoginPath = "/Account/Login";
    o.Cookie.Name = "qlcd.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = cookiePolicy;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.SlidingExpiration = false;
    o.Events.OnValidatePrincipal = async c =>
    {
        if (!await BaseController.ValidSession(c.HttpContext.RequestServices.GetRequiredService<MyStockContext>(), c.Principal!))
        {
            c.RejectPrincipal();
            await c.HttpContext.SignOutAsync("Cookies");
        }
    };
    o.Events.OnRedirectToLogin = c =>
    {
        if (c.Request.Path.StartsWithSegments("/api"))
            c.Response.StatusCode = 401;
        else
            c.Response.Redirect(c.RedirectUri);
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = c =>
    {
        c.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
}).AddScheme<AuthenticationSchemeOptions, BearerHandler>("Bearer", _ =>
{
});
builder.Services.AddAuthorization();
// SignalR dùng cookie đăng nhập sẵn có của website.
builder.Services.AddSignalR();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("refresh", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (AppFault e)
    {
        await Results.Problem(statusCode: e.Status, title: e.Message).ExecuteAsync(context);
    }
    catch (DbUpdateConcurrencyException)
    {
        await Results.Problem(statusCode: 409, title: "Dữ liệu đã thay đổi. Hãy tải lại.").ExecuteAsync(context);
    }
    catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627 || sql.Number == 1205))
    {
        await Results.Problem(statusCode: 409, title: "Dữ liệu bị trùng hoặc có thao tác đồng thời. Hãy tải lại.").ExecuteAsync(context);
    }
    catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 1205)
    {
        await Results.Problem(statusCode: 409, title: "Có thao tác đồng thời. Hãy thử lại.").ExecuteAsync(context);
    }
    catch (Exception e)
    {
        app.Logger.LogError(e, "Request failed");
        await Results.Problem(statusCode: 500, title: "Không thể xử lý yêu cầu. Kiểm tra kết nối cơ sở dữ liệu hoặc log máy chủ.", detail: localDevelopment ? e.GetBaseException().Message : null).ExecuteAsync(context);
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
app.UseRouting();
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method) && (context.Request.Path.StartsWithSegments("/api/auth") || context.Request.Path.StartsWithSegments("/Account")) && context.Request.Headers.TryGetValue("Origin", out var origin) && origin.ToString() != context.Request.Scheme + "://" + context.Request.Host)
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
