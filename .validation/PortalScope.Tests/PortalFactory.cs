using System.Net;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Controllers;
using QuanLyCuDan.Models;

namespace PortalScope.Tests;

// All mutations are confined to this per-test in-memory database, never the shared SQL Server.
public sealed class PortalFactory(string? code = "TEST-A", string? type = "NEIGHBORHOOD") : WebApplicationFactory<HomeController>
{
    public const long GroupA = 101, GroupB = 102;
    public const string Password = "PortalTest!123";
    private static readonly string PasswordHash = new PasswordHasher<AspNetUser>().HashPassword(new AspNetUser(), Password);
    private readonly SqliteConnection connection = OpenDatabase();
    private DbContextOptions<MyStockContext> Options => new DbContextOptionsBuilder<MyStockContext>().UseSqlite(connection).AddInterceptors(new SqliteTestClock()).Options;
    private bool cleanedUp;
    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["PortalScope:GroupCode"] = code, ["PortalScope:LocalityType"] = type
        }));
        builder.ConfigureServices(services => {
            services.RemoveAll<MyStockContext>();
            services.AddScoped<MyStockContext>(_ => new PortalTestContext(Options));
            using var db = new PortalTestContext(Options);
            db.Database.EnsureCreated();
            Seed(db);
        });
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions {
        AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("http://localhost")
    });

    public async Task<T> Read<T>(Func<MyStockContext, Task<T>> query)
    {
        using var db = new PortalTestContext(Options);
        return await query(db);
    }
    public async Task Change(Func<MyStockContext, Task> action)
    {
        using var db = new PortalTestContext(Options);
        await action(db);
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !cleanedUp)
        {
            cleanedUp = true;
            using var db = new PortalTestContext(Options);
            foreach (var key in db.Requests.Select(r => r.AttachmentStorageKey).Where(k => k != null).ToList())
            {
                if (!Guid.TryParseExact(key, "N", out _)) continue;
                var file = Path.Combine(Server.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, ".local", "attachments", key!);
                if (File.Exists(file)) File.Delete(file);
            }
        }
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }

    private static void Seed(PortalTestContext db)
    {
        db.ResidentialUnits.AddRange(
            Unit(100, "ROOT", "Test area", "AREA"),
            Unit(GroupA, "TEST-A", "Địa bàn thử nghiệm A", "GROUP", 100),
            Unit(GroupB, "TEST-B", "Địa bàn thử nghiệm B", "GROUP", 100),
            Unit(103, "INACTIVE", "Inactive group", "GROUP", 100, false),
            Unit(200, "INACTIVE-AREA", "Inactive area", "AREA", null, false),
            Unit(201, "INACTIVE-PARENT", "Inactive parent group", "GROUP", 200));
        var users = new[] { "head", "member", "otherhead", "outside", "newcomer", "no_provenance", "wrong_registration", "pending", "unlinked", "admin", "officer" };
        foreach (var name in users)
        {
            var u = new AspNetUser { Id = name, UserName = name, NormalizedUserName = name.ToUpperInvariant(),
                FullName = name + " person", PhoneNumber = "0900000000", IsActive = true,
                VerificationStatus = name == "pending" ? "PENDING" : "APPROVED",
                SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(),
                ReviewedAt = DateTime.UtcNow.AddDays(-1), ReviewedByUserId = "admin", CreatedAt = DateTime.UtcNow };
            u.PasswordHash = PasswordHash;
            if (name is "newcomer" or "wrong_registration") u.RegistrationProfile = JsonSerializer.Serialize(new RegistrationProfileInput {
                GroupId = name == "newcomer" ? GroupA : GroupB, DateOfBirth = new DateOnly(1990, 1, 1),
                Gender = "OTHER", Address = "Test registration address", ResidenceStatus = "RESIDING"
            });
            db.AspNetUsers.Add(u);
        }
        db.SaveChanges();
        db.Households.AddRange(House(1, "HOUSE-A1", GroupA), House(2, "HOUSE-A2", GroupA), House(3, "HOUSE-B", GroupB));
        db.SaveChanges();
        for (var i = 0; i < 8; i++)
        {
            var name = users[i];
            long? house = i switch { 0 or 1 => 1, 2 => 2, 3 => 3, 7 => 1, _ => null };
            db.Residents.Add(new Resident { Id = i + 1, FullName = name + " person", DateOfBirth = new DateOnly(1990, 1, 1),
                Gender = "OTHER", HouseholdId = house, RelationshipToHead = house == null ? null : "Member", PhoneNumber = "0900000000",
                ResidenceStatus = house == null ? "MOVED_OUT" : "RESIDING", CreatedAt = DateTime.UtcNow });
        }
        db.SaveChanges();
        for (var i = 0; i < 8; i++) db.AspNetUsers.Local.Single(u => u.Id == users[i]).ResidentId = i + 1;
        db.Households.Local.Single(h => h.Id == 1).HeadResidentId = 1;
        db.Households.Local.Single(h => h.Id == 2).HeadResidentId = 3;
        db.Households.Local.Single(h => h.Id == 3).HeadResidentId = 4;
        db.AspNetRoles.AddRange(new AspNetRole { Id = "admin-role", Name = "ADMIN", NormalizedName = "ADMIN", DisplayName = "Test admin", IsActive = true },
            new AspNetRole { Id = "resident-role", Name = "RESIDENT", NormalizedName = "RESIDENT", DisplayName = "Test resident", IsActive = true });
        db.UserRoleAssignments.Add(new UserRoleAssignment { Id = 1, UserId = "admin", RoleId = "admin-role", ScopeType = "SYSTEM",
            AssignedByUserId = "admin", AssignedAt = DateTime.UtcNow, ValidFrom = DateTime.UtcNow.AddDays(-1) });
        string[] permissions = ["profile.self.read", "profile.self.update_contact", "households.self.read", "residence_requests.self.read",
            "residence_requests.self.create", "residence_requests.self.cancel", "feedback.self.read", "feedback.self.create", "feedback.self.reply",
            "fees.self.read", "payments.self.read", "announcements.audience.read", "notifications.self.read", "notifications.self.mark_read", "attachments.self.read", "attachments.self.upload"];
        long grant = 1;
        foreach (var user in users.Take(8))
            foreach (var permission in permissions) db.VwEffectivePermissions.Add(Grant(grant++, user, permission, "SELF"));
        foreach (var p in new[] { "accounts.read", "accounts.update", "roles.assign" }) db.VwEffectivePermissions.Add(Grant(grant++, "admin", p, "SYSTEM"));
        foreach (var group in new[] { GroupA, GroupB })
            db.VwEffectivePermissions.Add(Grant(grant++, "officer", "announcements.read", "GROUP", group));

        db.Announcements.AddRange(Notice(1, GroupA, "visible-public", published: DateTime.UtcNow.AddDays(-1)), Notice(2, GroupB, "outside-public"),
            Notice(3, GroupA, "private-residents", "RESIDENTS"), Notice(4, GroupA, "hidden-draft", status: "DRAFT"),
            Notice(5, GroupA, "hidden-expired", expires: DateTime.UtcNow.AddDays(-1)),
            Notice(6, GroupA, "hidden-future", published: DateTime.UtcNow.AddDays(1)),
            Notice(7, GroupA, "hidden-unreviewed", review: "DRAFT"), Notice(8, GroupA, "hidden-unpublished", status: "UNPUBLISHED"),
            Notice(9, GroupA, "hidden-pending", review: "PENDING"));
        for (var i = 10; i < 24; i++) db.Announcements.Add(Notice(i, GroupA, "pagination-" + i));
        db.Requests.AddRange(Request(1, "head", 1, GroupA, "own-feedback"), Request(2, "member", 2, GroupA, "other-person-feedback"),
            Request(3, "head", 1, GroupB, "old-outside-feedback"), Request(4, "outside", 4, GroupB, "outside-feedback"),
            Request(5, "newcomer", 5, GroupA, "newcomer-move", "RESIDENCE", "MOVE_IN", "CANCELLED"),
            Request(6, "newcomer", 5, GroupA, "newcomer-feedback"));
        db.FeeCampaigns.AddRange(new FeeCampaign { Id = 1, Code = "FEE-A", Name = "local-fee", FeeType = "REQUIRED", ResidentialGroupId = GroupA, CreatedByUserId = "admin", IsActive = true },
            new FeeCampaign { Id = 2, Code = "FEE-B", Name = "outside-fee", FeeType = "REQUIRED", ResidentialGroupId = GroupB, CreatedByUserId = "admin", IsActive = true });
        db.Payments.AddRange(new Payment { Id = 1, CampaignId = 1, HouseholdId = 1, AmountDue = 123 },
            new Payment { Id = 2, CampaignId = 1, HouseholdId = 2, AmountDue = 987654 },
            new Payment { Id = 3, CampaignId = 2, HouseholdId = 1, AmountDue = 999999 });
        db.Activities.AddRange(Notification(1, "head", "head-personal"), Notification(2, "member", "member-personal"),
            Notification(3, "head", "hidden-outside-request", "Request", "3"), Notification(4, "head", "hidden-outside-announcement", "Announcement", "2"),
            Notification(5, "head", "hidden-other-payment", "Payment", "2"), Notification(6, "head", "own-request-notification", "Request", "1"),
            Notification(7, "head", "local-announcement-notification", "Announcement", "1"), Notification(8, "head", "local-payment-notification", "Payment", "1"));
        db.SaveChanges();
    }

    private static ResidentialUnit Unit(long id, string code, string name, string type, long? parent = null, bool active = true) =>
        new() { Id = id, Code = code, Name = name, UnitType = type, ParentUnitId = parent, IsActive = active };
    private static Household House(long id, string code, long group) => new() { Id = id, Code = code, ResidentialGroupId = group,
        Address = code + " address", RegisteredAt = new DateOnly(2026, 1, 1), CreatedByUserId = "admin", CreatedAt = DateTime.UtcNow };
    private static VwEffectivePermission Grant(long id, string user, string permission, string scope, long? group = null) =>
        new() { AssignmentId = id, UserId = user, PermissionCode = permission, ScopeType = scope, ResidentialGroupId = group,
            RoleId = "test-role", RoleName = "TEST", ValidFrom = DateTime.UtcNow.AddDays(-1) };
    private static Announcement Notice(long id, long group, string title, string audience = "PUBLIC", string status = "PUBLISHED",
        string review = "APPROVED", DateTime? published = null, DateTime? expires = null) => new() { Id = id, ResidentialGroupId = group,
            Title = title, Body = title + " body", Audience = audience, Status = status, ReviewStatus = review, CreatedByUserId = "admin",
            PublishedByUserId = "admin", PublishedAt = published ?? DateTime.UtcNow.AddDays(-2), ExpiresAt = expires, CreatedAt = DateTime.UtcNow };
    private static Request Request(long id, string user, long resident, long group, string subject, string kind = "FEEDBACK", string type = "FEEDBACK", string status = "PENDING") =>
        new() { Id = id, Code = "REQUEST-" + id, RequestKind = kind, RequestType = type, Source = "RESIDENT", SubmittedByUserId = user, ResidentId = resident,
            AssignedGroupId = group, Subject = subject, Body = subject + " body", Category = kind == "FEEDBACK" ? "OTHER" : null,
            StartDate = kind == "RESIDENCE" ? new DateOnly(2026, 1, 1) : null, Status = status, ClientRequestKey = Guid.NewGuid().ToString("N"), SubmittedAt = DateTime.UtcNow };
    private static Activity Notification(long id, string user, string code, string? type = null, string? target = null) => new() {
        Id = id, ActivityType = "NOTIFICATION", RecipientUserId = user, EventCode = code, TargetType = type, TargetId = target, OccurredAt = DateTime.UtcNow };
}

