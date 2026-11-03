using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class AspNetRole
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string NormalizedName { get; set; } = null!;

    public string? ConcurrencyStamp
    {
        get; set;
    }

    public string DisplayName { get; set; } = null!;

    public string? Description
    {
        get; set;
    }

    public bool IsSystemRole
    {
        get; set;
    }

    public bool IsActive
    {
        get; set;
    }

    public virtual ICollection<AspNetRoleClaim> AspNetRoleClaims { get; set; } = new List<AspNetRoleClaim>();

    public virtual ICollection<UserRoleAssignment> UserRoleAssignments { get; set; } = new List<UserRoleAssignment>();

    public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();

    public virtual ICollection<AspNetUser> Users { get; set; } = new List<AspNetUser>();
}
