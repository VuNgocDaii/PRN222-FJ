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



    await viewport(1366,768);await navigate('/Account/Login');await evaluate('document.querySelector("[name=UserName]").value='+JSON.stringify(user)+';document.querySelector("[name=Password]").value='+JSON.stringify(password)+';document.querySelector("form").requestSubmit()');await ready('/Resident');await navigate('/SignalR');await noOverflow('SignalR');check(await evaluate('getComputedStyle(document.documentElement).getPropertyValue("--primary").trim()==="#B71C1C"'),'SignalR theme');await capture('signalr');console.log(JSON.stringify({checks,exceptions,output}));await call('Browser.close');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{if(socket)socket.close();child.kill();});
