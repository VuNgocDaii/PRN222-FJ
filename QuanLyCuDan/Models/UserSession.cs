using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class UserSession
{
    public Guid Id
    {
        get; set;
    }

    public string UserId { get; set; } = null!;

    public string? DeviceLabel
    {
        get; set;
    }

    public DateTime CreatedAt
    {
        get; set;
    }

    public DateTime AbsoluteExpiresAt
    {
        get; set;
    }

    public DateTime? RevokedAt
    {
        get; set;
    }

    public string? RevocationReason
    {
        get; set;
    }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual AspNetUser User { get; set; } = null!;
}
