// Local fictional COMPACT-2.0 demo only; creates applicant accounts and removes its own records.
const assert=require('node:assert/strict');
const {randomUUID}=require('node:crypto');
const {spawnSync}=require('node:child_process');
const base=process.env.AUTH_TEST_URL||'http://127.0.0.1:5099';
if(!process.env.PORTAL_TEST_SQL_SERVER)throw new Error('Set PORTAL_TEST_SQL_SERVER to the local demo server used by the app.');
const groupCode=process.env.PORTAL_TEST_GROUP_CODE;
if(!groupCode || !/^[A-Za-z0-9_-]{1,30}$/.test(groupCode))throw new Error('Set PORTAL_TEST_GROUP_CODE to the explicitly configured portal locality.');
const prefix='onboard_'+randomUUID().replaceAll('-','');
const names=[prefix+'_head',prefix+'_member',prefix+'_legacy'];
function sql(query){const r=spawnSync('sqlcmd',['-S',process.env.PORTAL_TEST_SQL_SERVER,'-d','QuanLyCuDan','-E','-C','-I','-b','-h','-1','-y','4000','-w','65535','-Q','SET NOCOUNT ON; '+query],{encoding:'utf8'});if(r.status!==0)throw new Error(r.stderr||r.stdout);return r.stdout.trim();}
function browser(){const jar=new Map();return async(url,{method='GET',form}={})=>{const headers={Cookie:[...jar].map(([k,v])=>`${k}=${v}`).join('; ')};if(form)headers['Content-Type']='application/x-www-form-urlencoded';const r=await fetch(base+url,{method,headers,body:form?new URLSearchParams(form):undefined,redirect:'manual',signal:AbortSignal.timeout(20000)});r.headers.getSetCookie().forEach(v=>{const p=v.split(';')[0],i=p.indexOf('=');jar.set(p.slice(0,i),p.slice(i+1));});return r;};}
function input(html,name){const tag=[...html.matchAll(/<input\b[^>]*>/g)].map(m=>m[0]).find(t=>t.includes(`name="${name}"`));assert.ok(tag,'Missing '+name);return (tag.match(/value="([^"]*)"/)?.[1]||'').replace(/&#x([0-9a-f]+);/gi,(_,hex)=>String.fromCodePoint(parseInt(hex,16))).replace(/&amp;/g,'&');}
const csrf=html=>input(html,'__RequestVerificationToken');
async function page(request,url){const r=await request(url);assert.equal(r.status,200,url+': '+(r.status!==200?await r.text():''));return r.text();}
async function login(request,user,password){const html=await page(request,'/Account/Login');return request('/Account/Login',{method:'POST',form:{UserName:user,Password:password,__RequestVerificationToken:csrf(html)}});}
const post=(request,url,token,form)=>request(url,{method:'POST',form:{...form,__RequestVerificationToken:token}});
const idFor=name=>sql(`SELECT Id FROM AspNetUsers WHERE UserName='${name}'`);
const residentFor=name=>sql(`SELECT ResidentId FROM AspNetUsers WHERE UserName='${name}'`);
async function reviewData(admin,name){const html=await page(admin,'/Registrations');const id=idFor(name);const start=html.indexOf('id="detail-'+id+'"');assert.ok(start>=0);const dialog=html.slice(start,html.indexOf('</dialog>',start));return {dialog,token:csrf(dialog),form:{UserId:id,RowVersion:input(dialog,'RowVersion'),Approve:'true',Verified:'true',CreateResident:'true'}};}
(async()=>{
    assert.equal(sql("SELECT COUNT(*) FROM SchemaVersions WHERE VersionCode='COMPACT-2.0'"),'1');
    const group=sql(`SELECT g.Id FROM ResidentialUnits g JOIN ResidentialUnits a ON a.Id=g.ParentUnitId WHERE g.Code='${groupCode}' AND g.UnitType='GROUP' AND g.IsActive=1 AND a.UnitType='AREA' AND a.IsActive=1`);
    assert.ok(/^\d+$/.test(group),'Configured portal locality must exist and be active');
    const visitor=browser();
    const signup=await page(visitor,'/Account/Register');
    for(const field of ['DateOfBirth','Gender','CitizenId','Address'])assert.ok(signup.includes(`name="${field}"`));
    assert.ok(!signup.includes('name="GroupId"'),'Signup locality must be assigned by the server');
    const fields={UserName:names[0],FullName:prefix+' Head',PhoneNumber:'0901234567',DateOfBirth:'1990-02-03',Gender:'OTHER',Occupation:'Fictional test',Address:prefix+' address',GroupId:group,ResidenceStatus:'TEMPORARY',Password:'OnlyTest!123456',ConfirmPassword:'OnlyTest!123456'};
    const invalid=await post(visitor,'/Account/Register',csrf(signup),{...fields,DateOfBirth:'2999-01-01',CitizenId:'123'});
    assert.equal(invalid.status,200);assert.equal(idFor(names[0]),'');
    assert.equal((await post(visitor,'/Account/Register',csrf(signup),fields)).status,302);
    const pending=JSON.parse(sql(`SELECT VerificationStatus,ResidentId,RegistrationProfile FROM AspNetUsers WHERE UserName='${names[0]}' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER`));
    assert.equal(pending.VerificationStatus,'PENDING');assert.equal(pending.ResidentId,undefined);
    const saved=JSON.parse(pending.RegistrationProfile);assert.equal(saved.Address,fields.Address);assert.equal(saved.DateOfBirth,fields.DateOfBirth);assert.ok(!pending.RegistrationProfile.includes('Password'));assert.ok(!pending.RegistrationProfile.includes('OnlyTest'));
    assert.equal(sql(`SELECT COUNT(*) FROM Residents WHERE FullName='${fields.FullName}'`),'0','Pending signup created official resident');
    assert.equal(sql(`SELECT COUNT(*) FROM UserRoleAssignments WHERE UserId='${idFor(names[0])}'`),'0');
    assert.equal((await login(visitor,names[0],fields.Password)).status,200,'Pending applicant should remain on login with validation error');
    assert.equal((await visitor('/Resident')).status,302);
    const admin=browser();assert.equal((await login(admin,'admin','12345678')).status,302);
    const review=await reviewData(admin,names[0]);assert.ok(review.dialog.includes(fields.Address));assert.ok(review.dialog.includes('name="CreateResident"'));
    assert.equal((await post(admin,'/Registrations',review.token,{...review.form,Verified:'false'})).status,302);
    assert.equal(residentFor(names[0]),'NULL','Approval bypassed verification confirmation');
    assert.equal((await post(admin,'/Registrations',review.token,{...review.form,RowVersion:'AAAAAAAAAAA='})).status,302);
    assert.equal(residentFor(names[0]),'NULL');
    assert.equal((await post(admin,'/Registrations',review.token,{...review.form,HouseholdId:'9223372036854775807',RelationshipToHead:'Child'})).status,302);
    assert.equal(sql(`SELECT COUNT(*) FROM Residents WHERE FullName='${fields.FullName}'`),'0','Failed approval left resident behind');
    assert.equal((await post(admin,'/Registrations',review.token,review.form)).status,302);
    const residentId=residentFor(names[0]);assert.ok(/^\d+$/.test(residentId));
    const resident=JSON.parse(sql(`SELECT r.HouseholdId,h.HeadResidentId,r.FullName,r.ResidenceStatus,r.RelationshipToHead,h.Address FROM Residents r JOIN Households h ON h.Id=r.HouseholdId WHERE r.Id=${residentId} FOR JSON PATH,WITHOUT_ARRAY_WRAPPER`));
    assert.equal(String(resident.HeadResidentId),residentId);assert.equal(resident.ResidenceStatus,'TEMPORARY');assert.equal(resident.Address,fields.Address);
    assert.equal(sql(`SELECT COUNT(*) FROM UserRoleAssignments a JOIN AspNetRoles r ON r.Id=a.RoleId WHERE a.UserId='${idFor(names[0])}' AND a.ScopeType='SELF' AND r.Name IN('RESIDENT','ACCOUNT')`),'2');
    assert.equal(sql(`SELECT COUNT(*) FROM Requests WHERE ResidentId=${residentId} AND Source='STAFF' AND Status='APPROVED' AND ChangeAppliedAt IS NOT NULL`),'1');
    assert.equal((await post(admin,'/Registrations',review.token,review.form)).status,302);
    assert.equal(sql(`SELECT COUNT(*) FROM Residents WHERE FullName='${fields.FullName}'`),'1','Repeat approval duplicated resident');
    const signed=await login(visitor,names[0],fields.Password);assert.equal(signed.status,302);assert.equal(signed.headers.get('location'),'/Resident');
    for(const route of ['/Resident','/Resident/profile','/Resident/household','/Resident/fees'])await page(visitor,route);
    const other=browser();const otherSignup=await page(other,'/Account/Register');
    const memberFields={...fields,UserName:names[1],FullName:prefix+' Member',ResidenceStatus:'RESIDING'};
    assert.equal((await post(other,'/Account/Register',csrf(otherSignup),memberFields)).status,302);
    const memberReview=await reviewData(admin,names[1]);
    assert.equal((await post(admin,'/Registrations',memberReview.token,{...memberReview.form,HouseholdId:String(resident.HouseholdId),RelationshipToHead:'Member'})).status,302);
    const memberId=residentFor(names[1]);assert.ok(/^\d+$/.test(memberId));
    assert.equal(sql(`SELECT HouseholdId FROM Residents WHERE Id=${memberId}`),String(resident.HouseholdId));
    assert.equal(sql(`SELECT HeadResidentId FROM Households WHERE Id=${resident.HouseholdId}`),residentId,'Joining household replaced existing head');
    sql(`INSERT AspNetUsers(Id,UserName,NormalizedUserName,FullName,PasswordHash,SecurityStamp,ConcurrencyStamp,VerificationStatus,PhoneNumber)
        SELECT '${names[2]}','${names[2]}',UPPER('${names[2]}'),'${prefix} Legacy',PasswordHash,CONVERT(nvarchar(36),NEWID()),CONVERT(nvarchar(36),NEWID()),'APPROVED','0900000000' FROM AspNetUsers WHERE UserName='dan03'`);
    const legacy=browser();const legacyLogin=await login(legacy,names[2],'12345678');assert.equal(legacyLogin.status,302);assert.equal(legacyLogin.headers.get('location'),'/Account/Profile');
    assert.ok((await page(legacy,'/Account/Profile')).includes('/Account/ResidentRegistration'));
    const supplement=await page(legacy,'/Account/ResidentRegistration');
    const supplementFields={DateOfBirth:'1988-01-01',Gender:'OTHER',Address:fields.Address,GroupId:group,ResidenceStatus:'RESIDING',rowVersion:input(supplement,'rowVersion')};
    assert.equal((await legacy('/Account/ResidentRegistration',{method:'POST',form:supplementFields})).status,400,'Supplement requires CSRF');
    assert.equal((await post(legacy,'/Account/ResidentRegistration',csrf(supplement),{...supplementFields,rowVersion:'AAAAAAAAAAA='})).status,409);
    assert.equal((await post(legacy,'/Account/ResidentRegistration',csrf(supplement),supplementFields)).status,302);
    assert.equal(sql(`SELECT VerificationStatus FROM AspNetUsers WHERE UserName='${names[2]}'`),'PENDING');
    assert.equal(residentFor(names[2]),'NULL');assert.equal((await legacy('/Account/Profile')).status,302,'Supplement should end active login');
    const legacyReview=await reviewData(admin,names[2]);
    assert.equal((await post(admin,'/Registrations',legacyReview.token,{...legacyReview.form,HouseholdId:String(resident.HouseholdId),RelationshipToHead:'Member'})).status,302);
    assert.ok(/^\d+$/.test(residentFor(names[2])));
    assert.equal((await visitor('/Account/ResidentRegistration')).status,403,'Linked account must not resubmit its identity');
    assert.equal((await admin('/Account/ResidentRegistration')).status,403,'Admin must not register itself as a resident');
    console.log('PASS: existing approved/unlinked account can submit missing profile, returns to pending, logs out and receives independent approval');
    console.log('PASS: complete signup data, validation, pending-only storage, no password in profile, independent verified approval, rollback/stale guards, new household/head, joining existing household, audit snapshots, automatic role grants, repeat approval safety and resident login');
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
