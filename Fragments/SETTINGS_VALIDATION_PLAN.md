# Settings Proto Validation and Error Unification Plan

This document outlines how to add ProtoValidate constraints to Settings protos and introduce a unified `SettingsError` (mirroring `AuthError`) across Settings write RPC responses. The scope focuses on proto schemas and packaging — no C# validation pipelines are required in this phase.

## Inventory (Targets)

-   Core Settings protos
    -   `Fragments/Protos/IT/WebServices/Fragments/Settings/SettingsInterface.proto`
    -   `Fragments/Protos/IT/WebServices/Fragments/Settings/SettingsRecord.proto`
-   Nested/related settings referenced by Settings
    -   `Fragments/Protos/IT/WebServices/Fragments/Authorization/Events/EventsSettings.proto`
    -   Provider settings (optional pass): Fortis, Stripe, Paypal, Manual, Crypto
        -   Paths under `Fragments/Protos/IT/WebServices/Fragments/Authorization/Payment/**/` (e.g., `Stripe/StripeSettings.proto`, etc.)

## New Protos

-   Add: `Fragments/Protos/IT/WebServices/Fragments/Settings/SettingsError.proto`
    -   Imports:
        -   `buf/validate/validate.proto`
        -   `Protos/IT/WebServices/Fragments/Errors.proto` (for `ValidationIssue`)
    -   Messages:
        -   `message SettingsError {`
            -   `SettingsErrorReason Type = 1;`
            -   `string Message = 2;`
            -   `repeated IT.WebServices.Fragments.ValidationIssue Validation = 3;`
            -   `}`
        -   `enum SettingsErrorReason {`
            -   `SETTINGS_REASON_UNSPECIFIED = 0;`
            -   CMS (100–149): `CMS_ERROR_UNKNOWN = 149;`
            -   Personalization (200–249): `PERSONALIZATION_ERROR_UNKNOWN = 249;`
            -   Subscription (300–349): `SUBSCRIPTION_ERROR_UNKNOWN = 349;`
            -   Comments (400–449): `COMMENTS_ERROR_UNKNOWN = 449;`
            -   Notification (500–549): `NOTIFICATION_ERROR_UNKNOWN = 549;`
            -   Events (600–649): `EVENTS_ERROR_UNKNOWN = 649;`
            -   Generic (900–999):
                -   `SETTINGS_ERROR_UNAUTHORIZED = 900;`
                -   `SETTINGS_ERROR_SERVICE_OFFLINE = 901;`
                -   `SETTINGS_ERROR_VALIDATION_FAILED = 902;`
                -   `SETTINGS_ERROR_UNKNOWN = 999;`
            -   `}`

Notes:

-   Start with the generic/unknown reasons above; expand with granular codes later to match product needs.

## Changes To Existing Write Protos (Requests)

-   File: `.../Settings/SettingsInterface.proto`
    -   Add import: `import "buf/validate/validate.proto";`
    -   Mark each write request `Data` field as required:
        -   `ModifyCMSPublicDataRequest { CMSPublicRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyCMSPrivateDataRequest { CMSPrivateRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyCMSOwnerDataRequest { CMSOwnerRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyPersonalizationPublicDataRequest { PersonalizationPublicRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyPersonalizationPrivateDataRequest { PersonalizationPrivateRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyPersonalizationOwnerDataRequest { PersonalizationOwnerRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifySubscriptionPublicDataRequest { SubscriptionPublicRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifySubscriptionPrivateDataRequest { SubscriptionPrivateRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifySubscriptionOwnerDataRequest { SubscriptionOwnerRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyCommentsPublicDataRequest { CommentsPublicRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyCommentsPrivateDataRequest { CommentsPrivateRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyCommentsOwnerDataRequest { CommentsOwnerRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyNotificationPublicDataRequest { NotificationPublicRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyNotificationPrivateDataRequest { NotificationPrivateRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyNotificationOwnerDataRequest { NotificationOwnerRecord Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyEventPublicSettingsRequest { IT.WebServices.Fragments.Authorization.Events.EventPublicSettings Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyEventPrivateSettingsRequest { IT.WebServices.Fragments.Authorization.Events.EventPrivateSettings Data = 1 [(buf.validate.field).required = true]; }`
        -   `ModifyEventOwnerSettingsRequest { IT.WebServices.Fragments.Authorization.Events.EventOwnerSettings Data = 1 [(buf.validate.field).required = true]; }`

