import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {existsSync,mkdirSync,writeFileSync,readFileSync} from 'node:fs';
import path from 'node:path';
const root=process.cwd(),dotnet=existsSync('.tools/dotnet/dotnet.exe')?path.join(root,'.tools/dotnet/dotnet.exe'):path.join(process.env.DOTNET_ROOT,'dotnet.exe');
const fixture=path.join(root,'.test-data','boundaries-'+randomUUID()),passed=[];
for(const [project,setting,foreignDb,ownDb] of [['Server','School','announcements.db','school.db'],['AnnouncementServer','Portal','school.db','announcements.db']]){
 const directory=path.join(fixture,project);mkdirSync(directory,{recursive:true});const sentinel='Unrelated school database must remain untouched';writeFileSync(path.join(directory,foreignDb),sentinel);
 const child=spawn(dotnet,[path.join(root,`src/SchoolMessenger.${project}/bin/Debug/net10.0/SchoolMessenger.${project}.dll`),'--urls','http://127.0.0.1:0'],{cwd:path.join(root,'src','SchoolMessenger.'+project),windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',[setting+'__DataDirectory']:directory,[setting+'__AdminPassword']:'Test-'+randomUUID(),Portal__BridgeKey:'Bridge-'+randomUUID()}});
 let log='';child.stdout.on('data',b=>log+=b);child.stderr.on('data',b=>log+=b);
 const timer=setTimeout(()=>child.kill(),10000);const code=await new Promise(resolve=>child.once('exit',resolve));clearTimeout(timer);
 assert.notEqual(code,0);assert.match(log,/InvalidOperationException/);assert.equal(existsSync(path.join(directory,ownDb)),false);assert.equal(readFileSync(path.join(directory,foreignDb),'utf8'),sentinel);
 const name=project+' refuses the other service database directory before initialization';passed.push(name);console.log('PASS '+name);
}
mkdirSync('artifacts',{recursive:true});writeFileSync('artifacts/data-boundaries-test-results.json',JSON.stringify({at:new Date().toISOString(),passed},null,2));console.log(passed.length+' data isolation checks passed.');