// Adapt SQL Server defaults, permission view and row versions only in this isolated SQLite test database.
// Production SQL Server constraints and the effective-permission view still require separate verification.
internal sealed class PortalTestContext(DbContextOptions<MyStockContext> options) : MyStockContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<VwEffectivePermission>().ToView(null).ToTable("TestEffectivePermissions").HasKey(p => new { p.AssignmentId, p.PermissionCode });
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
            {
                if (property.GetColumnType() == "nvarchar(max)") property.SetColumnType("TEXT");
                if (property.GetDefaultValueSql()?.Contains("sysutcdatetime", StringComparison.OrdinalIgnoreCase) == true)
                    property.SetDefaultValueSql("CURRENT_TIMESTAMP");
                if (property.Name == "RowVersion") property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
    }
    private void Versions()
    {
        foreach (var e in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
            if (e.Metadata.FindProperty("RowVersion") != null) e.Property("RowVersion").CurrentValue = RandomNumberGenerator.GetBytes(8);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { Versions(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { Versions(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}

// SQLite lacks SQL Server's DateTimeOffset clock translation. Capture that clock as a query constant for tests.
internal sealed class SqliteTestClock : ExpressionVisitor, IQueryExpressionInterceptor
{
    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData) => Visit(queryExpression);
    protected override Expression VisitMember(MemberExpression node) => node.Member.DeclaringType == typeof(DateTimeOffset) && node.Member.Name == "UtcNow"
        ? Expression.Constant(DateTimeOffset.UtcNow) : base.VisitMember(node);
}
