#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(path.join(path.dirname(new URL(import.meta.url).pathname).replace(/^\//, ''), '..'));
const tsGenRoot = path.join(root, 'ts-gen');

function ensureModuleSyntax(file) {
  const src = fs.readFileSync(file, 'utf8');
  if (!/export\s+\*/.test(src) && !/export\s+\{/.test(src)) {
    fs.appendFileSync(file, '\nexport {};\n');
  }
}

function walk(dir) {
  if (!fs.existsSync(dir)) return;
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, ent.name);
    if (ent.isDirectory()) walk(full);
    else if (ent.isFile() && ent.name === 'index.ts') ensureModuleSyntax(full);
  }
}

walk(tsGenRoot);
console.log('fix-empty-indexes: ensured module syntax in index files');

