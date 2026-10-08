import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const script = path.resolve('Docs/CommanderFinalization/GrandFix/refresh-grand-fix-manifest.ps1');
const fixtures = [];
function run(cmd, args, cwd) {
  const result = spawnSync(cmd, args, { cwd, encoding: 'utf8', windowsHide: true });
  assert.ifError(result.error);
  return result;
}
function fixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'OpenEmpires-GrandFix-manifest-'));
  fixtures.push(root);
  const files = {
    'Game/Assets/Scripts/Commander.cs': 'public class Commander {}\n',
    'Game/Assets/Input/Commands.inputactions': '{"maps":[]}\n',
    'Game/Assets/Input/Commands.inputactions.meta': 'guid: 0123456789abcdef\n',
    'Game/Assets/Plugins/WebGL/Speech.jslib': 'mergeInto(LibraryManager.library, {});\n',
    'Game/Packages/manifest.json': '{"dependencies":{}}\n',
    'Game/ProjectSettings/ProjectVersion.txt': 'm_EditorVersion: 6000.5.9f1\n',
    'backend/src/main.rs': 'fn main() {}\n',
    'backend/Cargo.toml': '[package]\nname="fixture"\nversion="0.1.0"\n',
    'backend/speech-gateway/.env.example': 'COMMANDER_GATEWAY_ENABLED=false\nCOMMANDER_OPENAI_API_KEY=\n',
    'backend/speech-gateway/private-state/budget.json': '{"privateRuntimeState":true}\n',
    'Game/.env': 'TEST_SECRET_DO_NOT_DISCLOSE=fixture-only\n'
  };
  for (const [name, body] of Object.entries(files)) {
    const file = path.join(root, name); fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, body);
  }
  for (const args of [['init','--quiet'], ['add','.'], ['-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','--quiet','-m','fixture']]) {
    const result = run('git', args, root); assert.equal(result.status, 0, result.stderr);
  }
  return root;
}
function generate(root, verify = false) {
  return run('powershell', ['-NoProfile','-ExecutionPolicy','Bypass','-File',script,
    '-RepositoryRoot',root,'-UnityRelativePath','Game','-OutputPath','Game/manifest.json',
    ...(verify ? ['-VerifyOnly'] : [])], root);
}
function manifest(root) { return JSON.parse(fs.readFileSync(path.join(root, 'Game/manifest.json'), 'utf8')); }

test('clean checkout inventory includes committed source, input/meta, Web bridge and backend', () => {
  const root = fixture(); const result = generate(root); assert.equal(result.status, 0, result.stderr);
  const m = manifest(root); const paths = m.records.map(r => r.path);
  for (const expected of ['Game/Assets/Scripts/Commander.cs','Game/Assets/Input/Commands.inputactions',
    'Game/Assets/Input/Commands.inputactions.meta','Game/Assets/Plugins/WebGL/Speech.jslib','backend/src/main.rs'])
    assert.ok(paths.includes(expected), expected);
  assert.ok(!paths.includes('Game/.env')); assert.ok(!paths.includes('Game/manifest.json'));
  assert.equal(m.changes.length, 0); assert.ok(m.records.length >= 8);
  assert.equal(generate(root, true).status, 0);
});

test('staged, unstaged, untracked and deleted paths remain distinct and complete', () => {
  const root = fixture();
  fs.appendFileSync(path.join(root, 'Game/Assets/Scripts/Commander.cs'), '// staged\n');
  assert.equal(run('git', ['add','Game/Assets/Scripts/Commander.cs'], root).status, 0);
  fs.appendFileSync(path.join(root, 'Game/Assets/Scripts/Commander.cs'), '// unstaged\n');
  fs.unlinkSync(path.join(root, 'Game/Assets/Input/Commands.inputactions'));
  fs.writeFileSync(path.join(root, 'Game/Assets/Plugins/WebGL/worker.mjs'), 'export const fixture = true;\n');
  const result = generate(root); assert.equal(result.status, 0, result.stderr);
  const m = manifest(root);
  assert.ok(m.changes.some(c => c.path === 'Game/Assets/Scripts/Commander.cs' && c.index === 'M' && c.worktree === 'M'));
  assert.ok(m.changes.some(c => c.path.endsWith('Commands.inputactions') && c.worktree === 'D'));
  assert.ok(m.changes.some(c => c.path.endsWith('worker.mjs') && c.index === '?' && c.worktree === '?'));
  const deleted = m.records.find(r => r.path.endsWith('Commands.inputactions'));
  assert.equal(deleted.exists, false); assert.equal(deleted.sha256, null);
  assert.equal(generate(root, true).status, 0);
});

test('verify rejects changed bytes and newly untracked relevant source, without regenerating snapshot', () => {
  const root = fixture(); assert.equal(generate(root).status, 0);
  const snapshot = fs.readFileSync(path.join(root, 'Game/manifest.json'), 'utf8');
  fs.appendFileSync(path.join(root, 'backend/src/main.rs'), '// drift\n');
  assert.notEqual(generate(root, true).status, 0);
  assert.equal(fs.readFileSync(path.join(root, 'Game/manifest.json'), 'utf8'), snapshot);
  assert.equal(generate(root).status, 0);
  fs.writeFileSync(path.join(root, 'Game/Assets/Plugins/WebGL/new-worker.mjs'), '// new source\n');
  assert.notEqual(generate(root, true).status, 0);
});

test('secret exclusion and recursive self-hash exclusion persist when output already exists', () => {
  const root = fixture(); assert.equal(generate(root).status, 0);
  assert.equal(generate(root).status, 0);
  const serialized = fs.readFileSync(path.join(root, 'Game/manifest.json'), 'utf8');
  assert.ok(!serialized.includes('fixture-only')); assert.ok(!serialized.includes('Game/.env'));
  assert.ok(!manifest(root).records.some(r => r.path === 'Game/manifest.json'));
  assert.equal(generate(root, true).status, 0);
});

test('explicit secret-free gateway template is hashed but runtime private state is never inventoried', () => {
  const root=fixture();assert.equal(generate(root).status,0);
  const records=manifest(root).records;
  assert.ok(records.some(r=>r.path==='backend/speech-gateway/.env.example'&&r.sha256));
  assert.ok(!records.some(r=>r.path.includes('/private-state/')));
  assert.equal(generate(root,true).status,0);
});

process.on('exit', () => {
  // Only generated fixture roots are touched; no worktree cleanup or shell deletion.
  for (const root of fixtures) {
    const resolved = path.resolve(root), temp = path.resolve(os.tmpdir());
    if (path.dirname(resolved) === temp && path.basename(resolved).startsWith('OpenEmpires-GrandFix-manifest-'))
      fs.rmSync(resolved, { recursive: true, force: true });
  }
});
