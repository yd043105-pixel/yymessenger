// Native layout check: isolated loopback service, invented teachers, no school data.
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {existsSync,mkdirSync,readFileSync} from 'node:fs';
import path from 'node:path';
import net from 'node:net';
assert.equal(process.platform,'win32');
const root=process.cwd(),services=path.resolve(process.env.YY_TEST_SERVICE_ROOT??root);
const sdk=existsSync('.tools/dotnet/dotnet.exe')?path.resolve('.tools/dotnet/dotnet.exe'):process.env.DOTNET_ROOT?path.join(process.env.DOTNET_ROOT,'dotnet.exe'):'dotnet';
const fixture=path.join(root,'.test-data','submission-layout-'+randomUUID()),output=path.join(root,'artifacts','submission-layout');mkdirSync(output,{recursive:true});
const password='Test-'+randomUUID();const listener=net.createServer();await new Promise(r=>listener.listen(0,'127.0.0.1',r));const address='http://127.0.0.1:'+listener.address().port;await new Promise(r=>listener.close(r));
let logs='',ui;
const server=spawn(sdk,[path.join(services,'src/SchoolMessenger.Server/bin/Debug/net10.0/SchoolMessenger.Server.dll'),'--urls',address],{cwd:services,windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',School__DataDirectory:path.join(fixture,'server'),School__AdminPassword:password,FeedbackMail__Enabled:'false',Feedback__AutomaticReportsEnabled:'false',Operations__AlertMailEnabled:'false'}});
server.stdout.on('data',b=>logs+=b);server.stderr.on('data',b=>logs+=b);
const pause=ms=>new Promise(r=>setTimeout(r,ms));
class Client{
  cookies=new Map();csrf='';
  async request(route,method='GET',body){const headers={Cookie:[...this.cookies].map(([k,v])=>k+'='+v).join('; ')};if(method!=='GET')headers['X-CSRF-TOKEN']=this.csrf;const options={method,headers};if(body instanceof FormData)options.body=body;else if(body!==undefined){headers['Content-Type']='application/json';options.body=JSON.stringify(body);}const response=await fetch(address+route,options);for(const cookie of response.headers.getSetCookie()){const pair=cookie.split(';')[0],i=pair.indexOf('=');this.cookies.set(pair.slice(0,i),pair.slice(i+1));}const text=await response.text();assert.equal(response.status,200,route+': '+text);return text?JSON.parse(text):{};}
  async login(username){this.csrf=(await this.request('/api/session')).csrfToken;await this.request('/api/login','POST',{username,password});const session=await this.request('/api/session');this.csrf=session.csrfToken;return session.user;}
}
try{
  let ready=false;for(let i=0;i<200;i++){assert.equal(server.exitCode,null,logs);try{if((await fetch(address+'/health')).ok){ready=true;break;}}catch{}await pause(150);}assert.ok(ready,'Fixture did not start');
  const owner=new Client();await owner.login('admin');const ids=[];
  for(let i=1;i<=16;i++){const user=await owner.request('/api/admin/users','POST',{username:'layout.'+i,name:'가상교사'+String(i).padStart(2,'0'),department:'업무검사',password,isAdmin:false,canBroadcast:false});ids.push(user.id);}
  const request=await owner.request('/api/messages','POST',{clientId:randomUUID(),title:'부서별 운영 계획 자료 제출',body:'부서별 운영 계획 자료를 제출해 주세요.',recipientIds:ids,attachmentIds:[],all:false,submissionDeadline:Date.now()+7*86400000});
  const recipient=new Client();await recipient.login('layout.1');const form=new FormData();form.append('file',new Blob(['synthetic-submission: isolated layout verification']),'2026학년도_부서별_운영계획서_교육과정_평가계획_세부운영내용_최종검토완료본_담당교사확인_제출자료.txt');const file=await recipient.request('/api/attachments','POST',form);
  await recipient.request('/api/submission-requests/'+request.id+'/submit','POST',{clientId:randomUUID(),attachmentIds:[file.id]});
  ui=spawn(sdk,[path.join(root,'src/SchoolMessenger.Desktop/bin/Debug/net10.0-windows/SchoolMessenger.dll'),'--verify-ui'],{cwd:root,windowsHide:true,env:{...process.env,SCHOOL_UI_SERVER:address,SCHOOL_UI_USER:'admin',SCHOOL_UI_PASSWORD:password,SCHOOL_UI_OUTPUT:path.join(output,'client-preview.png'),SCHOOL_UI_LOCALDIR:path.join(fixture,'client'),YY_SUBMISSION_UI_ONLY:'1',YY_SUBMISSION_PREVIEW_ID:request.id}});
  let timeout;const code=await Promise.race([new Promise((resolve,reject)=>{ui.once('error',reject);ui.once('exit',resolve);}),new Promise(resolve=>{timeout=setTimeout(()=>resolve(-1),90000);})]);clearTimeout(timeout);
  assert.equal(code,0,existsSync('artifacts/ui-error.txt')?readFileSync('artifacts/ui-error.txt','utf8'):'Native UI check failed');
  const report=JSON.parse(readFileSync(path.join(output,'submission-layout-checks.json'),'utf8'));for(const check of report.checks)console.log('PASS '+check);console.log(report.checks.length+' native submission layout checks passed.');
}finally{if(ui?.exitCode===null)ui.kill();if(server.exitCode===null){const closed=new Promise(r=>server.once('exit',r));server.kill();await closed;}}
