#!/usr/bin/env node
import { promises as fs } from 'node:fs';
import path from 'node:path';

const rawDir = path.dirname(new URL(import.meta.url).pathname);
const dir = process.platform === 'win32' && rawDir.startsWith('/') ? rawDir.slice(1) : rawDir;
const root = path.resolve(path.join(dir, '..'));

const repoReadme = path.join(root, 'README.md');
const backup = path.join(root, '.README.repo.bak');

async function main() {
  try {
    // Restore original README if backup exists
    try {
      await fs.copyFile(backup, repoReadme);
      await fs.rm(backup, { force: true });
      console.log('Restored repository README.md');
    } catch {}
  } catch (e) {
    console.error('postpack-readme failed:', e);
    process.exit(1);
  }
}

main();
