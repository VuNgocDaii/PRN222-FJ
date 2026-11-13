namespace QuanLyCuDan.Models;

// Portal-owned, read-only features. Catalog entries never imply an actual appointment or membership.
public sealed class PortalOrganization
{
    public long Id { get; set; }
    public long ResidentialGroupId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Description { get; set; }
    public string? SourceNote { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public ResidentialUnit ResidentialGroup { get; set; } = null!;
    public List<PortalOrganizationPosition> Positions { get; set; } = [];
    public List<PortalOrganizationMember> Members { get; set; } = [];
}

public sealed class PortalOrganizationPosition
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int? MinimumCount { get; set; }
    public int? MaximumCount { get; set; }
    public bool NeedsClarification { get; set; }
    public string? SourceText { get; set; }
    public string? SourceNote { get; set; }
    public PortalOrganization Organization { get; set; } = null!;
    public List<PortalOrganizationAssignment> Assignments { get; set; } = [];
}

public sealed class PortalOrganizationAssignment
{
    public long Id { get; set; }
    public long PositionId { get; set; }
    public long ResidentId { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsConfirmed { get; set; }
    public bool PublishName { get; set; }
    public PortalOrganizationPosition Position { get; set; } = null!;
    public Resident Resident { get; set; } = null!;
}

public sealed class PortalOrganizationMember
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long ResidentId { get; set; }
    public string? RoleLabel { get; set; }
    public DateOnly? JoinedOn { get; set; }
    public DateOnly? LeftOn { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsConfirmed { get; set; }
    public PortalOrganization Organization { get; set; } = null!;
    public Resident Resident { get; set; } = null!;
}
