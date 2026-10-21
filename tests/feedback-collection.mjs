import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import {tmpdir} from 'node:os';
import path from 'node:path';

const fixture=mkdtempSync(path.join(process.env.YY_TEST_DATA_ROOT??tmpdir(),'feedback-collector-')),inbox=path.join(fixture,'inbox');mkdirSync(inbox);
const sample={id:'office-'+'a'.repeat(32),revision:1,summary:'Synthetic reproduction: select test then blank page',updatedAt:Date.now()};
const write=(name,reports)=>writeFileSync(path.join(inbox,name),JSON.stringify({schema:1,service:'office',reports}));
const collect=(status=0)=>{const result=spawnSync(process.execPath,['scripts/Collect-Feedback.mjs',fixture],{encoding:'utf8',windowsHide:true});assert.equal(result.status,status,result.stderr);return status===0?JSON.parse(result.stdout):null;};
write('first.json',[{...sample,ownerId:'PRIVATE-NOT-FOR-EXPORT',details:'PRIVATE-ORIGINAL'}]);write('second.json',[sample]);
let result=collect();assert.equal(result.pending,1);const text=readFileSync(result.output,'utf8');assert.ok(!text.includes('PRIVATE'));assert.equal(JSON.parse(text).reports[0].summary,sample.summary);
writeFileSync(path.join(fixture,'processed.json'),JSON.stringify({[sample.id]:{revision:1,result:'verified'}}));assert.equal(collect().pending,0);
writeFileSync(path.join(fixture,'processed.json'),JSON.stringify({[sample.id]:{revision:1,result:'deferred'}}));assert.equal(collect().pending,1);
write('second.json',[{...sample,revision:2,summary:sample.summary+' revised'}]);assert.equal(collect().pending,1);
write('third.json',[{...sample,revision:2,summary:'Conflicting synthetic reproduction summary'}]);collect(1);
write('third.json',[{...sample,revision:3}]);write('first.json',[{...sample,id:'../outside'}]);collect(1);
write('first.json',[{...sample,summary:'x'.repeat(2001)}]);collect(1);
console.log('PASS feedback collector: deduplication, private-field omission, processed revisions, updated revisions, conflicting copies, ID validation and size limit');
