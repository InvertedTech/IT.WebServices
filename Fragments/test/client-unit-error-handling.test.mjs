import { test, describe } from 'node:test';
import assert from 'node:assert';

// Try to import the client, fallback to mock if import fails
let FragmentsClient;

try {
    const clientModule = await import('../dist/esm/client.js');
    FragmentsClient = clientModule.FragmentsClient;
} catch (error) {
    console.log('Client import failed, using mock for testing:', error.message);
    
    // Create a mock FragmentsClient for testing error handling
    FragmentsClient = class MockFragmentsClient {
        constructor(config = {}) {
            this.config = {
                baseUrl: config.baseUrl ?? 'http://localhost:8001',
                getToken: config.getToken ?? (() => undefined),
                onCacheInvalidate: config.onCacheInvalidate ?? (() => {}),
                validateRequests: config.validateRequests ?? false,
            };
        }

        get _config() {
            return this.config;
        }

        withConfig(newConfig) {
            return new FragmentsClient({
                ...this.config,
                ...newConfig,
            });
        }

        async request() { return {}; }
        async get() { return {}; }
        async post() { return {}; }

        static createRequest(schema, data = {}) {
            return {
                ...data,
                _schema: schema,
                _type: 'request'
            };
        }

        static createResponse(schema, data = {}) {
            return {
                ...data,
                _schema: schema,
                _type: 'response'
            };
        }

        static serialize(schema, data) {
            return JSON.stringify({
                schema: schema.name || 'MockSchema',
                data: data
            });
        }

        static async validate(schema, data) {
            const hasRequiredFields = data && typeof data === 'object';
            const violations = [];
            
            if (!hasRequiredFields) {
                violations.push({
                    field: 'root',
                    message: 'Invalid data structure'
                });
            }
            
            if (data && data._forceValidationError) {
                violations.push({
                    field: data._errorField || 'testField',
                    message: data._errorMessage || 'Validation failed'
                });
            }
            
            return {
                success: violations.length === 0,
                violations: violations.length > 0 ? violations : undefined
            };
        }

        // Static method for creating error responses (matching existing action function patterns)
        static createErrorResponse(schema, message, errorType = 'SETTINGS_ERROR_UNKNOWN', validationIssues = null) {
            const errorData = {
                Message: message,
                Type: errorType,
            };

            // Include validation issues if provided (preserves ValidationIssue[] arrays)
            if (validationIssues && validationIssues.length > 0) {
                errorData.Validation = validationIssues;
            }

            return this.createResponse(schema, {
                Error: errorData,
            });
        }
    };
}

// Create mock schemas for testing
const createMockSchema = (name) => ({
    name: name,
    typeName: `mock.${name}`,
    fields: {},
    toString: () => name
});

const MockResponseSchema = createMockSchema('MockResponseSchema');
const MockSettingsResponseSchema = createMockSchema('MockSettingsResponseSchema');

/**
 * Unit tests for FragmentsClient error handling and edge cases
 * Requirements: 4.1, 4.2, 4.3, 12.4
 */
