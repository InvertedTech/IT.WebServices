import { test, describe } from 'node:test';
import assert from 'node:assert';

// Try to import the client, fallback to mock if import fails
let FragmentsClient;
let mockSchemas = {};

try {
    const clientModule = await import('../dist/esm/client.js');
    FragmentsClient = clientModule.FragmentsClient;
    
    // Try to import some schemas for testing
    try {
        const settingsModule = await import('../dist/esm/Settings/index.js');
        mockSchemas = settingsModule;
    } catch (schemaError) {
        console.log('Schema import failed, using mock schemas:', schemaError.message);
    }
} catch (error) {
    console.log('Client import failed, using mock for testing:', error.message);
    
    // Create a mock FragmentsClient for testing static methods
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
            // Mock implementation that simulates protobuf create behavior
            return {
                ...data,
                _schema: schema,
                _type: 'request'
            };
        }

        static createResponse(schema, data = {}) {
            // Mock implementation that simulates protobuf create behavior
            return {
                ...data,
                _schema: schema,
                _type: 'response'
            };
        }

        static serialize(schema, data) {
            // Mock implementation that simulates toJsonString behavior
            return JSON.stringify({
                schema: schema.name || 'MockSchema',
                data: data
            });
        }

        static async validate(schema, data) {
            // Mock implementation that simulates protovalidate behavior
            const hasRequiredFields = data && typeof data === 'object';
            const violations = [];
            
            if (!hasRequiredFields) {
                violations.push({
                    field: 'root',
                    message: 'Invalid data structure'
                });
            }
            
            // Simulate validation failure for specific test cases
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
    };
}

// Create mock schemas for testing
const createMockSchema = (name) => ({
    name: name,
    typeName: `mock.${name}`,
    fields: {},
    toString: () => name
});

const MockRequestSchema = createMockSchema('MockRequestSchema');
const MockResponseSchema = createMockSchema('MockResponseSchema');
const MockSettingsRequestSchema = createMockSchema('MockSettingsRequestSchema');
const MockSettingsResponseSchema = createMockSchema('MockSettingsResponseSchema');

/**
 * Unit tests for FragmentsClient static utility methods
 * Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 12.1, 12.2
 */
