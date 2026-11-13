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

    const out=path.join(local,'admin-restored');fs.mkdirSync(out,{recursive:true});
    const errors=[],apiCalls=[];socket.addEventListener('message',event=>{const d=JSON.parse(event.data);if(d.method==='Runtime.exceptionThrown')errors.push(d.params.exceptionDetails.text);if(d.method==='Network.requestWillBeSent'&&d.params.request.url.startsWith(base+'/api/'))apiCalls.push(d.params.request.url);});await send('Network.enable');
    await navigate(base+'/Account/Login');await evaluate('document.querySelector("[name=UserName]").value="admin";document.querySelector("[name=Password]").value="12345678";document.querySelector("form").requestSubmit()');
    for(let i=0;i<80;i++){await pause(100);if(await evaluate('location.pathname==="/Admin"&&document.readyState==="complete"'))break;if(i===79)throw Error('Login failed');}
    assert.ok(await evaluate('JSON.parse(localStorage.getItem("qlcd.accessToken")).accessToken'));
    const results=[];
    async function check(name,route) {
        for(const width of [1440,390,320]) {
            await viewport(width,width===1440?1000:844);await navigate(base+route);await pause(100);await noOverflow();
            const info=await evaluate('({title:document.querySelector("h1").textContent,active:document.querySelector("[data-tab].active")?.dataset.tab,rows:document.querySelectorAll("tbody tr").length})');results.push({name,width,...info});
            if(width!==320){const shot=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(out,name+'-'+width+'.png'),Buffer.from(shot.data,'base64'));}
        }
    }
    await check('users','/Admin');assert.equal(await evaluate('document.querySelector("[data-tab=users]").getAttribute("aria-current")'),'page');
    await evaluate('document.querySelector("[data-edit-user]").click()');assert.equal(await evaluate('document.getElementById("edit-user-dialog").open'),true);assert.ok(await evaluate('document.querySelector("#edit-user-dialog [name=FullName]").value'));await evaluate('document.querySelector("[data-close-user-dialog]").click()');
    await viewport(1440,1000);await navigate(base+'/Admin');
    const user=await evaluate('[...document.querySelector("#assignment-user").options].find(o=>o.textContent.startsWith("dan03"))?.value');
    await check('assignments','/Admin?tab=users&userId='+encodeURIComponent(user));assert.equal(await evaluate('!!document.getElementById("assign-role")'),true);
    await evaluate('const f=document.getElementById("assign-role"),r=[...f.elements.RoleId.options].find(o=>o.dataset.scopes.includes("GROUP"));f.elements.RoleId.value=r.value;f.elements.RoleId.dispatchEvent(new Event("change"));f.elements.ScopeType.value="GROUP";f.elements.ScopeType.dispatchEvent(new Event("change"));');
    assert.equal(await evaluate('document.getElementById("assignment-unit").required'),true);
    await check('roles','/Admin?tab=roles');assert.ok(await evaluate('document.querySelectorAll("#permission-list input[type=checkbox]").length'));
    assert.equal(await evaluate('document.getElementById("role-details").querySelector("button").disabled'),true);
    await check('permissions','/Admin?tab=permissions');
    const edit=await evaluate('document.querySelector("#permission-catalog a")?.getAttribute("href")');if(edit){await check('permission-edit',edit);assert.equal(await evaluate('document.querySelector("#permission-editor [name=Code]").readOnly'),true);}
    await viewport(1440,1000);await navigate(base+'/Admin');await evaluate('document.querySelector("[data-admin-confirm] button").click()');
    assert.equal(await evaluate('document.getElementById("action-dialog").open'),true);assert.ok(await evaluate('document.getElementById("action-dialog-description").textContent.includes("Khóa tài khoản")'));await evaluate('document.getElementById("action-dialog-cancel").click()');
    assert.equal(apiCalls.length,0);assert.equal(errors.length,0);
    fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({results,errors,apiCalls},null,2));
    console.log('PASS: restored admin tables/tabs, server-rendered data, rename and confirmation dialogs, scope selection, role protection, permission editor, localStorage JWT, responsive layouts and no API requests');await call('Browser.close');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{if(socket)socket.close();child.kill();});
