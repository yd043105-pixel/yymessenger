import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {existsSync,mkdirSync,writeFileSync} from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import {DatabaseSync} from 'node:sqlite';
import {acceptRequiredPoliciesForFixture} from './registration-fixture.mjs';

const root=process.cwd(),dotnet=path.join(root,'.tools/dotnet/dotnet.exe'),fixture=path.join(process.env.YY_TEST_DATA_ROOT??path.join(root,'.test-data'),'counseling-'+randomUUID());
const password='Synthetic-'+randomUUID(),key='Bridge-'+randomUUID(),portalData=path.join(fixture,'portal'),officeData=path.join(fixture,'office');mkdirSync(fixture,{recursive:true});
const delay=ms=>new Promise(r=>setTimeout(r,ms)),id=()=>randomUUID().replaceAll('-',''),passed=[];let portalProcess,officeProcess,ui,logs='';
function check(name){passed.push(name);console.log('PASS '+name);}
async function address(){const socket=net.createServer();await new Promise(r=>socket.listen(0,'127.0.0.1',r));const value='http://127.0.0.1:'+socket.address().port;await new Promise(r=>socket.close(r));return value;}
const portal=await address(),office=await address();
function start(name,base,env){const child=spawn(dotnet,[path.join(root,`src/SchoolMessenger.${name}/bin/Debug/net10.0/SchoolMessenger.${name}.dll`),'--urls',base],{windowsHide:true,cwd:root,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',...env}});child.stdout.on('data',b=>logs+=b);child.stderr.on('data',b=>logs+=b);return child;}
async function stop(p){if(p?.exitCode===null){p.kill();await new Promise(r=>p.once('exit',r));}}
async function until(action){for(let n=0;n<100;n++){try{const result=await action();if(result)return result;}catch{}await delay(100);}throw Error('Fixture did not become ready: '+logs.slice(-1800));}
class Client{
 constructor(base){this.base=base;this.cookies=new Map();this.csrf='';}
 async req(route,method='GET',body,expected=200){
  if(route==='register'&&body&&!body.agreement)body={...body,agreement:await acceptRequiredPoliciesForFixture(this.base)};
  const headers={Cookie:[...this.cookies].map(([k,v])=>k+'='+v).join('; ')};
  if(method!=='GET'){headers['X-CSRF-TOKEN']=this.csrf;headers['Content-Type']='application/json';}
  const r=await fetch(this.base+'/api/'+route,{method,headers,body:method==='GET'?undefined:JSON.stringify(body??{})});
  for(const cookie of r.headers.getSetCookie()){const part=cookie.split(';')[0],at=part.indexOf('=');this.cookies.set(part.slice(0,at),part.slice(at+1));}
  const text=await r.text();if(expected!==null)assert.equal(r.status,expected,route+' '+text);return {status:r.status,value:text?JSON.parse(text):null};
 }
 async json(...args){return (await this.req(...args)).value;}
 async login(name='admin'){this.csrf=(await this.json('session')).csrfToken;await this.json('login','POST',{username:name,password});const session=await this.json('session');this.csrf=session.csrfToken;this.user=session.user;return this;}
}
const date=new Date(Date.now()+86400000).toISOString().slice(0,10),nextDate=new Date(Date.now()+2*86400000).toISOString().slice(0,10);
const request=(room,periods=[1,2,3],extra={})=>({clientId:id(),classId:room,date,periodIds:periods,...extra});
function db(){const d=new DatabaseSync(path.join(portalData,'announcements.db'));d.exec('PRAGMA busy_timeout=30000');return d;}
try{
 portalProcess=start('AnnouncementServer',portal,{Portal__DataDirectory:portalData,Portal__AdminPassword:password,Portal__BridgeKey:key,Portal__CleanupSeconds:'3600'});
 officeProcess=start('Server',office,{School__DataDirectory:officeData,School__AdminPassword:password,Announcements__Address:portal,Announcements__BridgeKey:key});
 await Promise.all([until(async()=> (await fetch(portal+'/health')).ok),until(async()=> (await fetch(office+'/health')).ok)]);
 const admin=await new Client(portal).login(),teacher=await new Client(office).login();
 const roomA=(await admin.json('admin/classes','POST',{name:'가상 1-1',grade:1})).id,roomB=(await admin.json('admin/classes','POST',{name:'가상 2-1',grade:2})).id;
 await teacher.json('admin/users','POST',{username:'teacher2',name:'가상 다른 교사',department:'가상 부서',password,isAdmin:false,canBroadcast:false});const teacher2=await new Client(office).login('teacher2');
 async function person(name,role,room,internal){const personId=(await admin.json('admin/people','POST',{name,role,classId:role==='student'?room:null,internalUserId:internal,classIds:role==='teacher'?[room]:[],canBroadcast:false})).id;return personId;}
 const teacherId=await person('가상 담당 교사','teacher',roomA,teacher.user.id),teacher2Id=await person('가상 다른 교사','teacher',roomB,teacher2.user.id);
 const a=await person('가상 학생 A','student',roomA),b=await person('가상 학생 B','student',roomA),c=await person('가상 학생 C','student',roomB);
 const parentId=await person('가상 보호자','parent'),parent2Id=await person('가상 다른 보호자','parent');
 await admin.json('admin/families','POST',{parentId,studentId:a});await admin.json('admin/families','POST',{parentId:parent2Id,studentId:c});
 async function account(personId,username){const invite=await admin.json('admin/people/'+personId+'/invite','POST');const client=new Client(portal);client.csrf=(await client.json('session')).csrfToken;await client.json('register','POST',{code:invite.code,username,password});return client.login(username);}
 const student=await account(a,'student.a'),other=await account(b,'student.b'),different=await account(c,'student.c'),parent=await account(parentId,'parent.a'),parent2=await account(parent2Id,'parent.c');
 await until(async()=> (await teacher.json('external-announcements/setup')).teacherId===teacherId);
 await teacher.json('admin/users/'+teacher2.user.id,'PATCH',{name:'가상 다른 교사',department:'가상 부서',position:'학년부장',password:'',isAdmin:false,canBroadcast:false,active:true});
 await teacher.json('admin/staff/'+teacher2.user.id+'/seat','POST',{seatIp:'192.168.90.12'});await teacher2.login('teacher2');assert.equal(teacher2.user.position,'학년부장');assert.equal(teacher2.user.seatIp,'192.168.90.12');assert.equal(teacher2.user.isAdmin,false);
 await teacher2.req('admin/staff/'+teacher.user.id+'/seat','POST',{seatIp:'192.168.90.13'},403);await teacher2.req('admin/users/'+teacher.user.id,'PATCH',{name:'위조',department:'위조',position:'교장',isAdmin:true,active:true},403);check('administrator manages teacher identity, title and legacy-account seat IP without granting title-based permissions');
 const special=(await teacher.json('office/rooms','POST',{name:'가상 특별실',location:'가상 2층'})).id;
 await teacher.json('office/rooms/'+special,'POST',{name:'가상 특별실 수정',location:'가상 3층',active:true,revision:1});
 await teacher2.req('office/rooms/'+special,'POST',{name:'위조',location:'위조',active:false,revision:2},403);
 const roomPacket=(start,end,grade=1)=>({clientId:id(),roomId:special,title:'가상 수업',start:date+'T'+start+':00+09:00',end:date+'T'+end+':00+09:00',grade});
 for(const [start,end] of [['08:30','08:50'],['17:00','17:50'],['19:00','19:50'],['20:00','20:50'],['12:50','13:50']])await teacher2.req('office/bookings','POST',roomPacket(start,end),400);
 const seventh=(await teacher2.json('office/bookings','POST',roomPacket('15:50','16:40'))).id;const fourth=(await teacher2.json('office/bookings','POST',roomPacket('12:50','13:40',3))).id;
 await teacher2.req('office/bookings','POST',roomPacket('12:50','13:40',1),400);
 await teacher.json('office/rooms/'+special,'POST',{name:'가상 특별실 수정',location:'가상 3층',active:false,revision:2});await teacher2.req('office/bookings','POST',roomPacket('09:00','09:50'),409);
 const storedRooms=await teacher.json('office/bookings?from='+date+'&to='+nextDate);assert.ok(storedRooms.some(b=>b.id===seventh));assert.equal(storedRooms.find(b=>b.id===fourth).grade,3);check('room names and locations are editable; grade-specific first-to-seventh booking limits are enforced on the server and disabling preserves records');
 const view=async client=>client.json('counseling?date='+date);
 assert.equal((await view(teacher)).canManage,true);assert.deepEqual((await view(teacher2)).classes.map(c=>c.id),[roomB]);check('PC and teacher-app authenticated office connection maps only the signed-in teacher and assigned classes');
 assert.deepEqual((await teacher.json('counseling?date='+date+'&teacherId='+teacher2Id)).classes.map(c=>c.id),[roomA]);check('a caller-supplied teacher ID cannot override the office login identity');
 const unsigned=await fetch(portal+'/bridge/counseling?teacherId='+teacherId);assert.equal(unsigned.status,401);await new Client(portal).req('counseling','GET',undefined,401);check('unauthenticated apps and unsigned bridge requests are rejected');
 await student.req('counseling/slots','POST',request(roomA),403);await teacher.req('counseling/slots','POST',request(roomB),403);check('students cannot open slots; a teacher cannot open another class');
 const open=request(roomA);const slots=(await teacher.json('counseling/slots','POST',open)).ids;assert.equal(slots.length,3);assert.deepEqual((await teacher.json('counseling/slots','POST',open)).ids,slots);
 await teacher.req('counseling/slots','POST',{...open,periodIds:[1]},409);check('bulk slot opening is atomic and repeated request IDs do not create duplicate slots');
 await teacher.req('counseling/slots','POST',request(roomA,[2,4]),409);assert.equal((await view(teacher)).slots.length,3);check('one overlapping period rolls back the whole opening batch');
 await teacher2.req('counseling/slots/'+slots[0]+'/close','POST',{},404);
 await teacher.req('counseling/slots','POST',request(roomA,[999]),400);await teacher.req('counseling/slots','POST',request(roomA,[1,1]),400);await teacher.req('counseling/slots','POST',request(roomA,[],{date:'2026-02-30'}),400);check('foreign slot control and malformed dates or periods are rejected');
 assert.equal((await view(different)).slots.length,0);assert.equal((await view(parent2)).slots.length,0);check('available slots are limited to the student or linked-child class');
 await parent.req('counseling/bookings','POST',{clientId:id(),slotId:slots[0],studentId:b},403);
 await parent2.req('counseling/bookings','POST',{clientId:id(),slotId:slots[0],studentId:c},403);check('parents cannot supply an unrelated child or book a different class');
 const bookedRequest={clientId:id(),slotId:slots[0],studentId:a};const booking=(await parent.json('counseling/bookings','POST',bookedRequest)).id;
 assert.equal((await parent.json('counseling/bookings','POST',bookedRequest)).id,booking);await parent.req('counseling/bookings','POST',{...bookedRequest,slotId:slots[1]},409);check('reservation request retries are idempotent and changed contents are rejected');
 await student.req('counseling/bookings','POST',{clientId:id(),slotId:slots[1],studentId:a},409);check('the student and all guardians share one future reservation per student');
 assert.equal((await view(other)).bookings.length,0);assert.ok(!JSON.stringify(await view(other)).includes('가상 학생 A'));assert.equal((await view(teacher2)).bookings.length,0);assert.equal((await view(teacher)).bookings[0].studentName,'가상 학생 A');check('only the assigned teacher and student household can see reservation identities');
 await teacher.req('counseling/slots/'+slots[0]+'/close','POST',{},409);await other.req('counseling/bookings/'+booking+'/cancel','POST',{},404);await teacher2.req('counseling/bookings/'+booking+'/cancel','POST',{},404);check('occupied slots cannot close silently and other students or teachers cannot cancel');
 const blocker=(await other.json('counseling/bookings','POST',{clientId:id(),slotId:slots[1],studentId:b})).id;
 await parent.req('counseling/bookings','POST',{clientId:id(),slotId:slots[1],studentId:a,replaceBookingId:booking},409);
 assert.equal((await view(student)).bookings.find(b=>b.id===booking).canceledAt,null);check('changing to an occupied slot preserves the original reservation');
 const replacement=(await student.json('counseling/bookings','POST',{clientId:id(),slotId:slots[2],studentId:a,replaceBookingId:booking})).id;
 assert.ok((await view(parent)).bookings.find(b=>b.id===booking).canceledAt);await parent.json('counseling/bookings/'+replacement+'/cancel','POST');await parent.json('counseling/bookings/'+replacement+'/cancel','POST');check('atomic time change and repeated household cancellation release the old place');
 await other.json('counseling/bookings/'+blocker+'/cancel','POST');
 const race=await Promise.all([student.req('counseling/bookings','POST',{clientId:id(),slotId:slots[0],studentId:a},null),other.req('counseling/bookings','POST',{clientId:id(),slotId:slots[0],studentId:b},null)]);
 assert.deepEqual(race.map(r=>r.status).sort(),[200,409]);const winning=race.find(r=>r.status===200).value.id;check('simultaneous requests for one slot produce exactly one reservation');
 await teacher.json('counseling/bookings/'+winning+'/cancel','POST');
 const studentRace=await Promise.all([student.req('counseling/bookings','POST',{clientId:id(),slotId:slots[0],studentId:a},null),parent.req('counseling/bookings','POST',{clientId:id(),slotId:slots[1],studentId:a},null)]);
 assert.deepEqual(studentRace.map(r=>r.status).sort(),[200,409]);await teacher.json('counseling/bookings/'+studentRace.find(r=>r.status===200).value.id+'/cancel','POST');check('simultaneous student and parent requests for different slots cannot bypass one-per-student');
 const scheduled=(await teacher.json('counseling/slots','POST',request(roomA,[5],{opensAt:Date.now()+3600000}))).ids[0];
 await student.req('counseling/bookings','POST',{clientId:id(),slotId:scheduled,studentId:a},409);
 await teacher.json('counseling/slots/'+scheduled+'/close','POST');assert.ok(!(await view(student)).slots.some(s=>s.id===scheduled));
 await teacher.json('counseling/slots','POST',request(roomA,[5]));assert.ok((await view(student)).slots.some(s=>s.id===scheduled));check('scheduled opening, closed-slot hiding and safe reopening use server time');
 const own=(await parent.json('counseling/bookings','POST',{clientId:id(),slotId:slots[2],studentId:a})).id;
 await admin.json('admin/families/'+parentId+'/'+a,'DELETE');await parent.login('parent.a');await parent.req('counseling','GET',undefined,403);await parent.req('counseling/bookings/'+own+'/cancel','POST',{},403);check('revoked guardian links immediately remove reservation access');
 await admin.json('admin/families','POST',{parentId,studentId:a});
 const multiId=await person('가상 다자녀 보호자','parent');await admin.json('admin/families','POST',{parentId:multiId,studentId:a});await admin.json('admin/families','POST',{parentId:multiId,studentId:c});
 const multi=await account(multiId,'parent.multi');const secondClass=(await teacher2.json('counseling/slots','POST',request(roomB,[1]))).ids[0];
 const multiView=await view(multi);assert.deepEqual(multiView.students.map(s=>s.id).sort(),[a,c].sort());assert.deepEqual([...new Set(multiView.slots.map(s=>s.classId))].sort(),[roomA,roomB].sort());
 const childBooking=(await multi.json('counseling/bookings','POST',{clientId:id(),slotId:secondClass,studentId:c})).id;await parent2.json('counseling/bookings/'+childBooking+'/cancel','POST');check('multiple linked children keep separate class choices and any current guardian can cancel their child booking');
 const temp=await person('가상 임시 학부모','parent');const temporary=await account(temp,'parent.temporary');await temporary.req('counseling','GET',undefined,403);check('temporary parents cannot view or book school consultations');
 const d=db();d.prepare('UPDATE CounselingSlots SET StartsAt=?,EndsAt=? WHERE Id=?').run(Date.now()-10000,Date.now()+10000,slots[2]);d.close();
 await student.req('counseling/bookings/'+own+'/cancel','POST',{},409);check('already-started appointments cannot be changed or canceled');
 const db2=db();db2.prepare('UPDATE CounselingSlots SET StartsAt=?,EndsAt=? WHERE Id=?').run(Date.now()-200*86400000,Date.now()-199*86400000,slots[2]);db2.close();await view(teacher);assert.ok(!(await view(teacher)).slots.some(s=>s.id===slots[2]));check('six-month retention removes expired appointments and their booking rows');
 const roomC=(await admin.json('admin/classes','POST',{name:'가상 3-1',grade:3})).id;
 await admin.json('admin/people/'+teacher2Id,'PATCH',{name:'가상 다른 교사',active:true,classId:null,classIds:[roomB,roomC],canBroadcast:false});
 await until(async()=> (await teacher2.json('external-announcements/setup')).classes.some(c=>c.id===roomC));
 await teacher2.json('counseling/slots','POST',request(roomC,[4,8,9,10],{date:nextDate}));
 const gradeThree=await teacher2.json('counseling?date='+nextDate);const times=gradeThree.slots.filter(s=>s.classId===roomC).map(s=>[new Date(s.startsAt+9*3600000).toISOString().slice(11,16),new Date(s.endsAt+9*3600000).toISOString().slice(11,16)]);
 assert.deepEqual(times,[['12:50','13:40'],['17:00','17:50'],['19:00','19:50'],['20:00','20:50']]);check('teacher-selected counseling uses grade-three fourth period and the exact eighth and two night periods');
 if(process.env.YY_COUNSELING_UI==='1'){
  const output=path.join(root,'artifacts/counseling-ui');mkdirSync(output,{recursive:true});
  ui=spawn(dotnet,[path.join(root,'src/SchoolMessenger.Desktop/bin/Debug/net10.0-windows/SchoolMessenger.dll'),'--verify-counseling'],{windowsHide:true,env:{...process.env,SCHOOL_UI_SERVER:office,SCHOOL_UI_PASSWORD:password,SCHOOL_UI_LOCALDIR:path.join(fixture,'client'),YY_COUNSELING_UI_OUTPUT:output,YY_COUNSELING_DATE:date}});
  let timer;try{const code=await Promise.race([new Promise(resolve=>ui.once('exit',resolve)),new Promise((_,reject)=>{timer=setTimeout(()=>reject(Error('Counseling UI test timed out')),60000);})]);assert.equal(code,0,'Counseling native UI fixture failed');}finally{clearTimeout(timer);}check('native WPF counseling window renders actual server data and closes on logout');
 }
 const future=(await teacher.json('counseling/slots','POST',request(roomA,[6],{date:nextDate}))).ids[0];await student.json('counseling/bookings','POST',{clientId:id(),slotId:future,studentId:a});
 const backupFolder=path.join(portalData,'backups');mkdirSync(backupFolder,{recursive:true});const snapshot=path.join(backupFolder,'counseling.db');const backupDb=db();backupDb.prepare('VACUUM INTO ?').run(snapshot);backupDb.close();
 await admin.json('admin/graduations','POST',{studentIds:[a],at:Date.now()+3600000,confirmation:'졸업 자동 탈퇴 및 파기'});const due=db();due.prepare('UPDATE Graduations SET At=? WHERE StudentId=?').run(Date.now()-1,a);due.close();
 await admin.json('session');await until(()=>{const d=db();try{return d.prepare('SELECT COUNT(*) n FROM People WHERE Id=?').get(a).n===0;}finally{d.close();}});
 for(const file of [path.join(portalData,'announcements.db'),snapshot]){const d=new DatabaseSync(file);assert.equal(d.prepare('SELECT COUNT(*) n FROM CounselingBookings WHERE StudentId=? OR RequesterId=?').get(a,parentId).n,0);d.close();}check('graduation erases student and last-child parent reservation links from live DB and managed backups');
 await stop(portalProcess);portalProcess=null;await teacher.req('counseling?date='+date,'GET',undefined,503);check('external outage returns a clear failure instead of a false reservation success');
}finally{await stop(ui);await stop(officeProcess);await stop(portalProcess);}
mkdirSync('artifacts',{recursive:true});writeFileSync('artifacts/counseling-checks.json',JSON.stringify({version:'1.0.0',verifiedAt:new Date().toISOString(),passed},null,2));console.log(passed.length+' counseling checks passed.');
