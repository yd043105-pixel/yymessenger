import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {DatabaseSync} from 'node:sqlite';
import path from 'node:path';

export async function sectionChecks({admin,Client,person,register,service,data,check}){
 const id=()=>randomUUID().replaceAll('-','');
 const first=(await admin.request('/api/admin/classes','POST',{name:'분반검증 1반',grade:1})).id,second=(await admin.request('/api/admin/classes','POST',{name:'분반검증 2반',grade:1})).id;
 const internal=id(),homeInternal=id();
 const teacherId=await person(admin,'분반 담당','teacher',null,internal,[]),homeId=await person(admin,'분반 담임','teacher',null,homeInternal,[second]);
 await service('/heartbeat','POST',{teachers:[{id:teacherId,active:true,canBroadcast:false},{id:homeId,active:true,canBroadcast:false}]});
 const teacher=await register(admin,teacherId,'section.teacher'),home=await register(admin,homeId,'section.home');
 const a=await person(admin,'수강학생 A','student',first),b=await person(admin,'수강학생 B','student',second),excluded=await person(admin,'미수강학생','student',first),parentId=await person(admin,'분반 보호자','parent');
 await admin.request('/api/admin/families','POST',{parentId,studentId:b});
 const studentA=await register(admin,a,'section.a'),studentB=await register(admin,b,'section.b'),outsider=await register(admin,excluded,'section.excluded'),parent=await register(admin,parentId,'section.parent');
 const db=new DatabaseSync(path.join(data,'announcements.db'));db.prepare('INSERT OR REPLACE INTO TimetableHomerooms VALUES(?,?)').run(second,homeInternal);db.close();
 const request={name:'선택수학 A',term:'2026 2학기',teacherIds:[teacherId],studentIds:[]};const section=await admin.request('/api/admin/sections','POST',request);
 const packet={clientId:id(),title:'분반 검증 안내',body:'가상 공지',audience:'both',classIds:[],sectionIds:[section.id],all:false,attachmentIds:[]};
 await teacher.request('/api/announcements','POST',packet,403);
 await admin.request('/api/admin/sections/'+section.id,'POST',{...request,studentIds:[a,b],revision:1});
 await admin.request('/api/admin/sections/'+section.id,'POST',{...request,studentIds:[a],revision:1},409);
 const list=await teacher.request('/api/sections');assert.equal(list.find(s=>s.id===section.id).memberCount,2);assert.equal(list.find(s=>s.id===section.id).members,undefined);
 await home.request('/api/announcements','POST',packet,403);
 await admin.request('/api/admin/people/'+homeId,'PATCH',{name:'분반 담임',active:true,classId:null,classIds:[second],canBroadcast:true});await home.login('section.home');
 await home.request('/api/announcements','POST',packet,403);
 const notice=await teacher.request('/api/announcements','POST',packet);
 for(const receiver of [studentA,studentB,parent,home]){assert.ok((await receiver.request('/api/announcements')).some(n=>n.id===notice.id));await receiver.request('/api/announcements/'+notice.id);}
 await outsider.request('/api/announcements/'+notice.id,'GET',undefined,404);assert.ok(!(await outsider.request('/api/announcements')).some(n=>n.id===notice.id));
 assert.ok((await teacher.request('/api/announcements/'+notice.id)).receipts.some(r=>r.studentId===b));
 check('section notices require actual enrollment and assigned teacher; cross-class members, linked parents and homeroom teacher receive them; nonmembers cannot discover or read');
 await admin.request('/api/admin/sections/'+section.id,'POST',{...request,studentIds:[a],revision:2});
 for(const receiver of [studentB,parent,home])await receiver.request('/api/announcements/'+notice.id,'GET',undefined,404);
 await studentA.request('/api/announcements/'+notice.id);
 assert.ok(!(await teacher.request('/api/announcements/'+notice.id)).receipts.some(r=>r.studentId===b));
 await admin.request('/api/admin/sections/'+section.id,'POST',{...request,studentIds:[a,b],revision:3});
 await studentB.request('/api/announcements/'+notice.id,'GET',undefined,404);
 check('removal revokes student, parent and related homeroom access; re-enrollment does not restore historical private notices');
 const mixed={...packet,clientId:id(),classIds:[first]};await teacher.request('/api/announcements','POST',mixed,403);
 await outsider.request('/api/sections','GET',undefined,403);
 check('mixed class/section audience rejected and student cannot fetch staff section management data');
}
