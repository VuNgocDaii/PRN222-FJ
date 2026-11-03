using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class Payment
{
    public long Id
    {
        get; set;
    }

    public long CampaignId
    {
        get; set;
    }

    public long HouseholdId
    {
        get; set;
    }

    public decimal AmountDue
    {
        get; set;
    }

    public bool IsPaid
    {
        get; set;
    }

    public decimal? PaidAmount
    {
        get; set;
    }

    public string? ReceiptCode
    {
        get; set;
    }

    public string? Method
    {
        get; set;
    }

    public DateTime? PaidAt
    {
        get; set;
    }

    public string? RecordedByUserId
    {
        get; set;
    }

    public bool IsCancelled
    {
        get; set;
    }

    public string? CancellationReason
    {
        get; set;
    }

    public string? ClientPaymentKey
    {
        get; set;
    }

    public DateTime CreatedAt
    {
        get; set;
    }

    public byte[] RowVersion { get; set; } = null!;

    public virtual FeeCampaign Campaign { get; set; } = null!;

    public virtual Household Household { get; set; } = null!;

    public virtual AspNetUser? RecordedByUser
    {
        get; set;
    }
}
