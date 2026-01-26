import { test, describe } from 'node:test';
import { strict as assert } from 'node:assert';

describe('Direct Client Type Accessibility Tests', () => {
  test('should be able to import FragmentsClient directly', async () => {
    try {
      // Test importing the client directly without validation dependencies
      const clientModule = await import('../dist/esm/client.js');
      const { FragmentsClient } = clientModule;
      
      assert.ok(FragmentsClient, 'FragmentsClient should be importable');
      assert.ok(typeof FragmentsClient === 'function', 'FragmentsClient should be a constructor function');
      
      console.log('✓ FragmentsClient imported successfully');
    } catch (error) {
      console.error('Direct import failed:', error.message);
      throw error;
    }
  });

  test('should verify client static methods exist and are callable', async () => {
    const { FragmentsClient } = await import('../dist/esm/client.js');
    
    // Verify static methods exist
    assert.ok(typeof FragmentsClient.createRequest === 'function', 'createRequest should be a static method');
    assert.ok(typeof FragmentsClient.createResponse === 'function', 'createResponse should be a static method');
    assert.ok(typeof FragmentsClient.serialize === 'function', 'serialize should be a static method');
    assert.ok(typeof FragmentsClient.validate === 'function', 'validate should be a static method');
    
    console.log('✓ All static methods exist and are functions');
  });

  test('should verify client instance can be created and has methods', async () => {
    const { FragmentsClient } = await import('../dist/esm/client.js');
    
    const client = new FragmentsClient({
      baseUrl: 'http://localhost:8001'
    });
    
    // Verify instance methods exist
    assert.ok(typeof client.request === 'function', 'request should be an instance method');
    assert.ok(typeof client.get === 'function', 'get should be an instance method');
    assert.ok(typeof client.post === 'function', 'post should be an instance method');
    assert.ok(typeof client.withConfig === 'function', 'withConfig should be an instance method');
    
    console.log('✓ Client instance created with all methods');
  });

  test('should verify client configuration types work', async () => {
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

  test('should verify withConfig method works', async () => {
    const { FragmentsClient } = await import('../dist/esm/client.js');
    
    const client1 = new FragmentsClient({
      baseUrl: 'http://localhost:8001'
    });
    
    const client2 = client1.withConfig({
      getToken: () => 'new-token'
    });
    
    assert.ok(client2, 'withConfig should return a new client instance');
    assert.notStrictEqual(client1, client2, 'withConfig should return a different instance');
    
    console.log('✓ withConfig method works correctly');
  });

  test('should verify type definitions exist for client', async () => {
    try {
      const fs = await import('fs');
      
      // Check that client type definition file exists
      const clientTypeFile = 'dist/protos/client.d.ts';
      const exists = fs.default.existsSync(clientTypeFile);
      assert.ok(exists, `Client type definition file ${clientTypeFile} should exist`);
      
      // Read the type definition file to verify it contains expected exports
      const content = fs.default.readFileSync(clientTypeFile, 'utf8');
      assert.ok(content.includes('export interface ClientConfig'), 'ClientConfig interface should be exported');
      assert.ok(content.includes('export type HttpMethod'), 'HttpMethod type should be exported');
      assert.ok(content.includes('export type TokenGetter'), 'TokenGetter type should be exported');
      assert.ok(content.includes('export type CacheInvalidator'), 'CacheInvalidator type should be exported');
      
      console.log('✓ Client type definitions exist and contain expected exports');
    } catch (error) {
      console.error('Type definition check failed:', error.message);
      throw error;
    }
  });

  test('should verify package.json exports include client', async () => {
    try {
      const fs = await import('fs');
      
      // Read package.json
      const packageJson = JSON.parse(fs.default.readFileSync('package.json', 'utf8'));
      
      // Verify client export exists
      assert.ok(packageJson.exports['./client'], 'Client export should exist in package.json');
      assert.ok(packageJson.exports['./client'].types, 'Client export should have types field');
      assert.ok(packageJson.exports['./client'].import, 'Client export should have import field');
      
      console.log('✓ Package.json exports include client correctly');
    } catch (error) {
      console.error('Package.json check failed:', error.message);
      throw error;
    }
  });
});