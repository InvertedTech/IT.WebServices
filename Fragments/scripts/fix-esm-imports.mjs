#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(path.join(path.dirname(new URL(import.meta.url).pathname).replace(/^\//, ''), '..'));
const esmRoot = path.join(root, 'dist', 'esm');

function fixImports(file) {
  if (!fs.existsSync(file)) return;
  
  let content = fs.readFileSync(file, 'utf8');
  let modified = false;

  // Fix relative imports that don't have .js extension
  content = content.replace(/from\s+['"](\.[^'"]*?)['"];?/g, (match, importPath) => {
    if (!importPath.endsWith('.js') && !importPath.endsWith('/')) {
      // Check if this is a directory import
      const fullPath = path.resolve(path.dirname(file), importPath);
      if (fs.existsSync(fullPath) && fs.statSync(fullPath).isDirectory()) {
        // Directory import - add /index.js
        modified = true;
        return match.replace(importPath, importPath + '/index.js');
      } else {
        // File import - add .js
        modified = true;
        return match.replace(importPath, importPath + '.js');
      }
    }
    return match;
  });

  // Fix export * from imports
  content = content.replace(/export\s+\*\s+from\s+['"](\.[^'"]*?)['"];?/g, (match, importPath) => {
    if (!importPath.endsWith('.js') && !importPath.endsWith('/')) {
      // Check if this is a directory import
      const fullPath = path.resolve(path.dirname(file), importPath);
      if (fs.existsSync(fullPath) && fs.statSync(fullPath).isDirectory()) {
        // Directory import - add /index.js
        modified = true;
        return match.replace(importPath, importPath + '/index.js');
      } else {
        // File import - add .js
        modified = true;
        return match.replace(importPath, importPath + '.js');
      }
    }
    return match;
  });

  // Fix export * as imports
  content = content.replace(/export\s+\*\s+as\s+\w+\s+from\s+['"](\.[^'"]*?)['"];?/g, (match, importPath) => {
    if (!importPath.endsWith('.js') && !importPath.endsWith('/')) {
      // Check if this is a directory import
      const fullPath = path.resolve(path.dirname(file), importPath);
      if (fs.existsSync(fullPath) && fs.statSync(fullPath).isDirectory()) {
        // Directory import - add /index.js
        modified = true;
        return match.replace(importPath, importPath + '/index.js');
      } else {
        // File import - add .js
        modified = true;
        return match.replace(importPath, importPath + '.js');
      }
    }
    return match;
  });

  if (modified) {
    fs.writeFileSync(file, content, 'utf8');
    console.log(`Fixed imports in: ${path.relative(root, file)}`);
  }
}

function walk(dir) {
  if (!fs.existsSync(dir)) return;
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, ent.name);
    if (ent.isDirectory()) {
      walk(full);
    } else if (ent.isFile() && ent.name.endsWith('.js')) {
      fixImports(full);
    }
  }
}

console.log('Fixing ESM imports...');
walk(esmRoot);
console.log('ESM import fixing complete');