## Changes To Existing Response Protos (Errors)

-   File: `.../Settings/SettingsInterface.proto`
    -   Replace `ModifyResponseErrorType Error = 1;` with `SettingsError Error = 1;` in all Modify\*Response messages:
        -   CMS (Public/Private/Owner)
        -   Personalization (Public/Private/Owner)
        -   Subscription (Public/Private/Owner)
        -   Comments (Public/Private/Owner)
        -   Notification (Public/Private/Owner)
        -   Events (Public/Private/Owner)
-   Deprecation strategy (optional):
    -   If needed, temporarily keep the old enum `Error` and add `SettingsError Error2 = 2;` with comments indicating deprecation. Remove the old field in the next major bump. Otherwise, replace outright.

## Field-Level Validation Tags (Examples)

-   File: `.../Settings/SettingsRecord.proto` (add `import "buf/validate/validate.proto";`)

    -   `PersonalizationPublicRecord`:
        -   `Title`: `(buf.validate.field).string.min_len = 1`
        -   `MetaDescription`: `(buf.validate.field).string.max_len = 300` (tune as required)
    -   `ChannelRecord`:
        -   `ChannelId`, `ParentChannelId`: `(buf.validate.field).string.min_len = 1` and/or `pattern = "^[a-z0-9_-]+$"`
        -   `DisplayName`: `string.min_len = 1`, `string.max_len = 100`
        -   `UrlStub`: `string.pattern = "^[a-z0-9-]+$"`
        -   `YoutubeUrl`, `RumbleUrl`: optional `string.uri = true` (if desired)
    -   `CategoryRecord`:
        -   Similar to `ChannelRecord` for `CategoryId`, `DisplayName`, `UrlStub`
    -   `CMSPublicRecord`:
        -   `Channels`, `Categories`: `(buf.validate.field).repeated.unique = true` (and/or `min_items`)
    -   `CommentsPrivateRecord`:
        -   `BlackList`: `(buf.validate.field).repeated.items.string.min_len = 1`
    -   `SettingsPublicData.VersionNum`: consider `(buf.validate.field).uint32.gt = 0` if required

-   File: `.../Authorization/Events/EventsSettings.proto` (add `import "buf/validate/validate.proto";`)

    -   `EventPublicSettings.TicketClasses`: `(buf.validate.field).repeated.unique = true` or `min_items` as needed
    -   `EventPrivateSettings.Venues`: optionally `min_items`

-   Provider Settings (optional):
    -   Apply `required = true` to API keys/secrets, `min_len` for IDs, and patterns for account IDs where applicable.

## Codegen & Packaging

-   Ensure all modified protos import `validate.proto` and `SettingsError.proto` where needed.
-   Run buf/protoc pipelines to regenerate:
    -   C# types (for services that depend on Settings protos)
    -   TypeScript types (per `buf.gen.v2.yaml` and scripts)
-   Update TS package exports to include `SettingsError` and `SettingsErrorReason` where appropriate.
-   Add a changeset describing:
    -   New `SettingsError` proto
    -   Breaking changes where response `Error` type changed from enum to `SettingsError`.

## Migration Notes

-   Client code handling write responses must switch to:
    -   Reading `Error.Type` (enum) instead of old enum field
    -   Displaying `Error.Message`
    -   Optionally surfacing `Error.Validation` for per-field issues (when validation is enabled)
-   If deprecation path is chosen, support both fields temporarily and remove the old enum in the next major version.

## Acceptance Criteria

-   All Settings write requests have `Data` marked with `(buf.validate.field).required = true`.
-   All Settings write responses use `SettingsError Error` in place of the old enum.
-   `SettingsError.proto` exists and compiles with the rest of the package.
-   Codegen runs cleanly for C# and TS with no new warnings.
