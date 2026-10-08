import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, mkdirSync, copyFileSync } from 'node:fs';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

assert.equal(process.platform, 'darwin', 'Run iOS verification on macOS with Xcode');
const root = process.cwd(), output = path.join(root, 'artifacts/ios');
mkdirSync(output, { recursive: true });
const architecture = process.arch === 'arm64' ? 'arm64' : 'x64';
const bundle = path.join(root, `src/SchoolMessenger.Mobile/bin/Debug/net10.0-ios/iossimulator-${architecture}/SchoolMessenger.Mobile.app`);
const id = 'kr.school.yeoyang.messenger', delay = ms => new Promise(resolve => setTimeout(resolve, ms));
function command(args, env = process.env) {
  const result = spawnSync('xcrun', args, { encoding: 'utf8', env, timeout: 180000 });
  assert.equal(result.status, 0, result.stderr || 'xcrun failed'); return result.stdout.trim();
}
const runtimes = JSON.parse(command(['simctl', 'list', 'runtimes', '--json'])).runtimes.filter(r => r.isAvailable && r.identifier.includes('.iOS-'));
const runtime = runtimes.sort((a, b) => b.version.localeCompare(a.version, undefined, { numeric: true }))[0];
assert.ok(runtime, 'An installed iOS simulator runtime is required');
const types = JSON.parse(command(['simctl', 'list', 'devicetypes', '--json'])).devicetypes;
const type = types.find(t => t.name === 'iPhone 16') ?? types.find(t => t.name.startsWith('iPhone'));
const device = command(['simctl', 'create', 'yymessenger-' + randomUUID(), type.identifier, runtime.identifier]);
let serverLog = '', fixture;
try {
  command(['simctl', 'boot', device]); command(['simctl', 'bootstatus', device, '-b']);
  fixture = spawn(process.execPath, ['tests/portal-preview.mjs'], { cwd: root });
  fixture.stdout.on('data', data => { serverLog += data; }); fixture.stderr.on('data', data => { serverLog += data; });
  for (let attempt = 0; attempt < 120 && !serverLog.includes('timetable fixture ready.'); attempt++) { assert.equal(fixture.exitCode, null, 'Synthetic servers failed'); await delay(500); }
  assert.ok(serverLog.includes('timetable fixture ready.'), 'Synthetic fixture timed out');
  command(['simctl', 'install', device, bundle]);
  const container = command(['simctl', 'get_app_container', device, id, 'data']);
  const documents = path.join(container, 'Documents'); mkdirSync(documents, { recursive: true });
  copyFileSync('.test-data/portal-preview/access.json', path.join(documents, 'ios-test-fixture.json'));
  const verified = [];
  for (const phase of ['suite', 'restore']) {
    if (phase === 'restore') command(['simctl', 'terminate', device, id]);
    command(['simctl', 'launch', device, id], { ...process.env, SIMCTL_CHILD_YY_IOS_VERIFY: phase });
    const report = path.join(documents, 'ios-' + phase + '-results.json');
    for (let attempt = 0; attempt < 180 && !existsSync(report); attempt++) await delay(500);
    assert.ok(existsSync(report), 'iOS runtime report missing: ' + phase);
    const result = JSON.parse(readFileSync(report, 'utf8'));
    copyFileSync(report,path.join(output,'ios-'+phase+'-results.json'));
    command(['simctl', 'io', device, 'screenshot', path.join(output, 'ios-' + phase + '-preview.png')]);
    for (const check of result.passed) console.log('PASS '+check);
    assert.ok(result.ok, result.error ?? 'iOS runtime check failed');
    verified.push(...result.passed);
  }
  writeFileSync(path.join(output, 'ios-test-results.json'), JSON.stringify({ verifiedAt: new Date().toISOString(), simulator: type.name, runtime: runtime.version, passed: verified }, null, 2));
  console.log(verified.length + ' iOS runtime checks passed.');
} finally {
  fixture?.kill();
  command(['simctl', 'shutdown', device]); command(['simctl', 'delete', device]);
}
