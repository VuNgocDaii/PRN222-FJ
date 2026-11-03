const assert = require('node:assert/strict');
const base = process.env.AUTH_TEST_URL || 'http://127.0.0.1:5098';
const jar = new Map();
async function request(path, { method = 'GET', body, formBody, token, csrf, cookie } = {}) {
    const headers = { Cookie: cookie ?? [...jar].map(([key, value]) => `${key}=${value}`).join('; ') };
    if (body) headers['Content-Type'] = 'application/json';
    if (formBody) headers['Content-Type'] = 'application/x-www-form-urlencoded';
    if (token) headers.Authorization = 'Bearer ' + token;
    if (csrf) headers['X-CSRF-TOKEN'] = csrf;
    const response = await fetch(base + path, { method, headers, body: formBody || (body ? JSON.stringify(body) : undefined), redirect: 'manual', signal: AbortSignal.timeout(15000) });
    for (const value of response.headers.getSetCookie()) {
        const pair = value.split(';')[0];
        const index = pair.indexOf('=');
        jar.set(pair.slice(0, index), pair.slice(index + 1));
    }
    return response;
}
async function login() {
    const csrf = (await (await request('/api/auth/csrf')).json()).token;
    const response = await request('/api/auth/login', { method: 'POST', csrf, body: { userName: process.env.AUTH_TEST_USER || 'admin', password: process.env.AUTH_TEST_PASSWORD || '12345678' } });
    assert.equal(response.status, 200, 'API login must succeed');
    const data = await response.json();
    assert.equal(data.tokenType, 'Bearer');
    assert.equal(data.accessToken.split('.').length, 3);
    assert.ok(data.expiresIn > 0 && data.expiresIn <= 600);
    assert.ok(jar.get('qlcd.refresh'));
    assert.ok(!jar.get('qlcd.auth'), 'API login does not issue MVC cookie');
    return { data, csrf };
}
(async () => {
    assert.equal((await request('/api/me')).status, 401);
    assert.equal((await request('/api/auth/token', { method: 'POST' })).status, 404);
    const { data, csrf } = await login();
    assert.equal((await request('/api/me', { token: data.accessToken })).status, 200);
    const bad = data.accessToken.slice(0, -8) + 'xxxxxxxx';
    assert.equal((await request('/api/me', { token: bad })).status, 401);
    assert.equal((await request('/api/auth/refresh', { method: 'POST' })).status, 400, 'Refresh requires CSRF');
    const oldCookie = [...jar].map(([key, value]) => `${key}=${value}`).join('; ');
    const refresh = await request('/api/auth/refresh', { method: 'POST', csrf });
    assert.equal(refresh.status, 200);
    const renewed = await refresh.json();
    assert.notEqual(renewed.accessToken, data.accessToken);
    assert.notEqual([...jar].map(([key, value]) => `${key}=${value}`).join('; '), oldCookie);
    assert.equal((await request('/api/me', { token: renewed.accessToken })).status, 200);
    assert.equal((await request('/api/auth/logout', { method: 'POST', token: renewed.accessToken })).status, 204);
    assert.equal((await request('/api/me', { token: renewed.accessToken })).status, 401);
    assert.equal((await request('/api/auth/refresh', { method: 'POST', csrf })).status, 401);
    const next = await login();
    const reusedCookie = [...jar].map(([key, value]) => `${key}=${value}`).join('; ');
    const rotated = await request('/api/auth/refresh', { method: 'POST', csrf: next.csrf });
    assert.equal(rotated.status, 200);
    const active = await rotated.json();
    assert.equal((await request('/api/auth/refresh', { method: 'POST', csrf: next.csrf, cookie: reusedCookie })).status, 401);
    assert.equal((await request('/api/me', { token: active.accessToken })).status, 401, 'Refresh reuse revokes session');
    const loginPage = await (await request('/Account/Login')).text();
    const formToken = loginPage.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
    const mvcLogin = await request('/Account/Login', { method: 'POST', formBody: new URLSearchParams({
        UserName: process.env.AUTH_TEST_USER || 'admin', Password: process.env.AUTH_TEST_PASSWORD || '12345678', __RequestVerificationToken: formToken
    }).toString() });
    assert.equal(mvcLogin.status, 302);
    assert.ok(jar.get('qlcd.auth'));
    assert.ok(jar.get('qlcd.refresh'));
    assert.equal((await request('/Admin')).status, 200);
    const mvcCsrf = (await (await request('/api/auth/csrf')).json()).token;
    const mvcRefresh = await request('/api/auth/refresh', { method: 'POST', csrf: mvcCsrf });
    assert.equal(mvcRefresh.status, 200);
    const mvcToken = (await mvcRefresh.json()).accessToken;
    assert.equal((await request('/api/me', { token: mvcToken })).status, 200);
    assert.equal((await request('/Account/Logout', { method: 'POST', csrf: mvcCsrf })).status, 302);
    assert.equal((await request('/api/me', { token: mvcToken })).status, 401);
    console.log('PASS: API/MVC login, JWT validation, refresh rotation, CSRF, both logout flows, refresh reuse revocation');
})().catch(error => { console.error(error.message); process.exitCode = 1; });
