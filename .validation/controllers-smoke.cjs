// Local fictional demo database only. MVC form writes; fixtures removed in finally.
const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { spawnSync } = require('node:child_process');
const base = process.env.AUTH_TEST_URL || 'http://127.0.0.1:5099', server = process.env.PORTAL_TEST_SQL_SERVER;
if (!server) throw Error('Set PORTAL_TEST_SQL_SERVER to the local demo server.');
const name = 'mvc_' + randomUUID().replaceAll('-', '');
function sql(query) {
    const r = spawnSync('sqlcmd', ['-S', server, '-d', 'QuanLyCuDan', '-E', '-C', '-I', '-b', '-h', '-1', '-y', '4000', '-w', '65535', '-Q', 'SET NOCOUNT ON; ' + query], { encoding: 'utf8' });
    if (r.status !== 0) throw Error(r.stderr || r.stdout); return r.stdout.trim();
}
const row = q => JSON.parse(sql(q + ' FOR JSON PATH, WITHOUT_ARRAY_WRAPPER'));
function browser() {
    const jar = new Map();
    return async (path, form, cookie) => {
        const headers = { Cookie: cookie ?? [...jar].map(([k, v]) => `${k}=${v}`).join('; ') };
        if (form) headers['Content-Type'] = 'application/x-www-form-urlencoded';
        const body = form ? new URLSearchParams() : undefined;
        if (form) for (const [k, v] of Object.entries(form)) for (const x of Array.isArray(v) ? v : [v]) body.append(k, x);
        const r = await fetch(base + path, { method: form ? 'POST' : 'GET', headers, body, redirect: 'manual', signal: AbortSignal.timeout(15000) });
        for (const v of r.headers.getSetCookie()) { const p = v.split(';')[0], at = p.indexOf('='); jar.set(p.slice(0, at), p.slice(at + 1)); } return r;
    };
}
const csrf = h => h.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
async function html(request, path) { const r = await request(path), h = await r.text(); assert.equal(r.status, 200, path + ': ' + h.slice(0, 200)); return h; }
async function post(request, path, fields, source = '/Admin') { return request(path, { ...fields, __RequestVerificationToken: csrf(await html(request, source)) }); }
async function login(request, user, password) { const r = await post(request, '/Account/Login', { UserName: user, Password: password }, '/Account/Login'); assert.equal(r.status, 302); return r; }
function version(h, id) { const f = [...h.matchAll(/<form\b[\s\S]*?<\/form>/g)].map(m => m[0]).find(f => f.includes('/Permissions/Update/' + id)); assert.ok(f); return f.match(/name="Version"[^>]*value="([^"]+)"/)[1]; }
(async () => {
    const admin = browser(); await login(admin, 'admin', '12345678');
    for (const p of ['/Users', '/Roles', '/Permissions', '/RoleAssignments']) await html(admin, p);
    assert.equal((await admin('/Users/Create', { UserName: name })).status, 400);
    assert.equal((await post(admin, '/Users/Create', { UserName: name, FullName: 'MVC Test', Password: 'OnlyTest123!' })).status, 302);
    const user = row(`SELECT Id,RowVersion FROM AspNetUsers WHERE UserName='${name}'`);
    assert.equal((await post(admin, '/Users/Update/' + user.Id, { FullName: 'Changed MVC', IsActive: 'true', RowVersion: user.RowVersion })).status, 302);
    assert.equal((await post(admin, '/Users/Update/' + user.Id, { FullName: 'Stale overwrite', IsActive: 'true', RowVersion: user.RowVersion })).status, 200);
    assert.equal(sql(`SELECT FullName FROM AspNetUsers WHERE Id='${user.Id}'`), 'Changed MVC');
    assert.equal((await post(admin, '/Roles/Create', { Name: name, DisplayName: 'MVC Role' })).status, 302);
    let role = row(`SELECT Id,ConcurrencyStamp FROM AspNetRoles WHERE Name=UPPER('${name}')`);
    assert.equal((await post(admin, '/Roles/Update/' + role.Id, { DisplayName: 'Changed MVC Role', IsActive: 'true', ConcurrencyStamp: role.ConcurrencyStamp })).status, 302);
    assert.equal((await post(admin, '/Permissions/Create', { Code: name + '.read', Name: 'MVC Permission', Module: 'MVC', Scopes: ['SELF'] })).status, 302);
    const permission = row(`SELECT Id FROM Permissions WHERE Code='${name}.read'`);
    const oldVersion = version(await html(admin, '/Permissions'), permission.Id);
    assert.equal((await post(admin, '/Permissions/Update/' + permission.Id, { Name: 'Changed Permission', Module: 'MVC', Scopes: ['SELF'], Version: oldVersion })).status, 302);
    assert.equal((await post(admin, '/Permissions/Update/' + permission.Id, { Name: 'Stale Permission', Module: 'MVC', Scopes: ['SELF'], Version: oldVersion })).status, 200);
    assert.equal(sql(`SELECT Name FROM Permissions WHERE Id=${permission.Id}`), 'Changed Permission');
    role = row(`SELECT Id,ConcurrencyStamp FROM AspNetRoles WHERE Id='${role.Id}'`);
    assert.equal((await post(admin, '/Roles/Permissions/' + role.Id, { PermissionIds: [permission.Id], ConcurrencyStamp: role.ConcurrencyStamp })).status, 302);
    assert.equal((await post(admin, '/RoleAssignments/Assign', { userId: user.Id, RoleId: role.Id, ScopeType: 'SELF', ValidFrom: new Date(Date.now() - 60000).toISOString() })).status, 302);
    const assignment = row(`SELECT Id,RowVersion FROM UserRoleAssignments WHERE UserId='${user.Id}' AND RoleId='${role.Id}'`);
    assert.equal((await post(admin, '/RoleAssignments/Revoke/' + assignment.Id, { userId: user.Id, Reason: 'MVC test', RowVersion: assignment.RowVersion })).status, 302);
    role = row(`SELECT Id,ConcurrencyStamp FROM AspNetRoles WHERE Id='${role.Id}'`);
    assert.equal((await post(admin, '/Roles/Permissions/' + role.Id, { ConcurrencyStamp: role.ConcurrencyStamp })).status, 302);
    assert.equal((await post(admin, '/Permissions/Delete/' + permission.Id, { Version: version(await html(admin, '/Permissions'), permission.Id) })).status, 302);
    sql(`UPDATE AspNetUsers SET VerificationStatus='APPROVED' WHERE Id='${user.Id}'`);
    const visitor = browser(), signed = await login(visitor, name, 'OnlyTest123!');
    const oldCookie = signed.headers.getSetCookie().find(c => c.startsWith('qlcd.auth=')).split(';')[0];
    assert.equal((await visitor('/Users')).status, 403);
    assert.equal((await post(visitor, '/Account/ChangePassword', { CurrentPassword: 'OnlyTest123!', NewPassword: 'AnotherTest123!', confirmPassword: 'Mismatch123!' }, '/Account/Profile')).status, 200);
    assert.equal((await post(visitor, '/Account/ChangePassword', { CurrentPassword: 'OnlyTest123!', NewPassword: 'AnotherTest123!', confirmPassword: 'AnotherTest123!' }, '/Account/Profile')).status, 302);
    assert.equal((await visitor('/Account/Profile', undefined, oldCookie)).status, 302);
    await login(visitor, name, 'AnotherTest123!');
    console.log('PASS: MVC account/role/permission CRUD, stale writes, role permissions and clearing, grants/revocation, authorization, password validation and JWT revocation');
})().catch(e => { console.error(e); process.exitCode = 1; }).finally(() => {
    sql(`SET XACT_ABORT ON; BEGIN TRAN;
        DECLARE @u nvarchar(450)=(SELECT Id FROM AspNetUsers WHERE UserName='${name}');
        DECLARE @r nvarchar(450)=(SELECT Id FROM AspNetRoles WHERE Name=UPPER('${name}'));
        DECLARE @p int=(SELECT Id FROM Permissions WHERE Code='${name}.read');
        DELETE Activities WHERE ActorUserId=@u OR TargetId=@u OR TargetId=@r OR TargetId=CONVERT(nvarchar(450),@p) OR TargetId IN(SELECT CONVERT(nvarchar(450),Id) FROM UserRoleAssignments WHERE UserId=@u);
        DELETE UserRoleAssignments WHERE UserId=@u; DELETE RolePermissions WHERE RoleId=@r;
        DELETE AspNetRoles WHERE Id=@r; DELETE PermissionAllowedScopes WHERE PermissionId=@p; DELETE Permissions WHERE Id=@p;
        UPDATE RefreshTokens SET ReplacedByTokenId=NULL WHERE SessionId IN(SELECT Id FROM UserSessions WHERE UserId=@u);
        DELETE RefreshTokens WHERE SessionId IN(SELECT Id FROM UserSessions WHERE UserId=@u); DELETE UserSessions WHERE UserId=@u; DELETE AspNetUsers WHERE Id=@u; COMMIT;`);
    console.log('PASS: MVC form fixtures removed');
});
