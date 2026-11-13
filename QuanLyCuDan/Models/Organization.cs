using System.ComponentModel.DataAnnotations;

namespace QuanLyCuDan.Models;

public sealed class Organization
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public long ResidentialGroupId { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public ResidentialUnit ResidentialGroup { get; set; } = null!;
}

public sealed class OrganizationMember
{
    public long OrganizationId { get; set; }
    public long ResidentId { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Organization Organization { get; set; } = null!;
    public Resident Resident { get; set; } = null!;
}

public sealed class OrganizationInput
{
    [Required, StringLength(50), RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Code { get; set; } = "";
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [Range(1, long.MaxValue)] public long ResidentialGroupId { get; set; }
}

public sealed class OrganizationPage
{
    public List<Organization> Organizations { get; set; } = [];
    public List<OrganizationMember> Members { get; set; } = [];
    public List<Resident> Candidates { get; set; } = [];
    public List<ResidentialUnit> Groups { get; set; } = [];
    public long? SelectedId { get; set; }
    public bool CanManage { get; set; }
    public bool CanManageMembers { get; set; }
}
