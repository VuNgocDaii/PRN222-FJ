using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class FeeCampaign
{
    public long Id
    {
        get; set;
    }

    public string Code { get; set; } = null!;

    public long ResidentialGroupId
    {
        get; set;
    }

    public string Name { get; set; } = null!;

    public string? Description
    {
        get; set;
    }

    public string FeeType { get; set; } = null!;

    public DateOnly? DueDate
    {
        get; set;
    }

    public decimal AmountPerHousehold
    {
        get; set;
    }

    public bool IsActive
    {
        get; set;
    }

    public string CreatedByUserId { get; set; } = null!;

    public DateTime CreatedAt
    {
        get; set;
    }

    public byte[] RowVersion { get; set; } = null!;

    public virtual AspNetUser CreatedByUser { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ResidentialUnit ResidentialGroup { get; set; } = null!;
}
