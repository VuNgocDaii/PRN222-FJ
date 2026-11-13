// Run against a local COMPACT-2.0 DEMO database only. Creates and removes its own fixtures.
// PORTAL_TEST_SQL_SERVER must point to the same database server as the running app.
const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const base = process.env.AUTH_TEST_URL || 'http://127.0.0.1:5098';
const server = process.env.PORTAL_TEST_SQL_SERVER;
if (!server) throw new Error('Set PORTAL_TEST_SQL_SERVER explicitly; use a local DEMO database only.');
const fixture = 'portal_' + randomUUID().replaceAll('-', '');
function sql(query) {
    const result = spawnSync('sqlcmd', ['-S', server, '-d', process.env.PORTAL_TEST_DATABASE || 'QuanLyCuDan', '-E', '-C', '-I', '-b', '-h', '-1', '-y', '4000', '-w', '65535', '-Q', 'SET NOCOUNT ON; ' + query], { encoding: 'utf8' });
    if (result.status !== 0) throw new Error(result.stderr || result.stdout);
    return result.stdout.trim();
}
function browser() {
    const jar = new Map();
    return async (url, { method = 'GET', form, multipart } = {}) => {
        const headers = { Cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; ') };
        if (form) headers['Content-Type'] = 'application/x-www-form-urlencoded';
        const r = await fetch(base + url, { method, headers, body: multipart || (form ? new URLSearchParams(form) : undefined), redirect: 'manual', signal: AbortSignal.timeout(20000) });
        r.headers.getSetCookie().forEach(value => { const pair = value.split(';')[0], at = pair.indexOf('='); jar.set(pair.slice(0, at), pair.slice(at + 1)); });
        return r;
    };
}
function input(html, name) {
    const tag = [...html.matchAll(/<input\b[^>]*>/g)].map(m => m[0]).find(t => t.includes(`name="${name}"`));
    assert.ok(tag, 'Missing input: ' + name);
    return (tag.match(/value="([^"]*)"/)?.[1] || '').replace(/&#x([0-9a-f]+);/gi, (_, hex) => String.fromCodePoint(parseInt(hex, 16))).replace(/&amp;/g, '&');
}
const csrf = html => input(html, '__RequestVerificationToken');
const decoded = html => html.replace(/&#x([\da-f]+);/gi, (_, code) => String.fromCodePoint(parseInt(code, 16))).replace(/&#(\d+);/g, (_, code) => String.fromCodePoint(+code));
async function html(request, url) { const r = await request(url); assert.equal(r.status, 200, url + ': ' + (r.status !== 200 ? await r.text() : '')); return r.text(); }
async function login(request, user) {
    const page = await html(request, '/Account/Login');
    const r = await request('/Account/Login', { method: 'POST', form: { UserName: user, Password: '12345678', __RequestVerificationToken: csrf(page) } });
    assert.equal(r.status, 302, 'Login: ' + user);
    return r.headers.get('location');
}
const post = (request, url, token, fields) => request(url, { method: 'POST', form: { __RequestVerificationToken: token, ...fields } });
let initialized = false;
(async () => {
    assert.equal(sql("SELECT COUNT(*) FROM SchemaVersions WHERE VersionCode='COMPACT-2.0'"), '1');
    sql(`SET XACT_ABORT ON; BEGIN TRAN;
        DECLARE @group bigint=(SELECT h.ResidentialGroupId FROM AspNetUsers u JOIN Residents r ON r.Id=u.ResidentId JOIN Households h ON h.Id=r.HouseholdId WHERE u.UserName='dan03');
        IF @group IS NULL THROW 51000,'Expected fictional demo data',1;
        INSERT AspNetUsers(Id,UserName,NormalizedUserName,FullName,PasswordHash,SecurityStamp,ConcurrencyStamp,VerificationStatus,PhoneNumber)
            SELECT '${fixture}','${fixture}',UPPER('${fixture}'),'Portal Test',PasswordHash,CONVERT(nvarchar(36),NEWID()),CONVERT(nvarchar(36),NEWID()),'APPROVED','0900000000' FROM AspNetUsers WHERE UserName='dan03';
        INSERT Households(Code,ResidentialGroupId,Address,RegisteredAt,CreatedByUserId) VALUES('${fixture.slice(0,30)}',@group,'Fictional portal test address','2026-01-01','${fixture}');
        DECLARE @house bigint=SCOPE_IDENTITY();
        INSERT Residents(FullName,DateOfBirth,Gender,HouseholdId,RelationshipToHead,ResidenceStatus,PhoneNumber) VALUES('Portal Test','1990-01-01','OTHER',@house,N'Chủ hộ','RESIDING','0900000000');
        DECLARE @resident bigint=SCOPE_IDENTITY();
        UPDATE Households SET HeadResidentId=@resident WHERE Id=@house;
        UPDATE AspNetUsers SET ResidentId=@resident WHERE Id='${fixture}';
        INSERT UserRoleAssignments(UserId,RoleId,ScopeType,AssignedByUserId,AssignedAt,ValidFrom)
            SELECT '${fixture}',Id,'SELF','${fixture}',SYSUTCDATETIME(),SYSUTCDATETIME() FROM AspNetRoles WHERE Name='RESIDENT' AND IsActive=1;
        INSERT FeeCampaigns(Code,ResidentialGroupId,Name,FeeType,AmountPerHousehold,CreatedByUserId) VALUES('${fixture.slice(0,30)}',@group,'${fixture}_fee','REQUIRED',432123,'${fixture}');
        DECLARE @campaign bigint=SCOPE_IDENTITY(); INSERT Payments(CampaignId,HouseholdId,AmountDue) VALUES(@campaign,@house,432123);
        INSERT Announcements(ResidentialGroupId,Title,Body,Audience,Status,CreatedByUserId,PublishedByUserId,PublishedAt,ExpiresAt)
        VALUES(@group,'${fixture}_public','Visible public text','PUBLIC','PUBLISHED','${fixture}','${fixture}',DATEADD(day,-1,SYSUTCDATETIME()),NULL),
              (@group,'${fixture}_private','Private resident text','RESIDENTS','PUBLISHED','${fixture}','${fixture}',DATEADD(day,-1,SYSUTCDATETIME()),NULL),
              (@group,'${fixture}_draft','Draft text','PUBLIC','DRAFT','${fixture}',NULL,NULL,NULL),
              (@group,'${fixture}_expired','Expired text','PUBLIC','PUBLISHED','${fixture}','${fixture}',DATEADD(day,-2,SYSUTCDATETIME()),DATEADD(day,-1,SYSUTCDATETIME())),
              (@group,'${fixture}_future','Future text','PUBLIC','PUBLISHED','${fixture}','${fixture}',DATEADD(day,1,SYSUTCDATETIME()),NULL);
        UPDATE Announcements SET ReviewStatus='APPROVED',SubmittedAt=PublishedAt,ReviewedAt=PublishedAt,ReviewedByUserId=PublishedByUserId WHERE CreatedByUserId='${fixture}' AND Status='PUBLISHED';
        INSERT Activities(ActivityType,RecipientUserId,EventCode,TargetType,TargetId) VALUES('NOTIFICATION','${fixture}','FEEDBACK_REPLIED','Request','1');
        INSERT Activities(ActivityType,ActorUserId,EventCode,TargetType,TargetId) VALUES('AUDIT','${fixture}','${fixture}_audit','Test','0');
        COMMIT;`);
    initialized = true;
    const visitor = browser();
    const publicHtml = await html(visitor, '/?search=' + fixture);
    assert.ok(publicHtml.includes(fixture + '_public'));
    for (const suffix of ['private', 'draft', 'expired', 'future']) assert.ok(!publicHtml.includes(fixture + '_' + suffix), suffix + ' leaked');
    const announcementRows = JSON.parse(sql(`SELECT Id,Title FROM Announcements WHERE CreatedByUserId='${fixture}' FOR JSON PATH`));
    for (const row of announcementRows) assert.equal((await visitor('/thong-bao/' + row.Id)).status, row.Title.endsWith('_public') ? 200 : 404);
    assert.equal((await visitor('/Resident')).status, 302);
    assert.equal((await visitor('/Resident/requests', { method: 'POST', form: {} })).status, 302);
    const admin = browser(); assert.equal(await login(admin, 'admin'), '/Admin');
    // Registration onboarding is covered by registration-onboarding-smoke.cjs.
    // This fixture starts linked so portal checks do not depend on the retired manual-link form.
    await html(admin, '/Registrations?status=APPROVED');
    const resident = browser();
    assert.equal(await login(resident, fixture), '/Resident');
    for (const section of ['', '/profile', '/household', '/requests', '/feedback', '/fees', '/announcements', '/notifications']) await html(resident, '/Resident' + section);
    const privateHtml = await html(resident, '/Resident/announcements');
    assert.ok(privateHtml.includes(fixture + '_private'));
    assert.ok(!privateHtml.includes(fixture + '_draft'));
    assert.ok((await html(resident, '/Resident/fees')).includes(fixture + '_fee'));
    const contact = await html(resident, '/Resident/profile');
    assert.equal((await post(resident, '/Resident/contact', csrf(contact), { PhoneNumber: '0912345678', Email: 'portal@example.test', RowVersion: input(contact,'RowVersion'), ResidentVersion: input(contact,'ResidentVersion') })).status, 302);
    const updatedContact = await html(resident, '/Resident/profile');
    assert.ok(updatedContact.includes('0912345678'));
    assert.equal((await post(resident, '/Resident/contact', csrf(contact), { PhoneNumber: '0999999999', RowVersion: input(contact,'RowVersion'), ResidentVersion: input(contact,'ResidentVersion') })).status, 302);
    assert.ok(!(await html(resident, '/Resident/profile')).includes('0999999999'), 'Stale contact overwrote latest state');
    assert.equal((await resident('/Resident/contact', { method: 'POST', form: { PhoneNumber: '0900000000' } })).status, 400, 'Missing CSRF');
    let page = await html(resident, '/Resident/requests');
    const requestForm = { 'Input.Kind': 'RESIDENCE', 'Input.Type': 'TEMPORARY_ABSENCE', 'Input.Subject': 'Portal test absence', 'Input.Body': 'Fictional test request body', 'Input.StartDate': '2026-11-09', 'Input.ClientKey': randomUUID().replaceAll('-','') };
    let r = await post(resident, '/Resident/requests', csrf(page), requestForm);
    assert.equal(r.status,302,await r.text()); const requestUrl = r.headers.get('location');
    r = await post(resident, '/Resident/requests', csrf(page), requestForm);
    assert.equal(r.status,302); assert.equal(r.headers.get('location'),requestUrl,'Idempotent request created duplicate');
    r = await post(resident, '/Resident/requests', csrf(page), {...requestForm, 'Input.Body':'Different payload under same key'});
    assert.equal(r.status,200); assert.ok(decoded(await r.text()).includes('nội dung khác'));
    r = await post(resident, '/Resident/requests', csrf(page), {...requestForm, 'Input.ClientKey':randomUUID().replaceAll('-','')});
    assert.equal(r.status,200); assert.ok(decoded(await r.text()).includes('đang có yêu cầu'));
    const requestPage = await html(resident,requestUrl);
    r = await post(resident,requestUrl+'/cancel',csrf(requestPage),{rowVersion:'AAAAAAAAAAA='}); assert.equal(r.status,409);
    r = await post(resident,requestUrl+'/cancel',csrf(requestPage),{rowVersion:input(requestPage,'rowVersion')}); assert.equal(r.status,302);
    assert.ok(decoded(await html(resident,requestUrl)).includes('Đã hủy'));
    page = await html(resident,'/Resident/feedback');
    r = await post(resident,'/Resident/requests',csrf(page),{'Input.Kind':'FEEDBACK','Input.Type':'ENVIRONMENT','Input.Subject':'Portal test feedback','Input.Body':'<script>alert(1)</script> test feedback','Input.ClientKey':randomUUID().replaceAll('-','')});
    assert.equal(r.status,302,await r.text()); const feedbackUrl=r.headers.get('location');
    const feedbackPage=await html(resident,feedbackUrl);
    assert.ok(!feedbackPage.includes('<script>alert(1)</script>'));
    const upload=new FormData(); upload.set('__RequestVerificationToken',csrf(feedbackPage)); upload.set('rowVersion',input(feedbackPage,'rowVersion')); upload.set('file',new Blob(['%PDF-1.4\n%%EOF'],{type:'application/pdf'}),'test.pdf');
    r=await resident(feedbackUrl+'/attachment',{method:'POST',multipart:upload}); assert.equal(r.status,302,await r.text());
    r=await resident(feedbackUrl+'/attachment'); assert.equal(r.status,200); assert.equal(r.headers.get('x-content-type-options'),'nosniff'); assert.equal(await r.text(),'%PDF-1.4\n%%EOF');
    const replyPage=await html(resident,feedbackUrl), replyForm={body:'A fixture reply message',clientKey:randomUUID().replaceAll('-','')};
    assert.equal((await post(resident,feedbackUrl+'/reply',csrf(replyPage),replyForm)).status,302);
    assert.equal((await post(resident,feedbackUrl+'/reply',csrf(replyPage),replyForm)).status,302);
    assert.equal((await post(resident,feedbackUrl+'/reply',csrf(replyPage),{...replyForm,body:'Changed message'})).status,409);
    assert.equal(sql(`SELECT COUNT(*) FROM Activities WHERE ActorUserId='${fixture}' AND ActivityType='MESSAGE'`),'1');
    assert.equal((await post(resident,feedbackUrl+'/cancel',csrf(replyPage),{rowVersion:'AAAAAAAAAAA='})).status,404);
    const noticePage=await html(resident,'/Resident/notifications'); assert.ok(!noticePage.includes(fixture+'_audit'));
    const notificationId=sql(`SELECT Id FROM Activities WHERE ActivityType='NOTIFICATION' AND RecipientUserId='${fixture}'`);
    assert.equal((await post(resident,'/Resident/notifications/'+notificationId+'/read',csrf(noticePage),{})).status,302);
    assert.equal(sql(`SELECT COUNT(*) FROM Activities WHERE Id=${notificationId} AND ReadAt IS NOT NULL`),'1');
    const outsider=browser(); await login(outsider,'dan02');
    assert.equal((await outsider(feedbackUrl)).status,404); assert.equal((await outsider(feedbackUrl+'/attachment')).status,404);
    const outsiderPage=await html(outsider,'/Resident/notifications');
    assert.equal((await post(outsider,'/Resident/notifications/'+notificationId+'/read',csrf(outsiderPage),{})).status,404);
    const fees=decoded(await html(outsider,'/Resident/fees')); assert.ok(fees.includes('Chỉ chủ hộ hiện tại')); assert.ok(!fees.includes(fixture+'_fee'));
    assert.equal((await admin('/Resident')).status,403);
    console.log('PASS: public visibility, search, resident pages, login routing, contact concurrency, CSRF, create/cancel, idempotency, private feedback/replies, attachments, ownership, head-only fees, notifications, and permission denials');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{
    if(!initialized)return;
    try {
        const keys=sql(`SELECT AttachmentStorageKey FROM Requests WHERE SubmittedByUserId='${fixture}' AND AttachmentStorageKey IS NOT NULL`).split(/\r?\n/).map(k=>k.trim()).filter(k=>/^[a-f0-9]{32}$/.test(k));
        sql(`SET XACT_ABORT ON; BEGIN TRAN;
            DELETE Activities WHERE ActorUserId='${fixture}' OR RecipientUserId='${fixture}' OR (TargetType='User' AND TargetId='${fixture}');
            DELETE Requests WHERE SubmittedByUserId='${fixture}';
            DELETE Payments WHERE HouseholdId IN(SELECT Id FROM Households WHERE CreatedByUserId='${fixture}');
            DELETE FeeCampaigns WHERE CreatedByUserId='${fixture}'; DELETE Announcements WHERE CreatedByUserId='${fixture}';
            UPDATE AspNetUsers SET ResidentId=NULL WHERE Id='${fixture}'; UPDATE Households SET HeadResidentId=NULL WHERE CreatedByUserId='${fixture}';
            DELETE Residents WHERE HouseholdId IN(SELECT Id FROM Households WHERE CreatedByUserId='${fixture}');
            DELETE Households WHERE CreatedByUserId='${fixture}'; DELETE UserRoleAssignments WHERE UserId='${fixture}';
            UPDATE RefreshTokens SET ReplacedByTokenId=NULL WHERE SessionId IN(SELECT Id FROM UserSessions WHERE UserId='${fixture}');
            DELETE RefreshTokens WHERE SessionId IN(SELECT Id FROM UserSessions WHERE UserId='${fixture}');
            DELETE UserSessions WHERE UserId='${fixture}'; DELETE AspNetUsers WHERE Id='${fixture}'; COMMIT;`);
        for(const key of keys){ const file=path.resolve('QuanLyCuDan/.local/attachments',key); if(fs.existsSync(file))fs.unlinkSync(file); }
        console.log('PASS: portal fixtures and private test attachments removed');
    } catch(e){console.error('Cleanup failed for '+fixture,e.message);process.exitCode=1;}
});


