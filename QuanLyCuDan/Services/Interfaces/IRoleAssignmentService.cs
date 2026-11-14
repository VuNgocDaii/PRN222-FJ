using QuanLyCuDan.Auth;
using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IRoleAssignmentService
{
    Task<AssignmentListResult> ListAsync(ClaimsPrincipal actor, string? userId);
    Task AssignAsync(ClaimsPrincipal actor, string userId, AssignmentInput input);
    Task RevokeAsync(ClaimsPrincipal actor, string userId, long assignmentId, RevokeInput input);
}
