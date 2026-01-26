import { test, describe } from 'node:test';
import assert from 'node:assert';
import { FragmentsClient } from '../dist/esm/client.js';

describe('FragmentsClient Foundation Tests', () => {
    describe('Constructor and Configuration', () => {
        test('should create client with default configuration', () => {
            const client = new FragmentsClient();

            // Access config through testing getter
            const config = client._config;

            assert.strictEqual(config.baseUrl, 'http://localhost:8001');
            assert.strictEqual(typeof config.getToken, 'function');
            assert.strictEqual(typeof config.onCacheInvalidate, 'function');
            assert.strictEqual(config.validateRequests, false);
        });

        test('should create client with custom configuration', () => {
            const customConfig = {
                baseUrl: 'https://api.example.com',
                getToken: () => 'test-token',
                onCacheInvalidate: (tags, paths) => {
                    console.log('Cache invalidated:', { tags, paths });
                },
                validateRequests: true,
            };

            const client = new FragmentsClient(customConfig);
            const config = client._config;

            assert.strictEqual(config.baseUrl, 'https://api.example.com');
            assert.strictEqual(config.getToken(), 'test-token');
            assert.strictEqual(config.validateRequests, true);
        });

        test('should handle partial configuration with defaults', () => {
            const partialConfig = {
                baseUrl: 'https://custom.api.com',
                validateRequests: true,
            };

            const client = new FragmentsClient(partialConfig);
            const config = client._config;

            assert.strictEqual(config.baseUrl, 'https://custom.api.com');
            assert.strictEqual(config.validateRequests, true);
            assert.strictEqual(typeof config.getToken, 'function');
            assert.strictEqual(typeof config.onCacheInvalidate, 'function');
        });

        test('should handle async token getter', async () => {
            const asyncTokenGetter = async () => {
                return new Promise((resolve) => {
                    setTimeout(() => resolve('async-token'), 10);
                });
            };

            const client = new FragmentsClient({
                getToken: asyncTokenGetter,
            });

            const config = client._config;
            const token = await config.getToken();
            assert.strictEqual(token, 'async-token');
        });

        test('should handle undefined token getter', async () => {
            const client = new FragmentsClient({
                getToken: () => undefined,
            });

            const config = client._config;
            const token = await config.getToken();
            assert.strictEqual(token, undefined);
        });
    });

    describe('withConfig Method', () => {
        test('should create new client instance with modified config', () => {
            const originalClient = new FragmentsClient({
                baseUrl: 'https://original.com',
                validateRequests: false,
            });

            const newClient = originalClient.withConfig({
                baseUrl: 'https://modified.com',
                validateRequests: true,
            });

            // Verify original client is unchanged
            const originalConfig = originalClient._config;
            assert.strictEqual(originalConfig.baseUrl, 'https://original.com');
            assert.strictEqual(originalConfig.validateRequests, false);

            // Verify new client has modified config
            const newConfig = newClient._config;
            assert.strictEqual(newConfig.baseUrl, 'https://modified.com');
            assert.strictEqual(newConfig.validateRequests, true);

            // Verify they are different instances
            assert.notStrictEqual(originalClient, newClient);
        });

        test('should preserve unmodified config values', () => {
            const tokenGetter = () => 'original-token';
            const cacheInvalidator = () => { };

            const originalClient = new FragmentsClient({
                baseUrl: 'https://original.com',
                getToken: tokenGetter,
                onCacheInvalidate: cacheInvalidator,
                validateRequests: false,
            });

            const newClient = originalClient.withConfig({
                validateRequests: true,
            });

            const newConfig = newClient._config;
            assert.strictEqual(newConfig.baseUrl, 'https://original.com');
            assert.strictEqual(newConfig.getToken, tokenGetter);
            assert.strictEqual(newConfig.onCacheInvalidate, cacheInvalidator);
            assert.strictEqual(newConfig.validateRequests, true);
        });
    });

    describe('Static Utility Methods', () => {
        test('should have createRequest static method', () => {
            assert.strictEqual(typeof FragmentsClient.createRequest, 'function');
        });

        test('should have createResponse static method', () => {
            assert.strictEqual(typeof FragmentsClient.createResponse, 'function');
        });

        test('should have serialize static method', () => {
            assert.strictEqual(typeof FragmentsClient.serialize, 'function');
        });

        test('should have validate static method', () => {
            assert.strictEqual(typeof FragmentsClient.validate, 'function');
        });
    });

    describe('Instance Methods', () => {
        test('should have request method', () => {
            const client = new FragmentsClient();
            assert.strictEqual(typeof client.request, 'function');
        });

        test('should have get method', () => {
            const client = new FragmentsClient();
            assert.strictEqual(typeof client.get, 'function');
        });

        test('should have post method', () => {
            const client = new FragmentsClient();
            assert.strictEqual(typeof client.post, 'function');
        });

        test('should have withConfig method', () => {
            const client = new FragmentsClient();
            assert.strictEqual(typeof client.withConfig, 'function');
        });
    });

    describe('Error Handling and Edge Cases', () => {
        test('should handle empty configuration object', () => {
            const client = new FragmentsClient({});
            const config = client._config;

            assert.strictEqual(config.baseUrl, 'http://localhost:8001');
            assert.strictEqual(typeof config.getToken, 'function');
            assert.strictEqual(typeof config.onCacheInvalidate, 'function');
            assert.strictEqual(config.validateRequests, false);
        });

        test('should handle null/undefined configuration gracefully', () => {
            // Test with undefined (should use defaults)
            const client1 = new FragmentsClient(undefined);
            const config1 = client1._config;
            assert.strictEqual(config1.baseUrl, 'http://localhost:8001');

            // Test with empty object
            const client2 = new FragmentsClient({});
            const config2 = client2._config;
            assert.strictEqual(config2.baseUrl, 'http://localhost:8001');
        });

        test('should handle cache invalidation callback errors gracefully', () => {
            let callbackCalled = false;
            const client = new FragmentsClient({
                onCacheInvalidate: (tags, paths) => {
                    callbackCalled = true;
                    // Simulate callback execution
                    assert.ok(Array.isArray(tags));
                    assert.ok(Array.isArray(paths));
                },
            });

            const config = client._config;

            // Test that callback can be called without throwing
            assert.doesNotThrow(() => {
                config.onCacheInvalidate(['test-tag'], ['/test-path']);
            });

            assert.strictEqual(callbackCalled, true);
        });
    });

    describe('Framework Agnostic Design', () => {
        test('should work without Next.js specific dependencies', () => {
            // This test verifies that the client can be instantiated and configured
            // without requiring Next.js specific imports or globals
            const client = new FragmentsClient({
                baseUrl: 'https://api.example.com',
                getToken: () => 'test-token',
                // Don't provide onCacheInvalidate to test default behavior
            });

            const config = client._config;

            // Should have default no-op cache invalidation
            assert.doesNotThrow(() => {
                config.onCacheInvalidate(['tag'], ['/path']);
            });
        });

        test('should support Next.js cache invalidation when provided', () => {
            let revalidatedTags = [];
            let revalidatedPaths = [];

            const mockRevalidateTag = (tag) => {
                revalidatedTags.push(tag);
            };

            const mockRevalidatePath = (path) => {
                revalidatedPaths.push(path);
            };

            const client = new FragmentsClient({
                onCacheInvalidate: (tags, paths) => {
                    tags.forEach(mockRevalidateTag);
                    paths.forEach(mockRevalidatePath);
                },
            });

            const config = client._config;
            config.onCacheInvalidate(['tag1', 'tag2'], ['/path1', '/path2']);

            assert.deepStrictEqual(revalidatedTags, ['tag1', 'tag2']);
            assert.deepStrictEqual(revalidatedPaths, ['/path1', '/path2']);
        });
    });
});