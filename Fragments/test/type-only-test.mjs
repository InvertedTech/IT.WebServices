import { test, describe } from 'node:test';
import { strict as assert } from 'node:assert';
import fs from 'fs';

describe('Type-Only Accessibility Tests', () => {
  test('should verify client type definitions are accessible', () => {
    // Check that client type definition file exists and has proper exports
    const clientTypeFile = 'dist/protos/client.d.ts';
    assert.ok(fs.existsSync(clientTypeFile), 'Client type definition file should exist');
    
    const content = fs.readFileSync(clientTypeFile, 'utf8');
    
    // Verify key type exports
    assert.ok(content.includes('export interface ClientConfig'), 'ClientConfig interface should be exported');
    assert.ok(content.includes('export type HttpMethod'), 'HttpMethod type should be exported');
    assert.ok(content.includes('export type TokenGetter'), 'TokenGetter type should be exported');
    assert.ok(content.includes('export type CacheInvalidator'), 'CacheInvalidator type should be exported');
    assert.ok(content.includes('export interface RequestOptions'), 'RequestOptions interface should be exported');
    assert.ok(content.includes('export interface SimpleValidationResult'), 'SimpleValidationResult interface should be exported');
    assert.ok(content.includes('export declare class FragmentsClient'), 'FragmentsClient class should be exported');
    
    console.log('✓ All client type definitions are properly exported');
  });

  test('should verify package.json exports are correctly configured', () => {
    const packageJson = JSON.parse(fs.readFileSync('package.json', 'utf8'));
    
    // Verify client export exists
    assert.ok(packageJson.exports['./client'], 'Client export should exist in package.json');
    assert.ok(packageJson.exports['./client'].types, 'Client export should have types field');
    assert.ok(packageJson.exports['./client'].import, 'Client export should have import field');
    
    // Verify the paths are correct
    assert.strictEqual(packageJson.exports['./client'].types, './dist/protos/client.d.ts');
    assert.strictEqual(packageJson.exports['./client'].import, './dist/esm/client.js');
    
    console.log('✓ Package.json exports are correctly configured');
  });

  test('should verify main index exports include client', () => {
    const indexTypeFile = 'dist/protos/index.d.ts';
    assert.ok(fs.existsSync(indexTypeFile), 'Main index type definition file should exist');
    
    const content = fs.readFileSync(indexTypeFile, 'utf8');
    assert.ok(content.includes("export * from './client';"), 'Main index should export client');
    
    console.log('✓ Main index exports include client');
  });

  test('should verify protobuf message types are accessible', () => {
    // Check Settings types
    const settingsTypeFile = 'dist/protos/Settings/index.d.ts';
    assert.ok(fs.existsSync(settingsTypeFile), 'Settings type definitions should exist');
    
    // Check Authentication types  
    const authTypeFile = 'dist/protos/Authentication/index.d.ts';
    assert.ok(fs.existsSync(authTypeFile), 'Authentication type definitions should exist');
    
    // Check Content types
    const contentTypeFile = 'dist/protos/Content/index.d.ts';
    assert.ok(fs.existsSync(contentTypeFile), 'Content type definitions should exist');
    
    console.log('✓ Protobuf message type definitions are accessible');
  });

  test('should verify specific protobuf schema types exist', () => {
    // Check a specific Settings schema file
    const settingsRecordFile = 'dist/protos/Settings/SettingsRecord_pb.d.ts';
    if (fs.existsSync(settingsRecordFile)) {
      const content = fs.readFileSync(settingsRecordFile, 'utf8');
      assert.ok(content.includes('export type SettingsRecord'), 'SettingsRecord type should be exported');
      assert.ok(content.includes('export declare const SettingsRecordSchema'), 'SettingsRecordSchema should be exported');
      console.log('✓ Specific protobuf schema types exist and are properly exported');
    } else {
      console.log('⚠ SettingsRecord_pb.d.ts not found, skipping specific schema test');
    }
  });

  test('should verify client static method signatures in types', () => {
    const clientTypeFile = 'dist/protos/client.d.ts';
    const content = fs.readFileSync(clientTypeFile, 'utf8');
    
    // Check for static method declarations
    assert.ok(content.includes('static createRequest'), 'createRequest static method should be declared');
    assert.ok(content.includes('static createResponse'), 'createResponse static method should be declared');
    assert.ok(content.includes('static serialize'), 'serialize static method should be declared');
    assert.ok(content.includes('static validate'), 'validate static method should be declared');
    
    console.log('✓ Client static method signatures are properly declared in types');
  });

  test('should verify client instance method signatures in types', () => {
    const clientTypeFile = 'dist/protos/client.d.ts';
    const content = fs.readFileSync(clientTypeFile, 'utf8');
    
    // Check for instance method declarations
    assert.ok(content.includes('request<'), 'request method should be declared with generics');
    assert.ok(content.includes('get<'), 'get method should be declared with generics');
    assert.ok(content.includes('post<'), 'post method should be declared with generics');
    assert.ok(content.includes('withConfig'), 'withConfig method should be declared');
    
    console.log('✓ Client instance method signatures are properly declared in types');
  });
});