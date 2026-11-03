using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class PasswordResetChallenge
{
    public Guid Id
    {
        get; set;
    }

    public string UserId { get; set; } = null!;

    public byte[] TokenHash { get; set; } = null!;

    public string CreatedByUserId { get; set; } = null!;

    public DateTime CreatedAt
    {
        get; set;
    }

    public DateTime ExpiresAt
    {
        get; set;
    }

    public DateTime? ConsumedAt
    {
        get; set;
    }

    public virtual AspNetUser CreatedByUser { get; set; } = null!;

    public virtual AspNetUser User { get; set; } = null!;
}
