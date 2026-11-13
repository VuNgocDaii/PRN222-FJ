const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { spawnSync } = require('node:child_process');
const base = process.env.AUTH_TEST_URL || 'http://127.0.0.1:5098';
function browser() {
    const jar = new Map();
    return async (path, { method = 'GET', json, form, token, csrf } = {}) => {
        const headers = { Cookie: [...jar].map(([key, value]) => `${key}=${value}`).join('; ') };
        if (json) headers['Content-Type'] = 'application/json';
        if (form) headers['Content-Type'] = 'application/x-www-form-urlencoded';
        if (token) headers.Authorization = 'Bearer ' + token;
        if (csrf) headers['X-CSRF-TOKEN'] = csrf;
        const response = await fetch(base + path, { method, headers, body: json ? JSON.stringify(json) : form ? new URLSearchParams(form) : undefined, redirect: 'manual', signal: AbortSignal.timeout(15000) });
        response.headers.getSetCookie().forEach(value => {
            const pair = value.split(';')[0], at = pair.indexOf('=');
            jar.set(pair.slice(0, at), pair.slice(at + 1));
        });
        return response;
    };
}
async function mvcLogin(request, userName, password) {
    const html = await (await request('/Account/Login')).text();
    const anti = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
    assert.equal((await request('/Account/Login', { method: 'POST', form: { UserName: userName, Password: password, __RequestVerificationToken: anti } })).status, 302);
    const csrf = (await (await request('/api/auth/csrf')).json()).token;
    const tokenResponse = await request('/api/auth/refresh', { method: 'POST', csrf });
    assert.equal(tokenResponse.status, 200);
    return { token: (await tokenResponse.json()).accessToken, csrf };
}
const fixture = 'controller_smoke_' + randomUUID().replaceAll('-', '');
let created = false;
(async () => {
    const admin = browser();
    const credentials = await mvcLogin(admin, process.env.AUTH_TEST_USER || 'admin', process.env.AUTH_TEST_PASSWORD || '12345678');
    const get = async path => {
        const response = await admin(path, credentials);
        assert.equal(response.status, 200, path);
        return response.json();
    };
    const me = await get('/api/me');
    const users = await get('/api/users');
    assert.ok(users.items.length);
    const roles = await get('/api/roles');
    const permissions = await get('/api/permissions');
    assert.ok(roles.length && permissions.length);
    await get('/api/roles/' + roles[0].id + '/permissions');
    await get('/api/users/' + me.id + '/role-assignments');
    await get('/api/role-assignment-units');
    for (const path of ['/Admin', '/Account/Profile', '/Registrations', '/Registrations?status=APPROVED', '/Registrations?status=REJECTED', '/SignalR']) {
        assert.equal((await admin(path)).status, 200, path);
    }
    const systemRole = roles.find(role => role.isSystemRole);
    assert.equal((await admin('/api/roles/' + systemRole.id, { ...credentials, method: 'PUT', json: { displayName: systemRole.displayName, description: systemRole.description, isActive: true, concurrencyStamp: systemRole.concurrencyStamp } })).status, 400);
    assert.equal((await admin('/api/users/' + me.id + '/role-assignments', { ...credentials, method: 'POST', json: { roleId: systemRole.id, scopeType: 'SYSTEM', validFrom: new Date().toISOString() } })).status, 403);
    const own = users.items.find(user => user.id === me.id);
    assert.equal((await admin('/api/users/' + me.id, { ...credentials, method: 'PUT', json: { fullName: own.fullName, isActive: false, rowVersion: own.rowVersion } })).status, 400);
    const visitor = browser();
    const page = await (await visitor('/Account/Register')).text();
    const anti = page.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
    const groupId = page.match(/<option value="(\d+)">/)[1];
    const registered = await visitor('/Account/Register', { method: 'POST', form: { UserName: fixture, FullName: 'Controller Smoke Test', PhoneNumber: '0900000000', DateOfBirth: '1990-01-01', Gender: 'OTHER', Address: 'Fictional test address', GroupId: groupId, Password: 'TestOnly!123456', ConfirmPassword: 'TestOnly!123456', __RequestVerificationToken: anti } });
    assert.equal(registered.status, 302);
    created = true;
    const pending = (await get('/api/users?search=' + fixture)).items[0];
    assert.equal(pending.verificationStatus, 'PENDING');
    assert.equal((await visitor('/Registrations')).status, 302);
    const visitorCsrf = (await (await visitor('/api/auth/csrf')).json()).token;
    assert.equal((await visitor('/api/auth/login', { method: 'POST', csrf: visitorCsrf, json: { userName: fixture, password: 'TestOnly!123456' } })).status, 403);
    const review = await admin('/Registrations', { method: 'POST', csrf: credentials.csrf, form: { UserId: pending.id, RowVersion: pending.rowVersion, Approve: 'false' } });
    assert.equal(review.status, 302);
    assert.equal((await get('/api/users?search=' + fixture)).items[0].verificationStatus, 'REJECTED');
    assert.equal((await admin('/Account/LoginCore')).status, 404, 'Controller helpers are not actions');
    assert.equal((await admin('/Users/UsersCore', credentials)).status, 404);
    await admin('/Account/Logout', { method: 'POST', csrf: credentials.csrf });
    console.log('PASS: admin pages/APIs, protected roles, self-lock/self-assignment guards, registration and rejection without reason, private helpers not routable');
})().catch(error => { console.error(error.message); process.exitCode = 1; }).finally(() => {
    if (!created) return;
    // Delete only the uniquely named account created by this test and its review audit.
    const cleanup = spawnSync('powershell', ['-NoProfile', '-Command', `
        $ErrorActionPreference='Stop'
        $config=Get-Content -Raw QuanLyCuDan/appsettings.json | ConvertFrom-Json
        if(Test-Path QuanLyCuDan/appsettings.Development.json){
            $localConfig=Get-Content -Raw QuanLyCuDan/appsettings.Development.json | ConvertFrom-Json
            if($localConfig.ConnectionStrings.MyStock){$config.ConnectionStrings.MyStock=$localConfig.ConnectionStrings.MyStock}
        }
        $connection=[System.Data.SqlClient.SqlConnection]::new($config.ConnectionStrings.MyStock)
        $connection.Open()
        $command=$connection.CreateCommand()
        $command.CommandText='SET XACT_ABORT ON; BEGIN TRAN; DECLARE @id nvarchar(450)=(SELECT Id FROM AspNetUsers WHERE UserName=@name); DELETE FROM Activities WHERE TargetType=''User'' AND TargetId=@id; DELETE FROM AspNetUsers WHERE Id=@id; COMMIT;'
        [void]$command.Parameters.AddWithValue('@name',$env:CONTROLLER_TEST_FIXTURE)
        [void]$command.ExecuteNonQuery()
        $connection.Dispose()
    `], { env: { ...process.env, CONTROLLER_TEST_FIXTURE: fixture }, encoding: 'utf8' });
    if (cleanup.status !== 0) {
        console.error('Fixture cleanup failed for ' + fixture);
        process.exitCode = 1;
    } else console.log('PASS: temporary registration fixture removed');
});
