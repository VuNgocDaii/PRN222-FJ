using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class AspNetUser
{
    public string Id { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public string NormalizedUserName { get; set; } = null!;

    public string? Email
    {
        get; set;
    }

    public string? NormalizedEmail
    {
        get; set;
    }

    public bool EmailConfirmed
    {
        get; set;
    }

    public string? PasswordHash
    {
        get; set;
    }

    public string? SecurityStamp
    {
        get; set;
    }

    public string? ConcurrencyStamp
    {
        get; set;
    }

    public string? PhoneNumber
    {
        get; set;
    }

    public bool PhoneNumberConfirmed
    {
        get; set;
    }

    public bool TwoFactorEnabled
    {
        get; set;
    }

    public DateTimeOffset? LockoutEnd
    {
        get; set;
    }

    public bool LockoutEnabled
    {
        get; set;
    }

    public int AccessFailedCount
    {
        get; set;
    }

    public string FullName { get; set; } = null!;

    public bool IsActive
    {
        get; set;
    }

    public long? ResidentId
    {
        get; set;
    }

    public string VerificationStatus { get; set; } = null!;
    public string? RegistrationProfile { get; set; }

    public string? ReviewedByUserId
    {
        get; set;
    }

    public DateTime? ReviewedAt
    {
        get; set;
    }

    public string? RejectionReason
    {
        get; set;
    }

    public int PermissionVersion
    {
        get; set;
    }

    public DateTime CreatedAt
    {
        get; set;
    }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Activity> ActivityActorUsers { get; set; } = new List<Activity>();

    public virtual ICollection<Activity> ActivityRecipientUsers { get; set; } = new List<Activity>();

    public virtual ICollection<Announcement> AnnouncementCreatedByUsers { get; set; } = new List<Announcement>();

    public virtual ICollection<Announcement> AnnouncementPublishedByUsers { get; set; } = new List<Announcement>();

    public virtual ICollection<AspNetUserClaim> AspNetUserClaims { get; set; } = new List<AspNetUserClaim>();

    public virtual ICollection<AspNetUserLogin> AspNetUserLogins { get; set; } = new List<AspNetUserLogin>();

    public virtual ICollection<AspNetUserToken> AspNetUserTokens { get; set; } = new List<AspNetUserToken>();

    public virtual ICollection<FeeCampaign> FeeCampaigns { get; set; } = new List<FeeCampaign>();

    public virtual ICollection<Household> Households { get; set; } = new List<Household>();

    public virtual ICollection<AspNetUser> InverseReviewedByUser { get; set; } = new List<AspNetUser>();

    public virtual ICollection<PasswordResetChallenge> PasswordResetChallengeCreatedByUsers { get; set; } = new List<PasswordResetChallenge>();

    public virtual ICollection<PasswordResetChallenge> PasswordResetChallengeUsers { get; set; } = new List<PasswordResetChallenge>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Request> RequestProcessedByUsers { get; set; } = new List<Request>();

    public virtual ICollection<Request> RequestSubmittedByUsers { get; set; } = new List<Request>();

    public virtual Resident? Resident
    {
        get; set;
    }

    public virtual AspNetUser? ReviewedByUser
    {
        get; set;
    }

    public virtual ICollection<UserRoleAssignment> UserRoleAssignmentAssignedByUsers { get; set; } = new List<UserRoleAssignment>();

    public virtual ICollection<UserRoleAssignment> UserRoleAssignmentUsers { get; set; } = new List<UserRoleAssignment>();

    public virtual ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();

    public virtual ICollection<AspNetRole> Roles { get; set; } = new List<AspNetRole>();
}
