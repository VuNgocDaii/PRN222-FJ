using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class VwPopulationChange
{
    public long RequestId
    {
        get; set;
    }

    public string Code { get; set; } = null!;

    public long? ResidentId
    {
        get; set;
    }

    public long AssignedGroupId
    {
        get; set;
    }

    public string ChangeType { get; set; } = null!;

    public long? OriginalHouseholdId
    {
        get; set;
    }

    public long? TargetHouseholdId
    {
        get; set;
    }

    public string? OriginalResidenceStatus
    {
        get; set;
    }

    public string? ResultResidenceStatus
    {
        get; set;
    }

    public string? OriginalRelationship
    {
        get; set;
    }

    public string? ResultRelationship
    {
        get; set;
    }

    public long? OldHeadResidentId
    {
        get; set;
    }

    public long? NewHeadResidentId
    {
        get; set;
    }

    public DateOnly? EffectiveDate
    {
        get; set;
    }

    public DateOnly? EndDate
    {
        get; set;
    }

    public DateTime? ChangeAppliedAt
    {
        get; set;
    }

    public string? ProcessedByUserId
    {
        get; set;
    }

    public DateTime? ProcessedAt
    {
        get; set;
    }
}
