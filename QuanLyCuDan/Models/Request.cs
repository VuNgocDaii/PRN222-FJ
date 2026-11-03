using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class Request
{
    public long Id
    {
        get; set;
    }

    public string Code { get; set; } = null!;

    public string RequestKind { get; set; } = null!;

    public string Source { get; set; } = null!;

    public string SubmittedByUserId { get; set; } = null!;

    public long? ResidentId
    {
        get; set;
    }

    public long AssignedGroupId
    {
        get; set;
    }

    public string RequestType { get; set; } = null!;

    public string? Category
    {
        get; set;
    }

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;

    public DateOnly? StartDate
    {
        get; set;
    }

    public DateOnly? EndDate
    {
        get; set;
    }

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

    public DateTime? ChangeAppliedAt
    {
        get; set;
    }

    public string Status { get; set; } = null!;

    public string? ProcessedByUserId
    {
        get; set;
    }

    public DateTime? ProcessedAt
    {
        get; set;
    }

    public string? ProcessingNote
    {
        get; set;
    }

    public string? AttachmentStorageKey
    {
        get; set;
    }

    public string? AttachmentFileName
    {
        get; set;
    }

    public string? AttachmentContentType
    {
        get; set;
    }

    public long? AttachmentSizeBytes
    {
        get; set;
    }

    public byte[]? AttachmentHash
    {
        get; set;
    }

    public string ClientRequestKey { get; set; } = null!;

    public DateTime SubmittedAt
    {
        get; set;
    }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();

    public virtual ResidentialUnit AssignedGroup { get; set; } = null!;

    public virtual Resident? NewHeadResident
    {
        get; set;
    }

    public virtual Resident? OldHeadResident
    {
        get; set;
    }

    public virtual Household? OriginalHousehold
    {
        get; set;
    }

    public virtual AspNetUser? ProcessedByUser
    {
        get; set;
    }

    public virtual Resident? Resident
    {
        get; set;
    }

    public virtual AspNetUser SubmittedByUser { get; set; } = null!;

    public virtual Household? TargetHousehold
    {
        get; set;
    }
}
