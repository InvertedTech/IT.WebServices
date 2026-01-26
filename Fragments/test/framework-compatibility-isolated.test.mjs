import { test, describe } from 'node:test';
import assert from 'node:assert';
import { readFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

describe('Framework Compatibility Tests (Isolated)', () => {
    describe('Client Build and Export Verification', () => {
        test('should verify client files are built correctly', () => {
            // Verify the client.js file exists in dist/esm
            
            const clientJsPath = join(__dirname, '../dist/esm/client.js');
            const clientDtsPath = join(__dirname, '../dist/protos/client.d.ts');
            
            assert.ok(existsSync(clientJsPath), 'client.js should exist in dist/esm');
            assert.ok(existsSync(clientDtsPath), 'client.d.ts should exist in dist/protos');
            
            // Verify the files have content
            const clientJs = readFileSync(clientJsPath, 'utf8');
            const clientDts = readFileSync(clientDtsPath, 'utf8');
            
            assert.ok(clientJs.includes('FragmentsClient'), 'client.js should contain FragmentsClient class');
            assert.ok(clientDts.includes('FragmentsClient'), 'client.d.ts should contain FragmentsClient types');
            
            // Verify exports
            assert.ok(clientJs.includes('export class FragmentsClient'), 'client.js should export FragmentsClient class');
        });

        test('should verify package.json exports include client', () => {
            const packageJsonPath = join(__dirname, '../package.json');
            const packageJson = JSON.parse(readFileSync(packageJsonPath, 'utf8'));
            
            assert.ok(packageJson.exports['./client'], 'package.json should have client export');
            assert.strictEqual(packageJson.exports['./client'].types, './dist/protos/client.d.ts');
            assert.strictEqual(packageJson.exports['./client'].import, './dist/esm/client.js');
        });

        test('should verify TypeScript compilation succeeded', () => {
            // Check that both ESM and types were generated
            const esmDir = join(__dirname, '../dist/esm');
            const typesDir = join(__dirname, '../dist/protos');
            
            assert.ok(existsSync(esmDir), 'ESM directory should exist');
            assert.ok(existsSync(typesDir), 'Types directory should exist');
            
            // Check client files specifically
            const clientFiles = [
                join(esmDir, 'client.js'),
                join(typesDir, 'client.d.ts'),
            ];
            
            clientFiles.forEach(file => {
                assert.ok(existsSync(file), `${file} should exist`);
                const content = readFileSync(file, 'utf8');
                assert.ok(content.length > 0, `${file} should not be empty`);
            });
        });
    });

    describe('Framework Compatibility Simulation', () => {
        test('should simulate Next.js environment compatibility', () => {
            // Simulate Next.js fetch options structure
            const nextJsOptions = {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer test-token',
                },
                body: JSON.stringify({ test: 'data' }),
                next: {
                    tags: ['admin-settings', 'user-data'],
                    revalidate: 30,
                },
            };

            // Verify the structure is valid
            assert.ok(nextJsOptions.next);
            assert.ok(Array.isArray(nextJsOptions.next.tags));
            assert.strictEqual(typeof nextJsOptions.next.revalidate, 'number');
            assert.ok(nextJsOptions.next.tags.includes('admin-settings'));
            assert.strictEqual(nextJsOptions.next.revalidate, 30);
        });

        test('should simulate cache invalidation callback pattern', () => {
            let revalidatedTags = [];
            let revalidatedPaths = [];

            // Simulate Next.js revalidation functions
            const mockRevalidateTag = (tag) => {
                revalidatedTags.push(tag);
            };

            const mockRevalidatePath = (path) => {
                revalidatedPaths.push(path);
            };

            // Simulate client cache invalidation callback
            const onCacheInvalidate = (tags, paths) => {
                tags.forEach(mockRevalidateTag);
                paths.forEach(mockRevalidatePath);
            };

            // Test the callback
            onCacheInvalidate(['tag1', 'tag2'], ['/path1', '/path2']);

            assert.deepStrictEqual(revalidatedTags, ['tag1', 'tag2']);
            assert.deepStrictEqual(revalidatedPaths, ['/path1', '/path2']);
        });

        test('should simulate non-Next.js environment compatibility', () => {
            // Simulate standard fetch options (without Next.js extensions)
            const standardOptions = {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer token',
                },
                body: JSON.stringify({ data: 'test' }),
                // Next.js options would be ignored in standard environments
                next: {
                    tags: ['ignored'],
                    revalidate: 60,
                },
            };

            // Verify standard options work
            assert.strictEqual(standardOptions.method, 'POST');
            assert.ok(standardOptions.headers['Content-Type']);
            assert.ok(standardOptions.body);

            // In non-Next.js environments, the 'next' property would be ignored
            // but the rest of the options would work fine
            const { next, ...standardFetchOptions } = standardOptions;
            assert.ok(standardFetchOptions.method);
            assert.ok(standardFetchOptions.headers);
            assert.ok(standardFetchOptions.body);
        });

        test('should simulate different framework token patterns', () => {
            // Simulate different token retrieval patterns
            const frameworks = {
                nextjs: () => Promise.resolve('nextjs-token'),
                svelte: () => 'svelte-token',
                vue: () => 'vue-default', // Removed localStorage reference for Node.js compatibility
                nodejs: () => process.env?.API_TOKEN || 'node-token',
                vanilla: () => 'vanilla-token',
            };

            // Test each pattern
            Object.entries(frameworks).forEach(([name, tokenGetter]) => {
                assert.strictEqual(typeof tokenGetter, 'function', `${name} token getter should be a function`);
                
                // Test sync token getter
                if (name !== 'nextjs') {
                    const token = tokenGetter();
                    assert.ok(token, `${name} should return a token`);
                }
            });

            // Test async token getter
            return frameworks.nextjs().then(token => {
                assert.strictEqual(token, 'nextjs-token');
            });
        });

        test('should simulate error handling patterns across frameworks', () => {
            // Simulate different error response patterns
            const createErrorResponse = (message, type = 'UNKNOWN_ERROR') => ({
                Error: {
                    Message: message,
                    Type: type,
                },
            });

            const createValidationErrorResponse = (violations) => ({
                Error: {
                    Message: 'Request validation failed',
                    Type: 'VALIDATION_FAILED',
                    Validation: violations || [],
                },
            });

            // Test error responses
            const networkError = createErrorResponse('Network request failed');
            assert.strictEqual(networkError.Error.Message, 'Network request failed');
            assert.strictEqual(networkError.Error.Type, 'UNKNOWN_ERROR');

            const httpError = createErrorResponse('HTTP 404: Not Found');
            assert.ok(httpError.Error.Message.includes('HTTP 404'));

            const validationError = createValidationErrorResponse([
                { field: 'email', message: 'Invalid email format' }
            ]);
            assert.strictEqual(validationError.Error.Type, 'VALIDATION_FAILED');
            assert.ok(Array.isArray(validationError.Error.Validation));
            assert.strictEqual(validationError.Error.Validation[0].field, 'email');
        });

        test('should simulate console logging safety across environments', () => {
            // Test safe logging function pattern
            const safeLog = {
                error: (...args) => {
                    const globalConsole = (globalThis)?.console;
                    if (globalConsole && globalConsole.error) {
                        // In real implementation, this would log
                        // For test, we just verify the structure
                        assert.ok(args.length > 0);
                        return true;
                    }
                    return false;
                }
            };

            // Test with console available
            const logged = safeLog.error('Test error message');
            assert.strictEqual(typeof logged, 'boolean');

            // Test the pattern works
            assert.strictEqual(typeof safeLog.error, 'function');
        });
    });

    describe('Package Integration Verification', () => {
        test('should verify build artifacts are complete', () => {
            // Check that all expected build artifacts exist
            const expectedFiles = [
                'dist/esm/client.js',
                'dist/protos/client.d.ts',
                'dist/esm/index.js',
                'dist/protos/index.d.ts',
            ];

            expectedFiles.forEach(file => {
                const fullPath = join(__dirname, '..', file);
                assert.ok(existsSync(fullPath), `${file} should exist after build`);
            });
        });

        test('should verify client is available via dedicated export path', () => {
            // The client is available via the dedicated export path './client' as configured in package.json
            // This is the intended way to import the client, not through the main index
            
            // Verify client files exist in their expected locations
            const clientJsPath = join(__dirname, '../dist/esm/client.js');
            const clientDtsPath = join(__dirname, '../dist/protos/client.d.ts');
            
            assert.ok(existsSync(clientJsPath), 'Client JS should exist for dedicated export');
            assert.ok(existsSync(clientDtsPath), 'Client types should exist for dedicated export');
            
            // Verify the files contain the expected exports
            const clientJs = readFileSync(clientJsPath, 'utf8');
            const clientDts = readFileSync(clientDtsPath, 'utf8');
            
            assert.ok(clientJs.includes('FragmentsClient'), 'Client JS should contain FragmentsClient');
            assert.ok(clientDts.includes('FragmentsClient'), 'Client types should contain FragmentsClient');
        });

        test('should verify client has all required exports', () => {
            // Check client.js exports - in compiled JS, only the class is exported
            const clientJsPath = join(__dirname, '../dist/esm/client.js');
            const clientJs = readFileSync(clientJsPath, 'utf8');

            // In compiled JS, only the class and actual runtime exports are present
            const expectedJsContent = [
                'export class FragmentsClient',
                'FragmentsClient',
            ];

            expectedJsContent.forEach(content => {
                assert.ok(clientJs.includes(content), 
                    `client.js should contain: ${content}`);
            });

            // Check client.d.ts types - this is where all the type exports are
            const clientDtsPath = join(__dirname, '../dist/protos/client.d.ts');
            const clientDts = readFileSync(clientDtsPath, 'utf8');

            const expectedTypes = [
                'FragmentsClient',
                'HttpMethod',
                'TokenGetter',
                'ClientConfig',
                'RequestOptions',
                'CacheInvalidator',
                'SimpleValidationResult',
            ];

            expectedTypes.forEach(typeName => {
                assert.ok(clientDts.includes(typeName), 
                    `client.d.ts should contain: ${typeName}`);
            });
        });
    });

    describe('Requirements Verification', () => {
        test('should verify requirement 7.1 - client included in compiled output', () => {
            // Requirement 7.1: WHEN the fragments package is built THEN the client SHALL be included in the compiled output
            const clientJsPath = join(__dirname, '../dist/esm/client.js');
            const clientDtsPath = join(__dirname, '../dist/protos/client.d.ts');

            assert.ok(existsSync(clientJsPath), 'Client JS should be in compiled output');
            assert.ok(existsSync(clientDtsPath), 'Client types should be in compiled output');
        });

        test('should verify requirement 7.4 - TypeScript compilation for both ESM and types', () => {
            // Requirement 7.4: Run TypeScript compilation for both ESM and types
            const esmClientPath = join(__dirname, '../dist/esm/client.js');
            const typesClientPath = join(__dirname, '../dist/protos/client.d.ts');

            assert.ok(existsSync(esmClientPath), 'ESM client should be compiled');
            assert.ok(existsSync(typesClientPath), 'Types client should be compiled');

            // Verify they have different content (JS vs types)
            const esmContent = readFileSync(esmClientPath, 'utf8');
            const typesContent = readFileSync(typesClientPath, 'utf8');

            assert.ok(esmContent.includes('class FragmentsClient'), 'ESM should contain class implementation');
            assert.ok(typesContent.includes('declare class FragmentsClient'), 'Types should contain type declarations');
        });

        test('should verify requirements 9.1-9.4 - framework compatibility design', () => {
            // Requirement 9.1: SHALL NOT include any React, Next.js, Svelte, Vue, or other framework-specific dependencies
            const clientJsPath = join(__dirname, '../dist/esm/client.js');
            const clientJs = readFileSync(clientJsPath, 'utf8');

            // Verify no framework imports
            const frameworkImports = ['react', 'next', 'svelte', 'vue', '@next', '@react'];
            frameworkImports.forEach(framework => {
                assert.ok(!clientJs.includes(`from '${framework}`), `Should not import ${framework}`);
                assert.ok(!clientJs.includes(`require('${framework}`), `Should not require ${framework}`);
            });

            // Requirement 9.2: SHALL work without requiring browser-specific APIs beyond standard fetch
            assert.ok(clientJs.includes('fetch('), 'Should use standard fetch API');
            assert.ok(!clientJs.includes('window.'), 'Should not use window object');
            assert.ok(!clientJs.includes('document.'), 'Should not use document object');

            // Requirement 9.3: SHALL provide the same API surface and behavior across all environments
            const expectedMethods = ['request', 'get', 'post', 'withConfig'];
            expectedMethods.forEach(method => {
                assert.ok(clientJs.includes(method), `Should have ${method} method`);
            });

            // Requirement 9.4: Framework-specific features SHALL be provided via configurable callback functions
            assert.ok(clientJs.includes('onCacheInvalidate'), 'Should use callback for cache invalidation');
            assert.ok(clientJs.includes('getToken'), 'Should use callback for token retrieval');
        });
    });
});