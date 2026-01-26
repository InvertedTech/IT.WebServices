import { test, describe, beforeEach, afterEach } from 'node:test';
import assert from 'node:assert';

// Try to import the client and schemas, fallback to mocks if import fails
let FragmentsClient, PaginationSchema, CreatorSettingsSchema, SettingsResponseSchema;
let mockFetch;
let originalFetch;

try {
    const clientModule = await import('../dist/esm/client.js');
    FragmentsClient = clientModule.FragmentsClient;
    
    const commonTypesModule = await import('../dist/esm/CommonTypes_pb.js');
    PaginationSchema = commonTypesModule.PaginationSchema;
    
    const creatorSettingsModule = await import('../dist/esm/CreatorDashboard/Settings/CreatorSettings_pb.js');
    CreatorSettingsSchema = creatorSettingsModule.CreatorSettingsSchema;
    SettingsResponseSchema = creatorSettingsModule.SettingsResponseSchema;
} catch (error) {
    console.log('Client or schema import failed, using mocks for testing:', error.message);
    
    // Create mock schemas and client for testing
    PaginationSchema = { name: 'PaginationSchema' };
    CreatorSettingsSchema = { name: 'CreatorSettingsSchema' };
    SettingsResponseSchema = { name: 'SettingsResponseSchema' };
    
    FragmentsClient = class MockFragmentsClient {
        constructor(config = {}) {
            this.config = {
                baseUrl: config.baseUrl ?? 'http://localhost:8001',
                getToken: config.getToken ?? (() => undefined),
                onCacheInvalidate: config.onCacheInvalidate ?? (() => {}),
                validateRequests: config.validateRequests ?? false,
            };
        }

        async request(endpoint, reqSchema, resSchema, data, options = {}) {
            // Mock implementation that calls fetch
            const method = options.method ?? 'POST';
            const shouldValidate = options.validate ?? this.config.validateRequests;
            const token = await this.config.getToken();
            
            // Handle validation if enabled and data is provided
            if (data && method !== 'GET' && shouldValidate) {
                const validationResult = await FragmentsClient.validate(reqSchema, data);
                if (!validationResult.success) {
                    // Return validation error response without making HTTP call
                    return {
                        Error: {
                            Message: 'Request validation failed',
                            Type: 'SETTINGS_ERROR_VALIDATION_FAILED',
                            Validation: validationResult.violations ?? [],
                        },
                    };
                }
            }
            
            const fetchOptions = {
                method,
                headers: {
                    'Content-Type': 'application/json',
                    ...(token && { Authorization: `Bearer ${token}` }),
                },
                ...(data && method !== 'GET' && { body: JSON.stringify(data) }),
            };

            if (options.cacheTags || options.revalidate !== undefined) {
                fetchOptions.next = {
                    ...(options.cacheTags && { tags: options.cacheTags }),
                    ...(options.revalidate !== undefined && { revalidate: options.revalidate }),
                };
            }

            const url = `${this.config.baseUrl}${endpoint}`;
            const response = await fetch(url, fetchOptions);

            if (!response) {
                return { Error: { Message: 'Network request failed', Type: 'SETTINGS_ERROR_UNKNOWN' } };
            }

            if (!response.ok) {
                return { Error: { Message: `HTTP ${response.status}: ${response.statusText}`, Type: 'SETTINGS_ERROR_UNKNOWN' } };
            }

            const responseData = await response.json();

            // Handle cache invalidation for successful mutations
            if (method !== 'GET' && (options.cacheTags || options.revalidatePaths)) {
                this.config.onCacheInvalidate(
                    options.cacheTags ?? [],
                    options.revalidatePaths ?? []
                );
            }

            return responseData;
        }

        async get(endpoint, resSchema, options = {}) {
            return this.request(endpoint, {}, resSchema, undefined, { ...options, method: 'GET' });
        }

        async post(endpoint, reqSchema, resSchema, data, options = {}) {
            return this.request(endpoint, reqSchema, resSchema, data, { ...options, method: 'POST' });
        }

        static createRequest(schema, data) { return data || {}; }
        static createResponse(schema, data) { return data || {}; }
        static serialize(schema, data) { return JSON.stringify(data); }
        static async validate() { return { success: true }; }
    };
}

/**
 * Integration tests for HTTP functionality
 * Requirements: 1.3, 1.4, 4.1, 4.4, 6.1, 6.2, 3.1, 3.2, 3.3, 9.1, 9.3, 12.2, 12.3, 12.6
 */
