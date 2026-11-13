namespace QuanLyCuDan.Models;

public sealed class AdminViewModel
{
    public string Tab { get; set; } = "users";
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public List<AspNetUser> Users { get; set; } = new();
    public List<AspNetRole> Roles { get; set; } = new();
    public List<Permission> Permissions { get; set; } = new();
    public List<Organization> Organizations { get; set; } = [];
    public List<ResidentialUnit> Units { get; set; } = new();
    public List<UserRoleAssignment> Assignments { get; set; } = new();
    public AspNetUser? SelectedUser { get; set; }
    public AspNetRole? SelectedRole { get; set; }
    public Permission? EditingPermission { get; set; }
    public HashSet<string> OwnRoleIds { get; set; } = new();
    public bool CanAssign { get; set; }
}
