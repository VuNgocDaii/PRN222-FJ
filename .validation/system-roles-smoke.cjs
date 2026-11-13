// Mutates and restores protected/own roles on an isolated validation database only.
const assert = require('node:assert/strict');
const {spawnSync} = require('node:child_process');
const database=process.env.PORTAL_TEST_DATABASE,base=process.env.AUTH_TEST_URL;
if(!database?.includes('VillageValidation')||!base||!process.env.PORTAL_TEST_SQL_SERVER)throw Error('Use an isolated VillageValidation database.');
function sql(q){const r=spawnSync('sqlcmd',['-S',process.env.PORTAL_TEST_SQL_SERVER,'-d',database,'-E','-C','-I','-b','-h','-1','-y','4000','-w','65535','-Q','SET NOCOUNT ON; '+q],{encoding:'utf8'});if(r.status!==0)throw Error(r.stdout+r.stderr);return r.stdout.trim();}
const row=q=>JSON.parse(sql(q+' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER'));
const role=id=>row(`SELECT Id,DisplayName,Description,IsActive,ConcurrencyStamp,IsSystemRole FROM AspNetRoles WHERE Id='${id}'`);
const permissions=id=>JSON.parse(sql(`SELECT PermissionId FROM RolePermissions WHERE RoleId='${id}' FOR JSON PATH`)).map(p=>String(p.PermissionId));
const jar=new Map();
async function request(path,form){const h={Cookie:[...jar].map(([k,v])=>`${k}=${v}`).join('; ')};if(form)h['Content-Type']='application/x-www-form-urlencoded';const body=form?new URLSearchParams():undefined;if(form)for(const[k,v]of Object.entries(form))for(const x of Array.isArray(v)?v:[v])body.append(k,x);const r=await fetch(base+path,{method:form?'POST':'GET',headers:h,body,redirect:'manual',signal:AbortSignal.timeout(15000)});for(const c of r.headers.getSetCookie()){const p=c.split(';')[0],i=p.indexOf('=');jar.set(p.slice(0,i),p.slice(i+1));}return r;}
async function page(path){const r=await request(path);assert.equal(r.status,200,path);return r.text();}
const token=h=>h.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
async function post(path,fields){return request(path,{...fields,__RequestVerificationToken:token(await page('/Admin?tab=roles'))});}
const originals=new Map();
(async()=>{
    const login=await page('/Account/Login');assert.equal((await request('/Account/Login',{UserName:'admin',Password:'12345678',__RequestVerificationToken:token(login)})).status,302);
    for(const id of ['r-account','r-admin']){
        const before=role(id),ids=permissions(id);originals.set(id,{...before,permissionIds:ids});
        assert.equal(before.IsSystemRole,true,'Expected a seeded system role');
        const adminPage=await page('/Admin?tab=roles&roleId='+id);
        assert.ok(!adminPage.includes('Vai trò hệ thống được bảo vệ'));
        const details=adminPage.match(/<form\b[^>]*id="role-details"[\s\S]*?<\/form>/)?.[0];assert.ok(details,'Role edit form missing');assert.ok(!details.includes('readonly='),'Role information still read-only');assert.ok(!details.includes('disabled='),'Role edit form still disabled');
        const form={DisplayName:before.DisplayName,Description:'Role edit integration test',IsActive:String(before.IsActive),ConcurrencyStamp:before.ConcurrencyStamp};
        assert.equal((await post('/Roles/Update/'+id,form)).status,302,'System/own role information edit blocked');
        assert.equal(role(id).Description,form.Description);
        assert.equal((await post('/Roles/Update/'+id,{...form,DisplayName:'Stale edit'})).status,200,'Stale role edit was accepted');assert.equal(role(id).DisplayName,before.DisplayName);
        const removable=sql(`SELECT p.Id FROM Permissions p JOIN RolePermissions rp ON rp.PermissionId=p.Id WHERE rp.RoleId='${id}' AND p.Code='sessions.self.read'`);assert.ok(removable,'Expected removable session permission');
        const selected=ids.filter(p=>p!==removable);
        assert.equal((await post('/Roles/Permissions/'+id,{PermissionIds:selected,ConcurrencyStamp:role(id).ConcurrencyStamp})).status,302,'System/own role permission edit blocked');
        assert.deepEqual(permissions(id).sort(),selected.sort());
        assert.equal((await post('/Roles/Permissions/'+id,{PermissionIds:ids,ConcurrencyStamp:role(id).ConcurrencyStamp})).status,302);
        assert.equal((await post('/Roles/Update/'+id,{DisplayName:before.DisplayName,Description:before.Description||'',IsActive:String(before.IsActive),ConcurrencyStamp:role(id).ConcurrencyStamp})).status,302);
    }
    const rolesPage=await page('/Roles');for(const id of originals.keys())assert.ok(rolesPage.includes('/Roles/Update/'+id),'Standalone roles page hides system role editor');
    console.log('PASS: system and own ADMIN role information/permission edits; enabled admin/standalone editors; stale edits rejected');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{
    const quote=v=>v==null?'NULL':"N'"+String(v).replaceAll("'","''")+"'";
    try{for(const[id,original]of originals){sql(`SET XACT_ABORT ON; BEGIN TRAN; UPDATE AspNetRoles SET DisplayName=${quote(original.DisplayName)},Description=${quote(original.Description)},IsActive=${original.IsActive?1:0},ConcurrencyStamp=CONVERT(nvarchar(36),NEWID()) WHERE Id='${id}'; DELETE RolePermissions WHERE RoleId='${id}'; INSERT RolePermissions(RoleId,PermissionId) SELECT '${id}',Id FROM Permissions WHERE Id IN(${original.permissionIds.join(',')}); COMMIT;`);}console.log('PASS: original role metadata and permission sets restored');}
    catch(e){console.error('Restore failed: '+e.message);process.exitCode=1;}
});
