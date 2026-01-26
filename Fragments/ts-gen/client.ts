import { create, toJsonString, type Message } from '@bufbuild/protobuf';
import { type GenMessage } from '@bufbuild/protobuf/codegenv2';
import { getValidator } from './validation.js';
import { type Validator } from '@bufbuild/protovalidate';

// Global declarations for browser APIs when not available
declare global {
    function fetch(input: string, init?: any): Promise<any>;
}

// Safe logging function that works in all environments
const safeLog = {
    error: (...args: any[]) => {
        // Use globalThis to safely access console in all environments
        const globalConsole = (globalThis as any)?.console;
        if (globalConsole && globalConsole.error) {
            globalConsole.error(...args);
        }
    }
};

/**
 * HTTP methods supported by the client
 */
export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

/**
 * Token getter function type - can be sync or async
 */
export type TokenGetter = () => Promise<string | undefined> | string | undefined;

/**
 * Cache invalidation callback function type
 */
export type CacheInvalidator = (tags: string[], paths: string[]) => void;

/**
 * Simplified validation result interface
 */
export interface SimpleValidationResult {
    success: boolean;
    violations?: any[];
}

/**
 * Configuration interface for the FragmentsClient
 */
export interface ClientConfig {
    /**
     * Base URL for API requests
     * @default 'http://localhost:8001'
     */
    baseUrl?: string;

    /**
     * Function to retrieve authentication tokens (sync or async)
     */
    getToken?: TokenGetter;

    /**
     * Callback for cache invalidation (Next.js can pass revalidateTag/revalidatePath)
     */
    onCacheInvalidate?: CacheInvalidator;

    /**
     * Enable pre-request validation using protovalidate
     * @default false
     */
    validateRequests?: boolean;
}

/**
 * Per-request options that can override client configuration
 */
export interface RequestOptions {
    /**
     * HTTP method for the request
     */
    method?: HttpMethod;

    /**
     * Cache tags for Next.js caching
     */
    cacheTags?: string[];

    /**
     * Paths to revalidate after mutations
     */
    revalidatePaths?: string[];

    /**
     * Cache revalidation time in seconds
     */
    revalidate?: number;

    /**
     * Override client-level validation setting for this request
     */
    validate?: boolean;
}

/**
 * Extended fetch options interface that includes Next.js cache options
 */
interface ExtendedRequestInit {
    method?: string;
    headers?: Record<string, string>;
    body?: string;
    next?: {
        tags?: string[];
        revalidate?: number;
    };
}

/**
 * Shared client class for standardized API communication with protobuf serialization
 * 
 * This client encapsulates common patterns found in action functions like:
 * - Token retrieval and authentication headers
 * - Protobuf serialization using create() and toJsonString()
 * - Consistent error handling with protobuf error responses
 * - Next.js cache invalidation support (framework-agnostic)
 * - Pre-request validation using protovalidate
 */
export class FragmentsClient {
    private readonly config: Required<ClientConfig>;
    private validator?: Validator;

    // Expose config for testing purposes
    get _config() {
        return this.config;
    }

    constructor(config: ClientConfig = {}) {
        this.config = {
            baseUrl: config.baseUrl ?? 'http://localhost:8001',
            getToken: config.getToken ?? (() => undefined),
            onCacheInvalidate: config.onCacheInvalidate ?? (() => { }),
            validateRequests: config.validateRequests ?? false,
        };
    }

    /**
     * Create a new client instance with modified configuration
     * @param config Partial configuration to override
     * @returns New FragmentsClient instance
     */
    withConfig(config: Partial<ClientConfig>): FragmentsClient {
        return new FragmentsClient({
            ...this.config,
            ...config,
        });
    }

