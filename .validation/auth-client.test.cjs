const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const { test } = require('node:test');
const source = fs.readFileSync('QuanLyCuDan/wwwroot/js/auth-client.js', 'utf8');
function client(handler) {
    const calls = [], redirects = [];
    let now = 0;
    const context = {
        window: {}, navigator: { locks: { request: async (_, callback) => callback() } },
        location: { assign: path => redirects.push(path) },
        Date: { now: () => now },
        fetch: async (path, options) => {
            calls.push({ path, options });
            const result = await handler(path, options, calls);
            const status = result.status || 200;
            return { status, ok: status >= 200 && status < 300, json: async () => result.body || {} };
        }
    };
    vm.runInNewContext(source, context);
    return { api: context.window.authApi, calls, redirects, advance: ms => now += ms };
}
test('concurrent API calls share refresh; expired token rotates without bootstrap', async () => {
    let refreshes = 0;
    const app = client(async path => {
        if (path.endsWith('/csrf')) return { body: { token: 'csrf' } };
        if (path.endsWith('/refresh')) return { body: { accessToken: 'jwt-' + ++refreshes, expiresIn: 600 } };
        return { body: { ok: true } };
    });
    await Promise.all([app.api('me'), app.api('users')]);
    assert.equal(refreshes, 1);
    assert.equal(app.calls.filter(x => x.path === '/api/me')[0].options.headers.Authorization, 'Bearer jwt-1');
    app.advance(600000);
    await app.api('me');
    assert.equal(refreshes, 2);
    assert.ok(!app.calls.some(x => x.path === '/api/auth/token'));
});
test('401 refreshes once then retries with a different access token', async () => {
    let refreshes = 0, requests = 0;
    const app = client(async path => {
        if (path.endsWith('/csrf')) return { body: { token: 'csrf' } };
        if (path.endsWith('/refresh')) return { body: { accessToken: 'jwt-' + ++refreshes, expiresIn: 600 } };
        return ++requests === 1 ? { status: 401 } : { body: { ok: true } };
    });
    await app.api('me');
    assert.equal(refreshes, 2);
    assert.equal(requests, 2);
    assert.equal(app.redirects.length, 0);
});
test('invalid refresh redirects without retrying the refresh token', async () => {
    const app = client(async path => path.endsWith('/csrf') ? { body: { token: 'csrf' } } : { status: 401 });
    await assert.rejects(app.api('me'));
    assert.equal(app.calls.filter(x => x.path.endsWith('/refresh')).length, 1);
    assert.deepEqual(app.redirects, ['/Account/Login']);
});
test('lost refresh response is never retried', async () => {
    const app = client(async path => {
        if (path.endsWith('/csrf')) return { body: { token: 'csrf' } };
        throw new Error('connection lost');
    });
    await assert.rejects(app.api('me'));
    assert.equal(app.calls.filter(x => x.path.endsWith('/refresh')).length, 1);
    assert.equal(app.redirects.length, 1);
});
test('persistent API 401 is bounded to one retry', async () => {
    let refreshes = 0;
    const app = client(async path => {
        if (path.endsWith('/csrf')) return { body: { token: 'csrf' } };
        if (path.endsWith('/refresh')) return { body: { accessToken: 'jwt-' + ++refreshes, expiresIn: 600 } };
        return { status: 401 };
    });
    await assert.rejects(app.api('me'));
    assert.equal(app.calls.filter(x => x.path === '/api/me').length, 2);
    assert.equal(app.redirects.length, 1);
});
test('403 does not trigger token refresh or repeat a write', async () => {
    const app = client(async path => {
        if (path.endsWith('/csrf')) return { body: { token: 'csrf' } };
        if (path.endsWith('/refresh')) return { body: { accessToken: 'jwt', expiresIn: 600 } };
        return { status: 403, body: { title: 'Forbidden' } };
    });
    await assert.rejects(app.api('users', 'POST', { fullName: 'Demo' }), /Forbidden/);
    assert.equal(app.calls.filter(x => x.path === '/api/users').length, 1);
    assert.equal(app.calls.filter(x => x.path.endsWith('/refresh')).length, 1);
});
