#!/usr/bin/env node
import { promises as fs } from 'node:fs';
import path from 'node:path';

const rawDir = path.dirname(new URL(import.meta.url).pathname);
const dir = process.platform === 'win32' && rawDir.startsWith('/') ? rawDir.slice(1) : rawDir;
const root = path.resolve(path.join(dir, '..'));

const repoReadme = path.join(root, 'README.md');
const pkgReadme = path.join(root, 'README.PACKAGE.md');
const backup = path.join(root, '.README.repo.bak');

async function main() {
  try {
    // Backup existing README.md
    try {
      await fs.copyFile(repoReadme, backup);
      console.log('Backed up README.md -> .README.repo.bak');
    } catch {}

    // Replace with package-focused README if present
    await fs.copyFile(pkgReadme, repoReadme);
    console.log('Swapped README.md with README.PACKAGE.md for packing');
  } catch (e) {
    console.error('prepack-readme failed:', e);
    process.exit(1);
  }
}

main();