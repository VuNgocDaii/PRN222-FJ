using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class VwFeeBalance
{
    public long PaymentId
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

    public bool IsCancelled
    {
        get; set;
    }

    public decimal? AmountPaid
    {
        get; set;
    }

    public decimal? RemainingAmount
    {
        get; set;
    }
}
