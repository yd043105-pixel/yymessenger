import assert from 'node:assert/strict';
import dgram from 'node:dgram';
import {randomBytes} from 'node:crypto';
const product='Yeoyang.Office.Discovery.1',delay=ms=>new Promise(r=>setTimeout(r,ms));
export async function discoveryChecks({admin,ordinary,base,check}){
 await ordinary.req('admin/server-discovery','GET',undefined,403);await ordinary.req('admin/server-discovery','POST',{enabled:true,address:'https://10.0.0.1:7443/'},403);
 assert.equal((await fetch(base+'/api/admin/server-discovery')).status,401);
 const state=await admin.req('admin/server-discovery');assert.equal(state.settings.enabled,false);
 for(const address of ['http://10.0.0.1:7443/','https://localhost:7443/','https://127.0.0.1/','https://name:password@10.0.0.1/','https://10.0.0.1/private','https://10.0.0.1/?secret=x'])await admin.req('admin/server-discovery','POST',{enabled:true,address},400);
 const identity=await(await fetch(base+'/server-info')).json();assert.equal(identity.product,product);assert.deepEqual(Object.keys(identity).sort(),['product','schoolName']);
 check('Discovery settings are administrator-only, HTTPS-only, reject localhost/credentials/paths and publish no account or message data');
 const target='https://10.0.0.1:7443/';await admin.req('admin/server-discovery','POST',{enabled:true,address:target});
 for(let n=0;n<30&&!(await admin.req('admin/server-discovery')).listening;n++)await delay(100);
 assert.equal((await admin.req('admin/server-discovery')).listening,true);
 async function query(packet){const udp=dgram.createSocket('udp4');await new Promise(r=>udp.bind(0,'127.0.0.1',r));try{return await new Promise((resolve,reject)=>{const timeout=setTimeout(()=>resolve(null),450);udp.once('message',bytes=>{clearTimeout(timeout);resolve(bytes)});udp.once('error',error=>{clearTimeout(timeout);reject(error)});udp.send(Buffer.isBuffer(packet)?packet:Buffer.from(JSON.stringify(packet)),state.port,'127.0.0.1');});}finally{udp.close();}}
 const nonce=randomBytes(16).toString('hex');const reply=await query({product,nonce});assert.ok(reply);assert.ok(reply.length<=512);assert.deepEqual(JSON.parse(reply),{product,nonce,address:target});
 assert.equal(await query({product,nonce:randomBytes(16).toString('hex')}),null);
 await delay(1100);assert.equal(await query({product:'wrong',nonce}),null);assert.equal(await query({product,nonce:'not-a-nonce'}),null);assert.equal(await query(Buffer.alloc(600,65)),null);
 const last=await query({product,nonce:randomBytes(16).toString('hex')});assert.ok(last);
 check('Loopback-only fixture verifies nonce echoes, bounded UDP responses, malformed-packet rejection and per-source rate limiting');
 await admin.req('admin/server-discovery','POST',{enabled:false,address:target});
 for(let n=0;n<30&&(await admin.req('admin/server-discovery')).listening;n++)await delay(100);
 assert.equal((await admin.req('admin/server-discovery')).listening,false);assert.equal(await query({product,nonce}),null);
 check('Administrator can stop discovery without stopping the messenger API or changing firewall settings');
}
