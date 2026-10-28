import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {existsSync,readFileSync,writeFileSync,mkdirSync,copyFileSync} from 'node:fs';
import path from 'node:path';
assert.equal(process.platform,'darwin');
const output=path.resolve('artifacts/mac');mkdirSync(output,{recursive:true});
const runtime=process.arch==='arm64'?'maccatalyst-arm64':'maccatalyst-x64';
const bundle=path.resolve(`src/SchoolMessenger.Mac/bin/Debug/net10.0-maccatalyst/${runtime}/SchoolMessenger.Mac.app`);
const delay=ms=>new Promise(r=>setTimeout(r,ms));let logs='',appLogs='',report,app;
const fixture=spawn(process.execPath,['tests/portal-preview.mjs']);
fixture.stdout.on('data',b=>logs+=b);fixture.stderr.on('data',b=>logs+=b);
try{
  for(let i=0;i<240&&!logs.includes('timetable fixture ready.');i++){assert.equal(fixture.exitCode,null,logs);await delay(500);}
  assert.ok(logs.includes('timetable fixture ready.'),'Fixture startup failed: '+logs);
  const fixtureData=readFileSync('.test-data/portal-preview/access.json');
  app=spawn(path.join(bundle,'Contents/MacOS/SchoolMessenger.Mac'),[],{env:{...process.env,YY_MAC_FIXTURE_B64:fixtureData.toString('base64')}});
  app.stdout.on('data',b=>{appLogs+=b;const match=appLogs.match(/MAC_REPORT_PATH:([^\r\n]+)/);if(match)report=match[1];});app.stderr.on('data',b=>appLogs+=b);
  for(let i=0;i<600&&!(report&&existsSync(report));i++){assert.equal(app.exitCode,null,'Native app exited: '+appLogs);await delay(500);}
  writeFileSync(path.join(output,'mac-runtime.log'),appLogs);
  assert.ok(report&&existsSync(report),'Native report missing: '+appLogs);
  const result=JSON.parse(readFileSync(report,'utf8'));copyFileSync(report,path.join(output,'mac-results.json'));
  for(const name of ['mac-login.png','mac-messages.png','mac-compose.png','mac-message-detail.png'])if(existsSync(path.join(path.dirname(report),name)))copyFileSync(path.join(path.dirname(report),name),path.join(output,name));
  for(const check of result.passed)console.log('PASS '+check);assert.ok(result.ok,result.error);
}finally{app?.kill();fixture.kill();}
