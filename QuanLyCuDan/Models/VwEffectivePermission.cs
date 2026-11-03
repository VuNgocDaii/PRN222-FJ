using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class VwEffectivePermission
{
    public long AssignmentId
    {
        get; set;
    }

    public string UserId { get; set; } = null!;

    public string RoleId { get; set; } = null!;

    public string RoleName { get; set; } = null!;

    public string PermissionCode { get; set; } = null!;

    public string ScopeType { get; set; } = null!;

    public long? ResidentialAreaId
    {
        get; set;
    }

    public long? ResidentialGroupId
    {
        get; set;
    }

    public DateTime ValidFrom
    {
        get; set;
    }

    public DateTime? ValidUntil
    {
        get; set;
    }
}
