using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Models;
using QuanLyCuDan.Portal;
using Xunit;
using static PortalScope.Tests.PortalFactory;

namespace PortalScope.Tests;

public sealed class OrganizationHttpTests
{
    private static async Task Seed(PortalFactory app)
    {
        await app.Change(async db => {
            db.SchemaVersions.Add(new SchemaVersion { VersionCode = OrganizationReader.SchemaVersion });
            db.PortalOrganizations.AddRange(
                Org(1001, GroupA, "PUBLIC-A", "Hội nhóm công khai A", "ASSOCIATION"),
                Org(1002, GroupA, "PRIVATE-A", "INTERNAL-ORGANIZATION", "PARTY", false),
                Org(1003, GroupB, "PUBLIC-B", "OTHER-LOCALITY-ORGANIZATION", "SOCIAL"),
                Org(1004, GroupA, "INACTIVE-A", "INACTIVE-ORGANIZATION", "SECURITY", active: false));
            db.PortalOrganizationPositions.AddRange(
                new PortalOrganizationPosition { Id = 2001, OrganizationId = 1001, Code = "LEAD", Name = "Chức danh đã cấu hình", MinimumCount = 2, MaximumCount = 3 },
                new PortalOrganizationPosition { Id = 2002, OrganizationId = 1001, Code = "UNKNOWN", Name = "Chức danh cần xác nhận", NeedsClarification = true, SourceNote = "Tài liệu chưa nêu số lượng." },
                new PortalOrganizationPosition { Id = 2003, OrganizationId = 1002, Code = "INTERNAL", Name = "MY-PRIVATE-APPOINTMENT" });
            await db.SaveChangesAsync();
            db.PortalOrganizationAssignments.AddRange(
                Appointment(3001, 2001, 1, true, true),
                Appointment(3002, 2001, 2, true, false),
                Appointment(3003, 2001, 3, false, true),
                Appointment(3004, 2001, 4, true, true), // Corrupt cross-locality data still must not leak.
                Appointment(3005, 2003, 1, true, false),
                Appointment(3006, 2002, 1, true, true, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))));
            db.PortalOrganizationMembers.AddRange(
                Member(4001, 1001, 1, "MY-VERIFIED-ROLE", true), Member(4002, 1002, 1, "MY-INTERNAL-ROLE", true),
                Member(4003, 1001, 2, "OTHER-MEMBER-PRIVATE-ROLE", true), Member(4004, 1003, 1, "OUTSIDE-MEMBERSHIP", true),
                Member(4005, 1004, 1, "INACTIVE-MEMBERSHIP", true));
            var person = await db.Residents.SingleAsync(r => r.Id == 2);
            person.CitizenId = "123456789012"; person.PhoneNumber = "0999988877";
        });
    }

    [Theory]
    [InlineData("/co-cau-to-chuc")]
    [InlineData("/co-cau-to-chuc?groupId=102")]
    [InlineData("/hoi-nhom")]
    [InlineData("/gioi-thieu")]
    public async Task Public_catalog_is_local_active_and_public_only(string url)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Seed(app);
        var page = await Page(c, url);
        Assert.Contains("Hội nhóm công khai A", page);
        foreach (var hidden in new[] { "INTERNAL-ORGANIZATION", "OTHER-LOCALITY-ORGANIZATION", "INACTIVE-ORGANIZATION" }) Assert.DoesNotContain(hidden, page);
        Assert.Contains("community-portal.css", page); Assert.DoesNotContain("admin.css", page);
    }

    [Theory]
    [InlineData(1001, 200)]
    [InlineData(1002, 404)]
    [InlineData(1003, 404)]
    [InlineData(1004, 404)]
    [InlineData(9999, 404)]
    public async Task Public_detail_cannot_be_accessed_by_changing_id(int id, int expected)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Seed(app);
        Assert.Equal(expected, (int)(await c.GetAsync($"/to-chuc/{id}?groupId=102")).StatusCode);
    }

    [Fact]
    public async Task Detail_distinguishes_quotas_unknowns_and_consent_filtered_personnel()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Seed(app);
        var page = await Page(c, "/to-chuc/1001");
        Assert.Contains("2–3 người", page); Assert.Contains("Chưa xác định", page); Assert.Contains("Cần xác nhận", page);
        Assert.Contains("head person", page);
        foreach (var hidden in new[] { "member person", "otherhead person", "outside person", "123456789012", "0999988877", "1990-01-01" }) Assert.DoesNotContain(hidden, page);
        Assert.Contains("Chưa cập nhật thông tin", page); // Future appointment does not populate the second position.
    }

    [Theory]
    [InlineData("head", "/Resident/groups", 200)]
    [InlineData("member", "/Resident/groups", 200)]
    [InlineData("head", "/Resident/my-groups", 200)]
    [InlineData("outside", "/Resident/groups", 403)]
    [InlineData("outside", "/Resident/my-groups", 403)]
    [InlineData("outside", "/Resident/organization/1001", 403)]
    [InlineData("unlinked", "/Resident/groups", 403)]
    [InlineData("newcomer", "/Resident/groups", 403)]
    [InlineData("newcomer", "/Resident/my-groups", 403)]
    public async Task Internal_catalog_reuses_verified_local_household_gate(string user, string url, int expected)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Seed(app); await Login(c, user);
        var response = await c.GetAsync(url + "?residentId=1&groupId=101"); Assert.Equal(expected, (int)response.StatusCode);
        if (user == "head" && url.EndsWith("/groups")) Assert.Contains("INTERNAL-ORGANIZATION", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Two_confirmed_people_with_the_same_name_are_not_collapsed()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Seed(app);
        await app.Change(async db => {
            (await db.Residents.SingleAsync(r => r.Id == 2)).FullName = "head person";
            (await db.PortalOrganizationAssignments.SingleAsync(a => a.Id == 3002)).PublishName = true;
        });
        var page = await Page(c, "/to-chuc/1001");
        Assert.Equal(2, Regex.Matches(page, "<li>head person</li>").Count);
    }

    [Fact]
    public async Task My_groups_ignore_browser_person_id_and_never_list_other_members()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Seed(app); await Login(c, "head");
        var page = await Page(c, "/Resident/my-groups?residentId=2&userId=member");
        Assert.Contains("MY-VERIFIED-ROLE", page); Assert.Contains("MY-INTERNAL-ROLE", page); Assert.Contains("MY-PRIVATE-APPOINTMENT", page);
        foreach (var hidden in new[] { "OTHER-MEMBER-PRIVATE-ROLE", "OUTSIDE-MEMBERSHIP", "INACTIVE-MEMBERSHIP", "Chức danh cần xác nhận" }) Assert.DoesNotContain(hidden, page);
        using var other = app.Browser(); await Login(other, "member"); page = await Page(other, "/Resident/my-groups?residentId=1");
        Assert.Contains("OTHER-MEMBER-PRIVATE-ROLE", page); Assert.DoesNotContain("MY-VERIFIED-ROLE", page);
    }

    [Fact]
    public async Task Internal_detail_still_scopes_id_and_uses_public_name_consent()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Seed(app); await Login(c, "head");
        await Page(c, "/Resident/organization/1002");
        foreach (var id in new[] { 1003, 1004, 9999 }) Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Resident/organization/" + id)).StatusCode);
        var page = await Page(c, "/Resident/organization/1001"); Assert.Contains("head person", page); Assert.DoesNotContain("member person", page);
    }

    [Fact]
    public async Task No_membership_data_shows_empty_state_and_no_self_join_endpoint()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Seed(app); await Login(c, "otherhead");
        Assert.Contains("Chưa cập nhật thông tin tham gia", await Page(c, "/Resident/my-groups"));
        foreach (var url in new[] { "/co-cau-to-chuc", "/Resident/organization/1001", "/Resident/groups" })
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await c.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync("/Resident/organization/1001/join", null)).StatusCode);
        Assert.Equal(5, await app.Read(db => db.PortalOrganizationMembers.CountAsync()));
    }

    [Fact]
    public async Task Existing_database_without_new_schema_marker_keeps_portals_available()
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        Assert.Contains("Thông tin tổ chức đang được cập nhật", await Page(c, "/co-cau-to-chuc"));
        await Page(c, "/"); await Page(c, "/gioi-thieu"); await Page(c, "/hoi-nhom");
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/to-chuc/1001")).StatusCode);
        await Login(c, "head"); Assert.Contains("Chưa cập nhật thông tin tham gia", await Page(c, "/Resident/my-groups"));
    }

    [Fact]
    public async Task Documents_list_is_scoped_by_sender_and_locality_and_requires_permission()
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        await app.Change(async db => {
            foreach (var r in await db.Requests.Where(r => r.Id <= 3).ToListAsync()) {
                r.AttachmentStorageKey = Guid.NewGuid().ToString("N"); r.AttachmentFileName = "document-" + r.Id + ".pdf";
            }
        });
        await Login(c, "head"); var page = await Page(c, "/Resident/documents?userId=member");
        Assert.Contains("document-1.pdf", page); Assert.DoesNotContain("document-2.pdf", page); Assert.DoesNotContain("document-3.pdf", page);
        await app.Change(async db => db.VwEffectivePermissions.RemoveRange(await db.VwEffectivePermissions.Where(p => p.UserId == "head" && p.PermissionCode == "attachments.self.read").ToListAsync()));
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/Resident/documents")).StatusCode);
    }

    [Fact]
    public async Task Resident_account_theme_is_isolated_from_management_login_and_pages()
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        var page = await Page(c, "/Account/Login"); Assert.Contains("admin.css", page); Assert.DoesNotContain("community-portal.css", page);
        page = await Page(c, "/Account/Login?returnUrl=%2FResident"); Assert.Contains("community-portal.css", page); Assert.DoesNotContain("admin.css", page);
        page = await Page(c, "/Account/Register"); Assert.Contains("community-portal.css", page); Assert.DoesNotContain("admin.css", page);
        await Login(c, "admin"); page = await Page(c, "/Admin"); Assert.Contains("admin.css", page); Assert.DoesNotContain("community-portal.css", page);
    }

    [Fact]
    public async Task Public_kind_filters_and_schema_inactive_locality_cannot_bypass_scope()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Seed(app);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/co-cau-to-chuc?kind=SYSTEM")).StatusCode);
        Assert.DoesNotContain("Hội nhóm công khai A", await Page(c, "/co-cau-to-chuc?kind=PARTY"));
        await app.Change(async db => (await db.ResidentialUnits.SingleAsync(g => g.Id == GroupA)).IsActive = false);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync("/co-cau-to-chuc")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync("/to-chuc/1001")).StatusCode);
    }

    private static PortalOrganization Org(long id, long group, string code, string name, string kind, bool publiclyVisible = true, bool active = true) => new() {
        Id=id, ResidentialGroupId=group, Code=code, Name=name, Kind=kind, IsPublic=publiclyVisible, IsActive=active };
    private static PortalOrganizationAssignment Appointment(long id, long position, long resident, bool confirmed, bool publish, DateOnly? starts = null) => new() {
        Id=id, PositionId=position, ResidentId=resident, IsConfirmed=confirmed, PublishName=publish, StartsOn=starts };
    private static PortalOrganizationMember Member(long id, long organization, long resident, string role, bool confirmed) => new() {
        Id=id, OrganizationId=organization, ResidentId=resident, RoleLabel=role, IsConfirmed=confirmed };
    private static async Task<string> Page(HttpClient c, string path) {
        var r = await c.GetAsync(path); var body = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.OK, $"{path}: {(int)r.StatusCode}: {body}"); return WebUtility.HtmlDecode(body);
    }
    private static async Task Login(HttpClient c, string user) {
        var page = await Page(c, "/Account/Login");
        var input = Regex.Matches(page,"<input\\b[^>]*>").Select(m => m.Value).Single(s => s.Contains("name=\"__RequestVerificationToken\""));
        var token = Regex.Match(input,"value=\"([^\"]*)\"").Groups[1].Value;
        var r = await c.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string> { ["UserName"]=user,["Password"]=Password,["__RequestVerificationToken"]=token }));
        Assert.Equal(HttpStatusCode.Redirect,r.StatusCode);
    }
}
