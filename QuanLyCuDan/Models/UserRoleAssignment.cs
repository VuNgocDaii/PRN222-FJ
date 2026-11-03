using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class UserRoleAssignment
{
    public long Id
    {
        get; set;
    }

    public string UserId { get; set; } = null!;

    public string RoleId { get; set; } = null!;

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

    public DateTime? RevokedAt
    {
        get; set;
    }

    public string? RevocationReason
    {
        get; set;
    }

    public string AssignedByUserId { get; set; } = null!;

    public DateTime AssignedAt
    {
        get; set;
    }

    public byte[] RowVersion { get; set; } = null!;

    public virtual AspNetUser AssignedByUser { get; set; } = null!;

    public virtual ResidentialUnit? ResidentialArea
    {
        get; set;
    }

    public virtual ResidentialUnit? ResidentialGroup
    {
        get; set;
    }

    public virtual AspNetRole Role { get; set; } = null!;

    public virtual AspNetUser User { get; set; } = null!;
}