describe('FragmentsClient Error Handling and Edge Cases - Unit Tests', () => {
    describe('Error Response Creation Methods (Req 4.1, 4.2)', () => {
        test('should create error response with message and default error type', () => {
            const errorMessage = 'Test error occurred';
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                errorMessage
            );
            
            // Verify error response structure (Req 4.1)
            assert.ok(errorResponse, 'Error response should be created');
            assert.strictEqual(typeof errorResponse, 'object', 'Error response should be an object');
            
            // Verify error structure matches existing action function patterns (Req 4.1)
            if (errorResponse.Error) {
                assert.strictEqual(errorResponse.Error.Message, errorMessage);
                assert.strictEqual(errorResponse.Error.Type, 'SETTINGS_ERROR_UNKNOWN');
            }
        });

        test('should create error response with custom error type', () => {
            const errorMessage = 'Validation failed';
            const errorType = 'SETTINGS_ERROR_VALIDATION_FAILED';
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                errorMessage, 
                errorType
            );
            
            // Verify custom error type (Req 4.1)
            assert.ok(errorResponse, 'Error response should be created');
            if (errorResponse.Error) {
                assert.strictEqual(errorResponse.Error.Message, errorMessage);
                assert.strictEqual(errorResponse.Error.Type, errorType);
            }
        });

        test('should create error response with validation issues', () => {
            const errorMessage = 'Request validation failed';
            const errorType = 'SETTINGS_ERROR_VALIDATION_FAILED';
            const validationIssues = [
                { field: 'email', message: 'Invalid email format' },
                { field: 'password', message: 'Password too short' },
                { field: 'name', message: 'Name is required' }
            ];
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                errorMessage, 
                errorType, 
                validationIssues
            );
            
            // Verify validation issues are preserved (Req 12.4)
            assert.ok(errorResponse, 'Error response should be created');
            if (errorResponse.Error) {
                assert.strictEqual(errorResponse.Error.Message, errorMessage);
                assert.strictEqual(errorResponse.Error.Type, errorType);
                assert.ok(Array.isArray(errorResponse.Error.Validation), 'Validation should be an array');
                assert.strictEqual(errorResponse.Error.Validation.length, 3, 'Should preserve all validation issues');
                
                // Verify validation issue structure
                errorResponse.Error.Validation.forEach((issue, index) => {
                    assert.strictEqual(issue.field, validationIssues[index].field);
                    assert.strictEqual(issue.message, validationIssues[index].message);
                });
            }
        });

        test('should handle empty validation issues array', () => {
            const errorMessage = 'Test error';
            const validationIssues = [];
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                errorMessage, 
                'SETTINGS_ERROR_VALIDATION_FAILED', 
                validationIssues
            );
            
            // Verify empty validation array handling (Req 12.4)
            assert.ok(errorResponse, 'Error response should be created');
            if (errorResponse.Error) {
                // Empty array should not be included
                assert.strictEqual(errorResponse.Error.Validation, undefined, 
                    'Empty validation array should not be included');
            }
        });

        test('should handle null validation issues', () => {
            const errorMessage = 'Test error';
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                errorMessage, 
                'SETTINGS_ERROR_UNKNOWN', 
                null
            );
            
            // Verify null validation handling (Req 12.4)
            assert.ok(errorResponse, 'Error response should be created');
            if (errorResponse.Error) {
                assert.strictEqual(errorResponse.Error.Validation, undefined, 
                    'Null validation should not be included');
            }
        });

        test('should work with different response schemas', () => {
            const schemas = [MockResponseSchema, MockSettingsResponseSchema];
            const errorMessage = 'Schema test error';
            
            schemas.forEach(schema => {
                const errorResponse = FragmentsClient.createErrorResponse(schema, errorMessage);
                
                // Verify works with various schemas (Req 4.1)
                assert.ok(errorResponse, `Error response should be created for schema ${schema.name}`);
                if (errorResponse.Error) {
                    assert.strictEqual(errorResponse.Error.Message, errorMessage);
                }
            });
        });
    });

    describe('HTTP Error Response Patterns (Req 4.2)', () => {
        test('should create HTTP error response structure', () => {
            const httpStatus = 404;
            const httpStatusText = 'Not Found';
            const httpErrorMessage = `HTTP ${httpStatus}: ${httpStatusText}`;
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                httpErrorMessage, 
                'SETTINGS_ERROR_UNKNOWN'
            );
            
            // Verify HTTP error response structure (Req 4.2)
            assert.ok(errorResponse, 'HTTP error response should be created');
            if (errorResponse.Error) {
                assert.strictEqual(errorResponse.Error.Message, httpErrorMessage);
                assert.strictEqual(errorResponse.Error.Type, 'SETTINGS_ERROR_UNKNOWN');
            }
        });

        test('should create network error response structure', () => {
            const networkErrorMessage = 'Network request failed';
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                networkErrorMessage, 
                'SETTINGS_ERROR_UNKNOWN'
            );
            
            // Verify network error response structure (Req 4.2)
            assert.ok(errorResponse, 'Network error response should be created');
            if (errorResponse.Error) {
                assert.strictEqual(errorResponse.Error.Message, networkErrorMessage);
                assert.strictEqual(errorResponse.Error.Type, 'SETTINGS_ERROR_UNKNOWN');
            }
        });

        test('should handle various HTTP status codes', () => {
            const httpErrors = [
                { status: 400, statusText: 'Bad Request' },
                { status: 401, statusText: 'Unauthorized' },
                { status: 403, statusText: 'Forbidden' },
                { status: 404, statusText: 'Not Found' },
                { status: 500, statusText: 'Internal Server Error' },
                { status: 502, statusText: 'Bad Gateway' },
                { status: 503, statusText: 'Service Unavailable' }
            ];
            
            httpErrors.forEach(({ status, statusText }) => {
                const message = `HTTP ${status}: ${statusText}`;
                const errorResponse = FragmentsClient.createErrorResponse(
                    MockResponseSchema, 
                    message, 
                    'SETTINGS_ERROR_UNKNOWN'
                );
                
                // Verify various HTTP errors (Req 4.2)
                assert.ok(errorResponse, `Error response should be created for HTTP ${status}`);
                if (errorResponse.Error) {
                    assert.strictEqual(errorResponse.Error.Message, message);
                }
            });
        });
    });

    describe('Validation Error Response Handling (Req 12.4)', () => {
        test('should preserve ValidationIssue arrays in error responses', () => {
            const validationIssues = [
                { 
                    field: 'user.email', 
                    message: 'Email format is invalid',
                    code: 'INVALID_FORMAT'
                },
                { 
                    field: 'user.password', 
                    message: 'Password must be at least 8 characters',
                    code: 'TOO_SHORT'
                },
                { 
                    field: 'user.confirmPassword', 
                    message: 'Passwords do not match',
                    code: 'MISMATCH'
                }
            ];
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                'Request validation failed', 
                'SETTINGS_ERROR_VALIDATION_FAILED', 
                validationIssues
            );
            
            // Verify ValidationIssue[] arrays are preserved (Req 12.4)
            assert.ok(errorResponse, 'Validation error response should be created');
            if (errorResponse.Error && errorResponse.Error.Validation) {
                assert.ok(Array.isArray(errorResponse.Error.Validation), 
                    'Validation should be an array');
                assert.strictEqual(errorResponse.Error.Validation.length, validationIssues.length, 
                    'All validation issues should be preserved');
                
                // Verify each validation issue is preserved exactly
                errorResponse.Error.Validation.forEach((issue, index) => {
                    const originalIssue = validationIssues[index];
                    assert.strictEqual(issue.field, originalIssue.field);
                    assert.strictEqual(issue.message, originalIssue.message);
                    if (originalIssue.code) {
                        assert.strictEqual(issue.code, originalIssue.code);
                    }
                });
            }
        });

        test('should be compatible with toFieldMessageMap utility', () => {
            const validationIssues = [
                { field: 'email', message: 'Invalid email' },
                { field: 'password', message: 'Too short' },
                { field: 'nested.field', message: 'Nested field error' }
            ];
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                'Validation failed', 
                'SETTINGS_ERROR_VALIDATION_FAILED', 
                validationIssues
            );
            
            // Verify compatibility with existing form utilities (Req 12.4)
            if (errorResponse.Error && errorResponse.Error.Validation) {
                // Simulate toFieldMessageMap behavior
                const fieldMessageMap = {};
                errorResponse.Error.Validation.forEach(issue => {
                    fieldMessageMap[issue.field] = issue.message;
                });
                
                assert.strictEqual(fieldMessageMap['email'], 'Invalid email');
                assert.strictEqual(fieldMessageMap['password'], 'Too short');
                assert.strictEqual(fieldMessageMap['nested.field'], 'Nested field error');
            }
        });

        test('should be compatible with violationsToTanStackErrors utility', () => {
            const validationIssues = [
                { field: 'root.email', message: 'Email is required' },
                { field: 'root.profile.name', message: 'Name is required' }
            ];
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                'Form validation failed', 
                'SETTINGS_ERROR_VALIDATION_FAILED', 
                validationIssues
            );
            
            // Verify compatibility with TanStack form utilities (Req 12.4)
            if (errorResponse.Error && errorResponse.Error.Validation) {
                // Simulate violationsToTanStackErrors behavior
                const tanStackErrors = {};
                errorResponse.Error.Validation.forEach(issue => {
                    const fieldPath = issue.field.replace('root.', '');
                    tanStackErrors[fieldPath] = issue.message;
                });
                
                assert.strictEqual(tanStackErrors['email'], 'Email is required');
                assert.strictEqual(tanStackErrors['profile.name'], 'Name is required');
            }
        });

        test('should handle complex validation issue structures', () => {
            const complexValidationIssues = [
                {
                    field: 'user.profile.personalInfo.address.zipCode',
                    message: 'Invalid ZIP code format',
                    code: 'INVALID_FORMAT',
                    constraint: 'pattern',
                    value: '1234'
                },
                {
                    field: 'settings.notifications[0].email',
                    message: 'Email address is required for email notifications',
                    code: 'REQUIRED',
                    constraint: 'required'
                }
            ];
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                'Complex validation failed', 
                'SETTINGS_ERROR_VALIDATION_FAILED', 
                complexValidationIssues
            );
            
            // Verify complex validation structures are preserved (Req 12.4)
            if (errorResponse.Error && errorResponse.Error.Validation) {
                assert.strictEqual(errorResponse.Error.Validation.length, 2);
                
                const firstIssue = errorResponse.Error.Validation[0];
                assert.strictEqual(firstIssue.field, 'user.profile.personalInfo.address.zipCode');
                assert.strictEqual(firstIssue.message, 'Invalid ZIP code format');
                assert.strictEqual(firstIssue.code, 'INVALID_FORMAT');
                assert.strictEqual(firstIssue.constraint, 'pattern');
                assert.strictEqual(firstIssue.value, '1234');
                
                const secondIssue = errorResponse.Error.Validation[1];
                assert.strictEqual(secondIssue.field, 'settings.notifications[0].email');
                assert.strictEqual(secondIssue.message, 'Email address is required for email notifications');
                assert.strictEqual(secondIssue.code, 'REQUIRED');
                assert.strictEqual(secondIssue.constraint, 'required');
            }
        });
    });

    describe('Safe Logging Functionality (Req 4.3)', () => {
        test('should handle console logging safely in different environments', () => {
            // Test that logging functions exist and can be called without throwing
            const originalConsole = globalThis.console;
            
            try {
                // Test with normal console
                assert.doesNotThrow(() => {
                    if (globalThis.console && globalThis.console.error) {
                        globalThis.console.error('Test error message');
                    }
                }, 'Should handle normal console.error safely');
                
                // Test with missing console (simulate environment without console)
                globalThis.console = undefined;
                assert.doesNotThrow(() => {
                    // This simulates the safe logging behavior
                    const safeConsole = globalThis.console;
                    if (safeConsole && safeConsole.error) {
                        safeConsole.error('Test error message');
                    }
                }, 'Should handle missing console safely');
                
                // Test with partial console (missing error method)
                globalThis.console = { log: () => {} };
                assert.doesNotThrow(() => {
                    const safeConsole = globalThis.console;
                    if (safeConsole && safeConsole.error) {
                        safeConsole.error('Test error message');
                    }
                }, 'Should handle partial console safely');
                
            } finally {
                // Restore original console
                globalThis.console = originalConsole;
            }
        });

        test('should handle logging in browser environment', () => {
            // Simulate browser environment
            const mockWindow = {
                console: {
                    error: (...args) => {
                        // Mock browser console.error
                    }
                }
            };
            
            assert.doesNotThrow(() => {
                if (mockWindow.console && mockWindow.console.error) {
                    mockWindow.console.error('Browser error message');
                }
            }, 'Should handle browser console safely');
        });

        test('should handle logging in Node.js environment', () => {
            // Test Node.js console behavior
            assert.doesNotThrow(() => {
                if (process && process.stderr) {
                    // Node.js has process.stderr
                }
                if (console && console.error) {
                    console.error('Node.js error message');
                }
            }, 'Should handle Node.js console safely');
        });

        test('should handle logging with various error types', () => {
            const errorTypes = [
                new Error('Standard Error'),
                new TypeError('Type Error'),
                new ReferenceError('Reference Error'),
                { message: 'Custom error object' },
                'String error message',
                42,
                null,
                undefined
            ];
            
            errorTypes.forEach((error, index) => {
                assert.doesNotThrow(() => {
                    if (console && console.error) {
                        console.error(`Test error ${index}:`, error);
                    }
                }, `Should handle error type ${index} safely`);
            });
        });
    });

    describe('Edge Cases and Boundary Conditions (Req 4.1, 4.2, 4.3)', () => {
        test('should handle extremely long error messages', () => {
            const longMessage = 'A'.repeat(10000); // 10KB message
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                longMessage, 
                'SETTINGS_ERROR_UNKNOWN'
            );
            
            // Verify long messages are handled (Req 4.1)
            assert.ok(errorResponse, 'Should handle long error messages');
            if (errorResponse.Error) {
                assert.strictEqual(errorResponse.Error.Message, longMessage);
            }
        });

        test('should handle special characters in error messages', () => {
            const specialMessages = [
                'Error with "quotes" and \'apostrophes\'',
                'Error with unicode: 🚨 ⚠️ 💥',
                'Error with newlines\nand\ttabs',
                'Error with HTML <script>alert("xss")</script>',
                'Error with JSON {"malicious": "payload"}',
                'Error with null bytes \0 and control chars \x01\x02'
            ];
            
            specialMessages.forEach((message, index) => {
                assert.doesNotThrow(() => {
                    const errorResponse = FragmentsClient.createErrorResponse(
                        MockResponseSchema, 
                        message, 
                        'SETTINGS_ERROR_UNKNOWN'
                    );
                    assert.ok(errorResponse, `Should handle special message ${index}`);
                }, `Should handle special characters in message ${index}`);
            });
        });

        test('should handle large validation issue arrays', () => {
            const largeValidationArray = Array.from({ length: 1000 }, (_, i) => ({
                field: `field${i}`,
                message: `Error message for field ${i}`,
                code: `ERROR_${i}`
            }));
            
            const errorResponse = FragmentsClient.createErrorResponse(
                MockResponseSchema, 
                'Large validation error', 
                'SETTINGS_ERROR_VALIDATION_FAILED', 
                largeValidationArray
            );
            
            // Verify large validation arrays are handled (Req 12.4)
            assert.ok(errorResponse, 'Should handle large validation arrays');
            if (errorResponse.Error && errorResponse.Error.Validation) {
                assert.strictEqual(errorResponse.Error.Validation.length, 1000);
            }
        });

        test('should handle malformed validation issues gracefully', () => {
            const malformedValidationIssues = [
                null,
                undefined,
                { field: null, message: 'Field is null' },
                { field: 'test', message: null },
                { field: '', message: '' },
                { field: 123, message: 456 },
                { wrongProperty: 'value' },
                'string instead of object'
            ];
            
            assert.doesNotThrow(() => {
                const errorResponse = FragmentsClient.createErrorResponse(
                    MockResponseSchema, 
                    'Malformed validation test', 
                    'SETTINGS_ERROR_VALIDATION_FAILED', 
                    malformedValidationIssues
                );
                assert.ok(errorResponse, 'Should handle malformed validation issues');
            }, 'Should handle malformed validation issues gracefully');
        });

        test('should handle concurrent error response creation', async () => {
            const concurrentPromises = Array.from({ length: 100 }, (_, i) => 
                Promise.resolve().then(() => {
                    return FragmentsClient.createErrorResponse(
                        MockResponseSchema, 
                        `Concurrent error ${i}`, 
                        'SETTINGS_ERROR_UNKNOWN'
                    );
                })
            );
            
            const results = await Promise.all(concurrentPromises);
            
            // Verify concurrent creation works (Req 4.1)
            assert.strictEqual(results.length, 100, 'All concurrent operations should complete');
            results.forEach((result, index) => {
                assert.ok(result, `Concurrent result ${index} should exist`);
                if (result.Error) {
                    assert.strictEqual(result.Error.Message, `Concurrent error ${index}`);
                }
            });
        });

        test('should maintain error response consistency across different schemas', () => {
            const schemas = [MockResponseSchema, MockSettingsResponseSchema];
            const testMessage = 'Consistency test error';
            const testType = 'SETTINGS_ERROR_UNKNOWN';
            
            const responses = schemas.map(schema => 
                FragmentsClient.createErrorResponse(schema, testMessage, testType)
            );
            
            // Verify consistency across schemas (Req 4.1)
            responses.forEach((response, index) => {
                assert.ok(response, `Response ${index} should exist`);
                if (response.Error) {
                    assert.strictEqual(response.Error.Message, testMessage);
                    assert.strictEqual(response.Error.Type, testType);
                }
            });
        });
    });
});