using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class Announcement
{
    public long Id
    {
        get; set;
    }

    public long ResidentialGroupId
    {
        get; set;
    }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string Audience { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string CreatedByUserId { get; set; } = null!;

    public string? PublishedByUserId
    {
        get; set;
    }

    public DateTime CreatedAt
    {
        get; set;
    }

    public DateTime? PublishedAt
    {
        get; set;
    }

    public DateTime? ExpiresAt
    {
        get; set;
    }

    public string? UnpublishReason
    {
        get; set;
    }

    public byte[] RowVersion { get; set; } = null!;

    public virtual AspNetUser CreatedByUser { get; set; } = null!;

    public virtual AspNetUser? PublishedByUser
    {
        get; set;
    }

    public virtual ResidentialUnit ResidentialGroup { get; set; } = null!;
}