    /**
     * Generic request method that handles all HTTP operations
     * @param endpoint API endpoint (relative to baseUrl)
     * @param reqSchema Request protobuf schema
     * @param resSchema Response protobuf schema
     * @param data Request data (optional for GET requests)
     * @param options Request options
     * @returns Promise resolving to typed response
     */
    async request<TReq extends Message, TRes extends Message>(
        endpoint: string,
        reqSchema: GenMessage<TReq>,
        resSchema: GenMessage<TRes>,
        data?: Partial<TReq>,
        options: RequestOptions = {}
    ): Promise<TRes> {
        const method = options.method ?? 'POST';
        const shouldValidate = options.validate ?? this.config.validateRequests;

        // Get authentication token
        const token = await this.config.getToken();

        // Create request message if data is provided
        let requestMessage: TReq | undefined;
        let requestBody: string | undefined;

        if (data && method !== 'GET') {
            requestMessage = create(reqSchema, data as any) as TReq;

            // Validate request if enabled
            if (shouldValidate) {
                const validationResult = await this.validateMessage(reqSchema, requestMessage);
                if (!validationResult.success) {
                    // Return error response instead of making HTTP request
                    return this.createValidationErrorResponse(resSchema, validationResult.violations);
                }
            }

            requestBody = toJsonString(reqSchema, requestMessage);
        }

        // Prepare fetch options
        const fetchOptions: ExtendedRequestInit = {
            method,
            headers: {
                'Content-Type': 'application/json',
                ...(token && { Authorization: `Bearer ${token}` }),
            },
            ...(requestBody && { body: requestBody }),
        };

        // Add Next.js cache options if provided
        if (options.cacheTags || options.revalidate !== undefined) {
            fetchOptions.next = {
                ...(options.cacheTags && { tags: options.cacheTags }),
                ...(options.revalidate !== undefined && { revalidate: options.revalidate }),
            };
        }

        try {
            const url = `${this.config.baseUrl}${endpoint}`;
            const response = await fetch(url, fetchOptions);

            // Handle null response like existing action functions
            if (!response) {
                safeLog.error('FragmentsClient: Network request failed - no response received');
                return this.createNetworkErrorResponse(resSchema);
            }

            // Handle HTTP errors like existing action functions
            if (!response.ok) {
                safeLog.error(`FragmentsClient: HTTP error ${response.status}: ${response.statusText}`);
                return this.createHttpErrorResponse(resSchema, response.status, response.statusText);
            }

            const responseData: TRes = await response.json();

            // Handle cache invalidation for successful mutations
            if (method !== 'GET' && (options.cacheTags || options.revalidatePaths)) {
                this.config.onCacheInvalidate(
                    options.cacheTags ?? [],
                    options.revalidatePaths ?? []
                );
            }

            return responseData;
        } catch (error) {
            // Log errors like existing action functions using console.error
            safeLog.error('FragmentsClient request failed:', error);
            
            // Return error response instead of throwing, matching existing patterns
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            return this.createErrorResponse(resSchema, errorMessage);
        }
    }

    /**
     * Convenience method for GET requests
     * @param endpoint API endpoint
     * @param resSchema Response protobuf schema
     * @param options Request options
     * @returns Promise resolving to typed response
     */
    async get<TRes extends Message>(
        endpoint: string,
        resSchema: GenMessage<TRes>,
        options: Omit<RequestOptions, 'method'> = {}
    ): Promise<TRes> {
        return this.request(endpoint, {} as any, resSchema, undefined, {
            ...options,
            method: 'GET',
        });
    }

    /**
     * Convenience method for POST requests
     * @param endpoint API endpoint
     * @param reqSchema Request protobuf schema
     * @param resSchema Response protobuf schema
     * @param data Request data
     * @param options Request options
     * @returns Promise resolving to typed response
     */
    async post<TReq extends Message, TRes extends Message>(
        endpoint: string,
        reqSchema: GenMessage<TReq>,
        resSchema: GenMessage<TRes>,
        data: Partial<TReq>,
        options: Omit<RequestOptions, 'method'> = {}
    ): Promise<TRes> {
        return this.request(endpoint, reqSchema, resSchema, data, {
            ...options,
            method: 'POST',
        });
    }

    /**
     * Static utility method to create protobuf request messages
     * @param schema Protobuf message schema
     * @param data Optional partial data to initialize the message
     * @returns Created message instance
     */
    static createRequest<T extends Message>(
        schema: GenMessage<T>,
        data?: Partial<T>
    ): T {
        return create(schema, data as any) as T;
    }

    /**
     * Static utility method to create protobuf response messages
     * @param schema Protobuf message schema
     * @param data Optional partial data to initialize the message
     * @returns Created message instance
     */
    static createResponse<T extends Message>(
        schema: GenMessage<T>,
        data?: Partial<T>
    ): T {
        return create(schema, data as any) as T;
    }

