import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import {acceptRequiredPoliciesForFixture} from './registration-fixture.mjs';
import {bridgeFetch} from './bridge-client.mjs';
const root=process.cwd(),fixture=path.resolve(process.env.YY_TEST_DATA_ROOT??'.test-data','server-resources-'+randomUUID());mkdirSync(fixture,{recursive:true});
const id=()=>randomUUID().replaceAll('-',''),password='Synthetic-'+randomUUID(),passed=[],delay=ms=>new Promise(r=>setTimeout(r,ms));
const check=text=>{passed.push(text);console.log('PASS '+text);};
async function address(){const s=net.createServer();await new Promise(r=>s.listen(0,'127.0.0.1',r));const url='http://127.0.0.1:'+s.address().port;await new Promise(r=>s.close(r));return url;}
class Client{
 constructor(base){this.base=base;this.cookies=new Map();}
 headers(){return {Cookie:[...this.cookies].map(([k,v])=>k+'='+v).join('; '),'X-CSRF-TOKEN':this.csrf??''};}
 async req(route,method='GET',body,expected=200){const headers=this.headers();let payload;if(body instanceof FormData)payload=body;else if(method!=='GET'){headers['Content-Type']='application/json';payload=JSON.stringify(body??{});}const r=await fetch(this.base+'/api/'+route,{method,headers,body:payload});for(const cookie of r.headers.getSetCookie()){const v=cookie.split(';')[0],p=v.indexOf('=');this.cookies.set(v.slice(0,p),v.slice(p+1));}const text=await r.text();assert.equal(r.status,expected,route+' '+text);return text?JSON.parse(text):null;}
 async login(name){this.csrf=(await this.req('session')).csrfToken;await this.req('login','POST',{username:name,password});this.csrf=(await this.req('session')).csrfToken;return this;}
}
for(const role of ['Server','AnnouncementServer']){
 const portal=role==='AnnouncementServer',prefix=portal?'Portal':'School',data=path.join(fixture,role),base=await address();let child,logs='';
 const env={...process.env,ASPNETCORE_ENVIRONMENT:'Development',[prefix+'__DataDirectory']:data,[prefix+'__AdminPassword']:password,Portal__BridgeKey:'Synthetic-'+randomUUID(),Announcements__Address:'',Operations__ResourceProfile:'balanced'};
 async function start(){logs='';child=spawn(path.join(root,'.tools/dotnet/dotnet.exe'),[path.join(root,'src/SchoolMessenger.'+role+'/bin/Debug/net10.0/SchoolMessenger.'+role+'.dll'),'--urls',base],{env,windowsHide:true});child.stdout.on('data',b=>logs+=b);child.stderr.on('data',b=>logs+=b);for(let n=0;n<100;n++){if(child.exitCode!==null)throw Error(logs);try{if((await fetch(base+'/health')).ok)return;}catch{}await delay(100);}throw Error('Startup timeout '+logs);}
 async function stop(){if(child?.exitCode===null){child.kill();await new Promise(r=>child.once('exit',r));}}
 try{
  await start();const admin=await new Client(base).login('admin'),guest=new Client(base);await guest.req('admin/server-resources','GET',undefined,401);
  if(portal){const room=(await admin.req('admin/classes','POST',{name:'Synthetic class',grade:1})).id,person=(await admin.req('admin/people','POST',{name:'Synthetic pupil',role:'student',classId:room,classIds:[],canBroadcast:false})).id,invite=await admin.req('admin/people/'+person+'/invite','POST');guest.csrf=(await guest.req('session')).csrfToken;await guest.req('register','POST',{code:invite.code,username:'ordinary',password,agreement:await acceptRequiredPoliciesForFixture(base)});}
  else await admin.req('admin/users','POST',{username:'ordinary',name:'Synthetic teacher',department:'Test',password,isAdmin:false,canBroadcast:false});
  const ordinary=await new Client(base).login('ordinary');await ordinary.req('admin/server-resources','GET',undefined,403);await ordinary.req('admin/server-resources','POST',{profile:'low-memory'},403);
  check(role+': unauthenticated and ordinary accounts cannot read hardware or change resource limits');
  const original=await admin.req('admin/server-resources');assert.equal(original.profile,'balanced');assert.equal(original.sample.serverGc,false);assert.ok(original.sample.processMemoryBytes>0);assert.ok(original.sample.os.length>0);assert.equal(original.sample.dataDirectory,data);assert.equal(original.limits.noticeImageBytes,200000);assert.equal(original.limits.noticeImages,5);
  assert.equal(original.sample.systemCpuPercent,null);assert.equal(original.sample.networkSendBytesPerSecond,null);assert.equal((await fetch(base+'/health')).status,200);
  check(role+': cached measurements describe this API server, workstation GC and notice limits; first CPU/network sample is unknown');
  const missing=new Client(base);missing.cookies=new Map(admin.cookies);await missing.req('admin/server-resources','POST',{profile:'low-memory'},400);
  await admin.req('admin/server-resources','POST',{profile:'unrestricted'},400);assert.equal((await admin.req('admin/server-resources')).profile,'balanced');
  await admin.req('admin/server-resources','POST',{profile:'low-memory',dataDirectory:'C:/Windows'});const limits=await admin.req('admin/server-resources');assert.equal(limits.limits.uploads,1);assert.equal(limits.limits.downloads,2);assert.equal(limits.limits.zip,1);assert.equal(limits.sampledAt,original.sampledAt);assert.equal(limits.sample.dataDirectory,data);
  assert.deepEqual(JSON.parse(readFileSync(path.join(data,'server-resource-policy.json'),'utf8')),{Profile:'low-memory'});
  check(role+': CSRF and profile allowlist protect settings; profile switches without moving data or invalidating cached sample');
  if(portal){
   const huge=await fetch(base+'/bridge/attachments',{method:'POST',body:Buffer.alloc(240001)});assert.equal(huge.status,413);
   const room=(await admin.req('admin/classes','POST',{name:'Uploader class',grade:2})).id,teacherId=(await admin.req('admin/people','POST',{name:'Synthetic uploader',role:'teacher',internalUserId:id(),classIds:[room],canBroadcast:false})).id;
   const proof=await bridgeFetch(env.Portal__BridgeKey,base+'/bridge/heartbeat',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({teachers:[{id:teacherId,active:true,canBroadcast:false}]})});assert.equal(proof.status,200);
   const invite=await admin.req('admin/people/'+teacherId+'/invite','POST'),uploader=new Client(base);uploader.csrf=(await uploader.req('session')).csrfToken;
   await uploader.req('register','POST',{code:invite.code,username:'uploader',password,agreement:await acceptRequiredPoliciesForFixture(base)});await uploader.login('uploader');
   async function* chunks(){yield Buffer.from('--sized\r\nContent-Disposition: form-data; name="clientId"\r\n\r\n'+id()+'\r\n--sized\r\nContent-Disposition: form-data; name="file"; filename="large.png"\r\nContent-Type: image/png\r\n\r\n');yield Buffer.alloc(240001);yield Buffer.from('\r\n--sized--\r\n');}
   const chunked=await fetch(base+'/api/attachments',{method:'POST',headers:{...uploader.headers(),'Content-Type':'multipart/form-data; boundary=sized'},body:chunks(),duplex:'half'});assert.equal(chunked.status,413,await chunked.text());
   assert.equal((await admin.req('admin/server-resources')).active.uploads,0);
   check('AnnouncementServer: declared and chunked oversized uploads stop at 240KB before scanning; failed requests release upload capacity');
  }
  if(!portal){
   await admin.req('admin/server-resources','POST',{profile:'balanced'});
   const bytes=Buffer.alloc(2*1024*1024,65),form=new FormData();form.append('file',new Blob([bytes]),'synthetic.txt');const file=await admin.req('attachments','POST',form);const session=await admin.req('session');
   await admin.req('messages','POST',{clientId:id(),title:'Synthetic download',body:'Only local fixture data',recipientIds:[session.user.id],department:null,broadcast:false,attachmentIds:[file.id]});
   await admin.req('admin/server-resources','POST',{profile:'low-memory'});let count=0;const consume=r=>r.arrayBuffer().then(b=>assert.deepEqual(Buffer.from(b),bytes));
   const first=await fetch(base+'/api/attachments/'+file.id+'/download',{headers:admin.headers()});assert.equal(first.status,200);
   const second=await fetch(base+'/api/attachments/'+file.id+'/download',{headers:admin.headers()});assert.equal(second.status,200);
   const third=await fetch(base+'/api/attachments/'+file.id+'/download',{headers:admin.headers()});assert.equal(third.status,429);assert.equal(third.headers.get('retry-after'),'2');
   const watch=performance.now();await Promise.all([consume(first),consume(second)]);assert.ok(performance.now()-watch>700,'downloads must be paced instead of fully buffered');
   for(let n=0;n<40;n++){count=(await admin.req('admin/server-resources')).active.downloads;if(count===0)break;await delay(50);}assert.equal(count,0);
   const retry=await fetch(base+'/api/attachments/'+file.id+'/download',{headers:admin.headers()});assert.equal(retry.status,200);await consume(retry);
   check('Server: two streamed downloads retain exact bytes, third is rejected, pacing applies and slots are released for retry');
   const abort=new AbortController();let release;const finish=new Promise(r=>release=r);
   async function* body(){yield Buffer.from('--fixture\r\nContent-Disposition: form-data; name="file"; filename="slow.txt"\r\nContent-Type: text/plain\r\n\r\nA');await finish;yield Buffer.from('\r\n--fixture--\r\n');}
   const slow=fetch(base+'/api/attachments',{method:'POST',headers:{...admin.headers(),'Content-Type':'multipart/form-data; boundary=fixture'},body:body(),duplex:'half',signal:abort.signal}).catch(()=>null);
   for(let n=0;n<40&&(await admin.req('admin/server-resources')).active.uploads===0;n++)await delay(50);
   const other=new FormData();other.append('file',new Blob(['synthetic']),'other.txt');await admin.req('attachments','POST',other,429);abort.abort();release();await slow;
   for(let n=0;n<40&&(await admin.req('admin/server-resources')).active.uploads!==0;n++)await delay(50);
   assert.equal((await admin.req('admin/server-resources')).active.uploads,0);await admin.req('attachments','POST',other);
   check('Server: one in-flight upload blocks a second and cancelled uploads release capacity');
  }
  await stop();await start();await admin.login('admin');assert.equal((await admin.req('admin/server-resources')).profile,'low-memory');
  const copied=new Client(base);copied.cookies=new Map(admin.cookies);await admin.req('logout','POST');await copied.req('admin/server-resources','GET',undefined,401);
  check(role+': resource policy survives restart and logout revokes copied administrator session');
 }finally{await stop();}
}
writeFileSync('artifacts/server-resource-test-results.json',JSON.stringify({verifiedAt:new Date().toISOString(),passed,realSchoolData:false},null,2));console.log(passed.length+' server resource checks passed.');
