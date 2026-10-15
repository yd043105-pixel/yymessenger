import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import vm from 'node:vm';

const manifest = JSON.parse(readFileSync('src/SchoolMessenger.Shared/updates.json', 'utf8'));
const script = readFileSync('src/SchoolMessenger.Shared/update-notice.js', 'utf8');
const passed = [];
function check(name) { passed.push(name); console.log('PASS ' + name); }
class Element {
  constructor(tag) { this.tag = tag; this.children = []; this.open = false; }
  append(...children) { for (const child of children) { child.isConnected = true; this.children.push(child); } }
  replaceChildren(...children) { this.children = []; this.append(...children); }
  setAttribute() {}
  showModal() { this.open = true; }
  close() { this.open = false; }
  focus() {}
}
function fixture({ notes = manifest, stored = 0, storageBlocked = false, offline = false, deferred = false } = {}) {
  const document = { body: new Element('body'), createElement: tag => new Element(tag) };
  let resolve, calls = 0, alerts = 0;
  const window = { alert() { alerts++; } };
  const localStorage = {
    getItem() { if (storageBlocked) throw Error('blocked'); return String(stored); },
    setItem(key, value) { if (storageBlocked) throw Error('blocked'); stored = Number(value); }
  };
  const fetch = () => {
    calls++;
    if (offline) return Promise.reject(Error('offline'));
    const response = { ok: true, json: async () => notes };
    return deferred ? new Promise(r => { resolve = () => r(response); }) : Promise.resolve(response);
  };
  vm.runInNewContext(script, { window, document, localStorage, fetch, AbortSignal });
  const nodes = () => document.body.children.flatMap(function visit(node) { return [node, ...node.children.flatMap(visit)]; });
  return { api: window.UpdateNotice, get stored() { return stored; }, get calls() { return calls; }, get alerts() { return alerts; },
    get dialog() { return document.body.children.find(x => x.tag === 'dialog'); },
    click(text) { nodes().find(n => n.tag === 'button' && n.textContent === text).onclick(); }, nodes, finish() { resolve(); } };
}
const fresh = fixture(); await fresh.api.show('office'); assert.ok(fresh.dialog.open); assert.equal(fresh.stored, 0);
fresh.click('나중에'); await fresh.api.show('office'); assert.ok(!fresh.dialog.open); assert.equal(fresh.stored, 0);
fresh.api.reset(); await fresh.api.show('office'); assert.ok(fresh.dialog.open); fresh.click('확인'); assert.equal(fresh.stored, manifest.sequence);
fresh.api.reset(); await fresh.api.show('office'); assert.ok(!fresh.dialog.open);
check('first login shows bundled notes; defer never acknowledges; confirmation survives a new login');
await fresh.api.show('office', true); assert.ok(fresh.dialog.open); fresh.click('확인');
check('manual history opens an already confirmed release');
for (const stored of [manifest.sequence, manifest.sequence + 1]) { const f = fixture({ stored }); await f.api.show('portal'); assert.equal(f.dialog, undefined); }
const upgraded = fixture({ stored: manifest.sequence - 1 }); await upgraded.api.show('portal'); assert.ok(upgraded.dialog.open);
check('newer releases show; same release and downgrades stay quiet');
const simultaneous = fixture({ deferred: true }); const a = simultaneous.api.show('office'), b = simultaneous.api.show('office');
simultaneous.finish(); await Promise.all([a, b]); assert.equal(simultaneous.calls, 1); assert.ok(simultaneous.dialog.open);
simultaneous.api.reset(); assert.ok(!simultaneous.dialog.open);
check('concurrent refreshes show one popup and logout closes it');
const stale = fixture({ deferred: true }); const request = stale.api.show('portal'); stale.api.reset(); stale.finish(); await request;
assert.equal(stale.dialog, undefined); check('a release fetch completing after logout cannot open a popup');
const blocked = fixture({ storageBlocked: true }); await blocked.api.show('portal'); blocked.click('확인'); assert.ok(!blocked.dialog.open);
const offline = fixture({ offline: true }); await offline.api.show('portal'); assert.equal(offline.alerts, 0);
await offline.api.show('portal', true); assert.equal(offline.alerts, 1);
check('blocked browser storage and unavailable release notes do not block school work');
const unsafe = structuredClone(manifest); unsafe.audiences.office['새 기능'] = ['<img src=x onerror=alert(1)>'];
const literal = fixture({ notes: unsafe }); await literal.api.show('office');
assert.ok(literal.nodes().some(n => n.tag === 'li' && n.textContent === unsafe.audiences.office['새 기능'][0]));
assert.ok(!literal.nodes().some(n => n.tag === 'img')); check('release text renders as text, never as HTML');
for (const audience of ['desktop', 'mobile', 'office', 'portal']) assert.ok(Object.values(manifest.audiences[audience]).flat().length);
for (const file of ['src/SchoolMessenger.Server/wwwroot/index.html', 'src/SchoolMessenger.Server/wwwroot/office/index.html', 'src/SchoolMessenger.AnnouncementServer/wwwroot/index.html']) {
  const html = readFileSync(file, 'utf8'); assert.ok(html.includes('id="updates"'));
  assert.ok(html.indexOf('src="/update-notice.js"') < html.indexOf('src="' + (file.includes('AnnouncementServer') ? 'portal.js' : file.includes('/office/') ? 'office.js' : 'admin.js') + '"'));
}
check('every shipped interface has release notes and loads the popup script before its login script');
mkdirSync('artifacts', { recursive: true });
writeFileSync('artifacts/update-notice-test-results.json', JSON.stringify({ at: new Date().toISOString(), passed }, null, 2));
console.log(passed.length + ' update notice checks passed.');
