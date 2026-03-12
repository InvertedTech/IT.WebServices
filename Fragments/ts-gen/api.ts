/**
 * Cross-platform API client utilities
 *
 * Framework-agnostic: works in Next.js (SSR/SSG), browser SPAs, and Node.js.
 * The caller is responsible for resolving the base URL from their environment.
 *
 * @example Next.js
 * ```ts
 * const isServer = typeof window === 'undefined';
 * const baseUrl = isServer
 *   ? (process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? '')
 *   : (process.env.NEXT_PUBLIC_API_BASE_URL ?? '');
 * export const api = createApiClient(baseUrl);
 * ```
 *
 * @example Vite SPA
 * ```ts
 * export const api = createApiClient(import.meta.env.VITE_API_BASE_URL ?? '');
 * ```
 */

// Declare fetch globally so this compiles without requiring DOM lib in tsconfig.
declare function fetch(
	url: string,
	init?: unknown,
): Promise<{
	ok: boolean;
	status: number;
	statusText: string;
	json(): Promise<unknown>;
}>;

// Minimal fetch type aliases — avoids requiring DOM lib in tsconfig.
type FetchCacheMode =
	| 'default'
	| 'force-cache'
	| 'no-cache'
	| 'no-store'
	| 'only-if-cached'
	| 'reload';

/** Subset of fetch init options we care about, plus an escape hatch for anything else. */
export interface FetchInit {
	method?: string;
	headers?: Record<string, string>;
	body?: string | null;
	cache?: FetchCacheMode;
	mode?: string;
	credentials?: string;
	signal?: unknown;
	[key: string]: unknown;
}

/** Next.js extended fetch options — passed through as-is; non-Next runtimes ignore them. */
export interface NextFetchExtensions {
	next?: {
		tags?: string[];
		revalidate?: number | false;
	};
}

export type ApiCallOptions = FetchInit & NextFetchExtensions;

const DEFAULT_HEADERS = {
	'Content-Type': 'application/json',
} as const;

/**
 * Cross-platform fetch wrapper with error handling.
 *
 * - Merges caller headers on top of the default Content-Type header.
 * - Defaults cache to `no-store` unless `cache` or `next` is explicitly provided
 *   (avoids the Next.js 2 MB data-cache write limit).
 * - Passes `next` through transparently for Next.js ISR tag/revalidation support.
 */
export async function apiCall<T>(
	url: string,
	options: ApiCallOptions = {},
): Promise<T> {
	const { next, cache, headers: callerHeaders, ...restOptions } = options;

	const mergedHeaders: Record<string, string> = {
		...DEFAULT_HEADERS,
		...callerHeaders,
	};

	const finalOptions: Record<string, unknown> = {
		...restOptions,
		headers: mergedHeaders,
	};

	// Respect an explicit cache option; otherwise default to no-store to avoid
	// the Next.js 2 MB data-cache limit. Skip the default when `next` is set,
	// since that implies the caller wants ISR caching behaviour.
	if (cache !== undefined) {
		finalOptions['cache'] = cache;
	} else if (!next) {
		finalOptions['cache'] = 'no-store';
	}

	if (next !== undefined) {
		finalOptions['next'] = next;
	}

	const response = await fetch(url, finalOptions);

	if (!response.ok) {
		throw new Error(
			`API call failed: ${response.status} ${response.statusText}`,
		);
	}

	return response.json() as Promise<T>;
}

/**
 * Build the endpoint map for a given base URL.
 * Call once during app initialization with your resolved base URL.
 */
