import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID,createHash} from 'node:crypto';
import {mkdirSync,existsSync,writeFileSync} from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import {DatabaseSync} from 'node:sqlite';
import {pathToFileURL} from 'node:url';

const root=process.cwd(),dotnet=existsSync('.tools/dotnet/dotnet.exe')?path.join(root,'.tools/dotnet/dotnet.exe'):path.join(process.env.DOTNET_ROOT,process.platform==='win32'?'dotnet.exe':'dotnet');
const data=path.join(root,'.test-data','enrollment-'+randomUUID()),password='Test-'+randomUUID(),key='Bridge-'+randomUUID();
mkdirSync(data,{recursive:true});mkdirSync('artifacts',{recursive:true});
const socket=net.createServer();await new Promise(r=>socket.listen(0,'127.0.0.1',r));const base='http://127.0.0.1:'+socket.address().port;await new Promise(r=>socket.close(r));
const delay=ms=>new Promise(r=>setTimeout(r,ms)),passed=[];let server,logs='';
const check=name=>{passed.push(name);console.log('PASS '+name);};
function start(){logs='';server=spawn(dotnet,[path.join(root,'src/SchoolMessenger.AnnouncementServer/bin/Debug/net10.0/SchoolMessenger.AnnouncementServer.dll'),'--urls',base],{cwd:path.join(root,'src/SchoolMessenger.AnnouncementServer'),windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',Portal__DataDirectory:data,Portal__AdminPassword:password,Portal__BridgeKey:key}});server.stdout.on('data',b=>logs+=b);server.stderr.on('data',b=>logs+=b);}
async function ready(){for(let i=0;i<100;i++){try{if((await fetch(base+'/health')).ok)return;}catch{}await delay(100);}throw Error(logs);}
async function stop(){if(server?.exitCode===null){server.kill();await new Promise(r=>server.once('exit',r));}}
class Client{
 cookies=new Map();csrf='';user=null;
 async req(route,method='GET',body,expected=200){
  const headers={Cookie:[...this.cookies].map(([k,v])=>k+'='+v).join('; ')};
  if(method!=='GET'){headers['Content-Type']='application/json';headers['X-CSRF-TOKEN']=this.csrf;}
  const response=await fetch(base+'/api/'+route,{method,headers,body:method==='GET'?undefined:JSON.stringify(body??{})});
  for(const item of response.headers.getSetCookie()){const value=item.split(';')[0],at=value.indexOf('=');this.cookies.set(value.slice(0,at),value.slice(at+1));}
  const text=await response.text();assert.equal(response.status,expected,route+' '+text);return text?JSON.parse(text):null;
 }
 async refresh(){const session=await this.req('session');this.csrf=session.csrfToken;this.user=session.user;return this;}
 async login(username,expected=200){await this.refresh();await this.req('login','POST',{username,password},expected);return this.refresh();}
}
async function signup(username,role='parent',extra={}){const guest=await new Client().refresh();return guest.req('register','POST',{name:'가상 '+username,username,password,role,...extra});}
async function person(admin,name,role,classId=null,internalUserId=null,classIds=[]){return(await admin.req('admin/people','POST',{name,role,classId,internalUserId,classIds,canBroadcast:false})).id;}
async function activate(admin,id,classId,active=true){await admin.req('admin/people/'+id,'PATCH',{name:'가상 학생',active,classId,classIds:[],canBroadcast:false});}
const hash=code=>createHash('sha256').update(code).digest('hex').toUpperCase();

try{
 start();await ready();const admin=await new Client().login('admin'),guest=await new Client().refresh();
 const c1=(await admin.req('admin/classes','POST',{name:'1학년 1반',grade:1})).id,c2=(await admin.req('admin/classes','POST',{name:'2학년 1반',grade:2})).id;
 assert.equal((await guest.req('registration/classes')).length,2);await guest.req('settings','GET',undefined,401);
 await guest.req('register','POST',{name:'권한 위조',username:'forged',password,role:'admin'},400);
 await guest.req('register','POST',{name:'권한 위조',username:'forged',password,role:'teacher'},400);
 await guest.req('register','POST',{name:'학생',username:'badstudent',password,role:'student',classId:c1,studentNumber:0},400);
 check('public signup exposes only class names and rejects forged roles and invalid student numbers');
 const pSignup=await signup('parent.one','parent',{active:true,canBroadcast:true,children:['fake']});assert.equal(pSignup.membership,'temporary');
 const parent=await new Client().login('parent.one');assert.equal(parent.user.membership,'temporary');assert.equal(parent.user.role,'parent');assert.equal(parent.user.canBroadcast,false);assert.deepEqual(await parent.req('children'),[]);
 for(const route of['announcements','timetable/setup','timetable/day','timetable/notices','admin/people'])await parent.req(route,'GET',undefined,403);
 assert.equal((await parent.req('settings')).studentCode,null);
 await guest.req('register','POST',{name:'중복',username:'PARENT.ONE',password,role:'parent'},409);
 check('individual parent signup is temporary; all school content is denied until a child is linked');
 const studentSignup=await signup('student.one','student',{classId:c1,studentNumber:7,active:true});assert.equal(studentSignup.membership,'pending');
 const student=await new Client().login('student.one',401);
 let roster=await admin.req('admin/people');const s1=roster.find(p=>p.username==='student.one').id;assert.equal(roster.find(p=>p.id===s1).application.pending,true);
 await admin.req('admin/people/'+s1+'/student-code','GET',undefined,400);await activate(admin,s1,c1);await student.login('student.one');
 assert.equal((await admin.req('admin/people')).find(p=>p.id===s1).application.pending,false);
 const code1=(await student.req('settings')).studentCode;assert.match(code1,/^[a-zA-Z0-9]{8}$/);assert.match(code1,/[a-z]/);assert.match(code1,/[A-Z]/);assert.match(code1,/[0-9]/);
 assert.equal((await admin.req('admin/people/'+s1+'/student-code')).code,code1);
 await signup('student.two','student',{classId:c2,studentNumber:3});roster=await admin.req('admin/people');const s2=roster.find(p=>p.username==='student.two').id;await activate(admin,s2,c2);
 const code2=(await admin.req('admin/people/'+s2+'/student-code')).code;assert.notEqual(code1,code2);
 check('students require school approval and receive stable distinct eight-character mixed-case alphanumeric codes');
 const wrongCase=[...code1].map(c=>/[a-z]/.test(c)?c.toUpperCase():/[A-Z]/.test(c)?c.toLowerCase():c).join('');
 await parent.req('settings/children','POST',{code:wrongCase},400);assert.deepEqual(await parent.req('children'),[]);
 await parent.req('settings/children','POST',{code:code1});await parent.refresh();assert.equal(parent.user.membership,'regular');assert.equal((await parent.req('children'))[0].id,s1);
 await parent.req('settings/children','POST',{code:code1});assert.equal((await parent.req('children')).length,1);
 await parent.req('settings/children','POST',{code:code2});assert.equal((await parent.req('children')).length,2);
 const setup=await parent.req('timetable/setup');assert.deepEqual(setup.classes.map(c=>c.id).sort(),[c1,c2].sort());
 check('case-sensitive child codes immediately grant regular membership; repeated entry is idempotent and multiple children work');
 await signup('parent.two');const second=await new Client().login('parent.two');await second.req('settings/children','POST',{code:code1});assert.equal((await second.req('children')).length,1);
 await student.req('settings/children','POST',{code:code2},403);await parent.req('admin/people/'+s1+'/student-code','GET',undefined,403);
 await student.req('admin/people/'+s2+'/student-code','GET',undefined,403);
 const missingCsrf=new Client();missingCsrf.cookies=new Map(parent.cookies);await missingCsrf.req('settings/children','POST',{code:code1},400);
 check('both guardians can link one child; student and parent accounts cannot read other student codes or bypass CSRF');
 const tid=await person(admin,'가상 교사','teacher',null,randomUUID().replaceAll('-',''),[c1]);
 await fetch(base+'/bridge/heartbeat',{method:'POST',headers:{Authorization:'Bearer '+key,'Content-Type':'application/json'},body:JSON.stringify({teachers:[{id:tid,active:true,canBroadcast:false}]})});
 const invite=await admin.req('admin/people/'+tid+'/invite','POST');await guest.req('register','POST',{code:invite.code,username:'teacher',password});const teacher=await new Client().login('teacher');
 const sent=await teacher.req('announcements','POST',{clientId:randomUUID().replaceAll('-',''),title:'자녀 대상 공지',body:'연결된 보호자에게만 보내는 가상 공지',audience:'both',classIds:[c1],all:false,attachmentIds:[]});
 assert.equal((await parent.req('announcements')).length,1);assert.equal((await second.req('announcements')).length,1);await signup('late.parent');const late=await new Client().login('late.parent');await late.req('settings/children','POST',{code:code1});await late.req('announcements/'+sent.id,'GET',undefined,404);
 check('connected guardians receive new scoped announcements; new relationships do not expose historical private notices');
 await signup('locked.parent');const locked=await new Client().login('locked.parent');for(let i=0;i<5;i++)await locked.req('settings/children','POST',{code:'bad'},400);
 await locked.req('settings/children','POST',{code:code1},429);assert.deepEqual(await locked.req('children'),[]);
 await stop();start();await ready();await admin.login('admin');await locked.login('locked.parent');await locked.req('settings/children','POST',{code:code1},429);await student.login('student.one');assert.equal((await student.req('settings')).studentCode,code1);
 check('five failed attempts lock a parent for fifteen minutes; lock and encrypted student code survive server restart');
 const db=new DatabaseSync(path.join(data,'announcements.db'));let keys=db.prepare('SELECT Hash,ProtectedCode FROM StudentCodes WHERE Active=1').all();assert.equal(keys.length,2);for(const row of keys)assert.ok(row.ProtectedCode!==code1&&row.ProtectedCode!==code2);assert.ok(keys.some(k=>k.Hash===hash(code1)));
 db.prepare('UPDATE FamilyCodeAttempts SET LockedUntil=? WHERE ParentId=?').run(Date.now()-1,locked.user.id);db.close();await locked.req('settings/children','POST',{code:code1});
 check('codes are encrypted with indexed hashes; an expired account lock allows a later valid attempt');
 const rotated=(await admin.req('admin/people/'+s1+'/student-code/reissue','POST')).code;assert.notEqual(rotated,code1);assert.equal((await parent.req('children')).length,2);assert.equal((await student.req('settings')).studentCode,rotated);
 await signup('new.parent');const fresh=await new Client().login('new.parent');await fresh.req('settings/children','POST',{code:code1},400);await fresh.req('settings/children','POST',{code:rotated});
 check('administrator reissue retires old code without dropping existing guardian relationships');
 await admin.req('admin/families/'+fresh.user.id+'/'+s1,'DELETE');await fresh.req('children','GET',undefined,401);await fresh.login('new.parent');assert.equal(fresh.user.membership,'temporary');await fresh.req('settings/children','POST',{code:rotated},400);await fresh.req('announcements','GET',undefined,403);
 const afterUnlink=(await student.req('settings')).studentCode;assert.notEqual(afterUnlink,rotated);assert.equal((await second.req('children')).length,1);
 check('removing a guardian downgrades an empty account and rotates the student code to prevent immediate unauthorized relinking');
 await activate(admin,s1,c1,false);await second.req('children','GET',undefined,401);await second.login('parent.two');assert.equal(second.user.membership,'temporary');await fresh.req('settings/children','POST',{code:afterUnlink},400);
 await activate(admin,s1,c1,true);const reactivated=(await admin.req('admin/people/'+s1+'/student-code')).code;assert.notEqual(reactivated,afterUnlink);await fresh.req('settings/children','POST',{code:reactivated});await second.refresh();assert.equal(second.user.membership,'regular');
 check('inactive students cannot be linked; reactivation creates a fresh code and membership follows current enrolled children');
 const more=new Set([code2,reactivated]);for(let i=0;i<20;i++){const id=await person(admin,'중복 검증 '+i,'student',c1),code=(await admin.req('admin/people/'+id+'/student-code')).code;assert.match(code,/^[a-zA-Z0-9]{8}$/);assert.ok(!more.has(code));more.add(code);}
 const db2=new DatabaseSync(path.join(data,'announcements.db'));assert.throws(()=>db2.prepare('INSERT INTO StudentCodes VALUES(?,?,?,0,0)').run(hash(code1),s2,'invalid'));assert.ok(db2.prepare('SELECT COUNT(*) AS count FROM StudentCodes WHERE Hash=? AND Active=0').get(hash(code1)).count===1);db2.close();
 check('database uniqueness blocks duplicate active and retired codes across students');
 if(process.argv.includes('--web')){
  const {chromium}=await import(process.env.YY_PLAYWRIGHT_MODULE?pathToFileURL(process.env.YY_PLAYWRIGHT_MODULE).href:'playwright');const browser=await chromium.launch({headless:true,channel:process.env.YY_BROWSER_CHANNEL??'msedge'});const context=await browser.newContext({viewport:{width:1100,height:850}}),page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  async function webLogin(username){await page.goto(base);await page.locator('#login-form [name=username]').fill(username);await page.locator('#login-form [name=password]').fill(password);await page.getByRole('button',{name:'로그인',exact:true}).click();await page.locator('#workspace').waitFor({state:'visible'});const notice=page.getByRole('button',{name:'확인',exact:true});if(await notice.isVisible())await notice.click();}
  try{
   await page.goto(base);await page.getByText('학생·학부모 회원가입',{exact:true}).click();const form=page.locator('#signup-form');await form.locator('[name=name]').fill('웹 가상 학부모');await form.locator('[name=username]').fill('web.parent');await form.locator('[name=password]').fill(password);await form.getByRole('button',{name:'회원가입 신청'}).click();await page.locator('#feedback').getByText(/임시회원 가입 완료/).waitFor();
   await webLogin('web.parent');await page.getByText('임시회원입니다. 자녀 고유번호를 입력하면 바로 정회원으로 전환됩니다.',{exact:true}).waitFor();assert.equal(await page.locator('#timetable').isVisible(),false);assert.equal(await page.locator('#news').isVisible(),false);
   await page.locator('#settings-content [name=code]').fill(reactivated);await page.locator('#settings-content').getByRole('button',{name:'자녀 연결',exact:true}).click();await page.locator('#identity').getByText(/정회원/).waitFor();await page.locator('#settings-content').getByText('가상 학생 · 1학년 1반',{exact:true}).waitFor();assert.equal(await page.locator('#timetable').isVisible(),true);
   await page.locator('#settings-content [name=code]').fill(code2);await page.locator('#settings-content').getByRole('button',{name:'자녀 연결',exact:true}).click();await page.locator('#settings-content').getByText('가상 학생 · 2학년 1반',{exact:true}).waitFor();assert.equal(await page.locator('#child option').count(),3);await page.screenshot({path:'artifacts/enrollment-web.png',fullPage:true});
   await page.locator('#logout').click();await page.locator('#login').waitFor({state:'visible'});assert.equal(await page.locator('#settings-content').innerText(),'');check('web individual signup, temporary restrictions, immediate upgrade, two children and logout clearing');
   await webLogin('student.one');await page.locator('#settings').click();await page.locator('#settings-content .code').getByText(reactivated,{exact:true}).waitFor();await page.locator('#logout').click();await page.locator('#login').waitFor({state:'visible'});assert.equal(await page.locator('#settings-content').innerText(),'');check('student web displays only its own code and removes the secret on logout');
   await page.getByText('학생·학부모 회원가입',{exact:true}).click();await form.locator('[name=role]').selectOption('student');await form.locator('[name=classId] option').nth(1).waitFor({state:'attached'});await form.locator('[name=name]').fill('웹 가상 학생');await form.locator('[name=username]').fill('web.student');await form.locator('[name=password]').fill(password);await form.locator('[name=classId]').selectOption(c2);await form.locator('[name=studentNumber]').fill('4');await form.getByRole('button',{name:'회원가입 신청'}).click();await page.locator('#feedback').getByText(/학생 가입 신청 완료/).waitFor();
   await webLogin('admin');const row=page.locator('#admin-content section.panel').filter({has:page.getByRole('heading',{name:'웹 가상 학생 · 학생',exact:true})});await row.getByLabel('학교 확인 후 학생 가입 승인',{exact:true}).check();await row.getByRole('button',{name:'가입 심사 저장'}).click();await page.locator('#confirm').getByRole('button',{name:'변경하기'}).click();const approved=page.locator('#admin-content section.panel').filter({has:page.getByRole('heading',{name:'웹 가상 학생 · 학생',exact:true})});await approved.getByRole('button',{name:'고유번호 확인',exact:true}).click();await approved.locator('.code').getByText(/학생 고유번호: [A-Za-z0-9]{8}/).waitFor();await page.locator('#logout').click();await page.locator('#login').waitFor({state:'visible'});check('web student application and administrator approval/code inspection');
   assert.deepEqual(errors,[]);check('signup and settings web flows complete without browser exceptions');
  }finally{await context.close();await browser.close();}
 }
 await stop();start();await ready();await parent.login('parent.one');let throttled=false;
 for(let i=0;i<105;i++){
  const response=await fetch(base+'/api/settings/children',{method:'POST',headers:{Cookie:[...parent.cookies].map(([k,v])=>k+'='+v).join('; '),'X-CSRF-TOKEN':parent.csrf,'Content-Type':'application/json'},body:JSON.stringify({code:code2})});
  if(response.status===429){throttled=true;break;}assert.equal(response.status,200);
 }
 assert.equal(throttled,true);check('IP rate limit also bounds repeated valid-code requests and account-based limit evasion');
 writeFileSync('artifacts/enrollment-checks.json',JSON.stringify({version:'1.0.0',verifiedAt:new Date().toISOString(),passed},null,2));console.log(passed.length+' enrollment checks passed.');
}finally{await stop();}
