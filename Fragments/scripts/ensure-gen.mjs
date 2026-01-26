#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const root = path.resolve(path.join(path.dirname(new URL(import.meta.url).pathname).replace(/^\//, ''), '..'));
const tsGenRoot = path.join(root, 'ts-gen');

function hasGeneratedFiles(dir) {
  if (!fs.existsSync(dir)) return false;
  let found = false;
  const walk = (d) => {
    if (found) return;
    for (const ent of fs.readdirSync(d, { withFileTypes: true })) {
      const full = path.join(d, ent.name);
      if (ent.isDirectory()) walk(full);
      else if (ent.isFile() && /_pb\.ts$/.test(ent.name)) { found = true; return; }
    }
  };
  try { walk(dir); } catch {}
  return found;
}

if (hasGeneratedFiles(tsGenRoot)) {
  console.log('[ensure-gen] Detected existing generated files — skipping codegen');
  process.exit(0);
}

console.log('[ensure-gen] ts-gen appears empty — running generate-ts.mjs');
const res = spawnSync(process.execPath, [path.join(root, 'generate-ts.mjs')], {
  cwd: root,
  stdio: 'inherit',
  shell: false,
});
if (res.status !== 0) {
  console.error('[ensure-gen] Code generation failed with status', res.status);
  process.exit(res.status || 1);
}
console.log('[ensure-gen] Code generation complete');

