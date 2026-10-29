// Optional wall-clock check against the isolated portal-preview fixture; never a school server.
import assert from 'node:assert/strict';
import {readFileSync,writeFileSync} from 'node:fs';
import {randomUUID} from 'node:crypto';
const fixture=JSON.parse(readFileSync('.test-data/portal-preview/access.json','utf8'));
assert.ok(['localhost','127.0.0.1'].includes(new URL(fixture.office).hostname));
let cookies=new Map(),csrf='';
async function request(route,body){
 const headers={Cookie:[...cookies].map(([k,v])=>k+'='+v).join('; ')};
 if(body){headers['Content-Type']='application/json';headers['X-CSRF-TOKEN']=csrf;}
 const response=await fetch(fixture.office+'/api/'+route,{method:body?'POST':'GET',headers,body:body?JSON.stringify(body):undefined});
 for(const cookie of response.headers.getSetCookie()){const pair=cookie.split(';')[0],i=pair.indexOf('=');cookies.set(pair.slice(0,i),pair.slice(i+1));}
 assert.equal(response.status,200);const text=await response.text();return text?JSON.parse(text):null;
}
async function login(username,password){cookies=new Map();csrf=(await request('session')).csrfToken;await request('login',{username,password});return await request('session');}
await login(fixture.admin,fixture.password);csrf=(await request('session')).csrfToken;
const username='lease.'+randomUUID().slice(0,12),password='Test-'+randomUUID();
await request('admin/users',{username,name:'가상 연결 만료',department:'가상 검증',password,isAdmin:false,canBroadcast:false});
const session=await login(username,password);csrf=session.csrfToken;
await request('presence/mobile',{});const began=Date.now();
const own=async()=>(await request('users')).find(p=>p.id===session.user.id);
assert.equal((await own()).mobile,true);assert.equal((await own()).online,false);
await new Promise(r=>setTimeout(r,46_000));assert.equal((await own()).mobile,true);
await new Promise(r=>setTimeout(r,45_000));assert.equal((await own()).mobile,false);
writeFileSync('artifacts/mobile-presence-expiry-check.json',JSON.stringify({passed:true,elapsedMilliseconds:Date.now()-began,leaseSeconds:90,syntheticLocalFixture:true},null,2));
console.log('PASS phone lease stays alive at 46 seconds and expires without another heartbeat after 90 seconds');
