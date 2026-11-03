using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class RefreshToken
{
    public Guid Id
    {
        get; set;
    }

    public Guid SessionId
    {
        get; set;
    }

    public byte[] TokenHash { get; set; } = null!;

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

    public DateTime? RevokedAt
    {
        get; set;
    }

    public Guid? ReplacedByTokenId
    {
        get; set;
    }

    public virtual ICollection<RefreshToken> InverseReplacedByToken { get; set; } = new List<RefreshToken>();

    public virtual RefreshToken? ReplacedByToken
    {
        get; set;
    }

    public virtual UserSession Session { get; set; } = null!;
}
