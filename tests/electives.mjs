import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {DatabaseSync} from 'node:sqlite';
import net from 'node:net';
import path from 'node:path';
import {acceptRequiredPoliciesForFixture} from './registration-fixture.mjs';
import {bridgeFetch} from './bridge-client.mjs';

const root=process.cwd(),sdk=existsSync('.tools/dotnet/dotnet.exe')?path.join(root,'.tools/dotnet/dotnet.exe'):process.env.DOTNET_ROOT?path.join(process.env.DOTNET_ROOT,process.platform==='win32'?'dotnet.exe':'dotnet'):'dotnet',id=()=>randomUUID().replaceAll('-','');
const fixture=path.resolve(process.env.YY_TEST_DATA_ROOT??'.test-data','electives-'+randomUUID()),password='Synthetic-'+randomUUID(),key='Synthetic-'+randomUUID()+randomUUID();mkdirSync(fixture,{recursive:true});
const passed=[],children=[];const check=name=>{passed.push(name);console.log('PASS '+name);},delay=ms=>new Promise(r=>setTimeout(r,ms));
async function address(){const s=net.createServer();await new Promise(r=>s.listen(0,'127.0.0.1',r));const base='http://127.0.0.1:'+s.address().port;await new Promise(r=>s.close(r));return base;}
async function start(role,base,extra={}){
 const prefix=role==='Server'?'School':'Portal',packageRoot=process.env.YY_PACKAGE_ROOT;
 const processChild=spawn(packageRoot?path.join(packageRoot,role==='Server'?'server':'announcement-server','SchoolMessenger.'+role+'.exe'):sdk,packageRoot?['--urls',base]:[path.join(root,'src/SchoolMessenger.'+role+'/bin/Debug/net10.0/SchoolMessenger.'+role+'.dll'),'--urls',base],{windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',[prefix+'__DataDirectory']:path.join(fixture,role),[prefix+'__AdminPassword']:password,Portal__BridgeKey:key,FeedbackMail__Enabled:'false',...extra}});
 children.push(processChild);let logs='';processChild.stdout.on('data',b=>logs+=b);processChild.stderr.on('data',b=>logs+=b);
 processChild.once('exit',()=>writeFileSync(path.join(fixture,role+'-'+id()+'.log'),logs));
 for(let n=0;n<120;n++){if(processChild.exitCode!==null)throw Error(logs);try{if((await fetch(base+'/health')).ok)return processChild;}catch{}await delay(100);}throw Error('Startup failed: '+logs.slice(-1500));
}
async function stop(child){if(child.exitCode===null&&child.signalCode===null){const ended=new Promise(r=>child.once('exit',r));child.kill();await ended;}}
class Client{
 constructor(base){this.base=base;this.cookies=new Map();}
 async req(route,method='GET',body,status=200,csrf=true){const headers={Cookie:[...this.cookies].map(([k,v])=>k+'='+v).join('; ')};if(method!=='GET'){headers['Content-Type']='application/json';if(csrf)headers['X-CSRF-TOKEN']=this.csrf??'';}const r=await fetch(this.base+'/api/'+route,{method,headers,body:method==='GET'?undefined:JSON.stringify(body??{})});for(const cookie of r.headers.getSetCookie()){const text=cookie.split(';')[0],at=text.indexOf('=');this.cookies.set(text.slice(0,at),text.slice(at+1))}const text=await r.text();assert.equal(r.status,status,route+' '+text);return text?JSON.parse(text):null;}
 async login(name){this.csrf=(await this.req('session')).csrfToken;await this.req('login','POST',{username:name,password});const session=await this.req('session');this.csrf=session.csrfToken;this.user=session.user;return this;}
}
const portal=await address(),office=await address();
try{
 let ps=await start('AnnouncementServer',portal),os=await start('Server',office,{Announcements__Address:portal,Announcements__BridgeKey:key});
 const pa=await new Client(portal).login('admin'),oa=await new Client(office).login('admin');
 const classA=(await pa.req('admin/classes','POST',{name:'1학년 1반',grade:1})).id,classB=(await pa.req('admin/classes','POST',{name:'2학년 1반',grade:2})).id;
 const classC=(await pa.req('admin/classes','POST',{name:'3학년 1반',grade:3})).id;
 const managerId=(await oa.req('admin/users','POST',{username:'manager',name:'Synthetic manager',department:'Test',password,isAdmin:false,canBroadcast:false})).id;
 const otherId=(await oa.req('admin/users','POST',{username:'ordinary',name:'Synthetic ordinary',department:'Test',password,isAdmin:false,canBroadcast:false})).id;
 const manager=await new Client(office).login('manager'),ordinary=await new Client(office).login('ordinary');
 const teacher=(await pa.req('admin/people','POST',{name:'Synthetic teacher',role:'teacher',classIds:[classA],internalUserId:managerId,canBroadcast:false})).id;
 async function student(username,room){const person=(await pa.req('admin/people','POST',{name:username,role:'student',classId:room,classIds:[],canBroadcast:false})).id;const invite=await pa.req('admin/people/'+person+'/invite','POST');const c=new Client(portal);c.csrf=(await c.req('session')).csrfToken;await c.req('register','POST',{code:invite.code,username,password,agreement:await acceptRequiredPoliciesForFixture(portal)});await c.login(username);return c;}
 const first=await student('student.first',classA),second=await student('student.second',classA),outsider=await student('student.other',classB),missing=await student('student.missing',classA),moved=await student('student.moved',classA);
 const senior=await student('student.senior',classC);
 const parent=new Client(portal);parent.csrf=(await parent.req('session')).csrfToken;await parent.req('register','POST',{name:'Synthetic parent',username:'parent.test',password,role:'parent',agreement:await acceptRequiredPoliciesForFixture(portal)});await parent.login('parent.test');
 await new Client(office).req('elective-management','GET',undefined,401);await new Client(portal).req('electives','GET',undefined,401);await ordinary.req('elective-management','GET',undefined,403);await manager.req('elective-management/setup','GET',undefined,403);
 await ordinary.req('admin/elective-managers','POST',{userId:otherId,enabled:true},403);await oa.req('admin/elective-managers','POST',{userId:managerId,enabled:true});assert.equal((await manager.req('elective-management/access')).canManage,true);assert.ok((await manager.req('elective-management/setup')).teachers.some(t=>t.id===teacher));
 check('Only administrators grant the duty; designated staff can reach the signed bridge and ordinary accounts cannot');
 const groupId=id(),course1=id(),course2=id(),now=Date.now();const payload={clientId:id(),title:'Synthetic elective round',term:'2026 term 2',grades:[1],startsAt:now-60000,endsAt:now+3600000,groups:[{id:groupId,name:'Choice group',required:1,courses:[{id:course1,name:'Course A',teacherIds:[teacher],type:'general',credits:3},{id:course2,name:'Course B',teacherIds:[teacher],type:'career',credits:3}]}],revision:0};
 for(const invalid of [{...payload,groups:null},...[[0],[3],[4],[1,1],[],null].map(grades=>({...payload,grades})),{...payload,grades:undefined,classIds:[classA]},{...payload,endsAt:now-120000},{...payload,groups:[{...payload.groups[0],required:0}]},{...payload,groups:[{...payload.groups[0],courses:[{...payload.groups[0].courses[0],teacherIds:[first.user.id]}]}]}])await manager.req('elective-management','POST',invalid,invalid.groups?.[0]?.courses?.[0]?.teacherIds?.[0]===first.user.id?409:400);
 const unsigned=await fetch(portal+'/bridge/electives?actor='+oa.user.id,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)});assert.equal(unsigned.status,401);
 check('Malformed rules, invalid/duplicate/third-year grades, invalid periods, non-teacher assignments and unsigned bridge requests are rejected');
 await manager.req('elective-management','POST',payload,400,false);const saved=await manager.req('elective-management','POST',payload);const round=saved.id;
 assert.equal((await manager.req('elective-management','POST',payload)).id,round);await manager.req('elective-management','POST',{...payload,title:'Changed retry'},409);
 assert.equal((await first.req('electives')).length,0);await first.req('electives/'+round,'GET',undefined,404);await parent.req('electives','GET',undefined,403);
 check('Drafts are invisible to students; creation is idempotent, changed retries conflict, and CSRF is required');
 await manager.req('elective-management/'+round,'POST',{...payload,clientId:id(),revision:0},409);
 const open={clientId:id(),revision:1,action:'open'};await manager.req('elective-management/'+round+'/state','POST',open);await manager.req('elective-management/'+round+'/state','POST',open);
 const visible=await first.req('electives/'+round);assert.equal(visible.canApply,true);assert.equal(visible.application.revision,0);assert.ok(!('students' in visible));assert.ok(!JSON.stringify(visible).includes(second.user.id));
 await outsider.req('electives/'+round,'GET',undefined,404);assert.equal((await outsider.req('electives')).length,0);await manager.req('elective-management/'+round,'POST',{...payload,clientId:id(),revision:2},409);
 assert.deepEqual(visible.grades,[1]);assert.ok(!('classIds' in visible));await senior.req('electives/'+round,'GET',undefined,404);assert.equal((await senior.req('electives')).length,0);await senior.req('electives/'+round+'/application','POST',{clientId:id(),revision:0,courseIds:[course1]},404);
 const sameGradeClass=(await pa.req('admin/classes','POST',{name:'1학년 2반',grade:1})).id;await pa.req('admin/people/'+second.user.id,'PATCH',{name:'Synthetic same-grade student',active:true,classId:sameGradeClass,classIds:[],canBroadcast:false});
 await second.login('student.second');assert.equal((await second.req('electives/'+round)).canApply,true);assert.ok((await second.req('electives')).some(r=>r.id===round));
 check('Grade-wide eligibility includes new classrooms and same-grade transfers, rejects third-year students and reveals no classroom target');
 check('Publication exposes only eligible students own application and freezes course/target configuration');
 const apply={clientId:id(),revision:0,courseIds:[course1]};await first.req('electives/'+round+'/application','POST',apply,400,false);
 await first.req('electives/'+round+'/application','POST',{...apply,courseIds:[]},400);await first.req('electives/'+round+'/application','POST',{...apply,courseIds:[course1,course2]},400);await first.req('electives/'+round+'/application','POST',{...apply,courseIds:[course1,course1]},400);await first.req('electives/'+round+'/application','POST',{...apply,courseIds:[id()]},400);
 check('The server validates exact choice count, duplicates, foreign course IDs and CSRF');
 const a1=await first.req('electives/'+round+'/application','POST',apply);assert.equal(a1.revision,1);assert.deepEqual(await first.req('electives/'+round+'/application','POST',apply),a1);await first.req('electives/'+round+'/application','POST',{...apply,courseIds:[course2]},409);
 const b1=await second.req('electives/'+round+'/application','POST',{clientId:id(),revision:0,courseIds:[course1]});assert.equal(b1.submitted,true);
 check('Multiple students can select the same course without a capacity or first-come restriction; retries do not duplicate');
 const race=await Promise.allSettled([first.req('electives/'+round+'/application','POST',{clientId:id(),revision:1,courseIds:[course2]}),first.req('electives/'+round+'/application','POST',{clientId:id(),revision:1,courseIds:[course1]})]);assert.equal(race.filter(r=>r.status==='fulfilled').length,1);
 const latest=(await first.req('electives/'+round)).application;assert.equal(latest.revision,2);
 const cancelled=await first.req('electives/'+round+'/cancel','POST',{clientId:id(),revision:latest.revision});assert.equal(cancelled.submitted,false);assert.deepEqual(cancelled.courseIds,[]);
 await first.req('electives/'+round+'/application','POST',{clientId:id(),revision:1,courseIds:[course1]},409);await first.req('electives/'+round+'/application','POST',{clientId:id(),revision:cancelled.revision,courseIds:[course2]});
 check('Concurrent edits have one winner; withdrawal clears choices, prevents stale revival and permits resubmission');
 await moved.req('electives/'+round+'/application','POST',{clientId:id(),revision:0,courseIds:[course1]});await pa.req('admin/people/'+moved.user.id,'PATCH',{name:'Synthetic moved student',active:true,classId:classB,classIds:[],canBroadcast:false});await moved.login('student.moved');await moved.req('electives/'+round,'GET',undefined,404);await moved.req('electives/'+round+'/application','POST',{clientId:id(),revision:1,courseIds:[course2]},404);
 const report=await manager.req('elective-management/'+round);assert.equal(report.students.length,3);assert.equal(report.students.filter(s=>s.submitted).length,2);assert.equal(report.students.find(s=>s.id===missing.user.id).submitted,false);assert.equal(report.excludedApplications,1);assert.equal(report.students.find(s=>s.id===second.user.id).classId,sameGradeClass);await ordinary.req('elective-management/'+round,'GET',undefined,403);
 await first.req('elective-management/'+round,'GET',undefined,404); // The public role exposes no office management route.
 await oa.req('admin/elective-managers','POST',{userId:managerId,enabled:false});await manager.req('elective-management/'+round,'GET',undefined,403);await oa.req('admin/elective-managers','POST',{userId:managerId,enabled:true});
 check('Named student reports are duty-only and revocation takes effect on the next request');
 await stop(os);await stop(ps);
 const legacyDb=new DatabaseSync(path.join(fixture,'AnnouncementServer/announcements.db'));const legacyPayload={...payload,classIds:[classA]};delete legacyPayload.grades;legacyDb.prepare('UPDATE ElectiveRounds SET Payload=? WHERE Id=?').run(JSON.stringify(legacyPayload),round);legacyDb.close();
 ps=await start('AnnouncementServer',portal);os=await start('Server',office,{Announcements__Address:portal,Announcements__BridgeKey:key});assert.equal((await first.req('electives/'+round)).application.revision,4);assert.equal((await manager.req('elective-management/access')).canManage,true);
 assert.deepEqual((await second.req('electives/'+round)).grades,[1]);assert.equal((await manager.req('elective-management/'+round)).students.length,3);
 check('Legacy classroom-scoped rounds migrate to whole grades on restart without losing applications');
 check('Both service restarts preserve applications, revisions, sessions and delegated manager permissions');
 const db=new DatabaseSync(path.join(fixture,'AnnouncementServer/announcements.db'));db.exec('PRAGMA busy_timeout=10000');
 function timing(start,end){const stored=JSON.parse(db.prepare('SELECT Payload FROM ElectiveRounds WHERE Id=?').get(round).Payload);stored.startsAt=start;stored.endsAt=end;db.prepare('UPDATE ElectiveRounds SET Payload=? WHERE Id=?').run(JSON.stringify(stored),round);}
 timing(Date.now()+3600000,Date.now()+7200000);await first.req('electives/'+round+'/application','POST',{clientId:id(),revision:4,courseIds:[course1]},409);timing(Date.now()-7200000,Date.now()-1000);await second.req('electives/'+round+'/cancel','POST',{clientId:id(),revision:1},409);timing(Date.now()-1000,Date.now()+3600000);
 check('The server clock blocks submissions before opening and cancellations after the deadline');
 await manager.req('elective-management/'+round+'/state','POST',{clientId:id(),revision:2,action:'finalize'},409);await manager.req('elective-management/'+round+'/state','POST',{clientId:id(),revision:2,action:'close'});
 await second.req('electives/'+round+'/application','POST',{clientId:id(),revision:1,courseIds:[course2]},409);
 const finalize={clientId:id(),revision:3,action:'finalize'};await manager.req('elective-management/'+round+'/state','POST',finalize);await manager.req('elective-management/'+round+'/state','POST',finalize);
 const final=await manager.req('elective-management/'+round);assert.equal(final.round.state,'finalized');assert.equal(final.sections.length,2);const sections=await pa.req('admin/sections');assert.equal(sections.length,2);assert.equal(sections.reduce((sum,s)=>sum+s.members.length,0),2);
 assert.equal(sections.find(s=>s.id===final.sections.find(c=>c.courseId===course1).sectionId).members[0].studentId,second.user.id);
 assert.equal((await first.req('electives/'+round)).application.submitted,true);await second.req('electives/'+round+'/cancel','POST',{clientId:id(),revision:1},409);
 check('Closing blocks edits; finalization atomically reuses current choices as section rosters and retries create no extra sections');
 assert.ok(!sections.some(s=>s.members.some(m=>m.studentId===moved.user.id)));
 check('Moved students are reported as excluded, non-applicants remain visible, and neither is silently assigned to a section');
 const curriculumCourses=[{id:id(),name:'공통국어',teacherIds:[teacher],type:'common',credits:2,area:'국어'},{id:id(),name:'화학',teacherIds:[teacher],type:'general',credits:3,area:'과학'},{id:id(),name:'물리학',teacherIds:[teacher],type:'career',credits:4,area:'과학'},{id:id(),name:'융합과학 탐구',teacherIds:[teacher],type:'convergence',credits:2,area:'과학'}];
 const curriculum={...payload,clientId:id(),title:'교육과정 학점 신청 검증',minCredits:5,maxCredits:5,groups:[{id:id(),name:'탐구 선택',required:1,courses:curriculumCourses}]};
 for(const bad of [{...curriculum,minCredits:7,maxCredits:7},{...curriculum,minCredits:6,maxCredits:5},{...curriculum,groups:[{...curriculum.groups[0],courses:curriculumCourses.map((c,i)=>i===0?{...c,type:'unknown'}:c)}]},{...curriculum,groups:[{...curriculum.groups[0],courses:curriculumCourses.map((c,i)=>i===0?{...c,credits:0}:c)}]},{...curriculum,groups:[{...curriculum.groups[0],courses:curriculumCourses.map((c,i)=>i===0?{...c,credits:21}:c)}]}])await manager.req('elective-management','POST',bad,400);
 const curriculumSaved=await manager.req('elective-management','POST',curriculum);await manager.req('elective-management/'+curriculumSaved.id+'/state','POST',{clientId:id(),revision:1,action:'open'});
 const curriculumView=await first.req('electives/'+curriculumSaved.id);assert.equal(curriculumView.minCredits,5);assert.equal(curriculumView.maxCredits,5);assert.deepEqual(curriculumView.groups[0].courses.map(c=>[c.type,c.credits]),curriculumCourses.map(c=>[c.type,c.credits]));
 for(const selections of [[curriculumCourses[1].id],[curriculumCourses[0].id,curriculumCourses[2].id],[curriculumCourses[0].id,curriculumCourses[3].id]])await first.req('electives/'+curriculumSaved.id+'/application','POST',{clientId:id(),revision:0,courseIds:selections},400);
 const curriculumRequest={clientId:id(),revision:0,courseIds:[curriculumCourses[0].id,curriculumCourses[1].id]};const curriculumResult=await first.req('electives/'+curriculumSaved.id+'/application','POST',curriculumRequest);assert.equal(curriculumResult.submitted,true);assert.deepEqual(await first.req('electives/'+curriculumSaved.id+'/application','POST',curriculumRequest),curriculumResult);
 const curriculumReport=await manager.req('elective-management/'+curriculumSaved.id);assert.equal(curriculumReport.students.filter(s=>s.submitted).length,1);assert.ok(curriculumReport.students.some(s=>s.id===missing.user.id&&!s.submitted));
 const commonOnly={...curriculum,clientId:id(),minCredits:2,maxCredits:2,groups:[{id:id(),name:'공통 이수',required:0,courses:[{...curriculumCourses[0],id:id()}]}]};const commonSaved=await manager.req('elective-management','POST',commonOnly);await manager.req('elective-management/'+commonSaved.id+'/state','POST',{clientId:id(),revision:1,action:'open'});await first.req('electives/'+commonSaved.id+'/application','POST',{clientId:id(),revision:0,courseIds:[commonOnly.groups[0].courses[0].id]});
 check('Four curriculum types, integer course credits, mandatory common courses, feasible credit bounds and server-side min/max checks persist through the signed bridge; common-only groups accept zero optional choices');
 for(let n=0;n<105;n++)db.prepare("INSERT INTO ElectiveRounds(Id,ClientId,Creator,Fingerprint,Payload,State,CreatedAt) VALUES(?,?,?,?,?,'draft',?)").run(id(),id(),oa.user.id,'fixture',JSON.stringify({...payload,clientId:id()}),Date.now()+n);
 assert.ok((await first.req('electives')).some(r=>r.id===round));assert.equal((await outsider.req('electives')).length,0);
 check('More recent drafts cannot hide an eligible published round or leak other grades rounds');
 const bothGrades={...payload,clientId:id(),grades:[1,2]};const bothSaved=await manager.req('elective-management','POST',bothGrades);await manager.req('elective-management/'+bothSaved.id+'/state','POST',{clientId:id(),revision:1,action:'open'});assert.equal((await outsider.req('electives/'+bothSaved.id)).canApply,true);assert.equal((await first.req('electives/'+bothSaved.id)).canApply,true);await senior.req('electives/'+bothSaved.id,'GET',undefined,404);assert.equal((await manager.req('elective-management/'+bothSaved.id)).students.length,5);
 check('A round can include both first and second years while excluding every third-year student');
 if(process.argv.includes('--ui')){
  const output=path.join(root,'artifacts/electives-2026-10-23/ui-'+id()+'.json');mkdirSync(path.dirname(output),{recursive:true});const packageRoot=process.env.YY_PACKAGE_ROOT;const ui=spawn(packageRoot?path.join(packageRoot,'client/SchoolMessenger.exe'):sdk,packageRoot?['--verify-electives']:[path.join(root,'src/SchoolMessenger.Desktop/bin/Debug/net10.0-windows/SchoolMessenger.dll'),'--verify-electives'],{windowsHide:true,env:{...process.env,SCHOOL_UI_SERVER:office,SCHOOL_UI_PASSWORD:password,SCHOOL_UI_OUTPUT:output,SCHOOL_UI_LOCALDIR:path.join(fixture,'ui')}});children.push(ui);
  await new Promise((resolve,reject)=>{const deadline=setTimeout(()=>reject(Error('Native UI timeout')),90000);ui.once('exit',code=>{clearTimeout(deadline);code===0?resolve():reject(Error('Native UI exit '+code))});ui.once('error',error=>{clearTimeout(deadline);reject(error)})});const result=JSON.parse(readFileSync(output,'utf8'));assert.ok(!result.error,result.error);assert.equal(result.checks.length,3);writeFileSync(path.join(path.dirname(output),'ui-checks.json'),JSON.stringify(result,null,2));for(const name of result.checks)check(name);
 }
 const backupFolder=path.join(fixture,'AnnouncementServer/backups');mkdirSync(backupFolder,{recursive:true});const backup=path.join(backupFolder,'announcements-electives-synthetic.db');db.prepare('VACUUM INTO ?').run(backup);
 const erased=first.user.id;await pa.req('admin/graduations','POST',{studentIds:[erased],at:Date.now()+2000,confirmation:'졸업 자동 탈퇴 및 파기'});await delay(2300);await pa.req('admin/people');
 for(let n=0;n<100&&db.prepare('SELECT 1 FROM People WHERE Id=?').get(erased);n++){await delay(100);await pa.req('admin/people');}
 // Account removal commits before background snapshot sanitation. Wait for the durable job,
 // not just People removal, before opening its backup (which is replaced atomically).
 for(let n=0;n<300&&db.prepare('SELECT 1 FROM PrivacyErasure WHERE Id=? AND PublicDone=0').get(erased);n++)await delay(100);
 assert.equal(db.prepare('SELECT COUNT(*) AS n FROM PrivacyErasure WHERE Id=? AND PublicDone=0').get(erased).n,0,'managed backup erasure must complete');
 assert.equal(db.prepare('SELECT COUNT(*) AS n FROM ElectiveApplications WHERE StudentId=?').get(erased).n,0);assert.equal(db.prepare('SELECT COUNT(*) AS n FROM ElectiveChoices WHERE StudentId=?').get(erased).n,0);assert.equal(db.prepare('SELECT COUNT(*) AS n FROM ElectiveReceipts WHERE ActorId=?').get(erased).n,0);assert.equal(db.prepare('SELECT COUNT(*) AS n FROM SectionMembers WHERE StudentId=?').get(erased).n,0);db.close();
 const snapshot=new DatabaseSync(backup,{readOnly:true});for(const table of ['ElectiveApplications','ElectiveChoices'])assert.equal(snapshot.prepare('SELECT COUNT(*) AS n FROM '+table+' WHERE StudentId=?').get(erased).n,0);assert.equal(snapshot.prepare('SELECT COUNT(*) AS n FROM ElectiveReceipts WHERE ActorId=?').get(erased).n,0);snapshot.close();
 check('Graduation erases applications, selections, retry receipts and finalized section membership');
 writeFileSync('artifacts/elective-checks.json',JSON.stringify({date:'2026-10-23',checks:passed,realSchoolData:false,capacityLimit:false,targetGrades:[1,2]},null,2));console.log(passed.length+' elective checks passed.');
}finally{for(const child of children.reverse())await stop(child)}
