using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class Permission
{
    public int Id
    {
        get; set;
    }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Module { get; set; } = null!;

    public string? Description
    {
        get; set;
    }

    public virtual ICollection<PermissionAllowedScope> PermissionAllowedScopes { get; set; } = new List<PermissionAllowedScope>();

    public virtual ICollection<AspNetRole> Roles { get; set; } = new List<AspNetRole>();
}
