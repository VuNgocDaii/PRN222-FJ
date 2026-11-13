using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;
public sealed record UserListResult(List<AspNetUser> Items, int Total, int Page, string? Search);
public sealed record RoleListResult(List<AspNetRole> Roles, List<Permission> Catalog);
public sealed record AssignmentListResult(List<AspNetUser> Accounts, List<AspNetRole> Roles, List<ResidentialUnit> Units, List<UserRoleAssignment> Assignments, string? UserId, List<Organization> Organizations);
public sealed record AdminPageResult(AdminViewModel? Model, HashSet<string> Permissions);
public sealed record DocumentDownload(byte[] Content, string ContentType, string FileName);
