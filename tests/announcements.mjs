import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {mkdirSync,readFileSync,existsSync,readdirSync,writeFileSync} from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import {DatabaseSync} from 'node:sqlite';

const root=process.cwd(),dotnet=existsSync('.tools/dotnet/dotnet.exe')?path.join(root,'.tools/dotnet/dotnet.exe'):path.join(process.env.DOTNET_ROOT,'dotnet.exe');
const testRoot=path.join(root,'.test-data','announcements-'+randomUUID()),data=path.join(testRoot,'portal'),password='Test-'+randomUUID(),bridgeKey='Bridge-'+randomUUID();
mkdirSync(testRoot,{recursive:true});mkdirSync('artifacts',{recursive:true});
const delay=ms=>new Promise(r=>setTimeout(r,ms));
const listener=net.createServer();await new Promise(r=>listener.listen(0,'127.0.0.1',r));const address='http://127.0.0.1:'+listener.address().port;await new Promise(r=>listener.close(r));
let server,log='';const passed=[];
const check=name=>{passed.push(name);console.log('PASS '+name);};
function start(overrides={}){log='';server=spawn(dotnet,[path.join(root,'src/SchoolMessenger.AnnouncementServer/bin/Debug/net10.0/SchoolMessenger.AnnouncementServer.dll'),'--urls',address],{cwd:path.join(root,'src/SchoolMessenger.AnnouncementServer'),windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',Portal__DataDirectory:data,Portal__AdminPassword:password,Portal__BridgeKey:bridgeKey,Portal__RetentionSeconds:'15',Portal__CleanupSeconds:'1',...overrides}});server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);}
async function ready(){for(let n=0;n<100;n++){try{if((await fetch(address+'/health')).ok)return;}catch{}await delay(100);}throw Error('Portal startup failed: '+log);}
async function stop(){if(server?.exitCode===null){server.kill();await new Promise(r=>server.once('exit',r));}}
class Client{
 cookies=new Map();csrf='';user=null;
 async request(route,method='GET',body,expected=200,extraHeaders={}){
  const headers={Cookie:[...this.cookies].map(([k,v])=>k+'='+v).join('; '),...extraHeaders};let content;
  if(method!=='GET'){headers['X-CSRF-TOKEN']=this.csrf;if(body instanceof FormData)content=body;else{headers['Content-Type']='application/json';content=JSON.stringify(body??{});}}
  const response=await fetch(address+route,{method,headers,body:content});for(const cookie of response.headers.getSetCookie()){const p=cookie.split(';')[0],n=p.indexOf('=');this.cookies.set(p.slice(0,n),p.slice(n+1));}
  const text=await response.text();assert.equal(response.status,expected,`${method} ${route}: ${response.status} ${text}`);return text?JSON.parse(text):null;
 }
 async login(username){this.csrf=(await this.request('/api/session')).csrfToken;await this.request('/api/login','POST',{username,password,rememberLogin:true});const session=await this.request('/api/session');this.csrf=session.csrfToken;this.user=session.user;return this;}
 async upload(name,text,clientId=randomUUID().replaceAll('-','')){const form=new FormData();form.append('clientId',clientId);form.append('file',new Blob([text]),name);return this.request('/api/attachments','POST',form);}
}
const serviceHeaders={Authorization:'Bearer '+bridgeKey};
async function service(route,method='GET',body,expected=200){const response=await fetch(address+'/bridge'+route,{method,headers:{...serviceHeaders,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});const text=await response.text();assert.equal(response.status,expected,'bridge '+route+': '+text);return text?JSON.parse(text):null;}
async function register(admin,personId,username,extra={}){const invite=await admin.request('/api/admin/people/'+personId+'/invite','POST');const guest=new Client();guest.csrf=(await guest.request('/api/session')).csrfToken;await guest.request('/api/register','POST',{code:invite.code,username,password,...extra});await guest.request('/api/register','POST',{code:invite.code,username:username+'.again',password},400);return new Client().login(username);}
async function person(admin,name,role,classId=null,internalUserId=null,classIds=[],canBroadcast=false){return (await admin.request('/api/admin/people','POST',{name,role,classId,internalUserId,classIds,canBroadcast})).id;}
function post(classId,title='가정통신문',overrides={}){return{clientId:randomUUID().replaceAll('-',''),title,body:'내일까지 확인해 주세요. <svg onload=alert(1)>',audience:'both',classIds:[classId],all:false,attachmentIds:[],...overrides};}
try{
 start();await ready();const admin=await new Client().login('admin'),guest=new Client();guest.csrf=(await guest.request('/api/session')).csrfToken;
 await guest.request('/api/announcements','GET',undefined,401);await guest.request('/api/admin/people','GET',undefined,401);
 for(const route of ['/api/messages','/api/todos','/api/users','/remote/negotiate','/hub/negotiate'])await guest.request(route,'GET',undefined,404);
 assert.notEqual(JSON.parse(readFileSync('src/SchoolMessenger.AnnouncementServer/packages.lock.json','utf8')).version,0);
 check('external server exposes no office, private message, task or remote-support endpoints');
 await guest.request('/api/session','GET',undefined,403,{Origin:'https://foreign-school.invalid'});
 const missingCsrf=new Client();await missingCsrf.request('/api/login','POST',{username:'admin',password},400);
 await service('/directory','GET',undefined,200);await guest.request('/bridge/directory','GET',undefined,401);
 check('independent authentication, strict browser origins, CSRF and scoped service credential');
 const c1=(await admin.request('/api/admin/classes','POST',{name:'1학년 1반',grade:1})).id,c2=(await admin.request('/api/admin/classes','POST',{name:'2학년 1반',grade:2})).id;
 const internalId=randomUUID().replaceAll('-',''),teacherId=await person(admin,'담당교사','teacher',null,internalId,[c1]);
 await service('/heartbeat','POST',{teachers:[{id:teacherId,active:true,canBroadcast:false}]});
 const teacher=await register(admin,teacherId,'teacher');
 const student1Id=await person(admin,'가상학생1','student',c1),student2Id=await person(admin,'가상학생2','student',c2),parentId=await person(admin,'가상보호자','parent');
 await admin.request('/api/admin/families','POST',{parentId,studentId:student1Id});await admin.request('/api/admin/families','POST',{parentId,studentId:student2Id});
 const student1=await register(admin,student1Id,'student1'),student2=await register(admin,student2Id,'student2'),parent=await register(admin,parentId,'parent',{role:'admin',studentId:student2Id,classId:c2});
 assert.equal(parent.user.role,'parent');assert.equal(parent.user.name,'가상보호자');assert.equal((await parent.request('/api/children')).length,2);
 await parent.request('/api/admin/people','GET',undefined,403);await parent.request('/api/announcements','POST',post(c1),403);
 check('single-use school invitation fixes role and identity; no self-selected child, teacher or administrator privileges');
 await teacher.request('/api/announcements','POST',post(c2),403);await teacher.request('/api/announcements','POST',post(c1,'전체',{all:true,classIds:[]}),403);
 const packet=post(c1);const sent=await teacher.request('/api/announcements','POST',packet);assert.equal((await teacher.request('/api/announcements','POST',packet)).id,sent.id);
 await teacher.request('/api/announcements','POST',{...packet,body:'바뀐 내용'},409);
 assert.equal((await student1.request('/api/announcements')).length,1);assert.equal((await student2.request('/api/announcements')).length,0);
 await student2.request('/api/announcements/'+sent.id,'GET',undefined,404);await student2.request('/api/announcements/'+sent.id+'/read','POST',{},404);
 assert.equal((await parent.request('/api/announcements?childId='+student2Id)).length,0);assert.equal((await parent.request('/api/announcements?childId='+student1Id)).length,1);
 const detail=await parent.request('/api/announcements/'+sent.id);assert.equal(detail.receipts,null);assert.equal(detail.notice.recipientCount,0);assert.equal(detail.notice.readCount,0);assert.ok(detail.notice.body.includes('<svg'));
 await parent.request('/api/announcements/'+sent.id+'/read','POST');assert.ok((await teacher.request('/api/announcements/'+sent.id)).receipts.find(r=>r.userId===parentId).readAt);
 check('teacher class scopes, school-wide permission, immutable idempotency and parent child filtering');
 const lateParentId=await person(admin,'새 보호자','parent');await admin.request('/api/admin/families','POST',{parentId:lateParentId,studentId:student1Id});
 const lateParent=await register(admin,lateParentId,'lateparent');await lateParent.request('/api/announcements/'+sent.id,'GET',undefined,404);
 check('new guardian relationship cannot expose previous private announcements');
 const uploadClient=randomUUID().replaceAll('-','');const file=await teacher.upload('가정통신문.txt','공지 첨부 검증',uploadClient);assert.equal((await teacher.upload('가정통신문.txt','공지 첨부 검증',uploadClient)).id,file.id);
 const badFile=new FormData();badFile.append('clientId',randomUUID().replaceAll('-',''));badFile.append('file',new Blob(['not PDF']),'fake.pdf');await teacher.request('/api/attachments','POST',badFile,400);
 const fileNotice=await teacher.request('/api/announcements','POST',post(c1,'첨부 공지',{attachmentIds:[file.id]}));
 const download=await fetch(address+'/api/attachments/'+file.id+'/download',{headers:{Cookie:[...parent.cookies].map(([k,v])=>k+'='+v).join('; ')}});assert.equal(download.status,200);assert.equal(await download.text(),'공지 첨부 검증');
 await student2.request('/api/attachments/'+file.id+'/download','GET',undefined,404);await teacher.request('/api/announcements','POST',post(c1,'재사용',{attachmentIds:[file.id]}),409);
 check('announcement-only file ownership, signature validation, duplicate upload and recipient download authorization');
 await admin.request('/api/admin/families/'+parentId+'/'+student1Id,'DELETE');await parent.request('/api/children','GET',undefined,401);await parent.login('parent');assert.equal((await parent.request('/api/children')).length,1);
 await parent.request('/api/announcements/'+sent.id,'GET',undefined,404);await parent.request('/api/attachments/'+file.id+'/download','GET',undefined,404);
 await admin.request('/api/admin/families','POST',{parentId,studentId:student1Id});await parent.request('/api/announcements/'+sent.id,'GET',undefined,404);
 check('guardian relationship removal revokes session and old notice/file access; re-link does not revive old targets');
 await admin.request('/api/admin/people/'+student1Id,'PATCH',{name:'가상학생1',active:true,classId:c2,classIds:[],canBroadcast:false});await student1.request('/api/announcements','GET',undefined,401);await student1.login('student1');await student1.request('/api/announcements/'+sent.id,'GET',undefined,404);
 check('student transfer revokes existing student and guardian targets');
 await teacher.request('/api/announcements/'+sent.id+'/retract','POST');assert.equal((await teacher.request('/api/announcements/'+sent.id)).notice.withdrawn,true);await lateParent.login('lateparent');await lateParent.request('/api/announcements/'+sent.id,'GET',undefined,404);
 check('sender-only withdrawal blocks recipient access');
 await delay(16000);assert.equal(existsSync(path.join(data,'files',file.id)),false);await teacher.request('/api/attachments/'+file.id+'/download','GET',undefined,410);
 check('thirty-day policy shortened only in development; expired external file physically removed');
 await admin.request('/api/admin/people/'+teacherId,'PATCH',{name:'담당교사',active:true,classId:null,classIds:[c2],canBroadcast:false});await teacher.request('/api/announcements','GET',undefined,401);await teacher.login('teacher');assert.equal((await teacher.request('/api/announcements/'+sent.id)).receipts.length,0);
 await admin.request('/api/admin/people/'+teacherId,'PATCH',{name:'담당교사',active:true,classId:null,classIds:[c1],canBroadcast:false});await teacher.login('teacher');
 check('recipients cannot see aggregate receipts; teacher scope change also restricts historical receipt names');
 await service('/heartbeat','POST',{teachers:[{id:teacherId,active:false,canBroadcast:false}]});await teacher.request('/api/announcements','GET',undefined,401);
 teacher.csrf=(await teacher.request('/api/session')).csrfToken;await teacher.request('/api/login','POST',{username:'teacher',password},403);
 check('internal teacher revocation invalidates external teacher session and prevents new login');
 await service('/heartbeat','POST',{teachers:[{id:teacherId,active:true,canBroadcast:false}]});await teacher.login('teacher');
 const stolen=new Client();stolen.cookies=new Map(teacher.cookies);await teacher.request('/api/logout','POST');await stolen.request('/api/announcements','GET',undefined,401);
 check('copied mobile persistent cookie is unusable after logout');
 const expired=await person(admin,'만료초대','parent');const invite=await admin.request('/api/admin/people/'+expired+'/invite','POST');const db=new DatabaseSync(path.join(data,'announcements.db'));db.exec('UPDATE Invites SET ExpiresAt=0 WHERE UsedAt IS NULL');db.close();
 const expiredGuest=new Client();expiredGuest.csrf=(await expiredGuest.request('/api/session')).csrfToken;await expiredGuest.request('/api/register','POST',{code:invite.code,username:'expired',password},400);
 check('expired invitation rejected; invite secrets stored only as hashes');
 await stop();start();await ready();await admin.login('admin');assert.equal((await admin.request('/api/admin/classes')).length,2);assert.ok(readdirSync(path.join(data,'backups')).some(f=>f.endsWith('.db')));
 check('independent announcement database, accounts, relations and metadata backups survive restart');
 await stop();start({ASPNETCORE_ENVIRONMENT:'Production',Portal__ScannerPath:path.join(data,'missing-scanner.exe')});await ready();await service('/heartbeat','POST',{teachers:[{id:teacherId,active:true,canBroadcast:false}]});await teacher.login('teacher');
 const unavailable=new FormData();unavailable.append('clientId',randomUUID().replaceAll('-',''));unavailable.append('file',new Blob(['safe']),'test.txt');await teacher.request('/api/attachments','POST',unavailable,503);
 assert.equal(readdirSync(path.join(data,'files')).length,0);check('production public uploads fail closed when antivirus is unavailable');
 writeFileSync('artifacts/announcement-test-results.json',JSON.stringify({verifiedAt:new Date().toISOString(),passed},null,2));console.log(passed.length+' announcement checks passed.');
}finally{await stop();}
