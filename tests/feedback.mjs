import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {existsSync,mkdirSync,writeFileSync} from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import {DatabaseSync} from 'node:sqlite';
import {acceptRequiredPoliciesForFixture} from './registration-fixture.mjs';

const root=process.cwd(),fixture=path.resolve(process.env.YY_TEST_DATA_ROOT??'.test-data','feedback-'+randomUUID());mkdirSync(fixture,{recursive:true});
const dotnet=existsSync('.tools/dotnet/dotnet.exe')?path.join(root,'.tools/dotnet/dotnet.exe'):process.env.DOTNET_ROOT?path.join(process.env.DOTNET_ROOT,process.platform==='win32'?'dotnet.exe':'dotnet'):'dotnet';
const password='Synthetic-'+randomUUID(),passed=[],pause=ms=>new Promise(r=>setTimeout(r,ms)),id=()=>randomUUID().replaceAll('-','');
const check=name=>{passed.push(name);console.log('PASS '+name);};
async function address(){const server=net.createServer();await new Promise(r=>server.listen(0,'127.0.0.1',r));const base='http://127.0.0.1:'+server.address().port;await new Promise(r=>server.close(r));return base;}
class Client{
 constructor(base){this.base=base;this.cookies=new Map();}
 async req(route,method='GET',body,status=200){const headers={Cookie:[...this.cookies].map(([k,v])=>k+'='+v).join('; '),'X-YY-Product':this.product??'windows','X-YY-Version':'1.0.0'};if(method!=='GET'){headers['Content-Type']='application/json';headers['X-CSRF-TOKEN']=this.csrf??'';}const response=await fetch(this.base+'/api/'+route,{method,headers,body:method==='GET'?undefined:JSON.stringify(body??{})});for(const cookie of response.headers.getSetCookie()){const value=cookie.split(';')[0],p=value.indexOf('=');this.cookies.set(value.slice(0,p),value.slice(p+1));}const text=await response.text();assert.equal(response.status,status,route+' '+text);return text?JSON.parse(text):null;}
 async login(username){this.csrf=(await this.req('session')).csrfToken;await this.req('login','POST',{username,password});this.csrf=(await this.req('session')).csrfToken;return this;}
}
for(const role of ['Server','AnnouncementServer']){
 const portal=role==='AnnouncementServer',prefix=portal?'Portal':'School',data=path.join(fixture,role),base=await address();let logs='';
 const child=spawn(dotnet,[path.join(root,'src/SchoolMessenger.'+role+'/bin/Debug/net10.0/SchoolMessenger.'+role+'.dll'),'--urls',base],{windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',[prefix+'__DataDirectory']:data,[prefix+'__AdminPassword']:password,Portal__BridgeKey:'Synthetic-'+randomUUID(),Portal__CleanupSeconds:'1',Announcements__Address:'',Feedback__AutomaticReportsEnabled:'true',FeedbackMail__Enabled:'false'}});
 child.stdout.on('data',b=>logs+=b);child.stderr.on('data',b=>logs+=b);
 try{
  let ready=false;for(let n=0;n<120;n++){if(child.exitCode!==null)throw Error(logs);try{if((await fetch(base+'/health')).ok){ready=true;break;}}catch{}await pause(100);}assert.ok(ready,logs);
  const admin=await new Client(base).login('admin'),anonymous=new Client(base);await anonymous.req('feedback','GET',undefined,401);await anonymous.req('admin/feedback','GET',undefined,401);
  let person;
  if(portal){const room=(await admin.req('admin/classes','POST',{name:'Synthetic class',grade:1})).id;person=(await admin.req('admin/people','POST',{name:'Synthetic pupil',role:'student',classId:room,classIds:[],canBroadcast:false})).id;const invite=await admin.req('admin/people/'+person+'/invite','POST');anonymous.csrf=(await anonymous.req('session')).csrfToken;await anonymous.req('register','POST',{code:invite.code,username:'ordinary',password,agreement:await acceptRequiredPoliciesForFixture(base)});}
  else person=(await admin.req('admin/users','POST',{username:'ordinary',name:'Synthetic teacher',department:'Test',password,isAdmin:false,canBroadcast:false})).id;
  const ordinary=await new Client(base).login('ordinary');
  await ordinary.req('admin/feedback/mail-status','GET',undefined,403);const mailStatus=await admin.req('admin/feedback/mail-status');assert.equal(mailStatus.enabled,false);assert.equal(mailStatus.configured,false);
  const payload={clientId:id(),kind:'bug',product:portal?'school-android':'windows',area:'messages',title:'Synthetic defect',details:'Sensitive-original-marker: click test then no response',version:'1.0.0'};
  const entry=await ordinary.req('feedback','POST',payload);assert.equal((await ordinary.req('feedback','POST',payload)).id,entry.id);await ordinary.req('feedback','POST',{...payload,title:'Different text'},409);
  check(role+': authenticated idempotent report creation rejects changed retries');
  assert.equal((await ordinary.req('feedback')).length,1);assert.equal((await admin.req('feedback')).length,0);assert.equal((await admin.req('admin/feedback')).length,1);
  await ordinary.req('admin/feedback','GET',undefined,403);await ordinary.req('admin/feedback/development-export','GET',undefined,403);
  check(role+': own reports are isolated; full listing and export require administrator');
  const db=new DatabaseSync(path.join(data,portal?'announcements.db':'school.db'));db.exec('PRAGMA busy_timeout=10000');const stored=db.prepare('SELECT Payload FROM FeedbackReports WHERE Id=?').get(entry.id);assert.ok(!stored.Payload.includes('Sensitive-original-marker'));
  assert.equal(db.prepare('SELECT COUNT(*) AS count FROM FeedbackMail WHERE ReportId=?').get(entry.id).count,1);
  await ordinary.req('admin/feedback/'+entry.id+'/retry-mail','POST',{},403);await admin.req('admin/feedback/'+entry.id+'/retry-mail','POST');
  const suggestion=await admin.req('feedback','POST',{...payload,clientId:id(),kind:'suggestion'});assert.equal(db.prepare('SELECT COUNT(*) AS count FROM FeedbackMail WHERE ReportId=?').get(suggestion.id).count,0);
  check(role+': one durable email per bug, no suggestion email and administrator-only retry');
  assert.equal((await admin.req('admin/feedback/development-export')).reports.length,0);
  const review={revision:1,status:'verified',developmentSummary:'Use synthetic accounts. Select test then observe empty result.',approveForDevelopment:true};
  await ordinary.req('admin/feedback/'+entry.id+'/review','POST',review,403);const noCsrf=new Client(base);noCsrf.cookies=new Map(admin.cookies);await noCsrf.req('admin/feedback/'+entry.id+'/review','POST',review,400);
  await admin.req('admin/feedback/'+entry.id+'/review','POST',review);await admin.req('admin/feedback/'+entry.id+'/review','POST',review,409);
  const exported=await admin.req('admin/feedback/development-export');assert.equal(exported.reports.length,1);assert.ok(!JSON.stringify(exported).includes('Sensitive-original-marker'));assert.ok(!JSON.stringify(exported).includes(person));assert.ok(!JSON.stringify(exported).includes('Synthetic pupil'));
  check(role+': encrypted original and review-controlled export omit reporter and original details; CSRF and revisions enforced');
  await ordinary.req('feedback','POST',{...payload,clientId:id(),details:'x'},400);await ordinary.req('feedback','POST',{...payload,clientId:id(),product:'untrusted'},400);
  for(let n=0;n<9;n++)await ordinary.req('feedback','POST',{...payload,clientId:id()});await ordinary.req('feedback','POST',{...payload,clientId:id()},429);await admin.req('feedback','POST',{...payload,clientId:id()});
  check(role+': invalid input and daily per-user quota are enforced independently');
  admin.product=portal?'school-android':'windows';const table=portal?'Classes':'Todos',route=portal?'admin/classes':'todos',fault=portal?{name:'PRIVATE REQUEST NAME',grade:1}:{title:'PRIVATE REQUEST MESSAGE',dueDate:null,status:'open',revision:1};
  db.exec("CREATE TRIGGER FeedbackSyntheticFailure BEFORE INSERT ON "+table+" BEGIN SELECT RAISE(ABORT, 'PRIVATE EXCEPTION SECRET'); END");
  await admin.req(route,'POST',fault,500);await admin.req(route,'POST',fault,500);db.exec('DROP TRIGGER FeedbackSyntheticFailure');
  const automatic=(await admin.req('feedback')).filter(row=>row.report.errorCode==='SqliteException');assert.equal(automatic.length,1);assert.equal(automatic[0].report.product,admin.product);assert.ok(!automatic[0].report.details.includes('PRIVATE'));assert.equal(automatic[0].mailStatus,'pending');
  check(role+': authenticated server exceptions are automatically reported once per 30 minutes without exception messages or request contents');
  if(portal){const parent=(await admin.req('admin/people','POST',{name:'Synthetic guardian',role:'parent',classId:null,classIds:[],canBroadcast:false})).id;const invite=await admin.req('admin/people/'+parent+'/invite','POST');const guest=new Client(base);guest.csrf=(await guest.req('session')).csrfToken;await guest.req('register','POST',{code:invite.code,username:'temporary',password,agreement:await acceptRequiredPoliciesForFixture(base)});const temporary=await new Client(base).login('temporary');await temporary.req('feedback','POST',{...payload,clientId:id()});assert.equal((await temporary.req('feedback')).length,1);await temporary.req('admin/feedback','GET',undefined,403);check(role+': temporary guardians can report signup problems without gaining administrative access');}
  if(portal){await admin.req('admin/graduations','POST',{studentIds:[person],at:Date.now()+1500,confirmation:'졸업 자동 탈퇴 및 파기'});await pause(2000);await ordinary.req('feedback','GET',undefined,401);for(let n=0;n<60&&db.prepare('SELECT COUNT(*) AS count FROM FeedbackReports WHERE OwnerId=?').get(person).count>0;n++)await pause(100);assert.equal(db.prepare('SELECT COUNT(*) AS count FROM FeedbackReports WHERE OwnerId=?').get(person).count,0);check(role+': graduation erases the student original and development summary');}
  db.close();
 }finally{if(child.exitCode===null){child.kill();await new Promise(r=>child.once('exit',r));}}
}
mkdirSync('artifacts',{recursive:true});writeFileSync('artifacts/feedback-checks.json',JSON.stringify({version:'1.0.0',passed},null,2));
