// Read-only browser QA for the public/resident upgrade. Credentials come only from the environment.
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const base = process.env.AUTH_TEST_URL;
const user = process.env.PORTAL_TEST_USER;
const password = process.env.PORTAL_TEST_PASSWORD;
if (!base || !user || !password) throw new Error('Set AUTH_TEST_URL, PORTAL_TEST_USER and PORTAL_TEST_PASSWORD.');
const run = Date.now().toString();
const output = path.resolve('QuanLyCuDan/.local/organization-ui', run);
fs.mkdirSync(output, { recursive: true });
const chrome = process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const child = spawn(chrome, ['--headless', '--disable-gpu', '--no-first-run', '--disable-extensions', '--remote-debugging-port=0', '--user-data-dir=' + path.join(output, 'chrome-profile')], { windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'] });
let socket, checks = 0;
const pause = ms => new Promise(r => setTimeout(r, ms));
const check = (condition, message) => { assert.ok(condition, message); checks++; };
(async () => {
    const endpoint = await new Promise((resolve, reject) => {
        let log = ''; const timer = setTimeout(() => reject(new Error('Chrome startup timeout')), 15000);
        child.stderr.on('data', data => { log += data; const m = log.match(/DevTools listening on (ws:\/\/[^\s]+)/); if (m) { clearTimeout(timer); resolve(m[1]); } });
        child.on('error', reject); child.on('exit', c => reject(new Error('Chrome exited: ' + c)));
    });
    socket = new WebSocket(endpoint); await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
    let seq = 0; const pending = new Map(), exceptions = [];
    socket.onmessage = e => {
        const data = JSON.parse(e.data), p = pending.get(data.id);
        if (p) { pending.delete(data.id); data.error ? p.reject(new Error(JSON.stringify(data.error))) : p.resolve(data.result); }
        if (data.method === 'Runtime.exceptionThrown') exceptions.push(data.params.exceptionDetails.text);
        if (data.method === 'Runtime.consoleAPICalled' && data.params.type === 'error') exceptions.push(JSON.stringify(data.params.args));
    };
    const call = (method, params = {}, sessionId) => new Promise((resolve, reject) => { const id = ++seq; pending.set(id, { resolve, reject }); socket.send(JSON.stringify({ id, method, params, sessionId })); });
    const { targetId } = await call('Target.createTarget', { url: 'about:blank' });
    const { sessionId } = await call('Target.attachToTarget', { targetId, flatten: true });
    const send = (method, params) => call(method, params, sessionId);
    await send('Page.enable'); await send('Runtime.enable');
    const evaluate = async expression => { const r = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true }); if (r.exceptionDetails) throw new Error(JSON.stringify(r.exceptionDetails)); return r.result.value; };
    const ready = async pathname => {
        for (let i = 0; i < 100; i++) { await pause(100); if (await evaluate('document.readyState === "complete" && location.pathname === ' + JSON.stringify(pathname))) return; }
        throw new Error('Navigation timeout: ' + pathname);
    };
    const navigate = async route => { await send('Page.navigate', { url: base + route }); await ready(new URL(base + route).pathname); };
    const viewport = (width, height) => send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
    const noOverflow = async label => { const g = await evaluate('({width:innerWidth,scroll:document.documentElement.scrollWidth})'); check(g.scroll <= g.width, label + ' overflow ' + JSON.stringify(g)); };
    const capture = async name => { const shot = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false }); fs.writeFileSync(path.join(output, name + '.png'), Buffer.from(shot.data, 'base64')); };
    const heading = () => evaluate('document.querySelector("h1")?.textContent || ""');

    for (const width of [390, 768, 1440]) {
        await viewport(width, width === 1440 ? 1100 : 1000);
        await navigate('/'); await noOverflow('home ' + width);
        check(await evaluate('getComputedStyle(document.body).getPropertyValue("--red").trim() === "#b71c1c"'), 'Red portal theme');
        if (width === 390) {
            check(await evaluate('document.querySelector(".mobile-menu-toggle").getAttribute("aria-expanded") === "false"'), 'Mobile menu starts closed');
            await evaluate('document.querySelector(".mobile-menu-toggle").click()');
            check(await evaluate('document.querySelector("#portal-main-menu").classList.contains("is-open")'), 'Mobile menu opens');
            await noOverflow('open mobile menu');
            await evaluate('document.querySelector(".mobile-menu-toggle").click()');
        }
        await capture('home-' + width);
        const firstNotice = await evaluate('document.querySelector(".announcement-card h3 a")?.getAttribute("href")');
        check(!!firstNotice, 'Public announcement exists'); await navigate(firstNotice); await noOverflow('announcement ' + width);
        await navigate('/co-cau-to-chuc'); await noOverflow('organization list ' + width);
        check(await evaluate('document.querySelectorAll(".organization-card").length === 10'), 'Exactly ten source organizations');
        await capture('organizations-' + width);
        const first = await evaluate('document.querySelector(".organization-card h3 a").getAttribute("href")');
        await navigate(first); await noOverflow('organization detail ' + width);
        check(await evaluate('document.body.innerText.includes("Chưa cập nhật thông tin")'), 'No fabricated personnel');
        await capture('organization-detail-' + width);
        await navigate('/hoi-nhom'); await noOverflow('associations ' + width);
        check(await evaluate('document.querySelectorAll(".organization-card").length === 6'), 'Five associations plus the dance group');
        await navigate('/gioi-thieu'); await noOverflow('about ' + width);
        await navigate('/Account/Register'); await noOverflow('registration ' + width);
        check(await evaluate('!document.querySelector("[name=GroupId]") && !!document.querySelector("[name=DateOfBirth]")'), 'Fixed locality and preserved registration fields');
        await evaluate('document.querySelector("[name=DateOfBirth]").value="2999-01-01"');
        check(await evaluate('document.querySelector("[name=DateOfBirth]").validity.rangeOverflow'), 'Registration date validation');
        await evaluate('document.querySelector("[name=DateOfBirth]").value=""');
        await capture('registration-' + width);
    }

    await navigate('/Account/Login?returnUrl=%2FResident');
    check(await evaluate(`document.querySelector('link[href*="community-portal.css"]') !== null`), 'Resident login has the separate portal layout');
    await capture('resident-login-1440');
    await evaluate(`document.querySelector('[name="UserName"]').value=${JSON.stringify(user)};document.querySelector('[name="Password"]').value=${JSON.stringify(password)};document.querySelector('form').requestSubmit();`);
    await ready('/Resident');
    for (const width of [390, 768, 1440]) {
        await viewport(width, width === 1440 ? 1100 : 1000);
        for (const section of ['', '/profile', '/household', '/requests', '/feedback', '/documents', '/fees', '/announcements', '/notifications', '/groups', '/my-groups']) {
            await navigate('/Resident' + section); await noOverflow('resident ' + section + ' ' + width);
            check(await evaluate('!!document.querySelector(".resident-shell") && !!document.querySelector("h1")'), 'Resident section rendered ' + section);
            if (['', '/requests', '/groups', '/my-groups'].includes(section)) await capture('resident-' + (section.slice(1) || 'overview') + '-' + width);
        }
        await navigate('/Resident/requests');
        check(await evaluate('document.querySelector("[data-request-field=target]").hidden'), 'Initial request fields hidden');
        await evaluate('document.getElementById("request-type").value="MOVE_IN";document.getElementById("request-type").dispatchEvent(new Event("change"))');
        check(await evaluate('!document.querySelector("[data-request-field=target]").hidden && document.getElementById("Input_TargetHouseholdCode").required'), 'Conditional receiving household field');
    }
    await navigate('/Resident/groups');
    const ownDetail = await evaluate('document.querySelector(".organization-card h3 a").getAttribute("href")');
    await navigate(ownDetail); await noOverflow('resident organization detail');
    check((await heading()).length > 0, 'Resident organization detail heading');
    await evaluate('document.querySelector(\'form[action="/Account/Logout"]\').requestSubmit()'); await ready('/Account/Login');
    await navigate('/Account/Login');
    check(await evaluate(`!!document.querySelector('link[href*="admin.css"]') && !document.querySelector('link[href*="community-portal.css"]')`), 'Default management login keeps its existing stylesheet');
    check(exceptions.length === 0, 'No JavaScript errors: ' + JSON.stringify(exceptions));
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ checks, failed: 0, widths: [390, 768, 1440], exceptions, base }, null, 2));
    fs.writeFileSync(path.resolve('QuanLyCuDan/.local/organization-ui/latest.json'), JSON.stringify({ output, checks, failed: 0 }, null, 2));
    console.log(`PASS: ${checks} browser checks; desktop/tablet/mobile, organization pages, old resident forms, isolated login styles, no overflow or JS errors.`);
    console.log('SCREENSHOTS: ' + output);
    await call('Browser.close');
})().catch(e => { console.error(e); process.exitCode = 1; }).finally(() => { if (socket) socket.close(); child.kill(); });
