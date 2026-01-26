#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(path.join(path.dirname(new URL(import.meta.url).pathname).replace(/^\//, ''), '..'));
const esmRoot = path.join(root, 'dist', 'esm');

const filesToFix = [
  'index.js',
  'Authorization/Events/index.js',
  'Authorization/Payment/index.js',
  'Authorization/Payment/Crypto/index.js',
  'Authorization/Payment/Fortis/index.js',
  'Authorization/Payment/Manual/index.js',
  'Authorization/Payment/Paypal/index.js',
  'Authorization/Payment/Stripe/index.js',
  'Comment/index.js',
  'Content/Music/index.js',
  'Content/Stats/index.js',
  'CreatorDashboard/index.js',
  'CreatorDashboard/Settings/index.js',
  'CreatorDashboard/Subscribers/index.js',
  'Generic/index.js',
  'Notification/index.js',
  'Page/index.js'
];

function fixDirectoryImports(filePath) {
  const fullPath = path.join(esmRoot, filePath);
  if (!fs.existsSync(fullPath)) return;
  
  let content = fs.readFileSync(fullPath, 'utf8');
  let modified = false;

  // Fix export * as Name from './Name.js' to export * as Name from './Name/index.js'
  content = content.replace(/export\s+\*\s+as\s+(\w+)\s+from\s+['"]\.\/(\w+)\.js['"];?/g, (match, name, dir) => {
    const dirPath = path.join(path.dirname(fullPath), dir);
    if (fs.existsSync(dirPath) && fs.statSync(dirPath).isDirectory()) {
      modified = true;
      return `export * as ${name} from './${dir}/index.js';`;
    }
    return match;
  });

  // Fix export * as connect from './connect.js' to export * as connect from './connect/index.js'
  content = content.replace(/export\s+\*\s+as\s+connect\s+from\s+['"]\.\/connect\.js['"];?/g, (match) => {
    const connectPath = path.join(path.dirname(fullPath), 'connect');
    if (fs.existsSync(connectPath) && fs.statSync(connectPath).isDirectory()) {
      modified = true;
      return "export * as connect from './connect/index.js';";
    }
    return match;
  });

  if (modified) {
    fs.writeFileSync(fullPath, content, 'utf8');
    console.log(`Fixed directory imports in: ${filePath}`);
  }
}

console.log('Fixing directory imports...');
filesToFix.forEach(fixDirectoryImports);
console.log('Directory import fixing complete');