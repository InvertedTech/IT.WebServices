#!/usr/bin/env node
import { promises as fs } from 'node:fs';
import path from 'node:path';

const rawDir = path.dirname(new URL(import.meta.url).pathname);
const dir = process.platform === 'win32' && rawDir.startsWith('/') ? rawDir.slice(1) : rawDir;
const root = path.resolve(path.join(dir, '..'));
const esmDir = path.join(root, 'dist', 'esm');
// No CommonJS output in ESM-only package

async function ensure(dir) {
  await fs.mkdir(dir, { recursive: true });
}

async function writeJSON(file, obj) {
  await fs.writeFile(file, JSON.stringify(obj, null, 2), 'utf8');
}

await ensure(esmDir);

await writeJSON(path.join(esmDir, 'package.json'), { type: 'module' });
console.log('Wrote module-type package.json to dist/esm');