import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { DatabaseSync } from 'node:sqlite';
import { existsSync } from 'node:fs';
import path from 'node:path';

export async function workChecks({Client,admin,password,address,data,check,start,stop,ready}){
 const users=[];
 for(const [username,name] of [['work.owner','수합교사'],['work.recipient','제출교사'],['work.observer','다른교사']]){
  await admin.request('/api/admin/users','POST',{username,name,department:'업무검사',password,isAdmin:false,canBroadcast:false});const c=new Client();await c.login(username);users.push(c);
 }
 const [owner,recipient,observer]=users;
 const send=(body,extra={})=>owner.request('/api/messages','POST',{clientId:randomUUID(),title:'업무 요청',body,recipientIds:[recipient.user.id],attachmentIds:[],all:false,...extra});
 const now=Date.now(),today=new Date(now+9*3600000).toISOString().slice(0,10),tomorrow=new Date(now+86400000+9*3600000).toISOString().slice(0,10);
 const original=await send('내일까지 점검표를 제출해 주세요.');
 let todos=await recipient.request('/api/todos');assert.equal(todos.length,1);assert.equal(todos[0].title,'점검표 제출');assert.equal(todos[0].dueDate,tomorrow);assert.equal(todos[0].status,'suggested');assert.equal(todos[0].sourceId,original.id);
 assert.equal((await owner.request('/api/todos')).length,0);assert.equal((await observer.request('/api/todos')).length,0);
 const edit={title:'점검표 제출',dueDate:tomorrow,status:'open',revision:todos[0].revision};
 await observer.request('/api/todos/'+todos[0].id,'PATCH',edit,404);await admin.request('/api/todos/'+todos[0].id,'PATCH',edit,404);
 await recipient.request('/api/todos/'+todos[0].id,'PATCH',edit);await recipient.request('/api/todos/'+todos[0].id,'PATCH',edit,409);
 check('received-message task candidates, Korean relative dates, confirmation, owner authorization and revision conflicts');
 await send('제출 요청을 취소합니다. 제출하지 마세요.');assert.equal((await recipient.request('/api/todos')).length,1);
 await send('2월 30일까지 신청해 주세요.');await send('내일 또는 모레 회신해 주세요.');
 todos=await recipient.request('/api/todos');assert.equal(todos.length,3);assert.equal(todos.filter(t=>t.dueDate===null).length,2);
 await recipient.request('/api/todos','POST',{title:'<img src=x onerror=alert(1)>',dueDate:null,status:'open',revision:0});
 await recipient.request('/api/todos','POST',{title:'잘못된 날짜',dueDate:'2026-02-30',status:'open',revision:0},400);
 check('task cancellation, invalid and ambiguous dates and safe literal user content');
 const deadline=Date.parse(tomorrow+'T23:59:59+09:00');
 const packet={clientId:randomUUID(),title:'점검 자료 제출',body:'점검 자료를 제출해 주세요.',recipientIds:[recipient.user.id,observer.user.id],attachmentIds:[],all:false,submissionDeadline:deadline};
 const request=await owner.request('/api/messages','POST',packet);
 assert.equal((await owner.request('/api/messages','POST',packet)).id,request.id);
 assert.equal((await owner.request('/api/submission-requests/'+request.id)).targets.length,2);
 assert.equal((await recipient.request('/api/submission-requests/'+request.id)).targets.length,1);
 assert.equal((await recipient.request('/api/submission-requests'))[0].targetCount,1);
 await admin.request('/api/submission-requests/'+request.id,'GET',undefined,404);
 await observer.request('/api/submission-requests/'+request.id+'/zip','GET',undefined,404);
 const unauthorized=new Client();await unauthorized.login('work.owner');
 await recipient.request('/api/submission-requests/'+request.id+'/close','POST',{},404);
 check('explicit submission requests, recipient snapshot, private recipient status and collector-only access');
 const wrong=await observer.upload('다른교사.txt','다른 사람의 파일');
 await recipient.request('/api/submission-requests/'+request.id+'/submit','POST',{clientId:randomUUID(),attachmentIds:[wrong.id]},400);
 const file=await recipient.upload('자료.txt','제출 파일 내용 ONE');
 const submission={clientId:randomUUID(),attachmentIds:[file.id]};
 const submitted=await recipient.request('/api/submission-requests/'+request.id+'/submit','POST',submission);
 assert.equal((await recipient.request('/api/submission-requests/'+request.id+'/submit','POST',submission)).id,submitted.id);
 await recipient.request('/api/attachments/'+file.id,'DELETE',undefined,404);
 await recipient.request('/api/messages','POST',{clientId:randomUUID(),title:'재사용 시도',body:'기존 제출 파일을 쪽지 첨부로 재사용',recipientIds:[observer.user.id],attachmentIds:[file.id],all:false},400);
 assert.equal(await owner.request('/api/attachments/'+file.id+'/download'),'제출 파일 내용 ONE');
 await observer.request('/api/attachments/'+file.id+'/download','GET',undefined,404);await admin.request('/api/attachments/'+file.id+'/download','GET',undefined,404);
 check('file submission ownership, retry idempotency, committed-file protection and isolated download authorization');
 const replacement=await recipient.upload('자료.txt','최신 제출 내용 TWO');
 await recipient.request('/api/submission-requests/'+request.id+'/submit','POST',{clientId:randomUUID(),attachmentIds:[replacement.id]});
 const detail=await owner.request('/api/submission-requests/'+request.id);assert.equal(detail.targets.find(t=>t.id===recipient.user.id).files[0].id,replacement.id);
 const zipResponse=await fetch(address+'/api/submission-requests/'+request.id+'/zip',{headers:{Cookie:[...owner.cookies].map(([k,v])=>k+'='+v).join('; ')}});
 assert.equal(zipResponse.status,200);assert.equal(zipResponse.headers.get('content-type'),'application/zip');
 const zip=Buffer.from(await zipResponse.arrayBuffer());assert.ok(zip.includes(Buffer.from('최신 제출 내용 TWO')));assert.ok(!zip.includes(Buffer.from('제출 파일 내용 ONE')));
 const names=[];for(let i=0;i<zip.length-46;i++)if(zip.readUInt32LE(i)===0x02014b50){const length=zip.readUInt16LE(i+28);names.push(zip.subarray(i+46,i+46+length).toString());i+=45+length+zip.readUInt16LE(i+30)+zip.readUInt16LE(i+32);}
 assert.ok(names.some(n=>n.includes(replacement.id)));assert.ok(names.includes('안내.txt'));assert.ok(names.every(n=>!n.startsWith('/')&&!n.split('/').includes('..')));
 check('latest replacement only in streamed ZIP, distinct safe paths and valid ZIP directory');
 await owner.request('/api/submission-requests/'+request.id+'/targets/'+observer.user.id,'PATCH',{exempt:true});
 await observer.request('/api/submission-requests/'+request.id+'/submit','POST',{clientId:randomUUID(),attachmentIds:[wrong.id]},409);
 await owner.request('/api/submission-requests/'+request.id+'/close','POST');
 const extra=await recipient.upload('추가.txt','종료 후 제출');await recipient.request('/api/submission-requests/'+request.id+'/submit','POST',{clientId:randomUUID(),attachmentIds:[extra.id]},409);
 check('submission exemption and request closure block new and replacement submissions');
 const db=new DatabaseSync(path.join(data,'school.db'));try{db.prepare('UPDATE Attachments SET ExpiresAt=? WHERE Id=?').run(Date.now()-1,replacement.id);}finally{db.close();}
 await admin.request('/api/admin/maintenance','POST');await owner.request('/api/attachments/'+replacement.id+'/download','GET',undefined,410);
 assert.equal(existsSync(path.join(data,'files',replacement.id)),false);
 const expired=(await owner.request('/api/submission-requests/'+request.id)).targets.find(t=>t.id===recipient.user.id);assert.ok(expired.submittedAt);assert.equal(expired.files[0].expired,true);
 check('expired submission file physically removed without resetting submitted status');
 const reminderDeadline=Date.parse(today+'T23:59:59+09:00')+3*86400000;
 const pending=await send('파일 제출 요청입니다.',{recipientIds:[recipient.user.id,observer.user.id],submissionDeadline:reminderDeadline});
 const reminderDay=new Date(reminderDeadline+9*3600000-2*86400000).toISOString().slice(0,10);
 const count=()=>{const database=new DatabaseSync(path.join(data,'school.db'),{readOnly:true});try{return database.prepare('SELECT COUNT(*) AS n FROM SubmissionReminders WHERE RequestId=?').get(pending.id).n;}finally{database.close();}};
 async function clock(time){await stop();start({School__ReminderNow:String(time)});await ready();for(let n=0;n<30;n++){await new Promise(r=>setTimeout(r,50));if(count()>0)break;}}
 await clock(Date.parse(reminderDay+'T08:59:00+09:00'));assert.equal(count(),0);
 await clock(Date.parse(reminderDay+'T09:00:00+09:00'));assert.equal(count(),2);
 await clock(Date.parse(reminderDay+'T11:00:00+09:00'));assert.equal(count(),2);
 check('two-day reminder start, Korean 09:00 boundary, offline persistence and once-daily restart deduplication');
 const submitAfter=await recipient.upload('늦은제출.txt','제출 이후 알림 중단');
 await recipient.request('/api/submission-requests/'+pending.id+'/submit','POST',{clientId:randomUUID(),attachmentIds:[submitAfter.id]});
 const lateDb=new DatabaseSync(path.join(data,'school.db'));try{lateDb.prepare('UPDATE Submissions SET CreatedAt=? WHERE RequestId=? AND UserId=?').run(reminderDeadline+1000,pending.id,recipient.user.id);}finally{lateDb.close();}
 assert.equal((await owner.request('/api/submission-requests/'+pending.id)).targets.find(t=>t.id===recipient.user.id).late,true);
 await owner.request('/api/submission-requests/'+pending.id+'/targets/'+observer.user.id,'PATCH',{exempt:true});
 await clock(Date.parse(reminderDay+'T11:00:00+09:00')+86400000);assert.equal(count(),2);
 await owner.request('/api/submission-requests/'+pending.id+'/targets/'+observer.user.id,'PATCH',{exempt:false});
 await clock(reminderDeadline+2*86400000);assert.equal(count(),3);
 await owner.request('/api/submission-requests/'+pending.id+'/close','POST');await clock(reminderDeadline+3*86400000);assert.equal(count(),3);
 check('submitted, exempt and closed requests stop reminders; overdue missing work is reminded without past-day flood');
 await stop();start();await ready();
 assert.ok((await recipient.request('/api/todos')).some(t=>t.title==='점검표 제출'&&t.status==='open'));
 assert.ok((await recipient.request('/api/submission-requests/'+pending.id)).targets[0].submittedAt);
 const page=await fetch(address+'/office/');assert.equal(page.status,200);assert.ok((await page.text()).includes('파일 제출'));
 assert.ok(page.headers.get('content-security-policy').includes("script-src 'self'"));
 check('tasks, submission history and web entry survive restart with existing browser security policy');
}
