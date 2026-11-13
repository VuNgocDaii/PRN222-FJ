// HTTP regression checks for MVC controllers and Razor Views. Uses local demo accounts.
const assert = require('node:assert/strict');
const base = process.env.AUTH_TEST_URL || 'http://127.0.0.1:5099';
function browser() {
    const cookies = new Map();
    return async (url, form) => {
        const headers = { Cookie: [...cookies].map(([k, v]) => `${k}=${v}`).join('; ') };
        if (form) headers['Content-Type'] = 'application/x-www-form-urlencoded';
        const result = await fetch(base + url, {
            method: form ? 'POST' : 'GET', headers,
            body: form ? new URLSearchParams(form) : undefined,
            redirect: 'manual', signal: AbortSignal.timeout(15000)
        });
        for (const cookie of result.headers.getSetCookie()) {
            const pair = cookie.split(';')[0], at = pair.indexOf('=');
            cookies.set(pair.slice(0, at), pair.slice(at + 1));
        }
        return result;
    };
}
async function page(request, url) {
    const result = await request(url);
    const html = await result.text();
    assert.equal(result.status, 200, `${url}: ${html.slice(0, 300)}`);
    assert.match(result.headers.get('content-type'), /text\/html/);
    assert.match(result.headers.get('cache-control'), /no-store/);
    assert.match(html, /<html/i);
    assert.ok(!/asp-(?:page|controller|action)=/.test(html), 'MVC links must be rendered');
    return html;
}
const csrf = html => {
    const found = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
    assert.ok(found, 'Missing antiforgery field');
    return found[1];
};
async function login(request, name) {
    const html = await page(request, '/Account/Login');
    const result = await request('/Account/Login', {
        UserName: name, Password: '12345678', __RequestVerificationToken: csrf(html)
    });
    assert.equal(result.status, 302, `Login ${name}`);
    return result.headers.get('location');
}
(async () => {
    const visitor = browser();
    for (const url of ['/', '/Home', '/Home/Index', '/Home/Privacy', '/Home/Error']) await page(visitor, url);
    const registration = await page(visitor, '/Account/Register');
    assert.match(registration, /action="\/Account\/Register"/);
    assert.match(registration, /name="FullName"/);
    assert.match(registration, /name="GroupId"/);
    assert.equal((await visitor('/Account/Register', {})).status, 400);
    assert.equal((await visitor('/Admin')).status, 302);

    const admin = browser();
    assert.equal(await login(admin, 'admin'), '/Admin');
    const adminHtml = await page(admin, '/Admin');
    assert.match(adminHtml, /href="\/Users"/, 'Administration must link to MVC views');
    for (const url of ['/Admin/Index', '/Account/Profile', '/Registrations', '/Registrations/Index', '/Management/overview', '/Users', '/Roles', '/Permissions', '/RoleAssignments']) await page(admin, url);
    assert.equal((await admin('/Management/announcements')).headers.get('location'), '/Announcements');
    assert.equal((await admin('/Account/Logout')).status, 405, 'Logout must require POST');
    assert.equal((await admin('/Account/Logout', {})).status, 400);

    const resident = browser();
    assert.equal(await login(resident, 'dan01'), '/Resident');
    for (const url of ['/Resident', '/Resident/profile', '/Resident/household', '/Resident/requests', '/Resident/fees', '/Resident/announcements', '/Resident/notifications']) await page(resident, url);
    const feedback = await page(resident, '/Feedback');
    assert.match(feedback, /css\/feedback\.css/, 'Style section must reach the layout');
    assert.match(feedback, /action="\/Feedback\/Create"/);
    assert.match(feedback, /js\/notification-client\.js/);
    assert.equal((await resident('/Feedback/Create', {})).status, 400);
    assert.equal((await resident('/Resident/request/9223372036854775807/attachment')).status, 404, 'Attachment GET must reach the authorized download handler');
    const profile = await page(resident, '/Account/Profile');
    assert.equal((await resident('/Account/Logout', { __RequestVerificationToken: csrf(profile) })).status, 302);
    assert.equal((await resident('/Resident')).status, 302);
    console.log('PASS: MVC routes and Razor Views, typed form fields, action links, layout sections, cache headers, cookie authorization, antiforgery, POST-only logout and attachment routing');
})().catch(error => { console.error(error); process.exitCode = 1; });