export function createApiEndpoints(baseUrl: string) {
	return {
		auth: {
			login: `${baseUrl}/auth/login`,
			logout: `${baseUrl}/auth/logout`,
			createUser: `${baseUrl}/auth/createuser`,
			adminCreateUser: `${baseUrl}/auth/admin/createuser`,
			refresh: `${baseUrl}/auth/renewtoken`,
			adminModifyOtherAuthProviders: `${baseUrl}/auth/admin/user/providers`,
			changePassword: `${baseUrl}/auth/password`,
			adminChangePassword: `${baseUrl}/auth/admin/password`,
			getCurrentUser: `${baseUrl}/auth/user`,
			editOwnUser: `${baseUrl}/auth/user`,
			getUserById: (userId: string) => `${baseUrl}/auth/user/${userId}`,
			getUserByName: (userName: string) =>
				`${baseUrl}/auth/username/${userName}`,
			getUserIds: `${baseUrl}/auth/user/ids`,
			getProfileImage: `${baseUrl}/auth/profileimage`,
			getUserProfileImage: (userId: string) =>
				`${baseUrl}/auth/user/${userId}/profileimage`,
			uploadProfileImage: `${baseUrl}/auth/profileimage`,
			adminUploadProfileImage: `${baseUrl}/auth/admin/profileimage`,
			totp: `${baseUrl}/auth/totp`,
			disableTotp: (id: string) => `${baseUrl}/auth/totp/${id}/disable/`,
			verifyTotp: (id: string) => `${baseUrl}/auth/totp/${id}/verify/`,
			adminGetUsers: `${baseUrl}/auth/admin/user`,
			adminAddUser: `${baseUrl}/auth/admin/user`,
			adminGetUser: (userId: string) =>
				`${baseUrl}/auth/admin/user/${userId}`,
			adminDisableUser: (userId: string) =>
				`${baseUrl}/auth/admin/user/${userId}/disable`,
			adminEnableUser: (userId: string) =>
				`${baseUrl}/auth/admin/user/${userId}/enable`,
			adminSetRoles: `${baseUrl}/auth/admin/user/roles`,
			adminSearch: `${baseUrl}/auth/admin/search`,
			adminGetTotp: (userId: string) =>
				`${baseUrl}/auth/admin/totp/${userId}`,
			adminCreateTotp: (userId: string) =>
				`${baseUrl}/auth/admin/totp/${userId}`,
			adminDisableTotp: (userId: string, totpId: string) =>
				`${baseUrl}/auth/admin/totp/${userId}/${totpId}/disable`,
			adminVerifyTotp: (userId: string, totpId: string) =>
				`${baseUrl}/auth/admin/totp/${userId}/${totpId}/verify`,
		},
		asset: {
			getAssetData: (id: string) => `${baseUrl}/cms/asset/${id}/data`,
			createAudioAsset: `${baseUrl}/cms/asset/audio`,
			createAsset: `${baseUrl}/cms/admin/asset`,
			getAsset: (id: string) => `${baseUrl}/cms/asset/${id}`,
			adminGetAsset: (id: string) => `${baseUrl}/cms/admin/asset/${id}`,
			adminGetAssetByOldId: (id: string) =>
				`${baseUrl}/cms/admin/asset/old/${id}`,
			adminGetImageAssets: `${baseUrl}/cms/admin/asset/image`,
			searchAssets: `${baseUrl}/cms/admin/asset/search`,
		},
		auditLog: {
			adminGetAuditLog: `${baseUrl}/admin/audit-log`,
		},
		careers: {
			createCareer: `${baseUrl}/admin/careers`,
			getCareersAdmin: `${baseUrl}/admin/careers`,
			getCareer: (id: string) => `${baseUrl}/careers/${id}`,
			getCareers: `${baseUrl}/careers`,
			adminUpdateCareer: (id: string) => `${baseUrl}/admin/careers/${id}`,
			adminDeleteCareer: (id: string) => `${baseUrl}/admin/careers/${id}`,
		},
		category: {
			createCategory: `${baseUrl}/settings/category/create`,
			deleteCategory: (id: string) =>
				`${baseUrl}/settings/category/delete/${id}`,
		},
		channel: {
			getChannels: `${baseUrl}/settings/channel`,
			createChannel: `${baseUrl}/settings/channel/create`,
			deleteChannel: (id: string) =>
				`${baseUrl}/settings/channel/delete/${id}`,
			getChannelDetails: (id: string) =>
				`${baseUrl}/settings/channel/details/${id}`,
		},
		comment: {
			adminDeleteComment: (id: string) =>
				`${baseUrl}/comment/admin/${id}/delete`,
			adminPinComment: (id: string) =>
				`${baseUrl}/comment/admin/${id}/pin`,
			adminUndeleteComment: (id: string) =>
				`${baseUrl}/comment/admin/${id}/undelete`,
			adminUnpinComment: (id: string) =>
				`${baseUrl}/comment/admin/${id}/unpin`,
			createCommentForContent: (id: string) =>
				`${baseUrl}/comment/content/${id}/create`,
			createCommentForComment: (id: string) =>
				`${baseUrl}/comment/${id}/create`,
			deleteOwnComment: (id: string) => `${baseUrl}/comment/${id}/delete`,
			editOwnComment: (id: string) => `${baseUrl}/comment/${id}/edit`,
			getCommentForContent: (id: string) =>
				`${baseUrl}/comment/content/${id}`,
			getCommentsForComment: (id: string) => `${baseUrl}/comment/${id}`,
			likeComment: (id: string) => `${baseUrl}/comment/${id}/like`,
			unlikeComment: (id: string) => `${baseUrl}/comment/${id}/unlike`,
		},
		cms: {
			// Content
			announceContent: (id: string) =>
				`${baseUrl}/cms/admin/content/${id}/announce`,
			createContent: `${baseUrl}/cms/admin/content`,
			adminListContent: `${baseUrl}/cms/admin/content`,
			deleteContent: (id: string) => `${baseUrl}/cms/admin/content/${id}`,
			adminGetContentById: (id: string) =>
				`${baseUrl}/cms/admin/content/${id}`,
			adminUpdateContent: (id: string) =>
				`${baseUrl}/cms/admin/content/${id}`,
			listContent: `${baseUrl}/cms/content`,
			getContentById: (id: string) => `${baseUrl}/cms/content/${id}`,
			getContentByUrl: (url: string) =>
				`${baseUrl}/cms/content/url?ContentUrl=${url}`,
			getRecentCategories: `${baseUrl}/cms/categories/recent`,
			getRecentTags: `${baseUrl}/cms/tags/recent`,
			getContentByChannel: (channelId: string, pageSize = 10) =>
				`${baseUrl}/cms/content?ChannelIds=[${channelId}]&PageSize=${pageSize}`,
			getRelated: (contentId: string, pageSize = 5) =>
				`${baseUrl}/cms/content/${contentId}/related/?PageSize=${pageSize}`,
			publishContent: (id: string) =>
				`${baseUrl}/cms/admin/content/${id}/publish`,
			searchContent: `${baseUrl}/cms/search`,
			unannounceContent: (id: string) =>
				`${baseUrl}/cms/admin/content/${id}/unannounce`,
			undeleteContent: (id: string) =>
				`${baseUrl}/cms/admin/content/${id}/undelete`,
			unpublishContent: (id: string) =>
				`${baseUrl}/cms/admin/content/${id}/unpublish`,
			// Pages
			createPage: `${baseUrl}/cms/admin/page`,
			adminListPages: `${baseUrl}/cms/admin/page`,
			deletePage: (id: string) => `${baseUrl}/cms/admin/page/${id}`,
			adminGetPageById: (id: string) => `${baseUrl}/cms/admin/page/${id}`,
			adminUpdatePage: (id: string) => `${baseUrl}/cms/admin/page/${id}`,
			listPages: `${baseUrl}/cms/page`,
			getPageById: (id: string) => `${baseUrl}/cms/page/${id}`,
			getPageByUrl: `${baseUrl}/cms/page/url`,
			publishPage: (id: string) =>
				`${baseUrl}/cms/admin/page/${id}/publish`,
			searchPages: `${baseUrl}/cms/page/search`,
			undeletePage: (id: string) =>
				`${baseUrl}/cms/admin/page/${id}/undelete`,
			unpublishPage: (id: string) =>
				`${baseUrl}/cms/admin/page/${id}/unpublish`,
		},
		settings: {
			publicSettings: `${baseUrl}/settings/public`,
			publicSettingsNewer: (version: string) =>
				`${baseUrl}/settings/public/newer/${version}`,
			adminSettings: `${baseUrl}/settings/admin`,
			adminSettingsNewer: (version: string) =>
				`${baseUrl}/settings/admin/newer/${version}`,
			ownerSettings: `${baseUrl}/settings/owner`,
			ownerSettingsNewer: (version: string) =>
				`${baseUrl}/settings/owner/newer/${version}`,
			channels: `${baseUrl}/settings/channel`,
			channelById: (channelId: string) =>
				`${baseUrl}/settings/channels/details/${channelId}`,
			saveCmsPublic: `${baseUrl}/settings/cms/public`,
			saveCmsPrivate: `${baseUrl}/settings/cms/private`,
			saveCmsOwner: `${baseUrl}/settings/cms/owner`,
			savePersonalizationPublic: `${baseUrl}/settings/personalization/public`,
			savePersonalizationPrivate: `${baseUrl}/settings/personalization/private`,
			savePersonalizationOwner: `${baseUrl}/settings/personalization/owner`,
			saveSubscriptionPublic: `${baseUrl}/settings/subscription/public`,
			saveSubscriptionPrivate: `${baseUrl}/settings/subscription/private`,
			saveSubscriptionOwner: `${baseUrl}/settings/subscription/owner`,
			saveCommentsPublic: `${baseUrl}/settings/comments/public`,
			saveCommentsPrivate: `${baseUrl}/settings/comments/private`,
			saveCommentsOwner: `${baseUrl}/settings/comments/owner`,
			saveNotificationPublic: `${baseUrl}/settings/notification/public`,
			saveNotificationPrivate: `${baseUrl}/settings/notification/private`,
			saveNotificationOwner: `${baseUrl}/settings/notification/owner`,
			saveEventsPublic: `${baseUrl}/settings/events/public`,
			saveEventsPrivate: `${baseUrl}/settings/events/private`,
			saveEventsOwner: `${baseUrl}/settings/events/owner`,
		},
		dashboard: {
			getDashboard: `${baseUrl}/admin/dashboard`,
		},
		events: {
			getEvent: (eventId: string) => `${baseUrl}/events/${eventId}`,
			getEvents: `${baseUrl}/events`,
			getTicket: (eventId: string, ticketId: string) =>
				`${baseUrl}/events/${eventId}/tickets/${ticketId}`,
			getTickets: (eventId: string) =>
				`${baseUrl}/events/${eventId}/tickets`,
			cancelTicket: (eventId: string, ticketId: string) =>
				`${baseUrl}/events/${eventId}/tickets/${ticketId}/cancel`,
			reserveTicket: (eventId: string) =>
				`${baseUrl}/events/${eventId}/tickets/reserve`,
			useTicket: `${baseUrl}/events/tickets/use`,
		},
		adminEvents: {
			createEvent: `${baseUrl}/admin/events/create`,
			createRecurringEvent: `${baseUrl}/admin/events/create-recurring`,
			getEvent: (eventId: string) => `${baseUrl}/admin/events/${eventId}`,
			getEvents: `${baseUrl}/admin/events`,
			modifyEvent: `${baseUrl}/admin/events/modify`,
			cancelEvent: `${baseUrl}/admin/events/cancel`,
			cancelAllRecurring: `${baseUrl}/admin/events/cancel-all-recurring`,
			getTicket: (eventId: string, ticketId: string) =>
				`${baseUrl}/admin/events/${eventId}/tickets/${ticketId}`,
			getTickets: (eventId: string) =>
				`${baseUrl}/admin/events/${eventId}/tickets`,
			cancelTicket: (eventId: string, ticketId: string) =>
				`${baseUrl}/admin/events/${eventId}/tickets/${ticketId}/cancel`,
			reserveTicket: (eventId: string) =>
				`${baseUrl}/admin/events/${eventId}/tickets/reserve`,
		},
		payments: {
			getSubscriptions: `${baseUrl}/payment/subscription`,
			getSubscriptionById: (id: string) =>
				`${baseUrl}/payment/subscription/${id}`,
			reconcileSubscription: `${baseUrl}/payment/subscription/reconcile`,
			newSubscription: (
				level: number,
				postalCode: string,
				successUrl: string,
				cancelUrl: string,
			) =>
				`${baseUrl}/payment/new/${level}?PostalCode=${postalCode}&SuccessUrl=${successUrl}&CancelUrl=${cancelUrl}`,
			cancelSubscription: `${baseUrl}/payment/subscription/cancel`,
			getSinglePayments: `${baseUrl}/payment/single`,
			getSinglePayment: (id: string) => `${baseUrl}/payment/single/${id}`,
			newSinglePayment: `${baseUrl}/payment/single/new`,
			finishStripe: `${baseUrl}/payment/stripe/subscription/finish`,
			finishFortis: `${baseUrl}/payment/fortis/subscription/finish`,
			newPaypal: `${baseUrl}/payment/paypal/subscription/new`,
		},
		adminPayments: {
			bulkCancel: `${baseUrl}/payment/admin/bulk/cancel`,
			bulkStart: `${baseUrl}/payment/admin/bulk/start`,
			getBulk: `${baseUrl}/payment/admin/bulk`,
			cancelUserSubscription: (userId: string, subscriptionId: string) =>
				`${baseUrl}/payment/admin/user/${userId}/subscription/${subscriptionId}/cancel`,
			getUserSubscription: (userId: string, subscriptionId: string) =>
				`${baseUrl}/payment/admin/user/${userId}/subscription/${subscriptionId}`,
			getUserSubscriptions: (userId: string) =>
				`${baseUrl}/payment/admin/user/${userId}/subscription`,
			getUserSinglePayment: (userId: string, paymentId: string) =>
				`${baseUrl}/payment/admin/user/${userId}/single/${paymentId}`,
			getUserSinglePayments: (userId: string) =>
				`${baseUrl}/payment/admin/user/${userId}/single`,
			reconcileUserSubscription: (
				userId: string,
				subscriptionId: string,
			) =>
				`${baseUrl}/payment/admin/user/${userId}/subscription/${subscriptionId}/reconcile`,
			getSubscriptions: `${baseUrl}/payment/admin/subscriptions`,
		},
		manualPayments: {
			adminCancelSubscription: (userId: string, subscriptionId: string) =>
				`${baseUrl}/payment/manual/admin/user/${userId}/subscription/${subscriptionId}/cancel`,
			cancelSubscription: (subscriptionId: string) =>
				`${baseUrl}/payment/manual/subscription/${subscriptionId}/cancel`,
			adminGetSubscription: (userId: string, subscriptionId: string) =>
				`${baseUrl}/payment/manual/admin/user/${userId}/subscription/${subscriptionId}`,
			adminGetSubscriptions: (userId: string) =>
				`${baseUrl}/payment/manual/admin/user/${userId}/subscription`,
			getSubscriptions: `${baseUrl}/payment/manual/subscription`,
			getSubscription: (subscriptionId: string) =>
				`${baseUrl}/payment/manual/subscription/${subscriptionId}`,
			adminNewSubscription: (userId: string) =>
				`${baseUrl}/payment/manual/admin/user/${userId}/subscription/new`,
			newSubscription: `${baseUrl}/payment/manual/subscription/new`,
		},
		stats: {
			like: (contentId: string) => `${baseUrl}/stats/${contentId}/like`,
			unlike: (contentId: string) =>
				`${baseUrl}/stats/${contentId}/unlike`,
			progress: (contentId: string) =>
				`${baseUrl}/stats/${contentId}/progress`,
			save: (contentId: string) => `${baseUrl}/stats/${contentId}/save`,
			unSave: (contentId: string) =>
				`${baseUrl}/stats/${contentId}/unsave`,
			logShare: (contentId: string) =>
				`${baseUrl}/stats/${contentId}/logshare`,
			getContentStats: (contentId: string) =>
				`${baseUrl}/stats/${contentId}`,
			getUserStats: (userId: string) => `${baseUrl}/stats/user/${userId}`,
			getCurrentUserStats: `${baseUrl}/stats/user`,
			getUserLikes: `${baseUrl}/stats/user/likes`,
			getUserProgress: `${baseUrl}/stats/user/progress`,
			getUserSaves: `${baseUrl}/stats/user/saves`,
		},
	} as const;
}

export type ApiEndpoints = ReturnType<typeof createApiEndpoints>;

/**
 * Create a complete API client bound to a base URL.
 *
 * Returns `endpoints` (the full URL map) and `call` (the fetch wrapper),
 * both pre-bound to the provided base URL.
 */
export function createApiClient(baseUrl: string) {
	return {
		endpoints: createApiEndpoints(baseUrl),
		call: <T>(url: string, options?: ApiCallOptions) =>
			apiCall<T>(url, options),
	};
}

export type ApiClient = ReturnType<typeof createApiClient>;
