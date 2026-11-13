// Local Chrome browser QA. Requires the local fictional demo accounts.
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const path = require('node:path');
const fs = require('node:fs');
const base = process.env.AUTH_TEST_URL || 'http://127.0.0.1:5098';
const local = path.resolve('QuanLyCuDan/.local');
const chrome = process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const child = spawn(chrome, ['--headless','--disable-gpu','--no-first-run','--disable-extensions','--remote-debugging-port=0','--user-data-dir='+path.join(local,'portal-browser-cdp')], { windowsHide:true, stdio:['ignore','ignore','pipe'] });
let socket;
const pause = ms => new Promise(resolve=>setTimeout(resolve,ms));
(async()=>{
    const endpoint = await new Promise((resolve,reject)=>{
        let log=''; const timer=setTimeout(()=>reject(new Error('Chrome CDP startup timeout')),15000);
        child.stderr.on('data',chunk=>{log+=chunk;const found=log.match(/DevTools listening on (ws:\/\/[^\s]+)/);if(found){clearTimeout(timer);resolve(found[1]);}});
        child.on('error',reject);child.on('exit',code=>reject(new Error('Chrome exited: '+code)));
    });
    socket=new WebSocket(endpoint);await new Promise((resolve,reject)=>{socket.onopen=resolve;socket.onerror=reject;});
    let sequence=0;const pending=new Map();
    socket.onmessage=event=>{const data=JSON.parse(event.data);const waiter=pending.get(data.id);if(waiter){pending.delete(data.id);data.error?waiter.reject(new Error(JSON.stringify(data.error))):waiter.resolve(data.result);}};
    const call=(method,params={},sessionId)=>new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params,sessionId}));});
    const {targetId}=await call('Target.createTarget',{url:'about:blank'});
    const {sessionId}=await call('Target.attachToTarget',{targetId,flatten:true});
    const send=(method,params)=>call(method,params,sessionId);
    await send('Page.enable');await send('Runtime.enable');
    const evaluate=async expression=>{const value=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(value.exceptionDetails)throw new Error(JSON.stringify(value.exceptionDetails));return value.result.value;};
    const navigate=async url=>{await send('Page.navigate',{url});for(let attempt=0;attempt<60;attempt++){await pause(100);if(await evaluate('document.readyState === "complete" && location.href === '+JSON.stringify(url)))return;}throw new Error('Navigation timeout: '+url);};
    const viewport=async(width,height)=>send('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile:false});
    const noOverflow=async()=>{const geometry=await evaluate('({width:innerWidth,scroll:document.documentElement.scrollWidth})');assert.ok(geometry.scroll<=geometry.width,JSON.stringify(geometry));};
    const capture=async name=>{const shot=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(local,name),Buffer.from(shot.data,'base64'));};
    if (process.env.PORTAL_BROWSER_MODE === 'announcements') {
        await viewport(1440,1100); await navigate(base+'/Account/Login');
        await evaluate(`document.querySelector('[name="UserName"]').value="canbo01";document.querySelector('[name="Password"]').value="12345678";document.querySelector('form').requestSubmit();`);
        for(let attempt=0;attempt<60;attempt++){await pause(100);if(await evaluate('location.pathname === "/Announcements" && document.readyState === "complete"'))break;if(attempt===59)throw new Error('Officer login failed');}
        await noOverflow();await capture('announcements-desktop.png');
        const first=await evaluate('document.querySelector(".announcement-table tbody a")?.href');
        if(first){await navigate(first);await noOverflow();await capture('announcement-detail-desktop.png');}
        await navigate(base+'/Announcements/Create');await noOverflow();
        await evaluate(`document.getElementById('Input_Title').value='Thông báo cộng đồng';document.getElementById('Input_Body').value='<script>alert(1)</script>\\nNội dung thử xem trước';document.getElementById('Input_Title').dispatchEvent(new Event('input'));document.getElementById('Input_Body').dispatchEvent(new Event('input'));document.querySelector('.announcement-preview').open=true;`);
        assert.equal(await evaluate('document.querySelector("#announcement-preview-body script") === null && document.getElementById("announcement-preview-title").textContent === "Thông báo cộng đồng"'),true);
        await capture('announcement-editor-desktop.png');
        await viewport(390,844);await noOverflow();await capture('announcement-editor-mobile.png');
        await navigate(base+'/Announcements');await noOverflow();await capture('announcements-mobile.png');
        if(first){await navigate(first);await noOverflow();await capture('announcement-detail-mobile.png');}
        console.log('PASS: Chrome officer navigation, announcement list/detail/editor on desktop/mobile, safe live preview and no page overflow');
    } else {
    await viewport(390,844);await navigate(base+'/');await noOverflow();await capture('portal-mobile.png');
    await navigate(base+'/Account/Register');await noOverflow();
    assert.equal(await evaluate('!!document.querySelector("[name=DateOfBirth]") && !!document.querySelector("[name=Address]") && document.querySelector("[name=GroupId]").options.length > 1'), true);
    await capture('registration-mobile.png');
    await navigate(base+'/Account/Login');
    await evaluate(`document.querySelector('[name="UserName"]').value=${JSON.stringify(process.env.PORTAL_TEST_USER||'dan03')};document.querySelector('[name="Password"]').value=${JSON.stringify(process.env.PORTAL_TEST_PASSWORD||'12345678')};document.querySelector('form').requestSubmit();`);
    for(let attempt=0;attempt<60;attempt++){await pause(100);if(await evaluate('location.pathname === "/Resident" && document.readyState === "complete"'))break;if(attempt===59)throw new Error('Resident login/navigation failed');}
    await noOverflow();await capture('resident-mobile.png');
    await navigate(base+'/Resident/requests');await noOverflow();
    assert.equal(await evaluate('document.querySelector("[data-request-field=target]").hidden'),true);
    await evaluate('document.getElementById("request-type").value="MOVE_IN";document.getElementById("request-type").dispatchEvent(new Event("change"));');
    assert.equal(await evaluate('document.querySelector("[data-request-field=target]").hidden'),false);
    assert.equal(await evaluate('document.getElementById("Input_TargetHouseholdCode").required'),true);
    await evaluate('document.getElementById("request-type").value="CHANGE_HEAD";document.getElementById("request-type").dispatchEvent(new Event("change"));');
    assert.equal(await evaluate('document.querySelector("[data-request-field=target]").hidden'),true);
    assert.equal(await evaluate('document.querySelector("[data-request-field=head]").hidden'),false);
    await viewport(1440,1100);await navigate(base+'/Resident');await noOverflow();await capture('resident-desktop.png');
    await navigate(base+'/Resident/fees');await noOverflow();
    console.log('PASS: Chrome desktop/mobile layouts, resident login/navigation, no horizontal overflow, and conditional request fields');
    }
    await send('Page.navigate',{url:base+'/Account/Profile'});await pause(200);
    await evaluate(`document.querySelector('form[action="/Account/Logout"]').requestSubmit()`);await pause(300);
    await call('Browser.close');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{if(socket)socket.close();child.kill();});