    /**
     * Static utility method to serialize protobuf messages to JSON strings
     * @param schema Protobuf message schema
     * @param data Message data to serialize
     * @returns JSON string representation
     */
    static serialize<T extends Message>(
        schema: GenMessage<T>,
        data: T
    ): string {
        return toJsonString(schema, data);
    }

    /**
     * Static utility method to validate protobuf messages using protovalidate
     * @param schema Protobuf message schema
     * @param data Message data to validate
     * @returns Promise resolving to validation result
     */
    static async validate<T extends Message>(
        schema: GenMessage<T>,
        data: T
    ): Promise<SimpleValidationResult> {
        try {
            const validator = await getValidator();
            const result = validator.validate(schema, data);
            return {
                success: result.kind === 'valid',
                violations: result.kind === 'invalid' ? result.violations : undefined,
            };
        } catch (error) {
            safeLog.error('Validation error:', error);
            return {
                success: false,
                violations: [{ message: 'Validation system error' }],
            };
        }
    }

    /**
     * Private method to validate messages using the instance validator
     */
    private async validateMessage<T extends Message>(
        schema: GenMessage<T>,
        data: T
    ): Promise<SimpleValidationResult> {
        if (!this.validator) {
            this.validator = await getValidator();
        }

        try {
            const result = this.validator.validate(schema, data);
            return {
                success: result.kind === 'valid',
                violations: result.kind === 'invalid' ? result.violations : undefined,
            };
        } catch (error) {
            safeLog.error('Validation error:', error);
            return {
                success: false,
                violations: [{ message: 'Validation system error' }],
            };
        }
    }

    /**
     * Private method to create error responses matching existing action function patterns
     * This creates a generic error response structure that matches the patterns used in
     * existing action functions like modifyPublicSubscriptionSettings
     */
    private createErrorResponse<T extends Message>(
        schema: GenMessage<T>,
        message: string
    ): T {
        // Create error response matching existing action function patterns
        // The structure matches what's used in functions like modifyPublicSubscriptionSettings
        return create(schema, {
            Error: {
                Message: message,
                Type: 'SETTINGS_ERROR_UNKNOWN', // Matches SettingsErrorReason.SETTINGS_ERROR_UNKNOWN
            },
        } as any) as T;
    }

    /**
     * Private method to create validation error responses
     * This preserves ValidationIssue[] arrays in error responses as required
     */
    private createValidationErrorResponse<T extends Message>(
        schema: GenMessage<T>,
        violations?: any[]
    ): T {
        return create(schema, {
            Error: {
                Message: 'Request validation failed',
                Type: 'SETTINGS_ERROR_VALIDATION_FAILED', // Matches validation error type
                Validation: violations ?? [], // Preserve ValidationIssue[] arrays
            },
        } as any) as T;
    }

    /**
     * Private method to create HTTP error responses
     * Handles HTTP errors uniformly like existing action functions
     */
    private createHttpErrorResponse<T extends Message>(
        schema: GenMessage<T>,
        status: number,
        statusText: string
    ): T {
        const message = `HTTP ${status}: ${statusText}`;
        return create(schema, {
            Error: {
                Message: message,
                Type: 'SETTINGS_ERROR_UNKNOWN',
            },
        } as any) as T;
    }

    /**
     * Private method to create network error responses
     * Handles network failures like existing action functions
     */
    private createNetworkErrorResponse<T extends Message>(
        schema: GenMessage<T>
    ): T {
        return create(schema, {
            Error: {
                Message: 'Network request failed',
                Type: 'SETTINGS_ERROR_UNKNOWN',
            },
        } as any) as T;
    }

    /**
     * Static method to create error responses for use in action functions
     * This allows consumers to create consistent error responses outside of the client
     * @param schema Response schema to create error for
     * @param message Error message
     * @param errorType Error type (defaults to SETTINGS_ERROR_UNKNOWN)
     * @param validationIssues Optional validation issues array
     * @returns Error response matching existing action function patterns
     */
    static createErrorResponse<T extends Message>(
        schema: GenMessage<T>,
        message: string,
        errorType: string = 'SETTINGS_ERROR_UNKNOWN',
        validationIssues?: any[]
    ): T {
        const errorData: any = {
            Message: message,
            Type: errorType,
        };

        // Include validation issues if provided (preserves ValidationIssue[] arrays)
        if (validationIssues && validationIssues.length > 0) {
            errorData.Validation = validationIssues;
        }

        return create(schema, {
            Error: errorData,
        } as any) as T;
    }
}