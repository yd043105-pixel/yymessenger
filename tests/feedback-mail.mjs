import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdirSync,writeFileSync} from 'node:fs';
import {randomUUID} from 'node:crypto';
import path from 'node:path';

const sdk=existsSync('.tools/dotnet/dotnet.exe')?path.resolve('.tools/dotnet/dotnet.exe'):process.env.DOTNET_ROOT?path.join(process.env.DOTNET_ROOT,process.platform==='win32'?'dotnet.exe':'dotnet'):'dotnet';
const directory=path.resolve(process.env.YY_TEST_DATA_ROOT??'.test-data','feedback-mail-'+randomUUID());
let result=spawnSync(sdk,['build','tests/FeedbackMailChecks','-p:RestoreLockedMode=true','--nologo','-v','quiet'],{encoding:'utf8',windowsHide:true});assert.equal(result.status,0,result.stdout+result.stderr);
result=spawnSync(sdk,['tests/FeedbackMailChecks/bin/Debug/net10.0/FeedbackMailChecks.dll',directory],{encoding:'utf8',windowsHide:true});assert.equal(result.status,0,result.stdout+result.stderr);const report=JSON.parse(result.stdout);mkdirSync('artifacts',{recursive:true});writeFileSync('artifacts/feedback-mail-checks.json',JSON.stringify(report,null,2));console.log('PASS '+report.checks.length+' feedback email queue and subject checks; no real SMTP delivery');