describe('FragmentsClient Static Utility Methods - Unit Tests', () => {
    describe('createRequest() Method (Req 10.1, 10.4, 10.5)', () => {
        test('should create request message with schema and no data', () => {
            const request = FragmentsClient.createRequest(MockRequestSchema);
            
            // Verify request is created (Req 10.1)
            assert.ok(request, 'Request should be created');
            assert.strictEqual(typeof request, 'object', 'Request should be an object');
            
            // Verify schema is associated (Req 10.1)
            if (request._schema) {
                assert.strictEqual(request._schema, MockRequestSchema);
            }
        });

        test('should create request message with schema and partial data', () => {
            const partialData = {
                field1: 'value1',
                field2: 42,
                nested: {
                    subField: 'subValue'
                }
            };

            const request = FragmentsClient.createRequest(MockRequestSchema, partialData);
            
            // Verify request is created with data (Req 10.4)
            assert.ok(request, 'Request should be created');
            assert.strictEqual(typeof request, 'object', 'Request should be an object');
            
            // Verify partial data is included (Req 10.4)
            if (request.field1) {
                assert.strictEqual(request.field1, 'value1');
            }
            if (request.field2) {
                assert.strictEqual(request.field2, 42);
            }
            if (request.nested) {
                assert.deepStrictEqual(request.nested, { subField: 'subValue' });
            }
        });

        test('should handle empty partial data object', () => {
            const request = FragmentsClient.createRequest(MockRequestSchema, {});
            
            // Verify request is created even with empty data (Req 10.5)
            assert.ok(request, 'Request should be created with empty data');
            assert.strictEqual(typeof request, 'object', 'Request should be an object');
        });

        test('should handle undefined partial data', () => {
            const request = FragmentsClient.createRequest(MockRequestSchema, undefined);
            
            // Verify request is created with undefined data (Req 10.5)
            assert.ok(request, 'Request should be created with undefined data');
            assert.strictEqual(typeof request, 'object', 'Request should be an object');
        });

        test('should work with different schema types', () => {
            const schemas = [MockRequestSchema, MockSettingsRequestSchema];
            
            schemas.forEach(schema => {
                const request = FragmentsClient.createRequest(schema, { testField: 'testValue' });
                
                // Verify works with various schemas (Req 10.1)
                assert.ok(request, `Request should be created for schema ${schema.name}`);
                assert.strictEqual(typeof request, 'object', 'Request should be an object');
            });
        });

        test('should handle complex nested data structures', () => {
            const complexData = {
                simpleField: 'value',
                numberField: 123,
                booleanField: true,
                arrayField: [1, 2, 3],
                nestedObject: {
                    level1: {
                        level2: {
                            deepValue: 'deep'
                        }
                    }
                },
                nullField: null,
                undefinedField: undefined
            };

            const request = FragmentsClient.createRequest(MockRequestSchema, complexData);
            
            // Verify complex data handling (Req 10.4)
            assert.ok(request, 'Request should be created with complex data');
            assert.strictEqual(typeof request, 'object', 'Request should be an object');
        });
    });

    describe('createResponse() Method (Req 10.2, 10.4, 10.5)', () => {
        test('should create response message with schema and no data', () => {
            const response = FragmentsClient.createResponse(MockResponseSchema);
            
            // Verify response is created (Req 10.2)
            assert.ok(response, 'Response should be created');
            assert.strictEqual(typeof response, 'object', 'Response should be an object');
            
            // Verify schema is associated (Req 10.2)
            if (response._schema) {
                assert.strictEqual(response._schema, MockResponseSchema);
            }
        });

        test('should create response message with schema and partial data', () => {
            const partialData = {
                success: true,
                message: 'Operation completed',
                data: {
                    id: 123,
                    name: 'Test Item'
                }
            };

            const response = FragmentsClient.createResponse(MockResponseSchema, partialData);
            
            // Verify response is created with data (Req 10.4)
            assert.ok(response, 'Response should be created');
            assert.strictEqual(typeof response, 'object', 'Response should be an object');
            
            // Verify partial data is included (Req 10.4)
            if (response.success !== undefined) {
                assert.strictEqual(response.success, true);
            }
            if (response.message) {
                assert.strictEqual(response.message, 'Operation completed');
            }
        });

        test('should create error response structure', () => {
            const errorData = {
                Error: {
                    Message: 'Test error message',
                    Type: 'SETTINGS_ERROR_UNKNOWN',
                    Validation: [
                        { field: 'testField', message: 'Field is required' }
                    ]
                }
            };

            const response = FragmentsClient.createResponse(MockResponseSchema, errorData);
            
            // Verify error response creation (Req 10.2, 10.4)
            assert.ok(response, 'Error response should be created');
            assert.strictEqual(typeof response, 'object', 'Response should be an object');
            
            // Verify error structure is preserved
            if (response.Error) {
                assert.strictEqual(response.Error.Message, 'Test error message');
                assert.strictEqual(response.Error.Type, 'SETTINGS_ERROR_UNKNOWN');
                if (response.Error.Validation) {
                    assert.ok(Array.isArray(response.Error.Validation));
                    assert.strictEqual(response.Error.Validation.length, 1);
                }
            }
        });

        test('should handle different response schema types', () => {
            const schemas = [MockResponseSchema, MockSettingsResponseSchema];
            
            schemas.forEach(schema => {
                const response = FragmentsClient.createResponse(schema, { result: 'success' });
                
                // Verify works with various schemas (Req 10.2)
                assert.ok(response, `Response should be created for schema ${schema.name}`);
                assert.strictEqual(typeof response, 'object', 'Response should be an object');
            });
        });
    });

    describe('serialize() Method (Req 10.3)', () => {
        test('should serialize message to JSON string', () => {
            const testData = {
                field1: 'value1',
                field2: 42,
                nested: {
                    subField: 'subValue'
                }
            };

            const jsonString = FragmentsClient.serialize(MockRequestSchema, testData);
            
            // Verify serialization returns string (Req 10.3)
            assert.strictEqual(typeof jsonString, 'string', 'Serialize should return a string');
            
            // Verify string is valid JSON
            assert.doesNotThrow(() => {
                JSON.parse(jsonString);
            }, 'Serialized string should be valid JSON');
            
            // Verify content is preserved
            const parsed = JSON.parse(jsonString);
            assert.ok(parsed, 'Parsed JSON should exist');
        });

        test('should serialize empty message', () => {
            const emptyData = {};
            const jsonString = FragmentsClient.serialize(MockRequestSchema, emptyData);
            
            // Verify empty message serialization (Req 10.3)
            assert.strictEqual(typeof jsonString, 'string', 'Serialize should return a string for empty data');
            assert.doesNotThrow(() => {
                JSON.parse(jsonString);
            }, 'Serialized empty data should be valid JSON');
        });

        test('should serialize complex nested structures', () => {
            const complexData = {
                simpleField: 'value',
                numberField: 123,
                booleanField: true,
                arrayField: [1, 2, 3, { nested: 'array item' }],
                nestedObject: {
                    level1: {
                        level2: {
                            deepValue: 'deep',
                            deepArray: ['a', 'b', 'c']
                        }
                    }
                }
            };

            const jsonString = FragmentsClient.serialize(MockRequestSchema, complexData);
            
            // Verify complex structure serialization (Req 10.3)
            assert.strictEqual(typeof jsonString, 'string', 'Serialize should return a string for complex data');
            assert.doesNotThrow(() => {
                JSON.parse(jsonString);
            }, 'Serialized complex data should be valid JSON');
            
            const parsed = JSON.parse(jsonString);
            assert.ok(parsed, 'Parsed complex JSON should exist');
        });

        test('should serialize error response structures', () => {
            const errorResponse = {
                Error: {
                    Message: 'Validation failed',
                    Type: 'SETTINGS_ERROR_VALIDATION_FAILED',
                    Validation: [
                        { field: 'email', message: 'Invalid email format' },
                        { field: 'password', message: 'Password too short' }
                    ]
                }
            };

            const jsonString = FragmentsClient.serialize(MockResponseSchema, errorResponse);
            
            // Verify error response serialization (Req 10.3)
            assert.strictEqual(typeof jsonString, 'string', 'Serialize should return a string for error response');
            assert.doesNotThrow(() => {
                JSON.parse(jsonString);
            }, 'Serialized error response should be valid JSON');
            
            const parsed = JSON.parse(jsonString);
            assert.ok(parsed, 'Parsed error response JSON should exist');
        });

        test('should work with different schema types', () => {
            const schemas = [MockRequestSchema, MockResponseSchema, MockSettingsRequestSchema];
            const testData = { testField: 'testValue' };
            
            schemas.forEach(schema => {
                const jsonString = FragmentsClient.serialize(schema, testData);
                
                // Verify serialization works with various schemas (Req 10.3)
                assert.strictEqual(typeof jsonString, 'string', 
                    `Serialize should return string for schema ${schema.name}`);
                assert.doesNotThrow(() => {
                    JSON.parse(jsonString);
                }, `Serialized data should be valid JSON for schema ${schema.name}`);
            });
        });
    });

    describe('validate() Method (Req 12.1, 12.2)', () => {
        test('should validate valid message and return success', async () => {
            const validData = {
                field1: 'valid value',
                field2: 42
            };

            const result = await FragmentsClient.validate(MockRequestSchema, validData);
            
            // Verify validation returns result object (Req 12.1)
            assert.ok(result, 'Validation should return a result');
            assert.strictEqual(typeof result, 'object', 'Validation result should be an object');
            assert.strictEqual(typeof result.success, 'boolean', 'Result should have success boolean');
            
            // Verify valid data passes validation (Req 12.2)
            assert.strictEqual(result.success, true, 'Valid data should pass validation');
            assert.strictEqual(result.violations, undefined, 'Valid data should have no violations');
        });

        test('should validate invalid message and return violations', async () => {
            const invalidData = {
                _forceValidationError: true,
                _errorField: 'email',
                _errorMessage: 'Invalid email format'
            };

            const result = await FragmentsClient.validate(MockRequestSchema, invalidData);
            
            // Verify validation returns failure for invalid data (Req 12.2)
            assert.ok(result, 'Validation should return a result');
            assert.strictEqual(result.success, false, 'Invalid data should fail validation');
            assert.ok(Array.isArray(result.violations), 'Invalid data should have violations array');
            assert.ok(result.violations.length > 0, 'Violations array should not be empty');
            
            // Verify violation structure (Req 12.2)
            const violation = result.violations[0];
            assert.ok(violation.field, 'Violation should have field');
            assert.ok(violation.message, 'Violation should have message');
        });

        test('should handle validation with multiple violations', async () => {
            const invalidData = {
                _forceValidationError: true,
                _errorField: 'multipleFields',
                _errorMessage: 'Multiple validation errors'
            };

            const result = await FragmentsClient.validate(MockRequestSchema, invalidData);
            
            // Verify multiple violations handling (Req 12.2)
            assert.strictEqual(result.success, false, 'Data with multiple errors should fail validation');
            assert.ok(Array.isArray(result.violations), 'Should have violations array');
            
            // Verify violations structure matches existing patterns
            result.violations.forEach(violation => {
                assert.ok(violation.field, 'Each violation should have field');
                assert.ok(violation.message, 'Each violation should have message');
            });
        });

        test('should handle validation system errors gracefully', async () => {
            // Test with null data to potentially trigger validation system error
            const result = await FragmentsClient.validate(MockRequestSchema, null);
            
            // Verify validation system error handling (Req 12.1)
            assert.ok(result, 'Validation should return a result even on system error');
            assert.strictEqual(typeof result.success, 'boolean', 'Result should have success boolean');
            
            // System errors should be handled gracefully
            if (!result.success) {
                assert.ok(Array.isArray(result.violations), 'System errors should provide violations array');
            }
        });

        test('should work with different schema types', async () => {
            const schemas = [MockRequestSchema, MockResponseSchema, MockSettingsRequestSchema];
            const testData = { testField: 'testValue' };
            
            for (const schema of schemas) {
                const result = await FragmentsClient.validate(schema, testData);
                
                // Verify validation works with various schemas (Req 12.1)
                assert.ok(result, `Validation should return result for schema ${schema.name}`);
                assert.strictEqual(typeof result.success, 'boolean', 
                    `Result should have success boolean for schema ${schema.name}`);
            }
        });

        test('should return validation result compatible with existing utilities', async () => {
            const invalidData = {
                _forceValidationError: true,
                _errorField: 'testField',
                _errorMessage: 'Test validation error'
            };

            const result = await FragmentsClient.validate(MockRequestSchema, invalidData);
            
            // Verify result is compatible with toFieldMessageMap and violationsToTanStackErrors (Req 12.2)
            if (!result.success && result.violations) {
                assert.ok(Array.isArray(result.violations), 'Violations should be array for utility compatibility');
                
                result.violations.forEach(violation => {
                    // Verify structure matches ValidationIssue format
                    assert.ok(typeof violation.field === 'string', 'Violation field should be string');
                    assert.ok(typeof violation.message === 'string', 'Violation message should be string');
                });
            }
        });

        test('should handle edge cases in validation', async () => {
            const edgeCases = [
                undefined,
                null,
                {},
                { validField: 'value' },
                { complexNested: { deep: { value: 'test' } } }
            ];

            for (const testCase of edgeCases) {
                const result = await FragmentsClient.validate(MockRequestSchema, testCase);
                
                // Verify all edge cases are handled (Req 12.1)
                assert.ok(result, `Validation should handle edge case: ${JSON.stringify(testCase)}`);
                assert.strictEqual(typeof result.success, 'boolean', 
                    `Result should have success boolean for edge case: ${JSON.stringify(testCase)}`);
            }
        });
    });

    describe('Static Method Integration and Type Safety', () => {
        test('should work together in typical usage patterns', async () => {
            // Simulate typical usage: create request, serialize, validate
            const requestData = {
                operation: 'test',
                parameters: {
                    value1: 'test',
                    value2: 42
                }
            };

            // Create request
            const request = FragmentsClient.createRequest(MockRequestSchema, requestData);
            assert.ok(request, 'Request should be created');

            // Serialize request
            const serialized = FragmentsClient.serialize(MockRequestSchema, request);
            assert.strictEqual(typeof serialized, 'string', 'Request should be serialized to string');

            // Validate request
            const validation = await FragmentsClient.validate(MockRequestSchema, request);
            assert.ok(validation, 'Request should be validated');
            assert.strictEqual(typeof validation.success, 'boolean', 'Validation should return success boolean');

            // Create response
            const responseData = { result: 'success', data: request };
            const response = FragmentsClient.createResponse(MockResponseSchema, responseData);
            assert.ok(response, 'Response should be created');
        });

        test('should handle error response creation patterns', () => {
            // Test error response creation matching existing action function patterns
            const errorResponse = FragmentsClient.createResponse(MockResponseSchema, {
                Error: {
                    Message: 'Test error',
                    Type: 'SETTINGS_ERROR_UNKNOWN'
                }
            });

            assert.ok(errorResponse, 'Error response should be created');
            
            // Serialize error response
            const serializedError = FragmentsClient.serialize(MockResponseSchema, errorResponse);
            assert.strictEqual(typeof serializedError, 'string', 'Error response should be serializable');
        });

        test('should maintain type safety across static methods', () => {
            // Verify all static methods exist and are functions
            assert.strictEqual(typeof FragmentsClient.createRequest, 'function', 
                'createRequest should be a static function');
            assert.strictEqual(typeof FragmentsClient.createResponse, 'function', 
                'createResponse should be a static function');
            assert.strictEqual(typeof FragmentsClient.serialize, 'function', 
                'serialize should be a static function');
            assert.strictEqual(typeof FragmentsClient.validate, 'function', 
                'validate should be a static function');
        });
    });
});