// Local fictional COMPACT-2.0 demo only; creates applicant accounts and removes its own records.
const assert=require('node:assert/strict');
const {randomUUID}=require('node:crypto');
const {spawnSync}=require('node:child_process');
const base=process.env.AUTH_TEST_URL||'http://127.0.0.1:5099';
if(!process.env.PORTAL_TEST_SQL_SERVER)throw new Error('Set PORTAL_TEST_SQL_SERVER to the local demo server used by the app.');
const prefix='onboard_'+randomUUID().replaceAll('-','');
const names=[prefix+'_head',prefix+'_member',prefix+'_legacy'];
function sql(query){const r=spawnSync('sqlcmd',['-S',process.env.PORTAL_TEST_SQL_SERVER,'-d',process.env.PORTAL_TEST_DATABASE||'QuanLyCuDan','-E','-C','-I','-b','-h','-1','-y','4000','-w','65535','-Q','SET NOCOUNT ON; '+query],{encoding:'utf8'});if(r.status!==0)throw new Error(r.stderr||r.stdout);return r.stdout.trim();}
function browser(){const jar=new Map();return async(url,{method='GET',form}={})=>{const headers={Cookie:[...jar].map(([k,v])=>`${k}=${v}`).join('; ')};if(form)headers['Content-Type']='application/x-www-form-urlencoded';const r=await fetch(base+url,{method,headers,body:form?new URLSearchParams(form):undefined,redirect:'manual',signal:AbortSignal.timeout(20000)});r.headers.getSetCookie().forEach(v=>{const p=v.split(';')[0],i=p.indexOf('=');jar.set(p.slice(0,i),p.slice(i+1));});return r;};}
function input(html,name){const tag=[...html.matchAll(/<input\b[^>]*>/g)].map(m=>m[0]).find(t=>t.includes(`name="${name}"`));assert.ok(tag,'Missing '+name);return (tag.match(/value="([^"]*)"/)?.[1]||'').replace(/&#x([0-9a-f]+);/gi,(_,hex)=>String.fromCodePoint(parseInt(hex,16))).replace(/&amp;/g,'&');}
const csrf=html=>input(html,'__RequestVerificationToken');
async function page(request,url){const r=await request(url);assert.equal(r.status,200,url+': '+(r.status!==200?await r.text():''));return r.text();}
async function login(request,user,password){const html=await page(request,'/Account/Login');return request('/Account/Login',{method:'POST',form:{UserName:user,Password:password,__RequestVerificationToken:csrf(html)}});}
const post=(request,url,token,form)=>request(url,{method:'POST',form:{...form,__RequestVerificationToken:token}});
const idFor=name=>sql(`SELECT Id FROM AspNetUsers WHERE UserName='${name}'`);
const residentFor=name=>sql(`SELECT ResidentId FROM AspNetUsers WHERE UserName='${name}'`);
async function reviewData(admin,name){const html=await page(admin,'/Registrations');const id=idFor(name);const start=html.indexOf('id="detail-'+id+'"');assert.ok(start>=0);const dialog=html.slice(start,html.indexOf('</dialog>',start));return {dialog,token:csrf(dialog),form:{UserId:id,RowVersion:input(dialog,'RowVersion'),Approve:'true',Verified:'true'}};}
(async()=>{
    const citizenId='9'+String(require('node:crypto').randomInt(10000000000,99999999999));
    const group=sql("SELECT h.ResidentialGroupId FROM AspNetUsers u JOIN Residents r ON r.Id=u.ResidentId JOIN Households h ON h.Id=r.HouseholdId WHERE u.UserName='dan01'");
    const visitor=browser(),admin=browser();
    const signup=await page(visitor,'/Account/Register');
    const fields={UserName:names[0],FullName:prefix+' Head',PhoneNumber:'0901234567',DateOfBirth:'1990-02-03',Gender:'OTHER',CitizenId:citizenId,Address:prefix+' address',GroupId:group,ResidenceStatus:'RESIDING',Password:'OnlyTest123!',ConfirmPassword:'OnlyTest123!'};
    assert.equal((await post(visitor,'/Account/Register',csrf(signup),fields)).status,302);
    assert.equal((await login(visitor,names[0],fields.Password)).status,200,'Pending login must fail');
    assert.equal((await login(admin,'admin','12345678')).status,302);
    let review=await reviewData(admin,names[0]);
    assert.ok(!review.dialog.includes('name="Verified"'),'Confirmation checkbox remains');
    assert.ok(!review.dialog.includes('name="CreateResident"'),'Creation form remains');
    assert.equal((await admin('/Registrations',{method:'POST',form:review.form})).status,400,'CSRF required');
    await post(admin,'/Registrations',review.token,{...review.form,RowVersion:'AAAAAAAAAAA='});
    assert.equal(sql(`SELECT VerificationStatus FROM AspNetUsers WHERE UserName='${names[0]}'`),'PENDING');
    await post(admin,'/Registrations',review.token,{...review.form,Verified:'false'});
    assert.equal(sql(`SELECT VerificationStatus FROM AspNetUsers WHERE UserName='${names[0]}'`),'APPROVED','Approval without resident blocked');
    assert.equal(residentFor(names[0]),'NULL');
    assert.equal(sql(`SELECT COUNT(*) FROM UserRoleAssignments a JOIN AspNetRoles r ON r.Id=a.RoleId WHERE a.UserId='${idFor(names[0])}' AND r.Name='RESIDENT'`),'0','Resident role granted without identity');
    const signed=await login(visitor,names[0],fields.Password);assert.equal(signed.status,302);assert.equal(signed.headers.get('location'),'/Account/Profile');
    await page(visitor,'/Account/Profile');
    assert.equal((await visitor('/Resident')).status,403);
    assert.equal((await visitor('/Registrations')).status,403);
    sql(`INSERT Residents(FullName,CitizenId,DateOfBirth,Gender,HouseholdId,RelationshipToHead,ResidenceStatus,MovedInAt)
        SELECT '${fields.FullName}','${citizenId}','1990-02-03','OTHER',r.HouseholdId,N'Thành viên','RESIDING',CONVERT(date,GETDATE()) FROM AspNetUsers u JOIN Residents r ON r.Id=u.ResidentId WHERE u.UserName='dan01'`);
    const residentId=sql(`SELECT Id FROM Residents WHERE CitizenId='${citizenId}'`);
    const approvedPage=await page(admin,'/Registrations?status=APPROVED');
    const start=approvedPage.indexOf('id="detail-'+idFor(names[0])+'"');assert.ok(start>=0);
    const dialog=approvedPage.slice(start,approvedPage.indexOf('</dialog>',start));
    const link={UserId:idFor(names[0]),RowVersion:input(dialog,'RowVersion'),Approve:'true',ResidentId:residentId};
    await post(admin,'/Registrations',csrf(dialog),{...link,ResidentId:'9223372036854775807'});
    assert.equal(residentFor(names[0]),'NULL','Wrong resident accepted');
    await post(admin,'/Registrations',csrf(dialog),link);
    assert.equal(residentFor(names[0]),residentId);
    assert.equal(sql(`SELECT COUNT(*) FROM UserRoleAssignments a JOIN AspNetRoles r ON r.Id=a.RoleId WHERE a.UserId='${idFor(names[0])}' AND a.ScopeType='SELF' AND a.RevokedAt IS NULL AND r.Name IN('ACCOUNT','RESIDENT')`),'1');
    await post(admin,'/Registrations',csrf(dialog),link);
    assert.equal(sql(`SELECT COUNT(*) FROM Activities WHERE EventCode='RESIDENT_LINK_COMPLETED' AND TargetId='${idFor(names[0])}'`),'1');
    const other=browser();const otherSignup=await page(other,'/Account/Register');
    await post(other,'/Account/Register',csrf(otherSignup),{...fields,UserName:names[1],FullName:prefix+' Member'});
    review=await reviewData(admin,names[1]);await post(admin,'/Registrations',review.token,review.form);
    assert.equal(sql(`SELECT VerificationStatus FROM AspNetUsers WHERE UserName='${names[1]}'`),'APPROVED');
    assert.equal(residentFor(names[1]),'NULL','Linked identity reused');
    const missing=browser();const missingSignup=await page(missing,'/Account/Register');
    await post(missing,'/Account/Register',csrf(missingSignup),{...fields,UserName:names[2],FullName:prefix+' Legacy',CitizenId:''});
    review=await reviewData(admin,names[2]);await post(admin,'/Registrations',review.token,{...review.form,Verified:'false'});
    assert.equal(sql(`SELECT VerificationStatus FROM AspNetUsers WHERE UserName='${names[2]}'`),'APPROVED','Missing CCCD blocks direct approval');
    assert.equal(residentFor(names[2]),'NULL');
    assert.equal((await login(missing,names[2],fields.Password)).status,302);
    console.log('PASS: direct approval without CCCD/resident/checkbox, account-only permissions, login, later matched link, no identity reuse, stale/tampered/repeated review, CSRF and reviewer authorization');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{
    try{
        sql(`SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @users TABLE(Id nvarchar(450)); INSERT @users SELECT Id FROM AspNetUsers WHERE UserName IN('${names[0]}','${names[1]}','${names[2]}');
            DECLARE @residents TABLE(Id bigint); INSERT @residents SELECT Id FROM Residents WHERE FullName IN('${prefix} Head','${prefix} Member','${prefix} Legacy');
            DECLARE @houses TABLE(Id bigint); INSERT @houses SELECT Id FROM Households WHERE Address='${prefix} address' AND CreatedByUserId='u-admin';
            DECLARE @requests TABLE(Id bigint); INSERT @requests SELECT Id FROM Requests WHERE ResidentId IN(SELECT Id FROM @residents);
            DELETE Activities WHERE RequestId IN(SELECT Id FROM @requests) OR ActorUserId IN(SELECT Id FROM @users) OR RecipientUserId IN(SELECT Id FROM @users) OR (TargetType='User' AND TargetId IN(SELECT Id FROM @users));
            DELETE Requests WHERE Id IN(SELECT Id FROM @requests);
            UPDATE AspNetUsers SET ResidentId=NULL WHERE Id IN(SELECT Id FROM @users);
            UPDATE Households SET HeadResidentId=NULL WHERE Id IN(SELECT Id FROM @houses);
            DELETE Residents WHERE Id IN(SELECT Id FROM @residents); DELETE Households WHERE Id IN(SELECT Id FROM @houses);
            DELETE UserRoleAssignments WHERE UserId IN(SELECT Id FROM @users);
            UPDATE RefreshTokens SET ReplacedByTokenId=NULL WHERE SessionId IN(SELECT Id FROM UserSessions WHERE UserId IN(SELECT Id FROM @users));
            DELETE RefreshTokens WHERE SessionId IN(SELECT Id FROM UserSessions WHERE UserId IN(SELECT Id FROM @users));
            DELETE UserSessions WHERE UserId IN(SELECT Id FROM @users); DELETE AspNetUsers WHERE Id IN(SELECT Id FROM @users); COMMIT;`);
        console.log('PASS: onboarding fixture accounts, residents, household and approval history removed');
    }catch(e){console.error('Cleanup failed for '+prefix,e.message);process.exitCode=1;}
});
