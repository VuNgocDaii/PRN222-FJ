using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class ResidentialUnit
{
    public long Id
    {
        get; set;
    }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string UnitType { get; set; } = null!;

    public long? ParentUnitId
    {
        get; set;
    }

    public string? Address
    {
        get; set;
    }

    public bool IsActive
    {
        get; set;
    }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Activity> ActivityFromGroups { get; set; } = new List<Activity>();

    public virtual ICollection<Activity> ActivityToGroups { get; set; } = new List<Activity>();

    public virtual ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();

    public virtual ICollection<FeeCampaign> FeeCampaigns { get; set; } = new List<FeeCampaign>();

    public virtual ICollection<Household> Households { get; set; } = new List<Household>();

    public virtual ICollection<ResidentialUnit> InverseParentUnit { get; set; } = new List<ResidentialUnit>();

    public virtual ResidentialUnit? ParentUnit
    {
        get; set;
    }

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual ICollection<UserRoleAssignment> UserRoleAssignmentResidentialAreas { get; set; } = new List<UserRoleAssignment>();

    public virtual ICollection<UserRoleAssignment> UserRoleAssignmentResidentialGroups { get; set; } = new List<UserRoleAssignment>();
}
