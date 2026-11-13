using System.ComponentModel.DataAnnotations;

namespace QuanLyCuDan.Auth;


public sealed record LoginInput(
    [Required, StringLength(256)] string UserName,
    [Required, StringLength(256)] string Password);

public sealed record PasswordInput(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(128)] string NewPassword);

public sealed record UserInput(
    [Required, StringLength(256)] string UserName,
    [Required, StringLength(150)] string FullName,
    [Required, MinLength(8), MaxLength(128)] string Password);

public sealed record UserEdit(
    [Required, StringLength(150)] string FullName,
    bool IsActive,
    [Required] string RowVersion);

public sealed record RoleInput(
    [Required, RegularExpression("^[A-Za-z][A-Za-z0-9_]{1,63}$")] string Name,
    [Required, StringLength(150)] string DisplayName,
    [StringLength(500)] string? Description);

public sealed record RoleEdit(
    [Required, StringLength(150)] string DisplayName,
    [StringLength(500)] string? Description,
    bool IsActive,
    [Required] string ConcurrencyStamp);

public sealed record PermissionEdit(
    int[]? PermissionIds,
    [Required] string ConcurrencyStamp);

public sealed record PermissionCreateInput(
    [Required, StringLength(100), RegularExpression("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$")] string Code,
    [Required, StringLength(200)] string Name,
    [Required, StringLength(50), RegularExpression("^[A-Za-z0-9_-]+$")] string Module,
    [StringLength(500)] string? Description,
    [Required, MinLength(1), MaxLength(4)] string[] Scopes);

public sealed record PermissionUpdateInput(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(50), RegularExpression("^[A-Za-z0-9_-]+$")] string Module,
    [StringLength(500)] string? Description,
    [Required, MinLength(1), MaxLength(4)] string[] Scopes,
    [Required] string Version);

public sealed record PermissionDeleteInput(
    [Required] string Version);

public sealed record AssignmentInput(
    [Required] string RoleId,
    [Required] string ScopeType,
    long? UnitId,
    DateTime ValidFrom,
    DateTime? ValidUntil,
    long? OrganizationId = null);

public sealed record RevokeInput(
    [Required, StringLength(1000)] string Reason,
    [Required] string RowVersion);

public sealed class AppFault(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
