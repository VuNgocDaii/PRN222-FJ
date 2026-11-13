using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using Xunit;
using static PortalScope.Tests.PortalFactory;

namespace PortalScope.Tests;

public sealed class PortalHttpTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/?groupId=102")]
    [InlineData("/?GroupId=not-a-number&areaId=100")]
    [InlineData("/Home/Index?groupId=102&page=-1")]
    public async Task Public_list_is_scoped_even_with_forged_query(string url)
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        var page = await Page(c, url);
        Assert.Contains("Địa bàn thử nghiệm A", page);
        Assert.Contains("visible-public", page);
        foreach (var title in new[] { "outside-public", "private-residents", "hidden-" }) Assert.DoesNotContain(title, page);
        Assert.DoesNotContain("name=\"groupId\"", page);
        Assert.DoesNotContain("Tất cả tổ dân phố", page);
    }

    [Theory]
    [InlineData(1, 200)]
    [InlineData(2, 404)]
    [InlineData(3, 404)]
    [InlineData(4, 404)]
    [InlineData(5, 404)]
    [InlineData(6, 404)]
    [InlineData(7, 404)]
    [InlineData(8, 404)]
    [InlineData(9, 404)]
    [InlineData(9999, 404)]
    public async Task Public_detail_checks_group_audience_review_and_dates(int id, int expected)
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        Assert.Equal(expected, (int)(await c.GetAsync($"/thong-bao/{id}?groupId=102")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/Home/Announcement/{id}")).StatusCode);
    }

    [Fact]
    public async Task Public_search_pagination_and_empty_results_remain_scoped()
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        var page = await Page(c, "/?search=pagination&page=2&groupId=102");
        Assert.Contains("Trang 2 / 2", page);
        Assert.DoesNotContain("asp-route-groupId", page);
        Assert.DoesNotContain("outside-public", page);
        page = await Page(c, "/?search=outside-public");
        Assert.Contains("Chưa có thông báo phù hợp", page);
        Assert.DoesNotContain("outside-public body", page);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "NEIGHBORHOOD")]
    [InlineData("MISSING", "NEIGHBORHOOD")]
    [InlineData("TEST-A", "INVALID")]
    [InlineData("INACTIVE", "NEIGHBORHOOD")]
    [InlineData("INACTIVE-PARENT", "VILLAGE")]
    [InlineData("ROOT", "VILLAGE")]
    public async Task Invalid_configuration_fails_closed_without_breaking_login(string? code, string? type)
    {
        using var app = new PortalFactory(code, type); using var c = app.Browser();
        foreach (var url in new[] { "/", "/thong-bao/1", "/Account/Register", "/Home/Privacy" })
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync(url)).StatusCode);
        var login = await Page(c, "/Account/Login");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(c, "/Account/Register", login, Signup())).StatusCode);
        Assert.Equal(0, await app.Read(db => db.AspNetUsers.CountAsync(u => u.UserName == "applicant")));
        await Login(c, "head");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync("/Resident")).StatusCode);
    }

    [Theory]
    [InlineData("102")]
    [InlineData("malformed")]
    [InlineData("")]
    public async Task Signup_ignores_client_group_and_keeps_pending_review_contract(string forged)
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        var page = await Page(c, "/Account/Register");
        Assert.Contains("Địa bàn thử nghiệm A", page);
        Assert.DoesNotContain("name=\"GroupId\"", page);
        var fields = Signup(); fields["GroupId"] = forged;
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, "/Account/Register", page, fields)).StatusCode);
        var user = await app.Read(db => db.AspNetUsers.SingleAsync(u => u.UserName == "applicant"));
        Assert.Equal("PENDING", user.VerificationStatus); Assert.Null(user.ResidentId);
        Assert.Equal(GroupA, RegistrationProfileInput.Read(user.RegistrationProfile)!.GroupId);
        Assert.DoesNotContain(Password, user.RegistrationProfile!);
        Assert.DoesNotContain("Password", user.RegistrationProfile!);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.Equal(0, await app.Read(db => db.UserRoleAssignments.CountAsync(a => a.UserId == user.Id)));
        Assert.Equal(0, await app.Read(db => db.Residents.CountAsync(r => r.FullName == "Applicant person")));
        var login = await Page(c, "/Account/Login");
        Assert.Equal(HttpStatusCode.OK, (await Post(c, "/Account/Login", login, new() { ["UserName"] = "applicant", ["Password"] = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Resident")).StatusCode);
    }

    [Theory]
    [InlineData("DateOfBirth", "2999-01-01")]
    [InlineData("CitizenId", "123")]
    [InlineData("Password", "short")]
    [InlineData("ConfirmPassword", "different")]
    [InlineData("PhoneNumber", "bad")]
    [InlineData("UserName", "@invalid")]
    [InlineData("Gender", "INVALID")]
    [InlineData("Address", "")]
    public async Task Signup_keeps_existing_validation(string field, string value)
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        var page = await Page(c, "/Account/Register"); var fields = Signup(); fields[field] = value;
        Assert.Equal(HttpStatusCode.OK, (await Post(c, "/Account/Register", page, fields)).StatusCode);
        Assert.Equal(0, await app.Read(db => db.AspNetUsers.CountAsync(u => u.FullName == "Applicant person")));
    }

    [Fact]
    public async Task Supplement_uses_server_group_and_retains_review_and_logout()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "unlinked");
        var page = await Page(c, "/Account/ResidentRegistration");
        Assert.DoesNotContain("name=\"GroupId\"", page);
        var fields = Signup(); fields["GroupId"] = "malformed"; fields["rowVersion"] = Input(page, "rowVersion");
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, "/Account/ResidentRegistration", page, fields)).StatusCode);
        var user = await app.Read(db => db.AspNetUsers.SingleAsync(u => u.Id == "unlinked"));
        Assert.Equal("PENDING", user.VerificationStatus); Assert.Null(user.ResidentId);
        Assert.Equal(GroupA, RegistrationProfileInput.Read(user.RegistrationProfile)!.GroupId);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Account/Profile")).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("profile")]
    [InlineData("household")]
    [InlineData("requests")]
    [InlineData("feedback")]
    [InlineData("fees")]
    [InlineData("announcements")]
    [InlineData("notifications")]
    public async Task Verified_local_member_can_use_existing_sections(string section)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "head");
        Assert.Contains("Địa bàn thử nghiệm A", await Page(c, "/Resident/" + section));
    }

    [Theory]
    [InlineData("/Resident")]
    [InlineData("/Resident/profile")]
    [InlineData("/Resident/household")]
    [InlineData("/Resident/requests")]
    [InlineData("/Resident/feedback")]
    [InlineData("/Resident/fees")]
    [InlineData("/Resident/announcements")]
    [InlineData("/Resident/notifications")]
    [InlineData("/Resident/announcement/1")]
    [InlineData("/Resident/request/1")]
    [InlineData("/Resident/request/1/attachment")]
    public async Task Other_locality_cannot_use_any_resident_get(string url)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "outside");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(url + "?GroupId=101")).StatusCode);
    }

    [Theory]
    [InlineData("/Resident/contact")]
    [InlineData("/Resident/requests")]
    [InlineData("/Resident/request/1/cancel")]
    [InlineData("/Resident/request/1/reply")]
    [InlineData("/Resident/request/1/attachment")]
    [InlineData("/Resident/notifications/1/read")]
    public async Task Other_locality_cannot_bypass_gate_with_direct_posts(string url)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "outside");
        var page = await Page(c, "/Account/Profile");
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(c, url, page, RequestFields())).StatusCode);
    }

    [Theory]
    [InlineData("unlinked")]
    [InlineData("no_provenance")]
    [InlineData("wrong_registration")]
    public async Task Missing_link_or_verified_local_registration_does_not_establish_membership(string user)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, user);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/Resident/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/Resident/profile")).StatusCode);
    }

    [Fact]
    public async Task Pending_applicant_cannot_log_in_or_gain_resident_access()
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        var page = await Page(c, "/Account/Login");
        Assert.Equal(HttpStatusCode.OK, (await Post(c, "/Account/Login", page, new() { ["UserName"] = "pending", ["Password"] = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Resident/requests")).StatusCode);
    }

    [Theory]
    [InlineData("overview", 200)]
    [InlineData("profile", 200)]
    [InlineData("requests", 200)]
    [InlineData("household", 403)]
    [InlineData("fees", 403)]
    [InlineData("feedback", 403)]
    [InlineData("announcements", 403)]
    [InlineData("notifications", 403)]
    public async Task Verified_householdless_applicant_has_only_existing_intake_access(string section, int expected)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "newcomer");
        var response = await c.GetAsync("/Resident/" + section); Assert.Equal(expected, (int)response.StatusCode);
        if (expected == 200)
        {
            var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Chưa được xác nhận vào hộ", page);
            Assert.DoesNotContain("name=\"Input.GroupId\"", page);
            Assert.DoesNotContain("href=\"/Resident/fees\"", page);
        }
    }

    [Theory]
    [InlineData("MOVE_IN")]
    [InlineData("ADD_MEMBER")]
    public async Task Householdless_intake_is_local_pending_and_does_not_grant_membership(string type)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "newcomer");
        var page = await Page(c, "/Resident/requests"); var fields = RequestFields(type);
        fields["Input.GroupId"] = "102"; fields["Input.TargetHouseholdCode"] = "HOUSE-A1";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, "/Resident/requests", page, fields)).StatusCode);
        var request = await app.Read(db => db.Requests.SingleAsync(r => r.ClientRequestKey == fields["Input.ClientKey"]));
        Assert.Equal(GroupA, request.AssignedGroupId); Assert.Equal("PENDING", request.Status); Assert.Equal(1L, request.TargetHouseholdId);
        Assert.Null(await app.Read(db => db.Residents.Where(r => r.Id == 5).Select(r => r.HouseholdId).SingleAsync()));
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/Resident/announcements")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Resident/request/6")).StatusCode);
    }

    [Theory]
    [InlineData("TEMPORARY_ABSENCE")]
    [InlineData("CHANGE_HEAD")]
    public async Task Householdless_applicant_cannot_send_member_operations(string type)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "newcomer");
        var page = await Page(c, "/Resident/requests"); var fields = RequestFields(type);
        Assert.Equal(HttpStatusCode.OK, (await Post(c, "/Resident/requests", page, fields)).StatusCode);
        Assert.Equal(0, await app.Read(db => db.Requests.CountAsync(r => r.ClientRequestKey == fields["Input.ClientKey"])));
    }

    [Theory]
    [InlineData("head", "HOUSE-A1", "local-fee")]
    [InlineData("member", "HOUSE-A1", "Chỉ chủ hộ hiện tại")]
    [InlineData("otherhead", "HOUSE-A2", "local-fee")]
    public async Task Household_and_fees_remain_private_and_head_only(string user, string house, string feeText)
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, user);
        var household = await Page(c, "/Resident/household?householdId=3&residentId=4");
        Assert.Contains(house, household); Assert.DoesNotContain("HOUSE-B address", household);
        Assert.DoesNotContain(house == "HOUSE-A1" ? "otherhead person" : "member person", household);
        var fees = await Page(c, "/Resident/fees?householdId=3"); Assert.Contains(feeText, fees); Assert.DoesNotContain("outside-fee", fees);
        if (user != "otherhead") Assert.DoesNotContain("987,654", fees);
        if (user == "member") Assert.DoesNotContain("local-fee", fees);
    }

    [Fact]
    public async Task Request_ownership_scope_and_all_id_mutations_are_checked()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "head");
        var page = await Page(c, "/Resident/feedback");
        Assert.Contains("own-feedback", page); Assert.DoesNotContain("other-person-feedback", page); Assert.DoesNotContain("old-outside-feedback", page);
        foreach (var id in new[] { 2, 3, 4 })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/Resident/request/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/Resident/request/{id}/attachment")).StatusCode);
            foreach (var action in new[] { "reply", "cancel", "attachment" })
                Assert.Equal(HttpStatusCode.NotFound, (await Post(c, $"/Resident/request/{id}/{action}", page,
                    new() { ["body"] = "Forged message", ["clientKey"] = Guid.NewGuid().ToString("N"), ["rowVersion"] = "AAAAAAAAAAA=" })).StatusCode);
        }
    }

    [Fact]
    public async Task Notifications_are_recipient_and_resource_scoped_in_get_and_post()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "head");
        var page = await Page(c, "/Resident/notifications"); Assert.Contains("head-personal", page);
        Assert.Contains("own-request-notification", page); Assert.DoesNotContain("member-personal", page); Assert.DoesNotContain("hidden-", page);
        foreach (var id in new[] { 2, 3, 4, 5 })
            Assert.Equal(HttpStatusCode.NotFound, (await Post(c, $"/Resident/notifications/{id}/read", page, new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, "/Resident/notifications/1/read", page, new())).StatusCode);
        Assert.NotNull(await app.Read(db => db.Activities.Where(a => a.Id == 1).Select(a => a.ReadAt).SingleAsync()));
    }

    [Fact]
    public async Task Requests_ignore_forged_group_reject_foreign_household_and_keep_idempotency_and_cancel()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "head");
        var page = await Page(c, "/Resident/requests"); var fields = RequestFields("MOVE_IN");
        fields["Input.TargetHouseholdCode"] = "HOUSE-B"; fields["Input.GroupId"] = "102";
        Assert.Equal(HttpStatusCode.OK, (await Post(c, "/Resident/requests", page, fields)).StatusCode);
        Assert.Equal(0, await app.Read(db => db.Requests.CountAsync(r => r.ClientRequestKey == fields["Input.ClientKey"])));
        fields = RequestFields(); fields["Input.GroupId"] = "malformed";
        var created = await Post(c, "/Resident/requests", page, fields); Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var url = created.Headers.Location!.ToString();
        Assert.Equal(GroupA, await app.Read(db => db.Requests.Where(r => r.ClientRequestKey == fields["Input.ClientKey"]).Select(r => r.AssignedGroupId).SingleAsync()));
        Assert.Equal(url, (await Post(c, "/Resident/requests", page, fields)).Headers.Location!.ToString());
        fields["Input.Body"] = "A different payload";
        Assert.Equal(HttpStatusCode.OK, (await Post(c, "/Resident/requests", page, fields)).StatusCode);
        var detail = await Page(c, url);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, url + "/cancel", detail, new() { ["rowVersion"] = Input(detail, "rowVersion") })).StatusCode);
        Assert.Contains("Đã hủy", await Page(c, url));
    }

    [Fact]
    public async Task Contact_update_is_self_only_and_stale_versions_are_rejected()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "head");
        var page = await Page(c, "/Resident/profile");
        var fields = new Dictionary<string, string> { ["PhoneNumber"] = "0911111111", ["Email"] = "test@example.invalid",
            ["RowVersion"] = Input(page, "RowVersion"), ["ResidentVersion"] = Input(page, "ResidentVersion"),
            ["ResidentId"] = "4", ["HouseholdId"] = "3", ["GroupId"] = "102" };
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, "/Resident/contact", page, fields)).StatusCode);
        var resident = await app.Read(db => db.Residents.SingleAsync(r => r.Id == 1)); Assert.Equal(1L, resident.HouseholdId); Assert.Equal("0911111111", resident.PhoneNumber);
        Assert.Equal("0900000000", await app.Read(db => db.Residents.Where(r => r.Id == 4).Select(r => r.PhoneNumber).SingleAsync()));
        fields["PhoneNumber"] = "0922222222";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, "/Resident/contact", page, fields)).StatusCode);
        Assert.Equal("0911111111", await app.Read(db => db.Residents.Where(r => r.Id == 1).Select(r => r.PhoneNumber).SingleAsync()));
    }

    [Fact]
    public async Task Own_attachment_roundtrip_and_reply_work_with_csrf_and_ownership()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "head");
        var page = await Page(c, "/Resident/request/1");
        using var data = new MultipartFormDataContent();
        data.Add(new StringContent(Input(page, "__RequestVerificationToken")), "__RequestVerificationToken");
        data.Add(new StringContent(Input(page, "rowVersion")), "rowVersion");
        data.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\n%%EOF")), "file", "test.pdf");
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/Resident/request/1/attachment", data)).StatusCode);
        var download = await c.GetAsync("/Resident/request/1/attachment"); Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("%PDF-1.4\n%%EOF", await download.Content.ReadAsStringAsync());
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        using var other = app.Browser(); await Login(other, "member");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/Resident/request/1/attachment")).StatusCode);
        page = await Page(c, "/Resident/request/1");
        var fields = new Dictionary<string, string> { ["body"] = "<script>alert(1)</script>", ["clientKey"] = Guid.NewGuid().ToString("N") };
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, "/Resident/request/1/reply", page, fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, "/Resident/request/1/reply", page, fields)).StatusCode);
        Assert.Equal(1, await app.Read(db => db.Activities.CountAsync(a => a.ClientMessageKey == fields["clientKey"])));
        var raw = await (await c.GetAsync("/Resident/request/1")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>alert(1)</script>", raw);
    }

    [Fact]
    public async Task Antiforgery_and_existing_permissions_are_not_bypassed()
    {
        using var app = new PortalFactory(); using var c = app.Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/Account/Register", new FormUrlEncodedContent(Signup()))).StatusCode);
        await Login(c, "head");
        foreach (var url in new[] { "/Resident/contact", "/Resident/requests", "/Resident/request/1/reply", "/Resident/request/1/attachment", "/Resident/notifications/1/read" })
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        await app.Change(async db => db.VwEffectivePermissions.RemoveRange(await db.VwEffectivePermissions.Where(p => p.UserId == "head" && p.PermissionCode == "fees.self.read").ToListAsync()));
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/Resident/fees")).StatusCode);
    }

    [Fact]
    public async Task Changing_locality_is_consistent_for_public_and_resident_portals()
    {
        using var app = new PortalFactory("TEST-B", "VILLAGE"); using var c = app.Browser();
        var page = await Page(c, "/"); Assert.Contains("Địa bàn thử nghiệm B", page); Assert.Contains("Thôn", page);
        Assert.Contains("outside-public", page); Assert.DoesNotContain("visible-public", page);
        await Login(c, "head"); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/Resident")).StatusCode);
        using var outside = app.Browser(); await Login(outside, "outside"); await Page(outside, "/Resident");
    }

    [Fact]
    public async Task Deactivated_locality_is_rechecked_on_each_request()
    {
        using var app = new PortalFactory(); using var c = app.Browser(); await Login(c, "head");
        var page = await Page(c, "/Resident/requests");
        await app.Change(async db => (await db.ResidentialUnits.SingleAsync(g => g.Id == GroupA)).IsActive = false);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync("/Resident/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(c, "/Resident/requests", page, RequestFields())).StatusCode);
    }

    [Fact]
    public async Task Management_routes_do_not_acquire_the_portal_scope()
    {
        using var app = new PortalFactory(null, null); using var c = app.Browser(); await Login(c, "admin");
        await Page(c, "/Admin"); await Page(c, "/Registrations");
        using var officer = app.Browser(); await Login(officer, "officer");
        var page = await Page(officer, "/Announcements");
        Assert.Contains("Địa bàn thử nghiệm A", page); Assert.Contains("Địa bàn thử nghiệm B", page);
        Assert.Contains("outside-public", await Page(officer, "/Announcements?search=outside-public"));
    }

    private static Dictionary<string, string> Signup() => new() { ["UserName"] = "applicant", ["FullName"] = "Applicant person", ["PhoneNumber"] = "0901234567",
        ["DateOfBirth"] = "1990-01-01", ["Gender"] = "OTHER", ["Address"] = "Test address", ["ResidenceStatus"] = "TEMPORARY",
        ["Password"] = Password, ["ConfirmPassword"] = Password };
    private static Dictionary<string, string> RequestFields(string type = "TEMPORARY_ABSENCE") => new() { ["Input.Kind"] = "RESIDENCE", ["Input.Type"] = type,
        ["Input.Subject"] = "Portal test request", ["Input.Body"] = "Test request body", ["Input.StartDate"] = "2026-11-13", ["Input.ClientKey"] = Guid.NewGuid().ToString("N") };
    private static async Task<string> Page(HttpClient c, string url)
    {
        var response = await c.GetAsync(url); var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode} {text}");
        return WebUtility.HtmlDecode(text);
    }
    private static string Input(string html, string name)
    {
        var tag = Regex.Matches(html, "<input\\b[^>]*>").Select(m => m.Value).FirstOrDefault(t => t.Contains($"name=\"{name}\""));
        Assert.NotNull(tag); return WebUtility.HtmlDecode(Regex.Match(tag!, "value=\"([^\"]*)\"").Groups[1].Value);
    }
    private static Task<HttpResponseMessage> Post(HttpClient c, string url, string page, Dictionary<string, string> fields)
    {
        var form = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = Input(page, "__RequestVerificationToken") };
        return c.PostAsync(url, new FormUrlEncodedContent(form));
    }
    private static async Task Login(HttpClient c, string name)
    {
        var page = await Page(c, "/Account/Login");
        var response = await Post(c, "/Account/Login", page, new() { ["UserName"] = name, ["Password"] = Password });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
