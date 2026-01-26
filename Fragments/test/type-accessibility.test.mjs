import { test, describe } from 'node:test';
import { strict as assert } from 'node:assert';

describe('TypeScript Type Accessibility Tests', () => {
  test('should be able to import client and protobuf types', async () => {
    try {
      // Test importing the client
      const { FragmentsClient } = await import('../dist/esm/client.js');
      assert.ok(FragmentsClient, 'FragmentsClient should be importable');
      
      // Test importing protobuf schemas and types
      const Settings = await import('../dist/esm/Settings/index.js');
      assert.ok(Settings.SettingsRecordSchema, 'SettingsRecordSchema should be importable');
      
      const Authentication = await import('../dist/esm/Authentication/index.js');
      assert.ok(Authentication, 'Authentication module should be importable');
      
      const Content = await import('../dist/esm/Content/index.js');
      assert.ok(Content, 'Content module should be importable');
      
      console.log('✓ All imports successful');
    } catch (error) {
      console.error('Import failed:', error.message);
      throw error;
    }
  });

  test('should verify client static methods are accessible', async () => {
    const { FragmentsClient } = await import('../dist/esm/client.js');
    
    // Verify static methods exist
    assert.ok(typeof FragmentsClient.createRequest === 'function', 'createRequest should be a static method');
    assert.ok(typeof FragmentsClient.createResponse === 'function', 'createResponse should be a static method');
    assert.ok(typeof FragmentsClient.serialize === 'function', 'serialize should be a static method');
    assert.ok(typeof FragmentsClient.validate === 'function', 'validate should be a static method');
    
    console.log('✓ All static methods accessible');
  });

  test('should verify client instance methods are accessible', async () => {
    const { FragmentsClient } = await import('../dist/esm/client.js');
    
    const client = new FragmentsClient({
      baseUrl: 'http://localhost:8001'
    });
    
    // Verify instance methods exist
    assert.ok(typeof client.request === 'function', 'request should be an instance method');
    assert.ok(typeof client.get === 'function', 'get should be an instance method');
    assert.ok(typeof client.post === 'function', 'post should be an instance method');
    assert.ok(typeof client.withConfig === 'function', 'withConfig should be an instance method');
    
    console.log('✓ All instance methods accessible');
  });

  test('should verify protobuf schemas work with client static methods', async () => {
    const { FragmentsClient } = await import('../dist/esm/client.js');
    const Settings = await import('../dist/esm/Settings/index.js');
    
    // Test createRequest with a real schema
    if (Settings.SettingsRecordSchema) {
      const request = FragmentsClient.createRequest(Settings.SettingsRecordSchema, {});
      assert.ok(request, 'createRequest should work with SettingsRecordSchema');
      
      // Test serialize
      const serialized = FragmentsClient.serialize(Settings.SettingsRecordSchema, request);
      assert.ok(typeof serialized === 'string', 'serialize should return a string');
      
      console.log('✓ Static methods work with protobuf schemas');
    } else {
      console.log('⚠ SettingsRecordSchema not available, skipping schema test');
    }
  });

  test('should verify type definitions are properly generated', async () => {
    // Import type definitions to verify they exist
    try {
      const fs = await import('fs');
      const path = await import('path');
      
      // Check that type definition files exist
      const typeFiles = [
        'dist/protos/client.d.ts',
        'dist/protos/index.d.ts',
        'dist/protos/Settings/index.d.ts',
        'dist/protos/Authentication/index.d.ts',
        'dist/protos/Content/index.d.ts'
      ];
      
      for (const file of typeFiles) {
        const exists = fs.default.existsSync(file);
        assert.ok(exists, `Type definition file ${file} should exist`);
      }
      
      console.log('✓ All type definition files exist');
    } catch (error) {
      console.error('Type definition check failed:', error.message);
      throw error;
    }
  });

  test('should verify client configuration types work correctly', async () => {
    const { FragmentsClient } = await import('../dist/esm/client.js');
    
    // Test different configuration options
    const configs = [
      { baseUrl: 'http://localhost:8001' },
      { 
        baseUrl: 'http://localhost:8001',
        getToken: () => 'test-token'
      },
      {
        baseUrl: 'http://localhost:8001',
        getToken: async () => 'async-token'
      },
      {
        baseUrl: 'http://localhost:8001',
        onCacheInvalidate: (tags, paths) => {
          console.log('Cache invalidation:', { tags, paths });
        }
      }
    ];
    
    for (const config of configs) {
      const client = new FragmentsClient(config);
      assert.ok(client, 'Client should be created with various config options');
    }
    
    console.log('✓ Client configuration types work correctly');
  });
});