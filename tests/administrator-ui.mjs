import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {mkdirSync,readFileSync,writeFileSync,existsSync} from 'node:fs';
import path from 'node:path';
import net from 'node:net';
const root=process.cwd(),fixture=path.resolve(process.env.YY_TEST_DATA_ROOT??'.test-data','administrator-ui-'+randomUUID()),data=path.join(fixture,'server'),sdk=path.join(root,'.tools/dotnet/dotnet.exe'),password='Synthetic-'+randomUUID();mkdirSync(fixture,{recursive:true});
const socket=net.createServer();await new Promise(r=>socket.listen(0,'127.0.0.1',r));const base='http://127.0.0.1:'+socket.address().port;await new Promise(r=>socket.close(r));
const delay=ms=>new Promise(r=>setTimeout(r,ms));let server,ui,watchdog,logs='';const output=path.join(root,'artifacts','administrator-ui');mkdirSync(output,{recursive:true});
async function stop(p){if(p?.exitCode===null){p.kill();await new Promise(r=>p.once('exit',r));}}
try{
 server=spawn(sdk,[path.join(root,'src/SchoolMessenger.Server/bin/Debug/net10.0/SchoolMessenger.Server.dll'),'--urls',base],{windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',School__DataDirectory:data,School__AdminPassword:password,Announcements__Address:''}});server.stdout.on('data',b=>logs+=b);server.stderr.on('data',b=>logs+=b);
 for(let i=0;i<100;i++){try{if((await fetch(base+'/health')).ok)break;}catch{}await delay(100);}assert.equal((await fetch(base+'/health')).status,200,logs);
 const cookies=new Map();let csrf;
 async function request(route,body){const headers={Cookie:[...cookies].map(([k,v])=>k+'='+v).join('; ')};if(body){headers['Content-Type']='application/json';headers['X-CSRF-TOKEN']=csrf??'';}const r=await fetch(base+'/api/'+route,{method:body?'POST':'GET',headers,body:body?JSON.stringify(body):undefined});for(const c of r.headers.getSetCookie()){const v=c.split(';')[0],i=v.indexOf('=');cookies.set(v.slice(0,i),v.slice(i+1));}const text=await r.text();assert.equal(r.status,200,text);return text?JSON.parse(text):null;}
 csrf=(await request('session')).csrfToken;await request('login',{username:'admin',password});csrf=(await request('session')).csrfToken;
 await request('admin/users',{username:'ordinary',name:'Synthetic teacher',department:'Test',password,isAdmin:false,canBroadcast:false});
 ui=spawn(sdk,[path.join(root,'src/SchoolMessenger.Desktop/bin/Debug/net10.0-windows/SchoolMessenger.dll'),'--verify-admin'],{windowsHide:true,env:{...process.env,SCHOOL_UI_SERVER:base,SCHOOL_UI_LOCALDIR:path.join(fixture,'client'),SCHOOL_UI_PASSWORD:password,SCHOOL_UI_TEACHER:'ordinary',YY_ADMIN_UI_OUTPUT:output}});
 const code=await Promise.race([new Promise(r=>ui.once('exit',r)),new Promise((_,reject)=>{watchdog=setTimeout(()=>reject(Error('Native administrator test timed out')),90000);})]);
 assert.equal(code,0,existsSync(path.join(output,'administrator-ui-error.txt'))?readFileSync(path.join(output,'administrator-ui-error.txt'),'utf8'):'Native UI verification failed');
 const report=JSON.parse(readFileSync(path.join(output,'administrator-ui-checks.json'),'utf8'));writeFileSync('artifacts/administrator-ui-test-results.json',JSON.stringify(report,null,2));for(const item of report.passed)console.log('PASS '+item);console.log(report.passed.length+' native administrator UI checks passed.');
}finally{clearTimeout(watchdog);await stop(ui);await stop(server);}
