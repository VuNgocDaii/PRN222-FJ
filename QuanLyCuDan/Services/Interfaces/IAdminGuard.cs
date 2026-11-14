using System.Security.Claims;

namespace QuanLyCuDan.Services.Interfaces;

// Các kiểm tra dùng chung cho nhóm quản trị: quyền hệ thống và bảo vệ ADMIN cuối cùng.
public interface IAdminGuard
{
    Task RequireAsync(ClaimsPrincipal actor, string permission);
    Task EnsureNotLastAdminAsync(string userId, long? assignmentId);
}
