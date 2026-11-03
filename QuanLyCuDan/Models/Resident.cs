using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class Resident
{
    public long Id
    {
        get; set;
    }

    public string FullName { get; set; } = null!;

    public string? CitizenId
    {
        get; set;
    }

    public DateOnly DateOfBirth
    {
        get; set;
    }

    public string Gender { get; set; } = null!;

    public string? PhoneNumber
    {
        get; set;
    }

    public string? Occupation
    {
        get; set;
    }

    public long? HouseholdId
    {
        get; set;
    }

    public string? RelationshipToHead
    {
        get; set;
    }

    public string ResidenceStatus { get; set; } = null!;

    public DateOnly? MovedInAt
    {
        get; set;
    }

    public DateOnly? MovedOutAt
    {
        get; set;
    }

    public DateTime CreatedAt
    {
        get; set;
    }

    public byte[] RowVersion { get; set; } = null!;

    public virtual AspNetUser? AspNetUser
    {
        get; set;
    }

    public virtual Household? Household { get; set; }

    public virtual ICollection<Household> Households { get; set; } = new List<Household>();

    public virtual ICollection<Request> RequestNewHeadResidents { get; set; } = new List<Request>();

    public virtual ICollection<Request> RequestOldHeadResidents { get; set; } = new List<Request>();

    public virtual ICollection<Request> RequestResidents { get; set; } = new List<Request>();
}
