// Local fictional demo database only. All announcement/role/account fixtures are removed in finally.
const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { spawnSync } = require('node:child_process');
const base = process.env.AUTH_TEST_URL || 'http://127.0.0.1:5099';
if (!process.env.PORTAL_TEST_SQL_SERVER) throw new Error('Set PORTAL_TEST_SQL_SERVER to the local demo server used by the app.');
const fixture = 'ann_' + randomUUID().replaceAll('-', '');
const writerId=fixture+'_writer', reviewerId=fixture+'_reviewer';
const writerRole=fixture+'_write_role', reviewRole=fixture+'_review_role';
function sql(query) {
    const r = spawnSync('sqlcmd', ['-S', process.env.PORTAL_TEST_SQL_SERVER, '-d', 'QuanLyCuDan', '-E', '-C', '-I', '-b', '-h', '-1', '-y', '4000', '-w', '65535', '-Q', 'SET NOCOUNT ON; '+query], {encoding:'utf8'});
    if (r.status !== 0) throw new Error(r.stderr || r.stdout); return r.stdout.trim();
}
function browser() { const jar=new Map(); return async(url,{method='GET',form}={})=>{
    const headers={Cookie:[...jar].map(([k,v])=>`${k}=${v}`).join('; ')}; if(form)headers['Content-Type']='application/x-www-form-urlencoded';
    const r=await fetch(base+url,{method,headers,body:form?new URLSearchParams(form):undefined,redirect:'manual',signal:AbortSignal.timeout(20000)});
    r.headers.getSetCookie().forEach(v=>{const p=v.split(';')[0],i=p.indexOf('=');jar.set(p.slice(0,i),p.slice(i+1));});return r;
};}
function input(html,name){const tag=[...html.matchAll(/<input\b[^>]*>/g)].map(m=>m[0]).find(t=>t.includes(`name="${name}"`));assert.ok(tag,'Missing '+name);return (tag.match(/value="([^"]*)"/)?.[1]||'').replace(/&#x([0-9a-f]+);/gi,(_,hex)=>String.fromCodePoint(parseInt(hex,16))).replace(/&amp;/g,'&');}
const csrf=html=>input(html,'__RequestVerificationToken');
async function html(request,url){const r=await request(url);assert.equal(r.status,200,url+': '+(r.status!==200?await r.text():''));return r.text();}
async function login(request,name){const page=await html(request,'/Account/Login');const r=await request('/Account/Login',{method:'POST',form:{UserName:name,Password:'12345678',__RequestVerificationToken:csrf(page)}});assert.equal(r.status,302,'Login '+name);return r.headers.get('location');}
const post=(request,url,token,form)=>request(url,{method:'POST',form:{__RequestVerificationToken:token,...form}});
const state=id=>JSON.parse(sql(`SELECT Status,ReviewStatus,Audience FROM Announcements WHERE Id=${id} FOR JSON PATH,WITHOUT_ARRAY_WRAPPER`));
async function transition(request,id,action,fields={}){const page=await html(request,'/Announcements/'+id);return post(request,'/Announcements/'+id+'/'+action,csrf(page),{rowVersion:input(page,'rowVersion'),...fields});}
let initialized=false;
(async()=>{
    const group=Number(sql("SELECT h.ResidentialGroupId FROM AspNetUsers u JOIN Residents r ON r.Id=u.ResidentId JOIN Households h ON h.Id=r.HouseholdId WHERE u.UserName='dan01'"));
    const foreign=Number(sql("SELECT h.ResidentialGroupId FROM AspNetUsers u JOIN Residents r ON r.Id=u.ResidentId JOIN Households h ON h.Id=r.HouseholdId WHERE u.UserName='dan03'"));
    const area=Number(sql(`SELECT ParentUnitId FROM ResidentialUnits WHERE Id=${group}`));
    assert.notEqual(group,foreign,'Demo fixture residents must belong to different groups');
    sql(`SET XACT_ABORT ON; BEGIN TRAN;
        INSERT AspNetUsers(Id,UserName,NormalizedUserName,FullName,PasswordHash,SecurityStamp,ConcurrencyStamp,VerificationStatus)
        SELECT '${writerId}','${writerId}',UPPER('${writerId}'),'Announcement writer test',PasswordHash,CONVERT(nvarchar(36),NEWID()),CONVERT(nvarchar(36),NEWID()),'NOT_APPLICABLE' FROM AspNetUsers WHERE UserName='canbo01';
        INSERT AspNetUsers(Id,UserName,NormalizedUserName,FullName,PasswordHash,SecurityStamp,ConcurrencyStamp,VerificationStatus)
        SELECT '${reviewerId}','${reviewerId}',UPPER('${reviewerId}'),'Announcement reviewer test',PasswordHash,CONVERT(nvarchar(36),NEWID()),CONVERT(nvarchar(36),NEWID()),'NOT_APPLICABLE' FROM AspNetUsers WHERE UserName='canbo01';
        INSERT AspNetRoles(Id,Name,NormalizedName,DisplayName,ConcurrencyStamp) VALUES('${writerRole}','${writerRole}',UPPER('${writerRole}'),'Test writer',CONVERT(nvarchar(36),NEWID())),('${reviewRole}','${reviewRole}',UPPER('${reviewRole}'),'Test reviewer',CONVERT(nvarchar(36),NEWID()));
        INSERT RolePermissions(RoleId,PermissionId) SELECT '${writerRole}',Id FROM Permissions WHERE Code IN('announcements.read','announcements.create','announcements.update','announcements.publish','announcements.unpublish');
        INSERT RolePermissions(RoleId,PermissionId) SELECT '${reviewRole}',Id FROM Permissions WHERE Code IN('announcements.read','announcements.approve');
        INSERT UserRoleAssignments(UserId,RoleId,ScopeType,ResidentialGroupId,ValidFrom,AssignedByUserId) VALUES('${writerId}','${writerRole}','GROUP',${group},DATEADD(day,-1,SYSUTCDATETIME()),'u-admin'),('${writerId}','${reviewRole}','GROUP',${foreign},DATEADD(day,-1,SYSUTCDATETIME()),'u-admin');
        INSERT UserRoleAssignments(UserId,RoleId,ScopeType,ResidentialAreaId,ValidFrom,AssignedByUserId) VALUES('${reviewerId}','${reviewRole}','AREA',${area},DATEADD(day,-1,SYSUTCDATETIME()),'u-admin'); COMMIT;`);
    initialized=true;
    const visitor=browser(); assert.equal((await visitor('/Announcements')).status,302);
    const writer=browser(),reviewer=browser(),outsider=browser(),resident=browser(),otherResident=browser(),admin=browser();
    assert.equal(await login(writer,writerId),'/Announcements'); assert.equal(await login(reviewer,reviewerId),'/Announcements');
    await login(outsider,'canbo02');await login(resident,'dan01');await login(otherResident,'dan03');await login(admin,'admin');
    assert.equal((await resident('/Announcements')).status,403);assert.equal((await admin('/Announcements')).status,403);
    const createPage=await html(writer,'/Announcements/Create');
    const fields={'Input.Title':fixture+'_public','Input.Body':'<script>alert(1)</script> Fictional community announcement','Input.Audience':'PUBLIC','Input.ResidentialGroupId':String(group)};
    assert.equal((await writer('/Announcements/Create',{method:'POST',form:fields})).status,400,'Missing CSRF');
    assert.equal((await post(writer,'/Announcements/Create',csrf(createPage),{...fields,'Input.ResidentialGroupId':String(foreign)})).status,403,'Create scope mixed with unrelated approval role');
    assert.equal((await post(writer,'/Announcements/Create',csrf(createPage),{...fields,'Input.Title':' ','Input.ExpiresAtLocal':'2000-01-01T12:00'})).status,200);
    assert.equal(sql(`SELECT COUNT(*) FROM Announcements WHERE CreatedByUserId='${writerId}'`),'0');
    const created=await post(writer,'/Announcements/Create',csrf(createPage),fields);assert.equal(created.status,302,await created.text());
    const url=created.headers.get('location'),id=Number(url.split('/').pop());assert.ok(Number.isInteger(id));
    assert.equal(state(id).ReviewStatus,'DRAFT'); assert.equal((await visitor('/thong-bao/'+id)).status,404);
    assert.ok(!(await html(writer,url)).includes('<script>alert(1)</script>'));
    assert.equal((await outsider(url)).status,404);assert.equal((await outsider(url+'/Edit')).status,404);
    assert.equal((await transition(writer,id,'Publish')).status,302);assert.equal(state(id).Status,'DRAFT','Unreviewed draft published');
    assert.equal((await transition(writer,id,'Submit')).status,302);
    assert.equal(state(id).ReviewStatus,'PENDING');
    assert.equal((await writer(url+'/Edit')).status,409,'Pending content was editable');
    assert.equal((await transition(writer,id,'Review',{approve:'true'})).status,403,'Approval permission from other group leaked');
    assert.equal((await transition(reviewer,id,'Review',{approve:'false',note:''})).status,302);assert.equal(state(id).ReviewStatus,'PENDING');
    assert.equal((await transition(reviewer,id,'Review',{approve:'false',note:'ừ'.repeat(1000)})).status,302);assert.equal(state(id).ReviewStatus,'REJECTED');
    assert.equal((await transition(writer,id,'Submit')).status,302);assert.equal(state(id).ReviewStatus,'PENDING');
    const stalePage=await html(reviewer,url),stale=input(stalePage,'rowVersion');
    assert.equal((await transition(writer,id,'Withdraw')).status,302);assert.equal(state(id).ReviewStatus,'DRAFT');
    assert.equal((await transition(writer,id,'Submit')).status,302);
    assert.equal((await post(reviewer,url+'/Review',csrf(stalePage),{rowVersion:stale,approve:'true'})).status,302);assert.equal(state(id).ReviewStatus,'PENDING','Stale approval applied');
    assert.equal((await transition(reviewer,id,'Review',{approve:'true',note:'Content verified'})).status,302);assert.equal(state(id).ReviewStatus,'APPROVED');
    assert.equal((await visitor('/thong-bao/'+id)).status,404,'Approved but unpublished content exposed');
    assert.equal((await transition(writer,id,'Publish')).status,302);assert.equal(state(id).Status,'PUBLISHED');
    assert.equal((await visitor('/thong-bao/'+id)).status,200);assert.equal((await resident('/Resident/announcement/'+id)).status,200);assert.equal((await otherResident('/Resident/announcement/'+id)).status,404);
    const recipients=sql(`SELECT COUNT(*) FROM Activities WHERE ActivityType='NOTIFICATION' AND TargetType='Announcement' AND TargetId='${id}'`);assert.ok(Number(recipients)>0);
    assert.equal(sql(`SELECT COUNT(*) FROM Activities a JOIN AspNetUsers u ON u.Id=a.RecipientUserId WHERE a.ActivityType='NOTIFICATION' AND a.TargetType='Announcement' AND a.TargetId='${id}' AND u.UserName='dan03'`),'0');
    assert.equal((await transition(writer,id,'Publish')).status,302);assert.equal(sql(`SELECT COUNT(*) FROM Activities WHERE ActivityType='NOTIFICATION' AND TargetType='Announcement' AND TargetId='${id}'`),recipients,'Repeat publish duplicated deliveries');
    assert.equal((await writer(url+'/Edit')).status,409,'Published content edited in place');
    assert.equal((await transition(writer,id,'Unpublish',{reason:' '})).status,302);assert.equal(state(id).Status,'PUBLISHED');
    assert.equal((await transition(writer,id,'Unpublish',{reason:'Fictional update required'})).status,302);assert.equal(state(id).Status,'UNPUBLISHED');
    assert.equal((await visitor('/thong-bao/'+id)).status,404);assert.equal((await resident('/Resident/announcement/'+id)).status,404);
    const edit=await html(writer,url+'/Edit');
    const edited={...fields,'Input.Audience':'RESIDENTS','Input.Title':fixture+'_private','Input.RowVersion':input(edit,'Input.RowVersion')};
    assert.equal((await post(writer,url+'/Edit',csrf(edit),{...edited,'Input.ResidentialGroupId':String(foreign)})).status,400);
    assert.equal((await post(writer,url+'/Edit',csrf(edit),edited)).status,302);assert.equal(state(id).ReviewStatus,'DRAFT');
    assert.equal((await transition(writer,id,'Publish')).status,302);assert.equal(state(id).Status,'UNPUBLISHED','Edited content reused old approval');
    assert.equal((await transition(writer,id,'Submit')).status,302);assert.equal((await transition(reviewer,id,'Review',{approve:'true'})).status,302);assert.equal((await transition(writer,id,'Publish')).status,302);
    assert.equal((await visitor('/thong-bao/'+id)).status,404,'Private publication leaked anonymously');assert.equal((await resident('/Resident/announcement/'+id)).status,200);
    const mine=await html(resident,'/Resident/notifications');assert.ok(mine.includes('/Resident/announcement/'+id));
    sql(`UPDATE Announcements SET ExpiresAt=DATEADD(millisecond,1,PublishedAt) WHERE Id=${id}`);
    assert.equal((await resident('/Resident/announcement/'+id)).status,404,'Expired private content remained visible');
    assert.ok((await html(writer,'/Announcements?search='+fixture+'&filter=PUBLISHED')).includes(fixture+'_private'));
    assert.equal((await writer('/Management/announcements')).status,302);
    assert.ok(Number(sql(`SELECT COUNT(*) FROM Activities WHERE ActivityType='AUDIT' AND TargetType='Announcement' AND TargetId='${id}'`))>=10);
    console.log('PASS: scoped draft/edit/submit/withdraw/reject/approve/publish/unpublish, separate GROUP/AREA permissions, no scope mixing, CSRF/XSS/concurrency guards, re-review after edits, public/private/expired visibility, recipient scoping, duplicate-publish safety and audit history');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{
    if(!initialized)return;
    try{sql(`SET XACT_ABORT ON; BEGIN TRAN;
        DECLARE @ids TABLE(Id varchar(64)); INSERT @ids SELECT CONVERT(varchar(64),Id) FROM Announcements WHERE CreatedByUserId='${writerId}';
        DELETE Activities WHERE TargetType='Announcement' AND TargetId IN(SELECT Id FROM @ids);
        DELETE Announcements WHERE CreatedByUserId='${writerId}';
        DELETE UserRoleAssignments WHERE UserId IN('${writerId}','${reviewerId}');
        DELETE RolePermissions WHERE RoleId IN('${writerRole}','${reviewRole}'); DELETE AspNetRoles WHERE Id IN('${writerRole}','${reviewRole}');
        UPDATE RefreshTokens SET ReplacedByTokenId=NULL WHERE SessionId IN(SELECT Id FROM UserSessions WHERE UserId IN('${writerId}','${reviewerId}'));
        DELETE RefreshTokens WHERE SessionId IN(SELECT Id FROM UserSessions WHERE UserId IN('${writerId}','${reviewerId}'));
        DELETE UserSessions WHERE UserId IN('${writerId}','${reviewerId}'); DELETE AspNetUsers WHERE Id IN('${writerId}','${reviewerId}'); COMMIT;`);console.log('PASS: announcement fixtures, notifications, audit entries, custom accounts and roles removed');}
    catch(e){console.error('Cleanup failed for '+fixture,e.message);process.exitCode=1;}
});
