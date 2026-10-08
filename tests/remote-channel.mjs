import assert from 'node:assert/strict';
import { RemoteChannel } from './features.mjs';
let channel;
const client = { async request(path, method, body) {
  const message = JSON.parse(body.replace('\u001e', ''));
  queueMicrotask(() => channel.parse(JSON.stringify({ type: 3, invocationId: message.invocationId, error: 'Method does not exist.' }) + '\u001e'));
  await new Promise(resolve => setTimeout(resolve, 25));
} };
channel = new RemoteChannel(client); channel.path = '/test';
await assert.rejects(channel.invoke('Frame'), /Method does not exist/);
assert.equal(channel.pending.size, 0);
console.log('PASS early hub rejection remains observable without an unhandled promise rejection');
