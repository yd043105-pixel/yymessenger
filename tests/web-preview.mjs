// Isolated browser verification server; no school data or fixed credentials.
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {mkdirSync,writeFileSync,existsSync,cpSync} from 'node:fs';
import path from 'node:path';
import net from 'node:net';
const root=process.cwd(),data=path.join(root,'.test-data','web-'+randomUUID()),password='Browser-'+randomUUID();
mkdirSync(data,{recursive:true});
const listener=net.createServer();await new Promise(r=>listener.listen(0,'127.0.0.1',r));const address='http://127.0.0.1:'+listener.address().port;await new Promise(r=>listener.close(r));
const runtime=path.join(data,'runtime');cpSync(path.join(root,'src/SchoolMessenger.Server/bin/Debug/net10.0'),runtime,{recursive:true});
const server=spawn(path.join(root,'.tools/dotnet/dotnet.exe'),[path.join(runtime,'SchoolMessenger.Server.dll'),'--urls',address],{cwd:path.join(root,'src/SchoolMessenger.Server'),windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',School__DataDirectory:data,School__AdminPassword:password}});
let log='';server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);
const delay=ms=>new Promise(r=>setTimeout(r,ms));
try{
 for(let n=0;n<100;n++){try{if((await fetch(address+'/health')).ok)break;}catch{}await delay(100);if(n===99)throw Error(log);}
 const cookies=new Map();let csrf;
 async function call(route,body){const headers={Cookie:[...cookies].map(([k,v])=>k+'='+v).join('; ')};if(body){headers['Content-Type']='application/json';headers['X-CSRF-TOKEN']=csrf;}
 const r=await fetch(address+route,{method:body?'POST':'GET',headers,body:body?JSON.stringify(body):undefined});for(const c of r.headers.getSetCookie()){const p=c.split(';')[0],i=p.indexOf('=');cookies.set(p.slice(0,i),p.slice(i+1));}const text=await r.text();if(!r.ok)throw Error(text);return text?JSON.parse(text):null;}
 csrf=(await call('/api/session')).csrfToken;await call('/api/login',{username:'admin',password});const admin=(await call('/api/session'));csrf=admin.csrfToken;
 const teacher=await call('/api/admin/users',{username:'teacher',name:'김교사',department:'교무기획부',password,isAdmin:false,canBroadcast:false});
 const ids=(await call('/api/users'));const id=ids.find(p=>p.name==='김교사').id;
 await call('/api/messages',{clientId:randomUUID(),title:'학생부 점검표 제출 안내',body:'내일까지 학생부 점검표를 제출해 주세요.\n<svg onload=alert(1)>는 실행되지 않는 원문입니다.',recipientIds:[id],attachmentIds:[],all:false,submissionDeadline:Date.now()+3*86400000});
 await call('/api/chats',{name:'교무기획부 협의',memberIds:[id]});
 await call('/api/surveys',{title:'교무회의 시간 조사',description:'가능한 시간을 선택해 주세요.',deadline:Date.now()+86400000,questions:[{text:'가능한 시간',kind:'choice',required:true,options:['15시','16시']}],targetIds:[id],all:false});
 writeFileSync(path.join(root,'artifacts/web-preview-session.json'),JSON.stringify({address,data,username:'teacher',password}));
 console.log('Browser verification server ready: '+address+'/office/');
 while(!existsSync(path.join(data,'stop'))&&server.exitCode===null)await delay(500);
}finally{server.kill();}
