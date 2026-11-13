const assert = require('node:assert/strict');
const base = process.env.AUTH_TEST_URL || 'http://127.0.0.1:5099';
const jar = new Map();
async function request(path, form, cookie) {
    const headers = { Cookie: cookie ?? [...jar].map(([k, v]) => `${k}=${v}`).join('; ') };
    if (form) headers['Content-Type'] = 'application/x-www-form-urlencoded';
    const r = await fetch(base + path, { method: form ? 'POST' : 'GET', headers, body: form ? new URLSearchParams(form) : undefined, redirect: 'manual', signal: AbortSignal.timeout(15000) });
    for (const value of r.headers.getSetCookie()) { const p = value.split(';')[0], at = p.indexOf('='); jar.set(p.slice(0, at), p.slice(at + 1)); }
    return r;
}
const csrf = html => html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
(async () => {
    for (const p of ['/api/auth/login', '/api/auth/refresh', '/api/users', '/api/roles', '/api/me', '/api/permissions']) assert.equal((await request(p)).status, 404, p);
    const h = await (await request('/Account/Login')).text();
    assert.equal((await request('/Account/Login', { UserName: 'admin', Password: '12345678' })).status, 400);
    const r = await request('/Account/Login', { UserName: process.env.AUTH_TEST_USER || 'admin', Password: process.env.AUTH_TEST_PASSWORD || '12345678', __RequestVerificationToken: csrf(h) });
    assert.equal(r.status, 302);
    const cookie = r.headers.getSetCookie().find(c => c.startsWith('qlcd.auth='));
    assert.match(cookie, /httponly/i); assert.match(cookie, /samesite=strict/i); assert.match(cookie, /path=\//i);
    const jwt = jar.get('qlcd.auth'); assert.equal(jwt.split('.').length, 3);
    const header = JSON.parse(Buffer.from(jwt.split('.')[0], 'base64url')), claims = JSON.parse(Buffer.from(jwt.split('.')[1], 'base64url'));
    assert.equal(header.alg, 'HS256'); assert.equal(claims.iss, 'QuanLyCuDan'); assert.equal(claims.aud, 'QuanLyCuDan.Web');
    assert.ok(claims.sid && claims.sub && claims.exp > Date.now() / 1000);
    assert.equal((await request('/Admin')).status, 200);
    const parts = jwt.split('.'); parts[2] = (parts[2][0] === 'A' ? 'B' : 'A') + parts[2].slice(1);
    assert.equal((await request('/Admin', undefined, 'qlcd.auth=' + parts.join('.'))).status, 302);
    const profile = await (await request('/Account/Profile')).text();
    assert.equal((await request('/Account/Logout', {})).status, 400);
    assert.equal((await request('/Account/Logout', { __RequestVerificationToken: csrf(profile) })).status, 302);
    assert.equal((await request('/Admin', undefined, 'qlcd.auth=' + jwt)).status, 302);
    console.log('PASS: no API routes, MVC login, JWT HttpOnly cookie, signed claims, signature rejection, CSRF and logout revocation');
})().catch(e => { console.error(e); process.exitCode = 1; });