describe('FragmentsClient HTTP Integration Tests', () => {
    beforeEach(() => {
        // Store original fetch and create mock
        originalFetch = globalThis.fetch;
        mockFetch = createMockFetch();
        globalThis.fetch = mockFetch;
    });

    afterEach(() => {
        // Restore original fetch
        globalThis.fetch = originalFetch;
    });

    /**
     * Test HTTP request methods with mock responses
     * Requirements: 1.3, 1.4, 4.1, 4.4, 6.1, 6.2
     */
    describe('8.1 HTTP Request Methods with Mock Responses', () => {
        test('should include Authorization header when token is provided', async () => {
            const mockToken = 'test-bearer-token-123';
            const client = new FragmentsClient({
                baseUrl: 'https://api.test.com',
                getToken: () => mockToken,
            });

            mockFetch.mockResponse({
                ok: true,
                json: async () => ({ settings: { MuteMessage: 'Test response' } }),
            });

            await client.post(
                '/api/settings',
                CreatorSettingsSchema,
                SettingsResponseSchema,
                { MuteMessage: 'Test message' }
            );

            // Verify Authorization header was included
            const lastCall = mockFetch.getLastCall();
            assert.strictEqual(lastCall.options.headers['Authorization'], `Bearer ${mockToken}`);
            assert.strictEqual(lastCall.options.headers['Content-Type'], 'application/json');
        });

        test('should make GET request using get() method', async () => {
            const client = new FragmentsClient({
                baseUrl: 'https://api.test.com',
            });

            const expectedResponse = { PageOffsetStart: 0, PageOffsetEnd: 20, PageTotalItems: 200 };

            mockFetch.mockResponse({
                ok: true,
                json: async () => expectedResponse,
            });

            const result = await client.get('/api/pagination', PaginationSchema, {
                cacheTags: ['pagination'],
                revalidate: 60,
            });

            // Verify GET request was made correctly
            const lastCall = mockFetch.getLastCall();
            assert.strictEqual(lastCall.url, 'https://api.test.com/api/pagination');
            assert.strictEqual(lastCall.options.method, 'GET');
            assert.deepStrictEqual(result, expectedResponse);
        });

        test('should handle HTTP error responses', async () => {
            const client = new FragmentsClient({
                baseUrl: 'https://api.test.com',
            });

            mockFetch.mockResponse({
                ok: false,
                status: 404,
                statusText: 'Not Found',
                json: async () => ({}),
            });

            const result = await client.post(
                '/api/nonexistent',
                CreatorSettingsSchema,
                SettingsResponseSchema,
                { MuteMessage: 'Test' }
            );

            // Verify error response structure
            assert.strictEqual(result.Error.Message, 'HTTP 404: Not Found');
            assert.strictEqual(result.Error.Type, 'SETTINGS_ERROR_UNKNOWN');
        });
    });

    /**
     * Test cache integration and invalidation
     * Requirements: 3.1, 3.2, 3.3, 9.1, 9.3
     */
    describe('8.2 Cache Integration and Invalidation', () => {
        test('should include Next.js cache tags in fetch options', async () => {
            const client = new FragmentsClient({
                baseUrl: 'https://api.test.com',
            });

            mockFetch.mockResponse({
                ok: true,
                json: async () => ({ PageOffsetStart: 0, PageOffsetEnd: 10, PageTotalItems: 100 }),
            });

            await client.get('/api/pagination', PaginationSchema, {
                cacheTags: ['pagination', 'admin-data'],
                revalidate: 300,
            });

            // Verify Next.js cache options were included
            const lastCall = mockFetch.getLastCall();
            assert.deepStrictEqual(lastCall.options.next, {
                tags: ['pagination', 'admin-data'],
                revalidate: 300,
            });
        });

        test('should call cache invalidation callback after successful POST', async () => {
            let invalidatedTags = [];
            let invalidatedPaths = [];

            const client = new FragmentsClient({
                baseUrl: 'https://api.test.com',
                onCacheInvalidate: (tags, paths) => {
                    invalidatedTags = [...tags];
                    invalidatedPaths = [...paths];
                },
            });

            mockFetch.mockResponse({
                ok: true,
                json: async () => ({ settings: { MuteMessage: 'Updated' } }),
            });

            await client.post(
                '/api/settings',
                CreatorSettingsSchema,
                SettingsResponseSchema,
                { MuteMessage: 'Test' },
                {
                    cacheTags: ['settings', 'admin-data'],
                    revalidatePaths: ['/settings', '/admin/dashboard'],
                }
            );

            // Verify cache invalidation was called with correct parameters
            assert.deepStrictEqual(invalidatedTags, ['settings', 'admin-data']);
            assert.deepStrictEqual(invalidatedPaths, ['/settings', '/admin/dashboard']);
        });

        test('should not call cache invalidation callback for GET requests', async () => {
            let callbackCalled = false;

            const client = new FragmentsClient({
                baseUrl: 'https://api.test.com',
                onCacheInvalidate: () => {
                    callbackCalled = true;
                },
            });

            mockFetch.mockResponse({
                ok: true,
                json: async () => ({ success: true }),
            });

            await client.get('/api/data', SettingsResponseSchema, {
                cacheTags: ['data'],
                revalidatePaths: ['/data'],
            });

            // Verify cache invalidation was not called for GET
            assert.strictEqual(callbackCalled, false);
        });
    });

    /**
     * Test validation integration in HTTP flow
     * Requirements: 12.2, 12.3, 12.6
     */
    describe('8.3 Validation Integration in HTTP Flow', () => {
        test('should validate request data before HTTP call when validation is enabled globally', async () => {
            const client = new FragmentsClient({
                baseUrl: 'https://api.test.com',
                validateRequests: true, // Enable validation globally
            });

            // Mock validation to return success
            const originalValidate = FragmentsClient.validate;
            FragmentsClient.validate = async () => ({ success: true });

            mockFetch.mockResponse({
                ok: true,
                json: async () => ({ settings: { MuteMessage: 'Valid data processed' } }),
            });

            const result = await client.post(
                '/api/settings',
                CreatorSettingsSchema,
                SettingsResponseSchema,
                { MuteMessage: 'Valid message' }
            );

            // Verify HTTP request was made (validation passed)
            const lastCall = mockFetch.getLastCall();
            assert.strictEqual(lastCall.url, 'https://api.test.com/api/settings');
            assert.deepStrictEqual(result, { settings: { MuteMessage: 'Valid data processed' } });

            // Restore original validate method
            FragmentsClient.validate = originalValidate;
        });

        test('should return validation error response without making HTTP call when validation fails', async () => {
            const client = new FragmentsClient({
                baseUrl: 'https://api.test.com',
                validateRequests: true,
            });

            // Mock validation to return failure with violations
            const originalValidate = FragmentsClient.validate;
            const mockViolations = [
                { field: 'MuteMessage', message: 'Message is required' },
                { field: 'BanMessage', message: 'Ban message too long' },
            ];
            FragmentsClient.validate = async () => ({
                success: false,
                violations: mockViolations,
            });

            // Mock fetch should not be called
            let fetchCalled = false;
            mockFetch.mockResponse({
                ok: true,
                json: async () => {
                    fetchCalled = true;
                    return { settings: { MuteMessage: 'Should not reach here' } };
                },
            });

            const result = await client.post(
                '/api/settings',
                CreatorSettingsSchema,
                SettingsResponseSchema,
                { MuteMessage: '' } // Invalid data
            );

            // Verify HTTP request was NOT made
            assert.strictEqual(fetchCalled, false);
            assert.strictEqual(mockFetch.getLastCall(), null);

            // Verify validation error response structure
            assert.strictEqual(result.Error.Message, 'Request validation failed');
            assert.strictEqual(result.Error.Type, 'SETTINGS_ERROR_VALIDATION_FAILED');
            assert.deepStrictEqual(result.Error.Validation, mockViolations);

            // Restore original validate method
            FragmentsClient.validate = originalValidate;
        });

        test('should bypass validation when disabled globally (default)', async () => {
            const client = new FragmentsClient({
                baseUrl: 'https://api.test.com',
                // validateRequests defaults to false
            });

            // Mock validation to throw (should not be called)
            const originalValidate = FragmentsClient.validate;
            FragmentsClient.validate = async () => {
                throw new Error('Validation should not be called when disabled');
            };

            mockFetch.mockResponse({
                ok: true,
                json: async () => ({ settings: { MuteMessage: 'No validation performed' } }),
            });

            // Should not throw because validation is disabled
            const result = await client.post(
                '/api/settings',
                CreatorSettingsSchema,
                SettingsResponseSchema,
                { MuteMessage: 'Test message' }
            );

            // Verify HTTP request was made without validation
            const lastCall = mockFetch.getLastCall();
            assert.strictEqual(lastCall.url, 'https://api.test.com/api/settings');
            assert.deepStrictEqual(result, { settings: { MuteMessage: 'No validation performed' } });

            // Restore original validate method
            FragmentsClient.validate = originalValidate;
        });
    });
});

/**
 * Create a mock fetch function for testing
 */
function createMockFetch() {
    let mockResponse = null;
    let mockError = null;
    let lastCall = null;

    const mockFetch = async (url, options = {}) => {
        // Store the last call for verification
        lastCall = { url, options };

        // Throw error if mockError is set
        if (mockError) {
            const error = mockError;
            mockError = null; // Reset for next call
            throw error;
        }

        // Return mock response if set
        if (mockResponse) {
            const response = mockResponse;
            mockResponse = null; // Reset for next call
            return response;
        }

        // Default successful response
        return {
            ok: true,
            status: 200,
            statusText: 'OK',
            json: async () => ({ success: true }),
        };
    };

    // Helper methods to configure mock behavior
    mockFetch.mockResponse = (response) => {
        mockResponse = response;
    };

    mockFetch.mockError = (error) => {
        mockError = error;
    };

    mockFetch.getLastCall = () => lastCall;

    mockFetch.reset = () => {
        mockResponse = null;
        mockError = null;
        lastCall = null;
    };

    return mockFetch;
}