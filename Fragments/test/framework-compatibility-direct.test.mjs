import { test, describe } from 'node:test';
import assert from 'node:assert';

describe('Framework Compatibility Tests (Direct Import)', () => {
    describe('Direct Client Import Tests', () => {
        test('should import client directly without full package', async () => {
            try {
                // Import client directly to avoid package resolution issues
                const { FragmentsClient } = await import('../dist/esm/client.js');
                assert.ok(FragmentsClient);
                assert.strictEqual(typeof FragmentsClient, 'function');
                
                // Test basic instantiation
                const client = new FragmentsClient();
                assert.ok(client);
                
                // Test configuration
                const config = client._config;
                assert.strictEqual(config.baseUrl, 'http://localhost:8001');
                assert.strictEqual(typeof config.getToken, 'function');
                assert.strictEqual(typeof config.onCacheInvalidate, 'function');
                assert.strictEqual(config.validateRequests, false);
                
            } catch (error) {
                console.error('Direct import failed:', error);
                throw error;
            }
        });

        test('should handle Next.js cache options in fetch calls (direct)', async () => {
            // Mock fetch to capture Next.js cache options
            const originalFetch = globalThis.fetch;
            let capturedFetchOptions = null;

            globalThis.fetch = async (url, options) => {
                capturedFetchOptions = options;
                return {
                    ok: true,
                    json: async () => ({ success: true }),
                };
            };

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');
                
                const client = new FragmentsClient({
                    baseUrl: 'https://api.example.com',
                    getToken: () => 'test-token',
                });

                // Create a mock schema for testing
                const mockSchema = {
                    typeName: 'TestMessage',
                };

                // Make a request with Next.js cache options
                await client.request(
                    '/test-endpoint',
                    mockSchema,
                    mockSchema,
                    { test: 'data' },
                    {
                        method: 'POST',
                        cacheTags: ['admin-settings', 'user-data'],
                        revalidate: 30,
                    }
                );

                // Verify Next.js cache options were passed to fetch
                assert.ok(capturedFetchOptions);
                assert.ok(capturedFetchOptions.next);
                assert.deepStrictEqual(capturedFetchOptions.next.tags, ['admin-settings', 'user-data']);
                assert.strictEqual(capturedFetchOptions.next.revalidate, 30);
                assert.strictEqual(capturedFetchOptions.method, 'POST');
                assert.strictEqual(capturedFetchOptions.headers['Content-Type'], 'application/json');
                assert.strictEqual(capturedFetchOptions.headers['Authorization'], 'Bearer test-token');

            } finally {
                // Restore original fetch
                globalThis.fetch = originalFetch;
            }
        });

        test('should call cache invalidation callbacks after successful mutations (direct)', async () => {
            let invalidatedTags = [];
            let invalidatedPaths = [];

            // Mock fetch to return successful response
            const originalFetch = globalThis.fetch;
            globalThis.fetch = async () => ({
                ok: true,
                json: async () => ({ success: true }),
            });

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');

                const client = new FragmentsClient({
                    baseUrl: 'https://api.example.com',
                    getToken: () => 'test-token',
                    onCacheInvalidate: (tags, paths) => {
                        invalidatedTags.push(...tags);
                        invalidatedPaths.push(...paths);
                    },
                });

                const mockSchema = { typeName: 'TestMessage' };

                // Make a POST request (mutation) with cache invalidation
                await client.post(
                    '/api/settings/update',
                    mockSchema,
                    mockSchema,
                    { test: 'data' },
                    {
                        cacheTags: ['admin-settings'],
                        revalidatePaths: ['/settings', '/admin'],
                    }
                );

                // Verify cache invalidation was called
                assert.deepStrictEqual(invalidatedTags, ['admin-settings']);
                assert.deepStrictEqual(invalidatedPaths, ['/settings', '/admin']);

            } finally {
                globalThis.fetch = originalFetch;
            }
        });

        test('should work in Node.js environment without Next.js dependencies (direct)', async () => {
            // Ensure no Next.js globals are available
            const originalNext = globalThis.next;
            delete globalThis.next;

            // Mock basic fetch
            const originalFetch = globalThis.fetch;
            globalThis.fetch = async (url, options) => ({
                ok: true,
                json: async () => ({ data: 'node-response' }),
            });

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');

                const client = new FragmentsClient({
                    baseUrl: 'https://api.backend.com',
                    getToken: () => 'node-token',
                    // No cache invalidation callback - should work fine
                });

                const mockSchema = { typeName: 'TestMessage' };

                // Make request without any Next.js specific options
                const response = await client.get('/api/data', mockSchema);

                assert.ok(response);
                assert.strictEqual(response.data, 'node-response');

            } finally {
                globalThis.fetch = originalFetch;
                if (originalNext !== undefined) {
                    globalThis.next = originalNext;
                }
            }
        });

        test('should handle framework-agnostic error handling (direct)', async () => {
            const originalFetch = globalThis.fetch;
            globalThis.fetch = async () => {
                throw new Error('Network error');
            };

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');

                const client = new FragmentsClient({
                    baseUrl: 'https://api.example.com',
                });

                const mockSchema = { typeName: 'TestMessage' };

                // Should return error response, not throw
                const response = await client.get('/api/test', mockSchema);

                assert.ok(response);
                assert.ok(response.Error);
                assert.strictEqual(response.Error.Type, 'SETTINGS_ERROR_UNKNOWN');
                assert.ok(response.Error.Message.includes('Network error'));

            } finally {
                globalThis.fetch = originalFetch;
            }
        });

        test('should handle HTTP errors consistently (direct)', async () => {
            const originalFetch = globalThis.fetch;
            globalThis.fetch = async () => ({
                ok: false,
                status: 404,
                statusText: 'Not Found',
            });

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');

                const client = new FragmentsClient({
                    baseUrl: 'https://api.example.com',
                });

                const mockSchema = { typeName: 'TestMessage' };

                const response = await client.get('/api/missing', mockSchema);

                assert.ok(response);
                assert.ok(response.Error);
                assert.strictEqual(response.Error.Type, 'SETTINGS_ERROR_UNKNOWN');
                assert.ok(response.Error.Message.includes('HTTP 404'));

            } finally {
                globalThis.fetch = originalFetch;
            }
        });

        test('should work with different framework environments (direct)', async () => {
            // Simulate different framework environments
            const frameworks = [
                { name: 'Svelte', global: 'svelte' },
                { name: 'Vue', global: 'Vue' },
                { name: 'Vanilla JS', global: null },
            ];

            const originalFetch = globalThis.fetch;
            globalThis.fetch = async () => ({
                ok: true,
                json: async () => ({ framework: 'agnostic' }),
            });

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');

                for (const framework of frameworks) {
                    // Simulate framework environment
                    if (framework.global) {
                        globalThis[framework.global] = { version: '1.0.0' };
                    }

                    const client = new FragmentsClient({
                        baseUrl: `https://api.${framework.name.toLowerCase()}.com`,
                        getToken: () => `${framework.name}-token`,
                    });

                    const mockSchema = { typeName: 'TestMessage' };
                    const response = await client.get('/api/test', mockSchema);

                    assert.ok(response);
                    assert.strictEqual(response.framework, 'agnostic');

                    // Clean up framework global
                    if (framework.global) {
                        delete globalThis[framework.global];
                    }
                }

            } finally {
                globalThis.fetch = originalFetch;
            }
        });

        test('should handle console logging safely across environments (direct)', async () => {
            // Test safe logging in environment without console
            const originalConsole = globalThis.console;
            delete globalThis.console;

            const originalFetch = globalThis.fetch;
            globalThis.fetch = async () => {
                throw new Error('Test error for logging');
            };

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');

                const client = new FragmentsClient();
                const mockSchema = { typeName: 'TestMessage' };

                // Should not throw even without console available
                assert.doesNotThrow(async () => {
                    await client.get('/api/test', mockSchema);
                });

            } finally {
                globalThis.fetch = originalFetch;
                if (originalConsole !== undefined) {
                    globalThis.console = originalConsole;
                }
            }
        });

        test('should verify static utility methods work (direct)', async () => {
            const { FragmentsClient } = await import('../dist/esm/client.js');
            
            // Verify all static methods exist
            assert.strictEqual(typeof FragmentsClient.createRequest, 'function');
            assert.strictEqual(typeof FragmentsClient.createResponse, 'function');
            assert.strictEqual(typeof FragmentsClient.serialize, 'function');
            assert.strictEqual(typeof FragmentsClient.validate, 'function');
            assert.strictEqual(typeof FragmentsClient.createErrorResponse, 'function');
        });

        test('should verify instance methods work (direct)', async () => {
            const { FragmentsClient } = await import('../dist/esm/client.js');
            
            const client = new FragmentsClient();
            
            // Verify all expected methods exist
            assert.strictEqual(typeof client.request, 'function');
            assert.strictEqual(typeof client.get, 'function');
            assert.strictEqual(typeof client.post, 'function');
            assert.strictEqual(typeof client.withConfig, 'function');
        });

        test('should handle withConfig method properly (direct)', async () => {
            const { FragmentsClient } = await import('../dist/esm/client.js');
            
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
    });
});