#!/usr/bin/env node
import { promises as fs } from 'node:fs';
import path from 'node:path';

async function main() {
  const [,, level = '', ...rest] = process.argv;
  const valid = new Set(['patch', 'minor', 'major']);
  if (!valid.has(level)) {
    console.error('Usage: node scripts/make-changeset.mjs <patch|minor|major> [message...]');
    process.exit(1);
  }
  const msg = (rest.join(' ').trim()) || `Automated ${level} bump`;

  const root = path.resolve(path.join(path.dirname(new URL(import.meta.url).pathname).replace(/^\//, ''), '..'));
  const pkgPath = path.join(root, 'package.json');
  const pkg = JSON.parse(await fs.readFile(pkgPath, 'utf8'));
  const pkgName = pkg.name;
  const csDir = path.join(root, '.changeset');
  await fs.mkdir(csDir, { recursive: true });

  const id = `${new Date().toISOString().replace(/[:.]/g,'-')}-${level}`;
  const file = path.join(csDir, `${id}.md`);
  const body = `---\n"${pkgName}": ${level}\n---\n\n${msg}\n`;
  await fs.writeFile(file, body, 'utf8');
  console.log(`Created changeset: ${path.relative(root, file)}`);
}

main().catch((e) => { console.error(e); process.exit(1); });
