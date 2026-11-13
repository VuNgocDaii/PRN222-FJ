// HTTP smoke against an existing local database. All SQL statements are SELECTs.
// Only normal login/logout writes sessions/tokens; no resident, household, request or announcement fixture is created.
const assert = require('node:assert/strict');
const { test } = require('node:test');
const { spawnSync } = require('node:child_process');
const base = process.env.AUTH_TEST_URL;
const server = process.env.PORTAL_TEST_SQL_SERVER;
const code = process.env.PORTAL_TEST_GROUP_CODE;
const localUser = process.env.PORTAL_TEST_RESIDENT_USER;
const outsideUser = process.env.PORTAL_TEST_OUTSIDE_USER;
const password = process.env.PORTAL_TEST_PASSWORD;
if (!base || !server || !code || !localUser || !outsideUser || !password)
    throw new Error('Set AUTH_TEST_URL, PORTAL_TEST_SQL_SERVER, PORTAL_TEST_GROUP_CODE, PORTAL_TEST_RESIDENT_USER, PORTAL_TEST_OUTSIDE_USER, PORTAL_TEST_PASSWORD explicitly.');
if (!/^[A-Za-z0-9_-]{1,30}$/.test(code)) throw new Error('Use the exact simple locality code verified in the local demo database.');
const literal = s => "N'" + s.replaceAll("'", "''") + "'";
function sql(query) {
    const result = spawnSync('sqlcmd', ['-S', server, '-d', process.env.PORTAL_TEST_DATABASE || 'QuanLyCuDan', '-E', '-C', '-I', '-b', '-l', '5', '-h', '-1', '-y', '4000', '-w', '65535', '-Q', 'SET NOCOUNT ON; ' + query], { encoding: 'utf8', windowsHide: true });
    if (result.status !== 0) throw new Error(result.stderr || result.stdout);
    return JSON.parse(result.stdout.trim() || '[]');
}
function browser() {
    const jar = new Map();
    return async (url, form) => {
        const headers = { Cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; ') };
        if (form) headers['Content-Type'] = 'application/x-www-form-urlencoded';
        const r = await fetch(base + url, { method: form ? 'POST' : 'GET', headers, body: form ? new URLSearchParams(form) : undefined, redirect: 'manual', signal: AbortSignal.timeout(15000) });
        for (const value of r.headers.getSetCookie()) { const pair = value.split(';')[0], i = pair.indexOf('='); jar.set(pair.slice(0, i), pair.slice(i + 1)); }
        return r;
    };
}
const decode = html => html.replace(/&#x([\da-f]+);/gi, (_, c) => String.fromCodePoint(parseInt(c, 16))).replace(/&#(\d+);/g, (_, c) => String.fromCodePoint(+c)).replaceAll('&amp;', '&');
function token(html) {
    const tag = [...html.matchAll(/<input\b[^>]*>/g)].map(m => m[0]).find(s => s.includes('name="__RequestVerificationToken"'));
    assert.ok(tag, 'Missing CSRF token'); return decode(tag.match(/value="([^"]*)"/)[1]);
}
async function page(request, url) { const r = await request(url); assert.equal(r.status, 200, url + ': ' + (r.status !== 200 ? await r.text() : '')); return decode(await r.text()); }
async function login(request, user) {
    const html = await page(request, '/Account/Login');
    const response = await request('/Account/Login', { UserName: user, Password: password, __RequestVerificationToken: token(html) });
    assert.equal(response.status, 302, 'Login failed for configured test user');
}
async function logout(request) { const html = await page(request, '/Account/Profile'); assert.equal((await request('/Account/Logout', { __RequestVerificationToken: token(html) })).status, 302); }

test('Single locality portals against existing SQL Server data', async t => {
    // Native sqlcmd console code pages vary on Windows; transport Vietnamese strings as lossless UTF-16 hex.
    const units = sql(`SELECT g.Id,g.Code,CONVERT(varchar(max),CONVERT(varbinary(max),g.Name),2) AS NameHex FROM ResidentialUnits g JOIN ResidentialUnits a ON a.Id=g.ParentUnitId WHERE g.Code=${literal(code)} AND g.UnitType='GROUP' AND g.IsActive=1 AND a.UnitType='AREA' AND a.IsActive=1 FOR JSON PATH`);
    assert.equal(units.length, 1); const unit = units[0]; unit.Name = Buffer.from(unit.NameHex, 'hex').toString('utf16le');
    const users = sql(`SELECT u.UserName,h.ResidentialGroupId FROM AspNetUsers u JOIN Residents r ON r.Id=u.ResidentId JOIN Households h ON h.Id=r.HouseholdId WHERE u.UserName IN(${literal(localUser)},${literal(outsideUser)}) FOR JSON PATH`);
    assert.equal(users.find(u => u.UserName === localUser)?.ResidentialGroupId, unit.Id);
    assert.notEqual(users.find(u => u.UserName === outsideUser)?.ResidentialGroupId, unit.Id);
    const notices = sql(`SELECT Id,CONVERT(varchar(max),CONVERT(varbinary(max),Title),2) AS TitleHex,ResidentialGroupId,Audience,Status,ReviewStatus FROM Announcements WHERE Status='PUBLISHED' AND ReviewStatus='APPROVED' AND PublishedAt<=SYSUTCDATETIME() AND (ExpiresAt IS NULL OR ExpiresAt>SYSUTCDATETIME()) FOR JSON PATH`).map(n => ({ ...n, Title: Buffer.from(n.TitleHex, 'hex').toString('utf16le') }));
    const own = notices.find(n => n.ResidentialGroupId === unit.Id && n.Audience === 'PUBLIC');
    const foreign = notices.find(n => n.ResidentialGroupId !== unit.Id && n.Audience === 'PUBLIC');
    const visitor = browser(), resident = browser(), outsider = browser();
    await t.test('Public list and forged query stay in the configured locality', async () => {
        const home = await page(visitor, '/?groupId=invalid'); assert.ok(home.includes(unit.Name)); assert.ok(!home.includes('name="groupId"')); assert.ok(!home.includes('Tất cả tổ dân phố'));
        if (!own || !foreign) throw new Error('This smoke case needs existing valid public announcements in both localities.');
        const found = await page(visitor, '/?search=' + encodeURIComponent(own.Title) + '&groupId=' + foreign.ResidentialGroupId); assert.ok(found.includes(own.Title));
        const hidden = await page(visitor, '/?search=' + encodeURIComponent(foreign.Title)); assert.ok(!hidden.includes('/thong-bao/' + foreign.Id + '"'));
    });
    await t.test('Public announcement IDs enforce locality and audience', async () => {
        assert.equal((await visitor('/thong-bao/' + own.Id)).status, 200);
        assert.equal((await visitor('/thong-bao/' + foreign.Id)).status, 404);
        for (const n of notices.filter(n => n.Audience !== 'PUBLIC')) assert.equal((await visitor('/thong-bao/' + n.Id)).status, 404);
    });
    await t.test('Signup has fixed locality, retains validation and CSRF', async () => {
        const signup = await page(visitor, '/Account/Register'); assert.ok(signup.includes(unit.Name)); assert.ok(!signup.includes('name="GroupId"'));
        assert.equal((await visitor('/Account/Register', { UserName: 'invalid!', GroupId: String(foreign.ResidentialGroupId), __RequestVerificationToken: token(signup) })).status, 200);
        assert.equal((await visitor('/Account/Register', {})).status, 400);
    });
    await login(resident, localUser); await login(outsider, outsideUser);
    try {
        await t.test('Verified local resident can load all existing sections with real permission view', async () => {
            for (const section of ['', '/profile', '/household', '/requests', '/feedback', '/fees', '/announcements', '/notifications']) await page(resident, '/Resident' + section);
            const requests = await page(resident, '/Resident/requests'); assert.ok(!requests.includes('name="Input.GroupId"')); assert.ok(requests.includes(unit.Name));
        });
        await t.test('Other locality is refused on direct resident GET and POST', async () => {
            for (const route of ['/Resident', '/Resident/fees', '/Resident/notifications', '/Resident/request/1', '/Resident/request/1/attachment']) assert.equal((await outsider(route)).status, 403);
            const account = await page(outsider, '/Account/Profile');
            assert.equal((await outsider('/Resident/requests', { 'Input.GroupId': String(unit.Id), __RequestVerificationToken: token(account) })).status, 403);
        });
        await t.test('Resident announcement detail also enforces audience and locality', async () => {
            assert.equal((await resident('/Resident/announcement/' + own.Id)).status, 200);
            assert.equal((await resident('/Resident/announcement/' + foreign.Id)).status, 404);
        });
    } finally { await logout(resident); await logout(outsider); }
});
