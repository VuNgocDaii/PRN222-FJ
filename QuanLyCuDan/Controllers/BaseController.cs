using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Controllers;

// Shared controller checks; feature controllers query DbContext directly.
public abstract class BaseController(MyStockContext context) : Controller
{
    protected MyStockContext db { get; } = context;

    protected static string CurrentUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new AppFault(401, "Bạn cần đăng nhập.");
    protected IQueryable<VwEffectivePermission> Effective(string userId) => db.VwEffectivePermissions.AsNoTracking().Where(x => x.UserId == userId);
    protected async Task RequirePermission(ClaimsPrincipal principal, string permission)
    {
        if (!await Effective(CurrentUserId(principal)).AnyAsync(x => x.PermissionCode == permission && x.ScopeType == "SYSTEM"))
            throw new AppFault(403, "Bạn không có quyền thực hiện thao tác này.");
    }

    // Resource checks keep permission and scope on the SAME assignment.
    protected Task<bool> CanAccessGroup(string userId, string permission, long groupId) => Effective(userId).AnyAsync(x => x.PermissionCode == permission && ((x.ScopeType == "GROUP" && x.ResidentialGroupId == groupId) || (x.ScopeType == "AREA" && db.ResidentialUnits.Any(g => g.Id == groupId && g.IsActive && g.ParentUnitId == x.ResidentialAreaId))));
    protected Task<bool> IsAdmin(string userId)
    {
        var now = DateTime.UtcNow;
        return db.UserRoleAssignments.AnyAsync(a => a.UserId == userId && a.Role.Name == "ADMIN" && a.Role.IsActive && a.ScopeType == "SYSTEM" && a.User.IsActive && a.RevokedAt == null && a.ValidFrom <= now && (a.ValidUntil == null || a.ValidUntil > now));
    }

    protected void Audit(string actor, string action, string type, string id) => db.Activities.Add(new Models.Activity { ActivityType = "AUDIT", ActorUserId = actor, EventCode = action, TargetType = type, TargetId = id, OccurredAt = DateTime.UtcNow });


    private static readonly PasswordHasher<AspNetUser> hasher = new();
    protected static string HashPassword(AspNetUser user, string password) => hasher.HashPassword(user, password);
    protected static bool VerifyPassword(AspNetUser user, string password)
    {
        if (user.PasswordHash is not { } stored)
            return false;
        if (!stored.StartsWith("SHA256$", StringComparison.Ordinal))
        {
            try
            {
                return hasher.VerifyHashedPassword(user, stored, password) != PasswordVerificationResult.Failed;
            }
            catch (Exception e) when (e is FormatException or ArgumentException)
            {
                return false;
            }
        }

        var parts = stored.Split('$');
        if (parts.Length != 3 || parts[1].Length != 32 || parts[2].Length != 64)
            return false;
        try
        {
            var salt = Convert.FromHexString(parts[1]);
            var bytes = Encoding.Unicode.GetBytes(password);
            var input = salt.Concat(bytes).ToArray();
            try
            {
                return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(parts[2]), SHA256.HashData(input));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
                CryptographicOperations.ZeroMemory(input);
            }
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [NonAction]
    public static async Task<bool> ValidSession(MyStockContext db, ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == null || !Guid.TryParse(principal.FindFirstValue("sid"), out var sid))
            return false;
        return await db.UserSessions.AsNoTracking().AnyAsync(s => s.Id == sid && s.UserId == id && s.RevokedAt == null && s.AbsoluteExpiresAt > DateTime.UtcNow && s.User.IsActive && s.User.VerificationStatus != "PENDING" && s.User.VerificationStatus != "REJECTED" && (s.User.LockoutEnd == null || s.User.LockoutEnd <= DateTimeOffset.UtcNow));
    }

    protected async Task ProtectLastAdmin(string userId, long? assignmentId)
    {
        if (!await IsAdmin(userId))
            return;
        var now = DateTime.UtcNow;
        if (!await db.UserRoleAssignments.AnyAsync(a => (assignmentId == null ? a.UserId != userId : a.Id != assignmentId) && a.Role.Name == "ADMIN" && a.Role.IsActive && a.ScopeType == "SYSTEM" && a.User.IsActive && (a.User.LockoutEnd == null || a.User.LockoutEnd <= DateTimeOffset.UtcNow) && a.RevokedAt == null && a.ValidFrom <= now && (a.ValidUntil == null || a.ValidUntil > now)))
            throw new AppFault(409, "Không được khóa hoặc thu hồi ADMIN cuối cùng.");
    }

    protected void SetVersion(object entity, string version)
    {
        try
        {
            db.Entry(entity).Property("RowVersion").OriginalValue = Convert.FromBase64String(version);
        }
        catch (FormatException)
        {
            throw new AppFault(400, "RowVersion không hợp lệ.");
        }
    }

    protected async Task<IActionResult> SubmitForm(Func<Task> save, Func<Task<IActionResult>> invalid, Func<IActionResult> success)
    {
        if (ModelState.IsValid)
        {
            try { await save(); TempData["Message"] = "Đã lưu thay đổi."; return success(); }
            catch (AppFault error) when (error.Status is 400 or 404 or 409) { ModelState.AddModelError("", error.Message); }
            catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "Dữ liệu đã thay đổi. Hãy tải lại trang."); }
            catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627 or 1205)
            { ModelState.AddModelError("", "Dữ liệu bị trùng hoặc đã thay đổi. Hãy kiểm tra lại."); }
        }
        db.ChangeTracker.Clear();
        return await invalid();
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = CurrentUserId(User);
            var access = (await Effective(userId).Where(x => x.ScopeType == "SYSTEM").Select(x => x.PermissionCode).ToListAsync()).ToHashSet();
            ViewData["Permissions"] = access;
            ViewData["CanUseResident"] = await Effective(userId).AnyAsync(x => x.ScopeType == "SELF" && x.PermissionCode == "profile.self.read");
            ViewData["CanManageAnnouncements"] = await Effective(userId).AnyAsync(x => (x.ScopeType == "GROUP" || x.ScopeType == "AREA") && x.PermissionCode == "announcements.read");
            ViewData["CanReviewRegistrations"] = access.Contains("accounts.read") && access.Contains("accounts.update") && await IsAdmin(userId);
        }
        await next();
    }
}
