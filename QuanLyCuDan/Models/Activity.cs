using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class Activity
{
    public long Id
    {
        get; set;
    }

    public string ActivityType { get; set; } = null!;

    public long? RequestId
    {
        get; set;
    }

    public string? ActorUserId
    {
        get; set;
    }

    public string? RecipientUserId
    {
        get; set;
    }

    public string EventCode { get; set; } = null!;

    public string? Body
    {
        get; set;
    }

    public string? FromStatus
    {
        get; set;
    }

    public string? ToStatus
    {
        get; set;
    }

    public long? FromGroupId
    {
        get; set;
    }

    public long? ToGroupId
    {
        get; set;
    }

    public string? TargetType
    {
        get; set;
    }

    public string? TargetId
    {
        get; set;
    }

    public string? SafeMetadata
    {
        get; set;
    }

    public string? ClientMessageKey
    {
        get; set;
    }

    public DateTime OccurredAt
    {
        get; set;
    }

    public DateTime? ReadAt
    {
        get; set;
    }

    public DateTime? DispatchedAt
    {
        get; set;
    }

    public int AttemptCount
    {
        get; set;
    }

    public DateTime? NextAttemptAt
    {
        get; set;
    }

    public virtual AspNetUser? ActorUser
    {
        get; set;
    }

    public virtual ResidentialUnit? FromGroup
    {
        get; set;
    }

    public virtual AspNetUser? RecipientUser
    {
        get; set;
    }

    public virtual Request? Request
    {
        get; set;
    }

    public virtual ResidentialUnit? ToGroup
    {
        get; set;
    }
}
