// Only run against the isolated fictional village validation database.
const assert = require('node:assert/strict');
const {spawnSync} = require('node:child_process');
const {randomUUID} = require('node:crypto');
const base = process.env.AUTH_TEST_URL || 'http://127.0.0.1:5099';
const database = process.env.PORTAL_TEST_DATABASE;
if (!database || !/VillageValidation/.test(database)) throw Error('Use a dedicated VillageValidation database.');
if (!process.env.PORTAL_TEST_SQL_SERVER) throw Error('Set PORTAL_TEST_SQL_SERVER.');
const prefix = 'village_test_' + randomUUID().replaceAll('-', '');
function runSql(q) {
    return spawnSync('sqlcmd', ['-S', process.env.PORTAL_TEST_SQL_SERVER, '-d', database, '-E', '-C', '-I', '-b', '-f', '65001', '-h', '-1', '-y', '4000', '-w', '65535', '-Q', 'SET NOCOUNT ON; ' + q], {encoding:'utf8'});
}
function sql(q) { const r=runSql(q); if(r.status!==0)throw Error(r.stdout+r.stderr); return r.stdout.trim(); }
const row = q => JSON.parse(sql(q+' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER'));
function browser() {
    const jar=new Map();
    return async (path, {form, method='GET', multipart}={}) => {
        const headers={Cookie:[...jar].map(([k,v])=>`${k}=${v}`).join('; ')};
        if(form)headers['Content-Type']='application/x-www-form-urlencoded';
        const r=await fetch(base+path,{method:form||multipart?'POST':method,headers,body:form?new URLSearchParams(form):multipart,redirect:'manual',signal:AbortSignal.timeout(20000)});
        for(const c of r.headers.getSetCookie()){const p=c.split(';')[0],i=p.indexOf('=');jar.set(p.slice(0,i),p.slice(i+1));}
        return r;
    };
}
function input(html,name) {
    const tag=[...html.matchAll(/<input\b[^>]*>/g)].map(m=>m[0]).find(t=>t.includes(`name="${name}"`));
    assert.ok(tag,'Missing input '+name);
    return (tag.match(/value="([^"]*)"/)?.[1]||'').replace(/&#x([0-9a-f]+);/gi,(_,n)=>String.fromCodePoint(parseInt(n,16))).replaceAll('&amp;','&').replaceAll('&quot;','"');
}
const csrf=h=>input(h,'__RequestVerificationToken');
async function page(b,path) {const r=await b(path);assert.equal(r.status,200,path+': '+(r.status!==200?await r.text():''));return r.text();}
async function login(b,name,password='12345678') {const h=await page(b,'/Account/Login');const r=await b('/Account/Login',{form:{UserName:name,Password:password,__RequestVerificationToken:csrf(h)}});assert.equal(r.status,302,'Login '+name+': '+await r.text());}
const post=(b,path,h,form)=>b(path,{form:{__RequestVerificationToken:csrf(h),...form}});
async function action(b,id,name,extra={}) {const h=await page(b,'/Announcements/'+id);return post(b,`/Announcements/${id}/${name}`,h,{rowVersion:input(h,'rowVersion'),...extra});}
const state=id=>row(`SELECT Status,ReviewStatus,ReviewedByUserId,PublishedByUserId FROM Announcements WHERE Id=${id}`);
let tempRole=false;
(async()=>{
    const group=Number(sql("SELECT Id FROM ResidentialUnits WHERE Code='THON-AN-HOA'"));
    const women=Number(sql("SELECT Id FROM Organizations WHERE Code='AH-PHU-NU'"));
    const youth=Number(sql("SELECT Id FROM Organizations WHERE Code='AH-CHI-DOAN'"));
    assert.equal(sql('SELECT COUNT(*) FROM (SELECT UserId FROM UserRoleAssignments WHERE RevokedAt IS NULL GROUP BY UserId HAVING COUNT(*)>1) q'),'0');
    assert.notEqual(runSql("INSERT UserRoleAssignments(UserId,RoleId,ScopeType,ValidFrom,AssignedByUserId) VALUES('demo-resident','r-account','SELF',SYSUTCDATETIME(),'u-admin')").status,0,'DB accepted a second role');
    assert.equal(sql("SELECT COUNT(*) FROM vw_EffectivePermissions WHERE UserId='demo-head' AND ScopeType='SELF' AND PermissionCode='profile.self.read'"),'1','Personal permissions missing from leadership role');
    const head=browser(),secretary=browser(),org=browser(),otherOrg=browser(),deputy=browser(),resident=browser(),outsider=browser(),commune=browser(),admin=browser(),visitor=browser();
    for(const [b,n,p] of [[head,'truongthon'],[secretary,'bithu'],[org,'hoiphunu'],[otherOrg,'chidoan'],[deputy,'cappho'],[resident,'cudan'],[outsider,'cudan_khac'],[commune,'canboxa'],[admin,'admin','12345678']]) await login(b,n,p);
    await page(head,'/Resident'); await page(head,'/Organizations'); await page(admin,'/Admin');
    await page(admin,'/RoleAssignments?userId=demo-deputy');
    const adminForm=await page(admin,'/Admin?tab=users&userId=demo-deputy');
    const occupied=await post(admin,'/RoleAssignments/Assign',adminForm,{userId:'demo-deputy',RoleId:'r-resident',ScopeType:'SELF',ValidFrom:'2026-01-01T00:00'});
    assert.equal(occupied.status,200,'Second role must return form validation');
    assert.equal(sql("SELECT COUNT(*) FROM UserRoleAssignments WHERE UserId='demo-deputy' AND RevokedAt IS NULL"),'1');
    async function create(b,title,audience='ORGANIZATION',organization=women,extra={}) {
        const h=await page(b,'/Announcements/Create');
        const r=await post(b,'/Announcements/Create',h,{'Input.Title':prefix+title,'Input.Body':'Nội dung minh họa cho kiểm tra nghiệp vụ thôn.','Input.ResidentialGroupId':group,'Input.Audience':audience,...(organization?{'Input.OrganizationId':organization}:{}),...extra});
        assert.equal(r.status,302,'Create: '+await r.text());return Number(r.headers.get('location').split('/').pop());
    }
    const h=await page(org,'/Announcements/Create');
    assert.equal((await post(org,'/Announcements/Create',h,{'Input.Title':prefix+'forbidden','Input.Body':'Nội dung thử','Input.ResidentialGroupId':group,'Input.Audience':'ORGANIZATION','Input.OrganizationId':youth})).status,400,'Cross-organization creation accepted');
    const id=await create(org,'private');
    assert.equal((await otherOrg('/Announcements/'+id)).status,404,'Other organization can read private draft');
    assert.equal((await visitor('/Announcements/'+id)).status,302);
    assert.equal((await action(org,id,'Submit')).status,302);
    assert.equal((await action(org,id,'Review',{approve:'true'})).status,403,'Organization head can approve');
    assert.equal((await action(org,id,'Unpublish',{reason:'forbidden'})).status,403,'Organization head can remove');
    assert.equal((await action(head,id,'Review',{approve:'false',note:''})).status,302);assert.equal(state(id).ReviewStatus,'PENDING');
    assert.equal((await action(head,id,'Review',{approve:'false',note:'Bổ sung thời gian sinh hoạt.'})).status,302);assert.equal(state(id).ReviewStatus,'REJECTED');
    await action(org,id,'Submit');
    const stale=await page(head,'/Announcements/'+id);
    assert.equal((await action(head,id,'Review',{approve:'true'})).status,302);
    assert.deepEqual(state(id),{Status:'PUBLISHED',ReviewStatus:'APPROVED',ReviewedByUserId:'demo-head',PublishedByUserId:'demo-head'});
    const notifications=()=>Number(sql(`SELECT COUNT(*) FROM Activities WHERE EventCode='ANNOUNCEMENT_PUBLISHED' AND ActivityType='NOTIFICATION' AND TargetId='${id}'`));
    assert.equal(notifications(),3,'Incorrect organization recipients');
    await post(head,`/Announcements/${id}/Review`,stale,{rowVersion:input(stale,'rowVersion'),approve:'true'});assert.equal(notifications(),3,'Repeated approval duplicated notifications');
    assert.equal((await resident('/Resident/announcement/'+id)).status,200);
    assert.equal((await otherOrg('/Resident/announcement/'+id)).status,404);
    assert.equal((await outsider('/Resident/announcement/'+id)).status,404);
    assert.equal((await visitor('/thong-bao/'+id)).status,404,'Private content exposed publicly');
    const notification=sql(`SELECT Id FROM Activities WHERE RecipientUserId='demo-resident' AND TargetId='${id}' AND EventCode='ANNOUNCEMENT_PUBLISHED'`);
    const notificationsPage=await page(resident,'/Resident/notifications');
    assert.equal((await post(resident,'/Resident/notifications/'+notification+'/read',notificationsPage,{})).status,302);
    assert.notEqual(sql(`SELECT CONVERT(varchar(30),ReadAt,126) FROM Activities WHERE Id=${notification}`),'NULL');
    assert.match(await page(head,'/Announcements/'+id),/Thống kê đã đọc/);
    await action(secretary,id,'Unpublish',{reason:'Điều chỉnh nội dung sinh hoạt.'});assert.equal(state(id).Status,'UNPUBLISHED','Secretary cannot remove head-approved announcement');
    const edit=await page(org,'/Announcements/'+id+'/Edit');
    assert.equal((await post(org,'/Announcements/'+id+'/Edit',edit,{'Input.Title':prefix+'private_updated','Input.Body':'Nội dung đã được chỉnh sửa và phải xét duyệt lại.','Input.Audience':'ORGANIZATION','Input.OrganizationId':women,'Input.ResidentialGroupId':group,'Input.RowVersion':input(edit,'Input.RowVersion')})).status,302);
    assert.equal(state(id).ReviewStatus,'DRAFT','Editing did not reset approval');
    await action(org,id,'Submit');await action(secretary,id,'Review',{approve:'true'});assert.equal(state(id).Status,'PUBLISHED');assert.equal(notifications(),3,'Republishing created duplicate notifications');
    const self=await create(secretary,'self','PUBLIC',null);await action(secretary,self,'Submit');await action(secretary,self,'Review',{approve:'true'});assert.equal(state(self).Status,'PUBLISHED','Self approval rejected');
    assert.equal((await visitor('/thong-bao/'+self)).status,200);
    await action(head,self,'Unpublish',{reason:'Kết thúc bài kiểm thử.'});assert.equal(state(self).Status,'UNPUBLISHED','Head cannot remove secretary post');
    const orgPage=await page(org,'/Organizations?id='+women);
    const target=sql("SELECT ResidentId FROM AspNetUsers WHERE Id='demo-resident'");
    assert.equal((await post(otherOrg,'/Organizations/Member',await page(otherOrg,'/Organizations'),{id:women,residentId:target,active:'true'})).status,403,'Other organization manages members');
    assert.equal((await post(org,'/Organizations/Member',orgPage,{id:women,residentId:sql("SELECT ResidentId FROM AspNetUsers WHERE Id='demo-outsider'"),active:'true'})).status,200,'Cross-village member must show validation');
    const directive=await create(commune,'directive','RESIDENTS',null,{'Input.Kind':'DIRECTIVE','Input.DueAtLocal':'2030-12-01T12:00'});
    assert.equal((await action(commune,directive,'Submit')).status,302);assert.equal(state(directive).ReviewStatus,'DRAFT','Directive without original document submitted');
    let dh=await page(commune,'/Announcements/'+directive);
    const pdf=new FormData();pdf.set('__RequestVerificationToken',csrf(dh));pdf.set('rowVersion',input(dh,'rowVersion'));pdf.set('file',new Blob(['%PDF-1.4\n% Fictional smoke document\n%%EOF'],{type:'application/pdf'}),'van-ban-mau.pdf');
    assert.equal((await commune(`/Announcements/${directive}/Document`,{multipart:pdf})).status,302);
    await action(commune,directive,'Submit');await action(head,directive,'Review',{approve:'true'});assert.equal(state(directive).Status,'PUBLISHED');
    assert.equal((await resident('/Resident/announcement/'+directive+'/Document')).status,200);
    assert.equal((await outsider('/Resident/announcement/'+directive+'/Document')).status,404);
    assert.equal((await visitor('/thong-bao/'+directive+'/Document')).status,404);
    assert.equal((await head(`/Announcements/${directive}/Review`,{form:{approve:'true'}})).status,400,'Missing CSRF accepted');
    console.log('PASS: single-role DB/service enforcement; mixed SELF/GROUP rights; organization isolation; leader-only review/removal; rejection, self approval, auto publication, duplicate/stale review, republish, recipient scoping, read statistics, directives/private PDF and CSRF');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{
    try {sql(`SET XACT_ABORT ON; BEGIN TRAN; DELETE Activities WHERE TargetType='Announcement' AND TargetId IN(SELECT CONVERT(varchar(20),Id) FROM Announcements WHERE Title LIKE '${prefix}%'); DELETE Announcements WHERE Title LIKE '${prefix}%'; COMMIT;`);console.log('PASS: workflow fixtures removed');}
    catch(e){console.error('Cleanup failed: '+e.message);process.exitCode=1;}
});
