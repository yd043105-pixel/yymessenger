// Run against tests/portal-preview.mjs and an already booted test emulator.
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {readFileSync,writeFileSync,mkdirSync,copyFileSync} from 'node:fs';
import path from 'node:path';

const root=process.cwd(),fixture=JSON.parse(readFileSync('.test-data/portal-preview/access.json','utf8'));
for(const base of [fixture.office,fixture.portal])assert.ok(['localhost','127.0.0.1'].includes(new URL(base).hostname),'Native test must use a local synthetic server');
const adb=process.env.YY_ADB??path.join(root,'.tools/android/platform-tools/adb.exe'),serial=process.env.YY_ANDROID_SERIAL??'emulator-5556',port=process.env.YY_ADB_PORT??'5039';
const work=path.join(root,'.test-data/app-preview'),screens=path.join(root,'artifacts/app-screens'),passed=[];mkdirSync(work,{recursive:true});mkdirSync(screens,{recursive:true});
const delay=ms=>new Promise(r=>setTimeout(r,ms));
function cmd(...args){const result=spawnSync(adb,['-P',port,'-s',serial,...args],{encoding:'utf8',windowsHide:true,timeout:240000});if(result.status!==0)throw Error((result.stderr||result.stdout||result.error?.message||'adb failed').slice(-700));return result.stdout;}
const decode=s=>s.replaceAll('&quot;','"').replaceAll('&lt;','<').replaceAll('&gt;','>').replaceAll('&amp;','&');
function dump(){cmd('shell','uiautomator','dump','/sdcard/yy-office.xml');cmd('pull','/sdcard/yy-office.xml',path.join(work,'office-ui.xml'));return [...readFileSync(path.join(work,'office-ui.xml'),'utf8').matchAll(/<node\s+([^>]+)>?/g)].map(m=>Object.fromEntries([...m[1].matchAll(/([\w-]+)="([^"]*)"/g)].map(a=>[a[1],decode(a[2])])));}
async function find(predicate){for(let n=0;n<25;n++){try{const value=dump().find(predicate);if(value)return value;}catch{}await delay(500);}throw Error('Native control missing: '+dump().map(v=>v.text).filter(Boolean).join(' | '));}
function tap(node){const b=node.bounds.match(/\d+/g).map(Number);cmd('shell','input','tap',String((b[0]+b[2])/2|0),String((b[1]+b[3])/2|0));}
async function click(text){tap(await find(n=>n.text===text&&n.class==='android.widget.Button'));await delay(500);}
async function input(index,value){await find(n=>n.class==='android.widget.EditText');const fields=dump().filter(n=>n.class==='android.widget.EditText');assert.ok(fields[index]);tap(fields[index]);cmd('shell','input','keyevent','KEYCODE_MOVE_END',...Array(100).fill('KEYCODE_DEL'));cmd('shell','input','text',value);cmd('shell','input','keyevent','KEYCODE_BACK');await delay(250);}
async function install(kind){const packageId='kr.school.yeoyang.'+kind;cmd('push',path.join(root,'artifacts/mobile/'+packageId+'-Signed.apk'),'/data/local/tmp/yy-'+kind+'.apk');cmd('shell','pm','install','-r','/data/local/tmp/yy-'+kind+'.apk');cmd('shell','pm','clear',packageId);cmd('shell','monkey','-p',packageId,'-c','android.intent.category.LAUNCHER','1');await delay(1500);return packageId;}
async function login(base,user){await input(0,base.replace('127.0.0.1','10.0.2.2'));await input(1,user);await input(2,fixture.password);await click('로그인');}
async function screenshot(name){const file='C:/Users/user/AppData/Local/YyMessengerAvd/'+name+'.png';cmd('emu','screenrecord','screenshot',file);copyFileSync(file,path.join(screens,name+'.png'));}
function check(text){passed.push(text);console.log('PASS '+text);}

await install('teacher');await find(n=>n.text==='온라인 교무실');await screenshot('android-teacher-login-current');assert.ok(!dump().some(n=>n.text==='학교 소식 · 학생/보호자/교사'));check('teacher package opens office login without a family space selector');
await login(fixture.portal,fixture.parent);await find(n=>n.text.includes('교사 앱에는 교직원 교무실'));check('teacher package rejects a family server/account before rendering office screens');
await login(fixture.office,fixture.admin);await click('확인');await find(n=>n.text==='교직원 업무 메시지 검증');await screenshot('android-teacher-office-current');
let monitorCookies=new Map(),monitorCsrf='';
async function monitor(route,body){const headers={Cookie:[...monitorCookies].map(([k,v])=>k+'='+v).join('; ')};if(body){headers['Content-Type']='application/json';headers['X-CSRF-TOKEN']=monitorCsrf;}const response=await fetch(fixture.office+'/api/'+route,{method:body?'POST':'GET',headers,body:body?JSON.stringify(body):undefined});for(const cookie of response.headers.getSetCookie()){const pair=cookie.split(';')[0],i=pair.indexOf('=');monitorCookies.set(pair.slice(0,i),pair.slice(i+1));}assert.equal(response.status,200);const value=await response.text();return value?JSON.parse(value):null;}
monitorCsrf=(await monitor('session')).csrfToken;await monitor('login',{username:fixture.admin,password:fixture.password});const monitorSession=await monitor('session');monitorCsrf=monitorSession.csrfToken;
async function phone(expected){for(let i=0;i<30;i++){const user=(await monitor('users')).find(p=>p.id===monitorSession.user.id);assert.equal(user.online,false);if(user.mobile===expected)return;await delay(200);}assert.fail('Phone foreground/background presence did not match '+expected);}
await phone(true);cmd('shell','input','keyevent','KEYCODE_HOME');await phone(false);
cmd('shell','monkey','-p','kr.school.yeoyang.teacher','-c','android.intent.category.LAUNCHER','1');await find(n=>n.text==='교직원 업무 메시지 검증');await phone(true);
check('native teacher foreground publishes phone-only presence, background clears it and resume restores it without a PC connection');
await find(n=>n.text==='미확인 메시지');await click('교직원 업무 메시지 검증');await find(n=>n.text==='교직원 전용 메시지입니다. 외부 공지 서버에는 나타나지 않습니다.');
cmd('shell','input','keyevent','KEYCODE_BACK');await delay(500);await find(n=>n.text==='교직원 업무 메시지 검증');assert.ok(!dump().some(n=>n.text==='미확인 메시지'));check('received message shows unread state and opening it persists read status, including a self-recipient');
assert.ok(!dump().some(n=>n.text==='받은 메시지'));await click('메시지');await find(n=>n.text==='받은 메시지');await screenshot('android-teacher-inbox-current');await click('홈');await find(n=>n.text==='교직원 업무 메시지 검증');check('teacher home keeps recent messages compact and the message tab exposes mailbox choices');
await click('학사력 · 행사 · 학교 일정');await find(n=>n.text==='교직원 학사일정 검증');await screenshot('android-teacher-calendar');check('native teacher calendar reads authenticated school events');
cmd('shell','input','keyevent','KEYCODE_BACK');await delay(500);await click('특별실 예약 · 나의 예약');await find(n=>n.text==='특별실 예약하기');await screenshot('android-teacher-rooms');
await click('특별실 예약하기');await input(0,'Android-native-reservation');
// The isolated fixture already reserves the first period. Pick the second period for both ends.
for(let index=0;index<2;index++){tap(await find(n=>n.class==='android.widget.EditText'&&n.text.startsWith('1교시')));tap(await find(n=>n.text.startsWith('2교시')));await delay(300);}
await screenshot('android-teacher-reservation-editor');await click('특별실 예약 저장');await find(n=>n.text==='날짜 조회'&&n.class==='android.widget.Button');
const bookingDay=new Date(Date.now()+9*3600000+86400000).toISOString().slice(0,10),bookingEnd=new Date(Date.now()+9*3600000+2*86400000).toISOString().slice(0,10);assert.ok((await monitor('office/bookings?from='+bookingDay+'&to='+bookingEnd)).some(b=>b.title==='Android-native-reservation'));check('native room and period editor saves a reservation confirmed by the authenticated office API');
cmd('shell','input','keyevent','KEYCODE_BACK');await delay(500);await click('내 정보');await click('로그아웃');await find(n=>n.text==='온라인 교무실');
await install('family');await find(n=>n.text==='우리 학교 소식');await screenshot('android-family-login-current');assert.ok(!dump().some(n=>n.text==='교직원 교무실 · 교내망/VPN'));check('family package has independent installation identity and no office selector');
await login(fixture.office,fixture.admin);await find(n=>n.text.includes('학생·학부모용 학교 앱은 학교 소식 서버의'));check('student/parent app rejects an internal teacher account before rendering private data');
await login(fixture.portal,fixture.parent);await click('확인');await find(n=>n.text==='10월 현장체험학습 안내');await screenshot('android-family-home-current');check('family app accepts linked guardian and displays only its school feed');
await click('내 정보');await click('로그아웃');await find(n=>n.text==='우리 학교 소식');
writeFileSync('artifacts/mobile-office-ui-test-results.json',JSON.stringify({version:'1.0.0',passed,verifiedAt:new Date().toISOString(),environment:'Android 36 emulator and local synthetic services'},null,2));
console.log(passed.length+' native Android office/family checks passed.');
