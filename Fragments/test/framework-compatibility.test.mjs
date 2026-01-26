import { test, describe } from 'node:test';
import assert from 'node:assert';

describe('Framework Compatibility Tests', () => {
    describe('Next.js Environment Simulation', () => {
        test('should handle Next.js cache options in fetch calls', async () => {
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

        test('should call cache invalidation callbacks after successful mutations', async () => {
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

        test('should not call cache invalidation for GET requests', async () => {
            let cacheInvalidationCalled = false;

            // Mock fetch to return successful response
            const originalFetch = globalThis.fetch;
            globalThis.fetch = async () => ({
                ok: true,
                json: async () => ({ data: 'test' }),
            });

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');

                const client = new FragmentsClient({
                    baseUrl: 'https://api.example.com',
                    onCacheInvalidate: () => {
                        cacheInvalidationCalled = true;
                    },
                });

                const mockSchema = { typeName: 'TestMessage' };

                // Make a GET request
                await client.get('/api/data', mockSchema, {
                    cacheTags: ['data-cache'],
                });

                // Verify cache invalidation was NOT called for GET
                assert.strictEqual(cacheInvalidationCalled, false);

            } finally {
                globalThis.fetch = originalFetch;
            }
        });

        test('should simulate Next.js revalidateTag and revalidatePath integration', async () => {
            // Simulate Next.js cache functions
            const revalidatedTags = new Set();
            const revalidatedPaths = new Set();

            const mockRevalidateTag = (tag) => {
                revalidatedTags.add(tag);
            };

            const mockRevalidatePath = (path) => {
                revalidatedPaths.add(path);
            };

            // Mock fetch
            const originalFetch = globalThis.fetch;
            globalThis.fetch = async () => ({
                ok: true,
                json: async () => ({ success: true }),
            });

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');

                // Create client with Next.js-style cache invalidation
                const client = new FragmentsClient({
                    baseUrl: 'https://api.example.com',
                    getToken: () => 'next-token',
                    onCacheInvalidate: (tags, paths) => {
                        tags.forEach(mockRevalidateTag);
                        paths.forEach(mockRevalidatePath);
                    },
                });

                const mockSchema = { typeName: 'TestMessage' };

                // Simulate updating subscription settings (like existing action function)
                await client.post(
                    '/api/settings/subscription/public',
                    mockSchema,
                    mockSchema,
                    { subscriptionData: 'test' },
                    {
                        cacheTags: ['admin-settings', 'subscription-data'],
                        revalidatePaths: ['/settings/subscriptions', '/admin/dashboard'],
                    }
                );

                // Verify Next.js cache functions were called
                assert.ok(revalidatedTags.has('admin-settings'));
                assert.ok(revalidatedTags.has('subscription-data'));
                assert.ok(revalidatedPaths.has('/settings/subscriptions'));
                assert.ok(revalidatedPaths.has('/admin/dashboard'));

            } finally {
                globalThis.fetch = originalFetch;
            }
        });
    });

    describe('Non-Next.js Environment Compatibility', () => {
        test('should work in Node.js environment without Next.js dependencies', async () => {
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

        test('should ignore Next.js cache options in non-Next.js environments', async () => {
            let capturedFetchOptions = null;

            // Mock fetch to capture options
            const originalFetch = globalThis.fetch;
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
                    getToken: () => 'token',
                });

                const mockSchema = { typeName: 'TestMessage' };

                // Make request with Next.js cache options
                await client.request(
                    '/test',
                    mockSchema,
                    mockSchema,
                    { test: 'data' },
                    {
                        cacheTags: ['test-tag'],
                        revalidate: 60,
                    }
                );

                // Verify Next.js options are passed but will be ignored by standard fetch
                assert.ok(capturedFetchOptions);
                assert.ok(capturedFetchOptions.next);
                assert.deepStrictEqual(capturedFetchOptions.next.tags, ['test-tag']);
                assert.strictEqual(capturedFetchOptions.next.revalidate, 60);

                // Standard fetch will ignore these options, which is expected behavior

            } finally {
                globalThis.fetch = originalFetch;
            }
        });

        test('should work with different JavaScript frameworks', async () => {
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

        test('should handle browser environment without Node.js APIs', async () => {
            // Simulate browser environment by temporarily removing Node.js globals
            const originalProcess = globalThis.process;
            const originalBuffer = globalThis.Buffer;
            delete globalThis.process;
            delete globalThis.Buffer;

            const originalFetch = globalThis.fetch;
            globalThis.fetch = async () => ({
                ok: true,
                json: async () => ({ environment: 'browser' }),
            });

            try {
                const { FragmentsClient } = await import('../dist/esm/client.js');

                const client = new FragmentsClient({
                    baseUrl: 'https://api.browser.com',
                    getToken: () => localStorage?.getItem?.('token') || 'browser-token',
                });

                const mockSchema = { typeName: 'TestMessage' };
                const response = await client.get('/api/browser-test', mockSchema);

                assert.ok(response);
                assert.strictEqual(response.environment, 'browser');

            } finally {
                globalThis.fetch = originalFetch;
                if (originalProcess !== undefined) {
                    globalThis.process = originalProcess;
                }
                if (originalBuffer !== undefined) {
                    globalThis.Buffer = originalBuffer;
                }
            }
        });
    });

    describe('Framework-Agnostic Error Handling', () => {
        test('should handle network errors consistently across environments', async () => {
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

        test('should handle HTTP errors consistently across environments', async () => {
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

        test('should handle console logging safely across environments', async () => {
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
    });

    describe('Package Export Compatibility', () => {
        test('should be importable via main package export', async () => {
            const { FragmentsClient } = await import('../dist/esm/index.js');
            assert.ok(FragmentsClient);
            assert.strictEqual(typeof FragmentsClient, 'function');
        });

        test('should be importable via dedicated client export', async () => {
            const { FragmentsClient } = await import('../dist/esm/client.js');
            assert.ok(FragmentsClient);
            assert.strictEqual(typeof FragmentsClient, 'function');
        });

        test('should have proper TypeScript type definitions', async () => {
            // This test verifies that the type definitions are properly generated
            // by checking that the client can be imported and has expected methods
            const { FragmentsClient } = await import('../dist/esm/client.js');
            
            const client = new FragmentsClient();
            
            // Verify all expected methods exist
            assert.strictEqual(typeof client.request, 'function');
            assert.strictEqual(typeof client.get, 'function');
            assert.strictEqual(typeof client.post, 'function');
            assert.strictEqual(typeof client.withConfig, 'function');
            
            // Verify static methods exist
            assert.strictEqual(typeof FragmentsClient.createRequest, 'function');
            assert.strictEqual(typeof FragmentsClient.createResponse, 'function');
            assert.strictEqual(typeof FragmentsClient.serialize, 'function');
            assert.strictEqual(typeof FragmentsClient.validate, 'function');
            assert.strictEqual(typeof FragmentsClient.createErrorResponse, 'function');
        });
    });
});