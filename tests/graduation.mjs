import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {mkdirSync,readFileSync,writeFileSync,unlinkSync,statSync,utimesSync,existsSync} from 'node:fs';
import {DatabaseSync} from 'node:sqlite';
import net from 'node:net';
import path from 'node:path';
import {acceptRequiredPoliciesForFixture} from './registration-fixture.mjs';

const root=process.cwd(),sdk=existsSync('.tools/dotnet/dotnet.exe')?path.join(root,'.tools/dotnet/dotnet.exe'):path.join(process.env.DOTNET_ROOT,process.platform==='win32'?'dotnet.exe':'dotnet'),work=path.join(root,'.test-data/graduation-'+randomUUID()),portalData=path.join(work,'portal'),officeData=path.join(work,'office');
const password='Synthetic-'+randomUUID(),key='SyntheticBridge-'+randomUUID(),passed=[],servers=[];
const check=name=>{passed.push(name);console.log('PASS '+name);},uuid=()=>randomUUID().replaceAll('-',''),pause=ms=>new Promise(r=>setTimeout(r,ms));
async function port(){const s=net.createServer();await new Promise(r=>s.listen(0,'127.0.0.1',r));const p=s.address().port;await new Promise(r=>s.close(r));return 'http://127.0.0.1:'+p;}
const portal=await port(),office=await port();
async function start(project,data,base,settings={}){
 mkdirSync(data,{recursive:true});let log='';
 const child=spawn(sdk,[path.join(root,`src/SchoolMessenger.${project}/bin/Debug/net10.0/SchoolMessenger.${project}.dll`),'--urls',base],{cwd:path.join(root,'src/SchoolMessenger.'+project),windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',[project==='Server'?'School__DataDirectory':'Portal__DataDirectory']:data,[project==='Server'?'School__AdminPassword':'Portal__AdminPassword']:password,Portal__BridgeKey:key,Portal__CleanupSeconds:'1',...settings}});
 servers.push(child);child.stdout.on('data',b=>log+=b);child.stderr.on('data',b=>log+=b);
 for(let i=0;i<100;i++){try{if((await fetch(base+'/health')).ok)return child;}catch{}if(child.exitCode!==null)throw Error(log);await pause(100);}throw Error(log);
}
class Client{
 cookies=new Map();csrf='';constructor(base=portal){this.base=base;}
 async request(route,method='GET',body,expected=200){
  const headers={Cookie:[...this.cookies].map(([k,v])=>k+'='+v).join('; ')};
  if(method!=='GET'){headers['Content-Type']='application/json';headers['X-CSRF-TOKEN']=this.csrf;}
  const response=await fetch(this.base+'/api/'+route,{method,headers,body:method==='GET'?undefined:JSON.stringify(body??{})});
  for(const cookie of response.headers.getSetCookie()){const p=cookie.split(';')[0],i=p.indexOf('=');this.cookies.set(p.slice(0,i),p.slice(i+1));}
  const text=await response.text();assert.equal(response.status,expected,route+' '+text);return text?JSON.parse(text):null;
 }
 async refresh(){this.csrf=(await this.request('session')).csrfToken;return this;}
 async login(username='admin'){await this.refresh();await this.request('login','POST',{username,password,rememberLogin:true});return this.refresh();}
}
const publicDb=()=>new DatabaseSync(path.join(portalData,'announcements.db')),privateDb=()=>new DatabaseSync(path.join(officeData,'school.db'));
async function until(test){for(let i=0;i<150;i++){if(test())return;await pause(100);}throw Error('Graduation processing timeout');}
function backup(db,data){const files=[];for(const folder of ['backups','migration-backups','recovery-backups']){mkdirSync(path.join(data,folder),{recursive:true});const file=path.join(data,folder,'synthetic.db');db.prepare('VACUUM INTO ?').run(file);const time=new Date(Date.now()-3*86400000);utimesSync(file,time,time);files.push({file,time:statSync(file).mtimeMs});}return files;}
function count(db,table,column,id){return db.prepare(`SELECT COUNT(*) n FROM ${table} WHERE ${column}=?`).get(id).n;}
try{
 const originalPortal=await start('AnnouncementServer',portalData,portal);const admin=await new Client().login();
 const room=(await admin.request('admin/classes','POST',{name:'Synthetic 3-1',grade:3})).id;
 async function student(n){const id=(await admin.request('admin/people','POST',{name:'SyntheticStudent'+n+'_'+uuid(),role:'student',classId:room,internalUserId:null,classIds:[],canBroadcast:false})).id;const invite=(await admin.request('admin/people/'+id+'/invite','POST')).code;const guest=await new Client().refresh();await guest.request('register','POST',{code:invite,username:'synthetic.student.'+n,password,agreement:await acceptRequiredPoliciesForFixture(portal)});const client=await new Client().login('synthetic.student.'+n);return{id,client,code:(await admin.request('admin/people/'+id+'/student-code')).code};}
 const a=await student(1),b=await student(2),keep=await student(3);
 async function parent(n,children){const client=await new Client().refresh();await client.request('register','POST',{name:'SyntheticParent'+n+'_'+uuid(),username:'synthetic.parent.'+n,password,role:'parent',agreement:await acceptRequiredPoliciesForFixture(portal)});await client.login('synthetic.parent.'+n);const id=(await client.request('session')).user.id;for(const c of children)await client.request('settings/children','POST',{code:c.code});return{id,client};}
 const single=await parent(1,[a]),multiple=await parent(2,[a,b]);
 const due={studentIds:[a.id],at:Date.now()+3600000,confirmation:'졸업 자동 탈퇴 및 파기'};
 await a.client.request('admin/graduations','POST',due,403);
 await admin.request('admin/graduations','POST',{...due,confirmation:''},400);
 await admin.request('admin/graduations','POST',{...due,at:Date.now()-1},400);
 await admin.request('admin/graduations','POST',{...due,studentIds:[a.id,single.id]},409);
 assert.equal((await admin.request('admin/graduations')).scheduled.length,0);
 check('only school administrator can schedule verified students; confirmation, future time and atomic target validation required');
 await admin.request('admin/graduations','POST',due);await admin.request('admin/graduations/'+a.id,'DELETE');
 assert.equal((await admin.request('admin/graduations')).scheduled.length,0);await admin.request('admin/graduations','POST',due);
 check('graduation schedule can be cancelled before its time');
 let db=publicDb();const originalNames=db.prepare('SELECT Id,Name FROM People WHERE Id IN (?,?)').all(a.id,single.id);const administrator=db.prepare("SELECT Id FROM People WHERE Role='admin'").get().Id;
 const notice=uuid();db.prepare("INSERT INTO Notices(Id,SenderId,ClientId,Fingerprint,Title,Body,Audience,PublishedAt,Withdrawn) VALUES(?,?,?,'synthetic','Synthetic notice','No personal names','both',?,0)").run(notice,administrator,uuid(),Date.now());
 for(const [user,child] of [[a.id,a.id],[single.id,a.id],[multiple.id,a.id],[b.id,b.id],[multiple.id,b.id],[keep.id,keep.id]])db.prepare('INSERT INTO Targets VALUES(?,?,?,?,NULL,0)').run(notice,user,child,room);
 const section=uuid();db.prepare("INSERT INTO Sections VALUES(?,'Synthetic section','Synthetic term',1)").run(section);db.prepare('INSERT INTO SectionMembers VALUES(?,?,?)').run(section,a.id,uuid());db.prepare('INSERT INTO NoticeSectionMembers VALUES(?,?,?,?)').run(notice,section,a.id,uuid());
 const portalBackups=backup(db,portalData);db.close();
 await start('Server',officeData,office,{Announcements__Address:portal,Announcements__BridgeKey:key});const officeAdmin=await new Client(office).login();
 db=privateDb();const owner=db.prepare("SELECT Id FROM Users WHERE Username='admin'").get().Id;
 const targets=[{userId:a.id,studentId:a.id,classId:room},{userId:single.id,studentId:a.id,classId:room},{userId:multiple.id,studentId:a.id,classId:room},{userId:keep.id,studentId:keep.id,classId:room}];
 const receipts=targets.map(t=>({userId:t.userId,studentId:t.studentId===a.id?null:t.studentId,classId:room,name:originalNames.find(n=>n.Id===t.userId)?.Name??'Synthetic retained',studentName:t.studentId===a.id?originalNames[0].Name:'Synthetic retained',readAt:null,revoked:false}));
 const detail={notice:{id:notice,title:'Synthetic notice',body:'No personal names',senderName:'Synthetic teacher',publishedAt:Date.now(),readAt:null,withdrawn:false,recipientCount:targets.length,readCount:0},attachments:[],receipts};
 db.prepare("INSERT INTO ExternalOutbox(Id,OwnerId,TeacherId,ClientId,Payload,Targets,State,CreatedAt,Detail) VALUES(?,?,?,?,?,?,'synthetic-archived',?,?)").run(uuid(),owner,administrator,uuid(),'{}',JSON.stringify(targets),Date.now(),JSON.stringify(detail));
 const officeBackups=backup(db,officeData);db.close();
 originalPortal.kill();await new Promise(r=>originalPortal.once('exit',r));
 db=publicDb();db.prepare('UPDATE Graduations SET At=? WHERE StudentId=?').run(Date.now()-1,a.id);assert.equal(count(db,'People','Id',a.id),1);db.close();
 await start('AnnouncementServer',portalData,portal);
 await until(()=>{const db=publicDb();try{return count(db,'People','Id',a.id)===0&&count(db,'People','Id',single.id)===0;}finally{db.close();}});
 await a.client.request('children','GET',undefined,401);await single.client.request('children','GET',undefined,401);await new Client().login('synthetic.student.3');
 await until(()=>{const db=publicDb();try{return db.prepare('SELECT COUNT(*) n FROM PrivacyErasure').get().n===0;}finally{db.close();}});
 await admin.request('admin/graduations/'+a.id,'DELETE',undefined,409);
 check('school-confirmed deadlines survive shutdown; startup erases overdue accounts before ordinary use and completed deletion cannot be cancelled');
 check('due student and parent with their last child are erased; copied sessions stop working; another enrolled student remains');
 assert.equal((await multiple.client.request('children')).length,1);
 db=publicDb();for(const [table,column] of [['Targets','StudentId'],['StudentCodes','StudentId'],['SectionMembers','StudentId'],['NoticeSectionMembers','StudentId'],['Families','StudentId'],['Invites','PersonId'],['RegistrationAgreements','SubjectId'],['DeviceSessions','UserId']])assert.equal(count(db,table,column,a.id),0);
 assert.ok(db.prepare('SELECT COUNT(*) n FROM RetiredStudentCodes').get().n>=1);assert.throws(()=>db.exec("UPDATE BackendAudit SET Result='altered'"),/append only/);assert.throws(()=>db.exec('DELETE FROM BackendAudit'),/append only/);db.close();
 check('student relationships, scopes, credentials, sessions and agreement links removed; unrelated child and audit protection preserved');
 for(const saved of portalBackups){const db=new DatabaseSync(saved.file);assert.equal(count(db,'People','Id',a.id),0);assert.equal(count(db,'People','Id',single.id),0);assert.equal(count(db,'People','Id',keep.id),1);db.close();assert.ok(Math.abs(statSync(saved.file).mtimeMs-saved.time)<2);for(const n of originalNames)assert.ok(!readFileSync(saved.file).includes(Buffer.from(n.Name)));}
 check('all three managed public backup folders are sanitized without losing other accounts or extending backup age');
 for(const file of [path.join(officeData,'school.db'),...officeBackups.map(x=>x.file)]){const db=new DatabaseSync(file);const row=db.prepare('SELECT Targets,Detail FROM ExternalOutbox').get();assert.deepEqual(JSON.parse(row.Targets).map(t=>t.studentId),[keep.id]);assert.equal(JSON.parse(row.Detail).receipts.length,1);db.close();}
 check('signed server synchronization sanitizes teacher receipt caches and all managed office backups before acknowledging completion');
 const inactive=(await admin.request('admin/people')).find(p=>p.id===b.id);
 await admin.request('admin/people/'+b.id,'PATCH',{name:inactive.name,active:false,classId:room,classIds:[],canBroadcast:false});
 const broken=path.join(portalData,'backups','synthetic-broken.db');writeFileSync(broken,'synthetic invalid SQLite snapshot');
 await admin.request('admin/graduations','POST',{...due,studentIds:[b.id]});db=publicDb();db.prepare('UPDATE Graduations SET At=? WHERE StudentId=?').run(Date.now()-1,b.id);db.close();
 check('school can schedule erasure of a previously suspended student instead of retaining their information indefinitely');
 await until(()=>{const db=publicDb();try{return count(db,'People','Id',b.id)===0;}finally{db.close();}});
 db=publicDb();assert.equal(count(db,'People','Id',multiple.id),0);assert.ok(db.prepare('SELECT COUNT(*) n FROM PrivacyErasure WHERE PublicDone=0').get().n>0);db.close();
 check('last remaining child graduation removes parent; failed backup sanitation stays pending and cannot claim completion');
 unlinkSync(broken);await until(()=>{const db=publicDb();try{return db.prepare('SELECT COUNT(*) n FROM PrivacyErasure').get().n===0;}finally{db.close();}});
 await multiple.client.request('children','GET',undefined,401);await keep.client.request('children');
 check('failed erasure retries and completes after recovery, preserving unrelated active accounts');
}finally{for(const child of servers.reverse())if(child.exitCode===null&&child.signalCode===null){child.kill();await new Promise(r=>child.once('exit',r));}}
mkdirSync('artifacts',{recursive:true});writeFileSync('artifacts/graduation-checks.json',JSON.stringify({version:'1.0.0',verifiedAt:new Date().toISOString(),passed},null,2));console.log(passed.length+' graduation checks passed.');
