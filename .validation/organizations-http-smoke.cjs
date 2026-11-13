const assert = require('node:assert/strict');
const { test } = require('node:test');
const base = process.env.AUTH_TEST_URL;
const user = process.env.PORTAL_TEST_USER, outside = process.env.PORTAL_TEST_OUTSIDE_USER, password = process.env.PORTAL_TEST_PASSWORD;
if (!base || !user || !outside || !password) throw new Error('Set AUTH_TEST_URL, PORTAL_TEST_USER, PORTAL_TEST_OUTSIDE_USER, PORTAL_TEST_PASSWORD.');
const decode = s => s.replace(/&#x([\da-f]+);/gi, (_, c) => String.fromCodePoint(parseInt(c, 16))).replaceAll('&amp;', '&');
function browser() {
    const jar = new Map();
    return async (route, form) => {
        const headers = { Cookie: [...jar].map(([k, v]) => k + '=' + v).join('; ') };
        if (form) headers['Content-Type'] = 'application/x-www-form-urlencoded';
        const r = await fetch(base + route, { method: form ? 'POST' : 'GET', headers, body: form ? new URLSearchParams(form) : undefined, redirect: 'manual', signal: AbortSignal.timeout(15000) });
        for (const s of r.headers.getSetCookie()) { const p = s.split(';')[0], i = p.indexOf('='); jar.set(p.slice(0, i), p.slice(i + 1)); }
        return r;
    };
}
async function page(request, route) { const r = await request(route), body = await r.text(); assert.equal(r.status, 200, route + ': ' + body.slice(0, 800)); return decode(body); }
function token(h) { const tag = [...h.matchAll(/<input\b[^>]*>/g)].map(m => m[0]).find(s => s.includes('name="__RequestVerificationToken"')); assert.ok(tag); return tag.match(/value="([^"]*)"/)[1]; }
async function login(request, name) { const h = await page(request, '/Account/Login'); assert.equal((await request('/Account/Login', { UserName: name, Password: password, __RequestVerificationToken: token(h) })).status, 302); }
async function logout(request) { const h = await page(request, '/Account/Profile'); assert.equal((await request('/Account/Logout', { __RequestVerificationToken: token(h) })).status, 302); }

test('Organizations on the isolated SQL Server app', async t => {
    const visitor = browser();
    const catalog = await page(visitor, '/co-cau-to-chuc?groupId=4');
    const ids = [...catalog.matchAll(/data-organization-id="(\d+)"/g)].map(m => m[1]);
    await t.test('The real SQL catalog renders exactly the ten documented organizations', async () => {
        assert.equal(ids.length, 10);
        for (const name of ['Chi bộ Đảng', 'Ban lãnh đạo thôn', 'Ban công tác Mặt trận', 'Đoàn Thanh niên', 'Hội Nông dân', 'Hội Phụ nữ', 'Hội Cựu chiến binh', 'Chi hội Người cao tuổi', 'Tổ an ninh cơ sở', 'Đoàn thể xã hội về dân vũ']) assert.ok(catalog.includes(name), name);
    });
    await t.test('Every detail renders without private person data or invented personnel', async () => {
        for (const id of ids) {
            const body = await page(visitor, '/to-chuc/' + id + '?groupId=4');
            assert.ok(body.includes('Chưa cập nhật thông tin'));
            assert.ok(!body.includes('CCCD') && !body.includes('PhoneNumber') && !body.includes('CitizenId'));
        }
        assert.equal((await visitor('/to-chuc/9223372036854775807')).status, 404);
    });
    await t.test('Association filter and about page use real locality information', async () => {
        const groups = await page(visitor, '/hoi-nhom'); assert.equal([...groups.matchAll(/data-organization-id=/g)].length, 6);
        const about = await page(visitor, '/gioi-thieu'); assert.ok(about.includes('Tổ A Demo') && about.includes('TO-A'));
        assert.equal((await visitor('/co-cau-to-chuc?kind=UNKNOWN')).status, 404);
    });
    await t.test('Resident login and registration have the new isolated theme', async () => {
        for (const route of ['/Account/Register', '/Account/Login?returnUrl=%2FResident']) {
            const h = await page(visitor, route); assert.ok(h.includes('community-portal.css') && !h.includes('admin.css'));
        }
        const h = await page(visitor, '/Account/Login'); assert.ok(h.includes('admin.css') && !h.includes('community-portal.css'));
    });
    const resident = browser(), other = browser(); await login(resident, user); await login(other, outside);
    try {
        await t.test('All old and new resident sections render against SQL Server', async () => {
            for (const section of ['', '/profile', '/household', '/requests', '/feedback', '/documents', '/fees', '/announcements', '/notifications', '/groups', '/my-groups']) await page(resident, '/Resident' + section);
            for (const id of ids) await page(resident, '/Resident/organization/' + id);
        });
        await t.test('My groups stays empty without verified membership data and ignores supplied resident ID', async () => {
            assert.ok((await page(resident, '/Resident/my-groups?residentId=3&userId=dan03')).includes('Chưa cập nhật thông tin tham gia'));
        });
        await t.test('Other-locality resident cannot use any new internal route', async () => {
            for (const route of ['/Resident/groups', '/Resident/my-groups', '/Resident/documents', '/Resident/organization/' + ids[0]]) assert.equal((await other(route)).status, 403);
        });
        await t.test('No self-join or appointment write action was introduced', async () => {
            assert.equal((await resident('/Resident/organization/' + ids[0] + '/join', {})).status, 404);
            assert.equal((await resident('/Resident/organization/' + ids[0], {})).status, 405);
        });
    } finally { await logout(resident); await logout(other); }
});
