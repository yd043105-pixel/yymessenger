import {existsSync,lstatSync,mkdirSync,readdirSync,readFileSync,writeFileSync} from 'node:fs';
import path from 'node:path';

// This collector never contacts the school server or publishes original reports.
const root=path.resolve(process.argv[2]??'.maintenance'),inbox=path.join(root,'inbox');
const safeFile=file=>{const stat=lstatSync(file);if(stat.isSymbolicLink()||!stat.isFile()||stat.size>1_048_576)throw Error('Only regular JSON files up to 1 MiB are accepted');return JSON.parse(readFileSync(file,'utf8').replace(/^\uFEFF/,''));};
if(existsSync(root)&&lstatSync(root).isSymbolicLink())throw Error('Maintenance directory must not be a link');
if(existsSync(inbox)&&lstatSync(inbox).isSymbolicLink())throw Error('Inbox must not be a link');
const ledgerPath=path.join(root,'processed.json'),ledger=existsSync(ledgerPath)?safeFile(ledgerPath):{},latest=new Map();
const files=existsSync(inbox)?readdirSync(inbox).filter(name=>name.endsWith('.json')).sort():[];
if(files.length>50)throw Error('Review and archive the inbox before adding more than 50 exports');
for(const name of files){
 const batch=safeFile(path.join(inbox,name));
 if(batch.schema!==1||!['office','announcements'].includes(batch.service)||!Array.isArray(batch.reports)||batch.reports.length>100)throw Error('Unsupported development export');
 for(const report of batch.reports){
  if(typeof report.id!=='string'||!new RegExp('^'+batch.service+'-[a-f0-9]{32}$').test(report.id)||!Number.isSafeInteger(report.revision)||report.revision<1||typeof report.summary!=='string'||report.summary.trim().length<10||report.summary.length>2000||!Number.isSafeInteger(report.updatedAt))throw Error('Invalid reviewed report');
  const safe={id:report.id,revision:report.revision,summary:report.summary,updatedAt:report.updatedAt};
  const previous=latest.get(safe.id);
  if(previous?.revision===safe.revision&&JSON.stringify(previous)!==JSON.stringify(safe))throw Error('Conflicting report copies: refresh the administrator export');
  if(!previous||safe.revision>previous.revision)latest.set(safe.id,safe);
 }
}
const reports=[...latest.values()].filter(report=>!(ledger[report.id]?.result==='verified'&&ledger[report.id]?.revision>=report.revision)).sort((a,b)=>a.updatedAt-b.updatedAt||a.id.localeCompare(b.id));
const day=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Seoul',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
const daily=path.join(root,'daily');
if(existsSync(daily)&&lstatSync(daily).isSymbolicLink())throw Error('Daily output must not be a link');
mkdirSync(daily,{recursive:true});
const output=path.join(daily,day+'.json');
if(existsSync(output)&&lstatSync(output).isSymbolicLink())throw Error('Output must not be a link');
writeFileSync(output,JSON.stringify({schema:1,day,timeZone:'Asia/Seoul',reports},null,2));
console.log(JSON.stringify({day,exportFiles:files.length,pending:reports.length,output}));
