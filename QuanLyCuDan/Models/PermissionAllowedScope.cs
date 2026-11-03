using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class PermissionAllowedScope
{
    public int PermissionId
    {
        get; set;
    }

    public string ScopeType { get; set; } = null!;

    public virtual Permission Permission { get; set; } = null!;
}
