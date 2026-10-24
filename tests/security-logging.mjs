import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {existsSync,mkdirSync,writeFileSync} from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import {DatabaseSync} from 'node:sqlite';

const root=process.cwd(),observe=process.argv.includes('--observe');
const packageRoot=process.argv.includes('--package')?path.join(root,'artifacts','여양고-교무메신저'):null;
const output=path.resolve(process.env.YY_SECURITY_OUTPUT??path.join(root,'artifacts'));
const work=path.join(process.env.YY_TEST_DATA_ROOT??path.join(root,'.test-data'),'logging-privacy-'+randomUUID());
const dotnet=path.join(root,'.tools/dotnet/dotnet.exe'),results=[];
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function address(){const server=net.createServer();await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const port=server.address().port;await new Promise(resolve=>server.close(resolve));return 'http://127.0.0.1:'+port;}
for(const project of ['Server','AnnouncementServer']){
  const data=path.join(work,project);mkdirSync(data,{recursive:true});
  const prefix=project==='Server'?'School':'Portal',base=await address(),canary='YY_SYNTHETIC_PRIVATE_'+randomUUID();
  const failureCanary='YY_SYNTHETIC_EXCEPTION_'+randomUUID(),password='Synthetic-'+randomUUID();
  const directory=packageRoot?path.join(packageRoot,project==='Server'?'server':'announcement-server'):path.join(root,'src','SchoolMessenger.'+project);
  const executable=packageRoot?path.join(directory,'SchoolMessenger.'+project+'.exe'):dotnet;
  const args=packageRoot?['--urls',base]:[path.join(root,`src/SchoolMessenger.${project}/bin/Debug/net10.0/SchoolMessenger.${project}.dll`),'--urls',base];
  const child=spawn(executable,args,{
    cwd:directory,windowsHide:true,
    env:{...process.env,ASPNETCORE_ENVIRONMENT:'Production',[prefix+'__DataDirectory']:data,[prefix+'__AdminPassword']:password,Portal__BridgeKey:'SyntheticBridge-'+randomUUID(),Portal__TunnelOnly:'false',DOTNET_PROCESSOR_COUNT:'2'}
  });
  let log='';child.stdout.on('data',bytes=>log+=bytes);child.stderr.on('data',bytes=>log+=bytes);
  try{
    let ready=false;
    for(let attempt=0;attempt<100;attempt++){
      try{if((await fetch(base+'/health')).ok){ready=true;break;}}catch{}
      if(child.exitCode!==null)throw Error(project+' did not start');await sleep(100);
    }
    assert.ok(ready,project+' startup timeout');
    const response=await fetch(base+'/api/session?audit_private_probe='+canary);
    assert.equal(response.status,200);await response.text();await sleep(250);
    const cookies=new Map();let csrf='';
    async function request(route,method='GET',body){
      const headers={Cookie:[...cookies].map(([key,value])=>key+'='+value).join('; ')};
      if(method!=='GET'){headers['Content-Type']='application/json';headers['X-CSRF-TOKEN']=csrf;}
      const reply=await fetch(base+'/api/'+route,{method,headers,body:method==='GET'?undefined:JSON.stringify(body)});
      for(const cookie of reply.headers.getSetCookie()){const pair=cookie.split(';')[0],index=pair.indexOf('=');cookies.set(pair.slice(0,index),pair.slice(index+1));}
      return reply;
    }
    csrf=(await (await request('session')).json()).csrfToken;
    assert.equal((await request('login','POST',{username:'admin',password})).status,200);
    csrf=(await (await request('session')).json()).csrfToken;
    const db=new DatabaseSync(path.join(data,project==='Server'?'school.db':'announcements.db'));
    try{db.exec('PRAGMA busy_timeout=30000');db.exec(`CREATE TRIGGER SyntheticPrivateFailure BEFORE INSERT ON ${project==='Server'?'Todos':'Classes'} BEGIN SELECT RAISE(ABORT,'${failureCanary}'); END`);}finally{db.close();}
    const failure=await request(project==='Server'?'todos':'admin/classes','POST',project==='Server'?{title:'Synthetic logging check',status:'open'}:{name:'Synthetic logging class',grade:1});
    assert.equal(failure.status,500);assert.ok(!(await failure.text()).includes(failureCanary));await sleep(250);
  }finally{
    if(child.exitCode===null&&child.signalCode===null){child.kill();await new Promise(resolve=>child.once('exit',resolve));}
  }
  const result={project,environment:'Production',queryValueLogged:log.includes(canary),exceptionValueLogged:log.includes(failureCanary),safeAuditPresent:log.includes('Backend request')&&log.includes('GET /api/session'),safeFailurePresent:log.includes('Request processing failed')};
  results.push(result);
  if(!observe){assert.equal(result.queryValueLogged,false,project+' logged a private query value');assert.equal(result.exceptionValueLogged,false,project+' logged a private exception value');assert.equal(result.safeAuditPresent,true,project+' removed useful sanitized audit logs');assert.equal(result.safeFailurePresent,true,project+' removed safe exception diagnostics');}
  console.log((observe?'OBSERVED ':'PASS ')+project+': queryValueLogged='+result.queryValueLogged+', exceptionValueLogged='+result.exceptionValueLogged+', safeAuditPresent='+result.safeAuditPresent);
}
mkdirSync(output,{recursive:true});writeFileSync(path.join(output,observe?'logging-before.json':packageRoot?'packaged-security-logging-checks.json':'security-logging-checks.json'),JSON.stringify({version:'1.0.0',verifiedAt:new Date().toISOString(),packaged:Boolean(packageRoot),syntheticDataOnly:true,observationOnly:observe,results},null,2));
