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

    const output=path.join(local,process.env.UI_REVIEW_OUTPUT||'ui-review');fs.mkdirSync(output,{recursive:true});
    const report=[],errors=[];
    socket.addEventListener('message',event=>{const d=JSON.parse(event.data);if(d.method==='Runtime.exceptionThrown')errors.push(d.params.exceptionDetails.text+' '+(d.params.exceptionDetails.exception?.description||''));});
    async function audit(name,route) {
        for(const width of [1440,390,320]) {
            await viewport(width,width===1440?1000:844);await navigate(base+route);await pause(200);
            const data=await evaluate(`(()=>{const visible=e=>e.getClientRects().length&&getComputedStyle(e).visibility!=='hidden';return {title:document.title,h1:document.querySelector('h1')?.textContent,scroll:document.documentElement.scrollWidth,width:innerWidth,unlabelled:[...document.querySelectorAll('input:not([type=hidden]),select,textarea')].filter(visible).filter(e=>!e.labels?.length&&!e.getAttribute('aria-label')&&!e.getAttribute('aria-labelledby')).map(e=>e.name||e.id),overflow:[...document.querySelectorAll('main *,aside *')].filter(visible).filter(e=>{const r=e.getBoundingClientRect();return r.left< -2||r.right>innerWidth+2}).slice(0,8).map(e=>e.tagName+'.'+e.className),checkboxes:[...document.querySelectorAll('input[type=checkbox]')].filter(visible).slice(0,2).map(e=>({width:e.getBoundingClientRect().width,height:e.getBoundingClientRect().height})),links:[...document.querySelectorAll('a[href]')].map(a=>a.getAttribute('href'))}})()`);
            report.push({name,route,viewport:width,...data});
            fs.writeFileSync(path.join(output,'report.json'),JSON.stringify({report,errors},null,2));
            if(width!==320){const shot=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(output,name+'-'+width+'.png'),Buffer.from(shot.data,'base64'));}
        }
    }
    async function signIn(user) {
        await send('Network.clearBrowserCookies');await navigate(base+'/Account/Login');
        await evaluate(`document.querySelector('[name="UserName"]').value=${JSON.stringify(user)};document.querySelector('[name="Password"]').value="12345678";document.querySelector('form').requestSubmit();`);
        for(let attempt=0;attempt<80;attempt++){await pause(100);if(await evaluate('location.pathname !== "/Account/Login" && document.readyState === "complete"'))return;}throw Error('Login failed '+user);
    }
    await audit('public','/');await audit('login','/Account/Login');await audit('registration','/Account/Register');await audit('privacy','/Home/Privacy');
    await signIn('admin');
    for(const [n,r]of [['admin','/Admin'],['users','/Users'],['roles','/Roles'],['permissions','/Permissions'],['assignments-empty','/RoleAssignments'],['registrations','/Registrations'],['approved','/Registrations?status=APPROVED'],['rejected','/Registrations?status=REJECTED'],['profile','/Account/Profile'],['system-overview','/Management/overview']])await audit(n,r);
    await navigate(base+'/RoleAssignments');
    const accountId=await evaluate('[...document.querySelector("select[name=userId]").options].find(o=>o.textContent.startsWith("dan03"))?.value');
    await audit('assignments','/RoleAssignments?userId='+encodeURIComponent(accountId));
    if(process.env.UI_REVIEW_ASSERT) {
        assert.equal(await evaluate('document.querySelector("[data-role-assignment]").elements.UnitId.disabled'),true);
        await evaluate('const f=document.querySelector("[data-role-assignment]"), role=[...f.elements.RoleId.options].find(o=>o.dataset.scopes.includes("GROUP")); f.elements.RoleId.value=role.value;f.elements.RoleId.dispatchEvent(new Event("change"));f.elements.ScopeType.value="GROUP";f.elements.ScopeType.dispatchEvent(new Event("change"));');
        assert.equal(await evaluate('document.querySelector("[data-role-assignment]").elements.ScopeType.value'),'GROUP');
        assert.equal(await evaluate('document.querySelector("[data-role-assignment]").elements.UnitId.required'),true);
    }
    await navigate(base+'/Account/Profile');
    if(process.env.UI_REVIEW_ASSERT) {
        await evaluate('const f=document.querySelector("[data-password-form]");f.elements.NewPassword.value="Test123456!";f.elements.confirmPassword.value="Different123!";f.elements.confirmPassword.dispatchEvent(new Event("input"));');
        assert.equal(await evaluate('document.querySelector("[data-password-form]").elements.confirmPassword.validity.customError'),true);
    }
    await evaluate('document.getElementById("open-password-dialog").click()');
    const dialog=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(output,'password-dialog.png'),Buffer.from(dialog.data,'base64'));
    for(const module of ['units','households','residents','residence-requests','feedback','fees','payments','notifications','reports','activities'])await audit('management-'+module,'/Management/'+module);
    await navigate(base+'/Registrations?status=APPROVED');
    if(await evaluate('!!document.querySelector("[data-review-open]")')) {
        await evaluate('document.querySelector("[data-review-open]").click()');
        assert.equal(await evaluate('!!document.querySelector("dialog[open]")'),true);
        const shot=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(output,'registration-dialog.png'),Buffer.from(shot.data,'base64'));
    }
    await signIn('canbo01');
    await audit('announcements','/Announcements');
    const detail=await evaluate('[...document.querySelectorAll("a[href]")].map(a=>a.getAttribute("href")).find(h=>new RegExp("^/Announcements/[0-9]+$").test(h))');
    if(detail)await audit('announcement-detail',detail);
    await audit('announcement-create','/Announcements/Create');await audit('officer-feedback','/Feedback');
    await signIn('dan03');
    for(const section of ['overview','profile','household','requests','fees','announcements','notifications'])await audit('resident-'+section,'/Resident/'+section);
    const requestRoute=report.filter(x=>x.name==='resident-requests').flatMap(x=>x.links).find(h=>new RegExp('^/Resident/request/[0-9]+$').test(h));
    if(requestRoute)await audit('resident-request-detail',requestRoute);
    await audit('resident-feedback','/Feedback');
    fs.writeFileSync(path.join(output,'report.json'),JSON.stringify({report,errors},null,2));
    const issues=report.filter(r=>r.scroll>r.width||r.unlabelled.length||r.checkboxes.some(c=>c.width>40));
    console.log(JSON.stringify({pages:report.length/3,errors,issues:issues.map(({name,viewport,scroll,width,unlabelled,checkboxes})=>({name,viewport,scroll,width,unlabelled,checkboxes})),output},null,2));
    if(process.env.UI_REVIEW_ASSERT){assert.equal(issues.length,0,'UI layout/accessibility issues remain');assert.equal(errors.length,0,'Browser runtime errors');}
    await call('Browser.close');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{if(socket)socket.close();child.kill();});
