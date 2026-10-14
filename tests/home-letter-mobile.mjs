// Requires tests/portal-preview.mjs, a local Android emulator and current debug APKs.
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {randomUUID} from 'node:crypto';
import path from 'node:path';
const root=process.cwd(),fixture=JSON.parse(readFileSync('.test-data/portal-preview/access.json','utf8'));
for(const base of [fixture.portal,fixture.office])assert.ok(new URL(base).hostname==='127.0.0.1','Synthetic loopback services only');
const sdk=process.env.YY_ANDROID_SDK??path.join(root,'.tools/android'),adb=process.env.YY_ADB??path.join(sdk,'platform-tools/adb.exe'),port=process.env.YY_ADB_PORT??'5039',serial=process.env.YY_ANDROID_SERIAL??'emulator-5556';
const work=path.join(root,'.test-data/home-letter'),screens=path.join(root,'artifacts/app-screens');mkdirSync(work,{recursive:true});mkdirSync(screens,{recursive:true});
if(!existsSync(path.join(work,'sample.jpg'))||!existsSync(path.join(work,'sample.png'))){
 const generated=spawnSync('powershell.exe',['-NoProfile','-Command',`
 Add-Type -AssemblyName System.Drawing
 $image=New-Object System.Drawing.Bitmap 1000,1414
 $canvas=[System.Drawing.Graphics]::FromImage($image)
 $font=New-Object System.Drawing.Font 'Arial',24
 try {
  $canvas.Clear([System.Drawing.Color]::White)
  $canvas.DrawString('SYNTHETIC SCHOOL NOTICE',$font,[System.Drawing.Brushes]::Navy,70,100)
  $canvas.DrawString('No real student or parent information.',$font,[System.Drawing.Brushes]::Black,70,230)
  $canvas.DrawString('Read inline and enlarge to check the text.',$font,[System.Drawing.Brushes]::Black,70,330)
  $image.Save((Join-Path $env:YY_HOME_LETTER_FIXTURES 'sample.jpg'),[System.Drawing.Imaging.ImageFormat]::Jpeg)
  $image.Save((Join-Path $env:YY_HOME_LETTER_FIXTURES 'sample.png'),[System.Drawing.Imaging.ImageFormat]::Png)
 } finally {$font.Dispose();$canvas.Dispose();$image.Dispose()}
 `],{env:{...process.env,YY_HOME_LETTER_FIXTURES:work},windowsHide:true,encoding:'utf8'});assert.equal(generated.status,0,generated.stderr);
}
const passed=[],delay=ms=>new Promise(r=>setTimeout(r,ms));function check(text){passed.push(text);console.log('PASS '+text);}
function cmd(...args){const result=spawnSync(adb,['-P',port,'-s',serial,...args],{encoding:'utf8',windowsHide:true,timeout:120000});if(result.status!==0)throw Error((result.stderr||result.error?.message||'adb failed').slice(-700));return result.stdout;}
const decode=s=>s.replaceAll('&quot;','"').replaceAll('&lt;','<').replaceAll('&gt;','>').replaceAll('&amp;','&');
function dump(){cmd('shell','uiautomator','dump','/sdcard/yy-home-letter.xml');cmd('pull','/sdcard/yy-home-letter.xml',path.join(work,'ui.xml'));return[...readFileSync(path.join(work,'ui.xml'),'utf8').matchAll(/<node\s+([^>]+)>?/g)].map(m=>Object.fromEntries([...m[1].matchAll(/([\w-]+)="([^"]*)"/g)].map(a=>[a[1],decode(a[2])])));}
async function find(predicate){let observed='';for(let i=0;i<30;i++){try{const nodes=dump(),item=nodes.find(predicate);if(item)return item;observed=nodes.map(n=>n.text).filter(Boolean).join(' | ');}catch(error){observed=error.message;}await delay(400);}await screenshot('android-home-letter-failure');throw Error('Native control missing: '+observed);}
function tap(node){const b=node.bounds.match(/\d+/g).map(Number);cmd('shell','input','tap',String((b[0]+b[2])/2|0),String((b[1]+b[3])/2|0));}
async function click(text){tap(await find(n=>(n.text===text||n['content-desc']===text||text.endsWith('.png')&&n['content-desc']?.startsWith(text+','))&&n.enabled==='true'));await delay(400);}
async function input(index,value){const fields=dump().filter(n=>n.class==='android.widget.EditText');assert.ok(fields[index]);tap(fields[index]);cmd('shell','input','keyevent','KEYCODE_MOVE_END',...Array(100).fill('KEYCODE_DEL'));cmd('shell','input','text',value);cmd('shell','input','keyevent','KEYCODE_BACK');await delay(250);}
async function scroll(text,partial=false){for(let i=0;i<9;i++){if(dump().some(n=>partial?n.text.includes(text):n.text===text))return;cmd('shell','input','swipe','540','1850','540','650','350');await delay(250);}throw Error('Scroll target missing '+text);}
async function screenshot(name){cmd('shell','screencap','-p','/sdcard/yy-home-letter.png');cmd('pull','/sdcard/yy-home-letter.png',path.join(screens,name+'.png'));}
async function install(kind){const name='kr.school.yeoyang.'+kind;cmd('shell','input','keyevent','KEYCODE_WAKEUP');cmd('shell','wm','dismiss-keyguard');cmd('push',path.join(root,'artifacts/mobile/'+name+'-Signed.apk'),'/data/local/tmp/yy-'+kind+'.apk');cmd('shell','pm','install','-r','/data/local/tmp/yy-'+kind+'.apk');cmd('shell','pm','clear',name);cmd('shell','monkey','-p',name,'-c','android.intent.category.LAUNCHER','1');await delay(2500);}
async function login(base,user){await find(n=>n.class==='android.widget.EditText');await input(0,base.replace('127.0.0.1','10.0.2.2'));await input(1,user);await input(2,fixture.password);await click('로그인');await click('확인');}
class Client{
 constructor(base){this.base=base;this.cookies=new Map();}
 async req(route,method='GET',body){const headers={Cookie:[...this.cookies].map(([k,v])=>k+'='+v).join('; ')};let payload;if(method!=='GET'){headers['X-CSRF-TOKEN']=this.csrf;if(body instanceof FormData)payload=body;else{headers['Content-Type']='application/json';payload=JSON.stringify(body??{});}}const r=await fetch(this.base+'/api/'+route,{method,headers,body:payload});for(const c of r.headers.getSetCookie()){const v=c.split(';')[0],at=v.indexOf('=');this.cookies.set(v.slice(0,at),v.slice(at+1));}const text=await r.text();assert.equal(r.status,200,route+' '+text);return text?JSON.parse(text):null;}
 async login(user){this.csrf=(await this.req('session')).csrfToken;await this.req('login','POST',{username:user,password:fixture.password});this.csrf=(await this.req('session')).csrfToken;return this;}
}
const teacher=await new Client(fixture.office).login(fixture.admin),setup=await teacher.req('external-announcements/setup');
const form=new FormData();form.append('kind','home-letter');form.append('file',new Blob([readFileSync(path.join(work,'sample.jpg'))]),'sample.jpg');const file=await teacher.req('external-announcements/attachments','POST',form);
const queued=await teacher.req('external-announcements','POST',{clientId:randomUUID().replaceAll('-',''),title:'가정통신문 이미지 검증',body:'가상학교 기능 검증 자료입니다. 중요한 안내는 텍스트 본문에도 함께 적습니다.',audience:'both',classIds:[setup.classes[0].id],all:false,attachmentIds:[file.id],kind:'home-letter'});
for(let i=0;i<120;i++){const row=(await teacher.req('external-announcements')).find(r=>r.id===queued.id);if(row.state==='published')break;if(row.state==='needs-review'||i===119)throw Error('Synthetic home-letter did not publish: '+row.state+' '+row.error);await delay(500);}
if(!process.argv.includes('--teacher-only')){
await install('family');await login(fixture.portal,fixture.parent);await click('가정통신문 이미지 검증');await find(n=>n.text==='sample.jpg');await screenshot('android-home-letter-inline');assert.ok(!dump().some(n=>n.text.includes('저장/공유')));check('parent app shows authenticated JPEG inline without a save/share button');
await scroll('확대해서 읽기');await click('확대해서 읽기');await find(n=>n.text==='가정통신문 확대');await screenshot('android-home-letter-enlarged');check('parent can enlarge the document after another authorization check');
cmd('shell','input','keyevent','KEYCODE_BACK');await delay(400);cmd('shell','input','keyevent','KEYCODE_BACK');await delay(400);
}
await install('teacher');await login(fixture.office,fixture.admin);await click('메시지');await click('학생·보호자 공지 작성 / 발송 현황');await find(n=>n.text==='가정통신문 · JPG/PNG 한 장당 500KB');await screenshot('android-home-letter-teacher-compose');check('separate teacher app defaults to home-letter mode with image and size guidance');
await input(1,'Native_home_letter');await input(2,'Native_image_view');tap(await find(n=>n.class==='android.widget.CheckBox'&&n.enabled==='true'));
cmd('push',path.join(work,'sample.png'),'/sdcard/Download/yy-home-letter.png');cmd('shell','am','broadcast','-a','android.intent.action.MEDIA_SCANNER_SCAN_FILE','-d','file:///sdcard/Download/yy-home-letter.png');await scroll('가정통신문 이미지 추가 · 한 장당 500KB');await click('가정통신문 이미지 추가 · 한 장당 500KB');
if(!dump().some(n=>n.text==='yy-home-letter.png'||n['content-desc']?.includes('yy-home-letter.png'))){const roots=await find(n=>n['content-desc']==='Show roots');tap(roots);await click('Downloads');}
await click('yy-home-letter.png');await find(n=>n.text==='공지 보내기');await scroll('yy-home-letter.png');check('native image picker preserves the teacher draft and uploads a PNG under the size limit');
await scroll('공지 보내기');await click('공지 보내기');await click('발송');await find(n=>n.text.includes('발송 대기열에 등록했습니다'));await click('메시지');await click('학생·보호자 공지 작성 / 발송 현황');await scroll('Native_home_letter',true);await find(n=>n.text.includes('Native_home_letter'));
for(let i=0;i<120;i++){const sent=(await teacher.req('external-announcements')).find(r=>r.request.title==='Native_home_letter');if(sent?.state==='published'){assert.equal(sent.detail.notice.kind,'home-letter');assert.equal(sent.detail.attachments[0].name,'yy-home-letter.png');break;}if(sent?.state==='needs-review'||i===119)throw Error('Native image notice did not publish: '+sent?.state);await delay(500);}check('teacher app publishes the selected image home-letter through the office bridge');
writeFileSync('artifacts/home-letter-mobile-test-results.json',JSON.stringify({version:'1.0.0',passed,verifiedAt:new Date().toISOString(),environment:'Android 36 emulator and local synthetic services'},null,2));console.log(passed.length+' native home-letter checks passed.');
