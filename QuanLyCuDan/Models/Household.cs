using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class Household
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

    public string Address { get; set; } = null!;

    public DateOnly RegisteredAt
    {
        get; set;
    }

    public long? HeadResidentId
    {
        get; set;
    }

    public bool IsArchived
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

    public virtual ICollection<Request> RequestOriginalHouseholds { get; set; } = new List<Request>();

    public virtual ICollection<Request> RequestTargetHouseholds { get; set; } = new List<Request>();

    public virtual Resident? Resident
    {
        get; set;
    }

    public virtual ResidentialUnit ResidentialGroup { get; set; } = null!;

    public virtual ICollection<Resident> Residents { get; set; } = new List<Resident>();
}
