import { test, describe } from 'node:test';
import { strict as assert } from 'node:assert';
import fs from 'fs';

describe('Export Path Tests', () => {
  test('should verify client export path files exist', () => {
    // Verify the files that the export paths point to exist
    const clientJsFile = 'dist/esm/client.js';
    const clientDtsFile = 'dist/protos/client.d.ts';
    
    assert.ok(fs.existsSync(clientJsFile), 'Client JS file should exist at export path');
    assert.ok(fs.existsSync(clientDtsFile), 'Client type definition file should exist at export path');
    
    console.log('✓ Client export path files exist');
  });

  test('should verify client JS file has proper exports', () => {
    const clientJsFile = 'dist/esm/client.js';
    const content = fs.readFileSync(clientJsFile, 'utf8');
    
    // Check for key exports in the compiled JS
    assert.ok(content.includes('export class FragmentsClient'), 'FragmentsClient class should be exported');
    
    console.log('✓ Client JS file has proper exports');
  });

  test('should verify validation export exists', () => {
    // Verify validation is also properly exported
    const validationJsFile = 'dist/esm/validation.js';
    const validationDtsFile = 'dist/protos/validation.d.ts';
    
    assert.ok(fs.existsSync(validationJsFile), 'Validation JS file should exist');
    assert.ok(fs.existsSync(validationDtsFile), 'Validation type definition file should exist');
    
    // Check package.json has validation export
    const packageJson = JSON.parse(fs.readFileSync('package.json', 'utf8'));
    assert.ok(packageJson.exports['./validation'], 'Validation export should exist in package.json');
    
    console.log('✓ Validation export exists and is properly configured');
  });

  test('should verify all major protobuf module exports exist', () => {
    const modules = ['Settings', 'Authentication', 'Content', 'Authorization'];
    
    for (const module of modules) {
      const jsFile = `dist/esm/${module}/index.js`;
      const dtsFile = `dist/protos/${module}/index.d.ts`;
      
      assert.ok(fs.existsSync(jsFile), `${module} JS index should exist`);
      assert.ok(fs.existsSync(dtsFile), `${module} type definition index should exist`);
      
      // Check package.json has export for this module
      const packageJson = JSON.parse(fs.readFileSync('package.json', 'utf8'));
      assert.ok(packageJson.exports[`./${module}`], `${module} export should exist in package.json`);
    }
    
    console.log('✓ All major protobuf module exports exist');
  });

  test('should verify main index files exist and export client', () => {
    const mainJsFile = 'dist/esm/index.js';
    const mainDtsFile = 'dist/protos/index.d.ts';
    
    assert.ok(fs.existsSync(mainJsFile), 'Main JS index should exist');
    assert.ok(fs.existsSync(mainDtsFile), 'Main type definition index should exist');
    
    // Check that main index exports client
    const jsContent = fs.readFileSync(mainJsFile, 'utf8');
    const dtsContent = fs.readFileSync(mainDtsFile, 'utf8');
    
    assert.ok(jsContent.includes("export * from './client';"), 'Main JS index should export client');
    assert.ok(dtsContent.includes("export * from './client';"), 'Main type definition index should export client');
    
    console.log('✓ Main index files exist and export client');
  });
});