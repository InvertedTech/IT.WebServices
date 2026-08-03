# Events Module: Production Readiness & Eventbrite Integration Plan

## Goal

Make the `Authorization/Events` module production ready, and refactor it to
support the Eventbrite API in a cross-platform way — mirroring how
`Authorization/Payment` abstracts across multiple payment providers — so that
ticket purchases can be gated by membership/subscription tier and purchased
tickets can be linked to the QR code service for check-in.

## Current state

### Events (`Authorization/Events/`)

Single project, filesystem-only, no provider abstraction:

- `Data/IEventDataProvider.cs` / `FileSystemEventDataProvider.cs` — event
  CRUD, one file per event under `<DataStore>/event/events/`.
- `Data/ITicketDataProvider.cs` / `FileSystemTicketDataProvider.cs` — ticket
  CRUD, filesystem-only.
- `Helpers/EventTicketClassHelper.cs`, `EventVenueHelper.cs`,
  `RecurrenceHelper.cs`.
- `Extensions/DIExtensions.cs` — `AddEventsClasses()` registers only the two
  data providers and two helpers; `MapEventsGrpcServices()` maps
  `ClaimsService`, `EventService`, `AdminEventService`.
- `Services/EventService.cs` — public gRPC surface (`[Authorize]`):
  `GetEvent`, `GetEvents`, `GetOwnTicket(s)`, `CancelOwnTicket`,
  `ReserveTicketForEvent`, `UseTicket`.
- `Services/Services/AdminEventService.cs` — admin gRPC surface, role-gated
  via `RoleAbilities.ROLE_IS_EVENT_MANAGER_OR_HIGHER` /
  `ROLE_IS_EVENT_TICKET_MANAGER_OR_HIGHER`.
- `Services/ClaimsService.cs` — implements the shared `ClaimsInterface`
  (see below), currently emits one claim per unused ticket the user holds.

**Known functional gaps:**

- `EventService.ReserveTicketForEvent` has its core reservation logic
  commented out — it returns success without reserving/creating a ticket
  (`// TODO: Handle Event Count Update`).
- `AdminEventService.AdminCancelOtherTicket` throws `NotImplementedException`;
  `AdminReserveEventTicketForUser` is a stub.
- Audit logging calls exist but are all commented out
  (`// TODO: Add Actor`).
- `EventsSettings.proto`'s `EventOwnerSettings.IsEnabled` flag is never
  checked anywhere.
- No Eventbrite or any third-party event integration exists in the repo
  today.
- No provider abstraction analogous to `IGenericPaymentProcessor` —
  `EventService`/`AdminEventService` depend directly on the filesystem
  data providers.
- No webhook/external-sync handling, no HTTP client, no retry/rate-limiting
  code anywhere in the module.
- No test project for Events.

### Payments (`Authorization/Payment/`) — the pattern to mirror

- One project per provider: `Base/`, `Fortis/`, `Paypal/`, `Stripe/`,
  `Manual/`, `Tax/`, aggregated by `Combined/`.
- Shared contract `Base/.../Generic/IGenericPaymentProcessor.cs`:
  `ProcessorName`, `IsEnabled`, and a set of operations each paired with an
  optional `*Supported` bool so a provider can declare partial support
  instead of throwing (e.g. `GetAllSubscriptionsSupported`,
  `GetAllPaymentsBetweenDatesSupported`).
- `Generic/GenericPaymentProcessorProvider.cs` — registry over
  `IEnumerable<IGenericPaymentProcessor>`, exposes `AllProviders`,
  `AllEnabledProviders` (filtered by `IsEnabled`), `GetProcessor(record)`
  (lookup by `ProcessorName`).
- Each provider: its own `DIExtensions.cs`, a `Clients/` folder wrapping the
  vendor SDK/HTTP calls, and `IsEnabled` sourced from a dynamic Settings
  proto (`StripePublicSettings.Enabled`), not `appsettings.json`.
- `Combined/DIExtensions.cs` composes all providers plus cross-cutting
  services (`AdminPaymentService`, `ClaimsService`, etc.) and background
  bulk jobs.
- **No inbound webhook receiver** — sync uses polling/reconciliation
  `IBulkJob`s instead (`LookForNewPaymentsOneDay/Week/Month`,
  `LookForMissingSubscriptions`, `ReconcileAll`, driven by
  `Combined/Helpers/BulkHelper.cs`).
- No repo-wide retry/rate-limiting pattern — only the vendor Fortis SDK has
  its own Polly-based retry, which is not something Stripe/Paypal's clients
  get for free.
- No test project exists for Payments either (only the vendor Fortis SDK's
  own generated tests).

### QR codes — user badges today, not tickets

- Lives in `Authentication/Services/Helpers/SignedQRHelper.cs` (uses the
  `QRCoder` package), invoked directly from
  `Authentication/Services/Controllers/UserApiController.cs`
  (`GET /api/auth/user/signed-qr`, `GET /api/auth/user/verify-qr`).
- Signs a `UserQRRecord(UserId, UserName, DisplayName,
  SubscriptionLevelCents, ExpiresOnUTC)` with the ECDSA key pair from
  `IT.WebServices.Crypto`, base64url-encodes it into a verify URL, renders
  as a PNG.
- Verification (`VerifySignedQR`) checks signature, a 5-minute TTL, and an
  env-var floor `QR_CODE_MINIMUM_SUB_LEVEL`; gated by
  `[Authorize(Roles = RoleAbilities.ROLE_IS_EVENT_TICKET_MANAGER_OR_HIGHER)]`
  — i.e. event ticket managers are already the intended scanners, just not
  for tickets yet.
- Plain internal C# class, no gRPC contract.
- `Authorization/Events` does not reference Authentication at all today
  (only references `Base`), and `EventTicketRecord` has no QR-related
  field.

### Settings — not up to par for a second provider

Settings live in `Settings/Services/` as one global `SettingsRecord`
(Public/Private/Owner tiers), stored as an append-only log of base64-encoded
protobuf on the filesystem (`Settings/Services/Data/
FileSettingsDataProvider.cs`) — not SQL, no encryption at rest. Access
control is role-based per RPC in `SettingsService.cs`: `GetPublicData` is
anonymous, `GetOwnerData`/`ModifyXOwnerData` are restricted to `ROLE_OWNER`
(admins are explicitly excluded from Owner-tier data, which is where secrets
live — see `RoleAbilities.ROLE_ADMIN`'s doc comment). Payment provider
secrets (Stripe `ClientSecret`, Fortis `UserApiKey`, Paypal `ClientSecret`)
already live this way, so confidentiality today rests entirely on the RBAC
gate + filesystem permissions, not encryption.

`EventsSettings.proto` (`Fragments/Protos/.../Authorization/Events/
EventsSettings.proto`) today:

```proto
message EventPublicSettings { repeated TicketClassRecord TicketClasses = 1; }
message EventPrivateSettings { repeated EventVenue Venues = 1; }
message EventOwnerSettings { bool IsEnabled = 1; }
```

A single global `IsEnabled` bool, no per-provider structure. Compare to
Payments: each provider gets its own `<Provider>PublicSettings`/
`<Provider>OwnerSettings` pair (`Enabled`, `Url`, `ClientID`,
`ClientSecret`, etc.), individually embedded as numbered sub-fields inside
`SubscriptionPublicRecord`/`SubscriptionOwnerRecord` in `SettingsRecord
.proto` — Payments' settings are multi-provider-by-design; Events' are flat
and single-concept. There's currently nowhere to put Eventbrite credentials,
and no way to enable/disable Eventbrite independently of the rest of the
event system.

`TicketClassRecord` already has a `TICKET_MEMBER_LEVEL_ACCESS` enum value
in its `Type` field, but no field linking a ticket class to an actual
`SubscriptionTier` — tiers (`SubscriptionTier { Name, Description, Color,
AmountCents }`, in `SharedTypes.proto`) are matched purely by exact
`AmountCents`, with no stable ID/slug (`Clients/Settings/
SubscriptionTierHelper.cs`).

**Caching/freshness is inconsistent across processes**, which matters for a
provider-enabled kill switch:
- `Base/Helpers/SettingsHelper.cs` (in-process, e.g. `Services/Combined`)
  polls every 10s.
- `Clients/Settings/PublicSettingsClient.cs`/`SettingsClient.cs`
  (cross-service gRPC clients) cache indefinitely (`Lazy<Task<T>>`) until
  `InvalidateCache()` is called — which only happens automatically when a
  write goes through that same client instance in that same process. A
  different replica has no path to learn about a change short of a
  restart; the proto's `GetXNewerData`/`VersionNum` RPCs exist for
  polling-based refresh but nothing currently consumes them.

Settings writes go through `SettingsService.cs`'s `ModifyXOwnerSettings`
RPCs: full read-modify-write of the whole `SettingsRecord` (no per-field
patch), `VersionNum++`, audit log entry via `IAuditLogService`. There's no
separate admin settings service — read and write are the same gRPC service,
gated per-RPC by role.

### Membership/subscription tier — separate system from RBAC roles

- `ONUser.SubscriptionLevel` (`Authentication/Shared/ONUser.cs`) — cents
  amount, carried as a claim (`ONUser.SubscriptionLevelType`), independent
  of the `RoleAbilities.Roles` RBAC list.
- Computed by `Authorization/Payment/Combined/Services/
  ClaimsServiceInternal.cs`: pulls all subscriptions
  (`IGenericSubscriptionFullRecordProvider.GetAllByUserId`, plus manual
  subs), filters to `PaidThruUTC > now`, picks the best by `PaidThruUTC`
  then `AmountCents`, emits `SubscriptionLevel` +
  `SubscriptionProviderType` claims.
- Cross-module federation is via the shared `ClaimsInterface.GetClaims`
  gRPC contract (`Fragments/Protos/.../Authorization/Claims.proto`) — both
  Payment's `ClaimsService` and Events' `ClaimsService` already implement
  this; it's the existing extension point for "does this user have X"
  checks.
- Tier lookup helper: `Clients/Settings/SubscriptionTierHelper.cs` —
  `GetForUser(ONUser)` / `GetForAmount(cents)` maps a cents amount to a
  configured `SubscriptionTier`. This is the established pattern to reuse.
- No existing policy/attribute-based gate for tier — only two ad-hoc cents
  comparisons exist today (`SignedQRHelper.VerifySignedQR`,
  `SubscriptionTierHelper`).

## Plan

### Phase 0 — Fix functional gaps before refactoring

Refactoring broken code just moves the breakage.

1. Implement `ReserveTicketForEvent`'s reservation logic and the event-count
   update.
2. Implement `AdminCancelOtherTicket` and `AdminReserveEventTicketForUser`.
3. Re-enable/wire audit logging using the audit log client already
   registered in DI.
4. Enforce `EventOwnerSettings.IsEnabled` in `EventService`/
   `AdminEventService`, matching Payments' per-provider `IsEnabled` gating.
5. Stand up a real test project
   (`IT.WebServices.Authorization.Events.Tests`) covering the filesystem
   providers and gRPC service logic — there's currently zero coverage to
   refactor against safely.
6. Decide ticket-reservation concurrency approach now (filesystem storage
   makes atomic capacity decrement harder than SQL would) — needed before
   the reservation logic is implemented, not after.

### Phase 1 — Split Events into Base + Providers (mirror Payments)

Restructure `Authorization/Events/` into:

- `Events/Base/` — shared domain records, plus `IGenericEventProvider`
  (analogous to `IGenericPaymentProcessor`): list/get events, get ticket
  classes/availability, create/cancel a reservation, sync attendee status —
  each paired with a `*Supported` bool since providers won't support the
  same operations symmetrically.
- `Events/FileSystem/` (or `Events/Local/`) — today's filesystem providers,
  rehomed as the "internal/manual" provider implementing
  `IGenericEventProvider`, so self-hosted events keep working unchanged.
- `Events/Eventbrite/` — new provider project: `EventbriteClient.cs`
  wrapping the Eventbrite REST API, `EventbriteGenericEventProvider.cs`,
  `Eventbrite/DIExtensions.cs`.
- `Events/Combined/` — aggregator: composes
  `AddFileSystemEventClasses(); AddEventbriteClasses();`, registers a
  `GenericEventProviderProvider` registry (name → provider, filtered by
  `IsEnabled`), maps gRPC services.

`IGenericEventProvider` needs, given Eventbrite is where checkout actually
happens:

- Read: list events, ticket classes, availability (to render your own event
  pages).
- A gate check before handing off to Eventbrite checkout (Phase 2).
- Inbound sync: order placed/updated, attendee, cancellation (Phase 3/sync).

### Phase 2 — Restructure Events settings for multi-provider support

Do this before or alongside Phase 1's project split, since Phase 2's
Eventbrite provider needs somewhere to put its config, and the current
`EventsSettings.proto` has no per-provider structure at all (just a single
flat `EventOwnerSettings.IsEnabled` bool).

1. **Mirror the Payments settings pattern**: replace the flat
   `EventOwnerSettings.IsEnabled` with a per-provider sub-message —
   `EventbritePublicSettings { bool Enabled; string Url; }` +
   `EventbriteOwnerSettings { string ClientID; string ClientSecret; }` (or
   a Private Token field, depending on the auth model chosen in Phase 3) —
   embedded as their own numbered sub-fields, the same way
   `SubscriptionPublicRecord`/`SubscriptionOwnerRecord` give Stripe/
   Paypal/Fortis each their own slot. This keeps the FileSystem provider's
   existing `IsEnabled` semantics intact while giving Eventbrite (and any
   future provider) independent enable/disable and credentials.
2. **Add a tier-mapping field** to `TicketClassRecord` (e.g.
   `RequiredSubscriptionAmountCents`, matching how `SubscriptionTierHelper`
   already keys tiers by `AmountCents` rather than a stable ID) — needed
   for Phase 4's gating; no such field exists today despite
   `TICKET_MEMBER_LEVEL_ACCESS` already existing as an enum value with
   nothing behind it.
3. **Decide on secrets handling** for Eventbrite credentials before adding
   them to the existing plaintext-on-disk settings store (no encryption at
   rest today — confidentiality relies entirely on the `ROLE_OWNER` gRPC
   gate + filesystem permissions). Using the same store is consistent with
   existing precedent (Stripe/Paypal/Fortis secrets already live this way)
   but should be a deliberate choice, not an oversight.
4. **Address cache-invalidation staleness** for the Eventbrite `Enabled`
   flag specifically: `PublicSettingsClient`/`SettingsClient` cache
   indefinitely per-process and only invalidate when a write goes through
   that same client instance — a different replica has no path to learn of
   a change short of restart. Since gating logic will read this flag on
   the hot path of every ticket-purchase attempt, an unbounded-staleness
   kill switch is a real correctness/safety risk; worth wiring up the
   existing but currently-unused `GetXNewerData`/`VersionNum` polling RPCs,
   or lowering `SettingsHelper`'s 10s timer's blast radius by ensuring
   every process that checks this flag actually goes through it.

### Phase 3 — Eventbrite-specific integration design

1. **Auth**: OAuth or a static Private Token — determines the exact shape
   of `EventbriteOwnerSettings` from Phase 2.
2. **ID mapping**: mirror Payments' `ProcessorName`/`ProcessorCustomerID`
   pattern (`GenericSubscriptionRecord`, `Fragments/Protos/.../
   Authorization/Payment/DataRecords.proto:60-62`) rather than inventing a
   provider enum. Payments has no `PaymentProviderType` enum — provider
   identity is a plain `string ProcessorName` field matched against C#
   constants (`PaymentConstants.PROCESSOR_NAME_STRIPE/FORTIS/PAYPAL`,
   `Authorization/Payment/Base/.../PaymentConstants.cs:11-16`), resolved via
   `GenericPaymentProcessorProvider.cs:27`
   (`AllProviders.FirstOrDefault(p => p.ProcessorName == record.ProcessorName)`).
   Add the same shape to Events:
   - `EventRecord`/`EventTicketRecord` get a `string ProcessorName` field
     (e.g. `EVENT_PROCESSOR_NAME_EVENTBRITE = "eventbrite"`,
     `EVENT_PROCESSOR_NAME_FILESYSTEM = "filesystem"`, defined as constants
     analogous to `PaymentConstants`), resolved through
     `GenericEventProviderProvider.GetProcessor(record)` the same way
     Payments does.
   - `EventTicketRecord` gets a `ProcessorTicketID` field (Eventbrite order/
     attendee ID) — same role as `ProcessorSubscriptionID`/
     `ProcessorPaymentID` — for reconciling inbound webhook/sync data
     against the local record.
   - `EventRecord` gets a `ProcessorEventID` field for the Eventbrite event
     ID, same role as `ProcessorCustomerID` scoped to the parent record
     instead of the ticket.
3. **Rate limiting**: no existing pattern to reuse (Payments has none of
   its own — only the vendor Fortis SDK gets Polly for free). Add a simple
   token-bucket or `System.Threading.RateLimiting` wrapper in
   `EventbriteClient`, plus Polly retry/backoff for 429/5xx.
4. **Error handling**: map Eventbrite error responses to gRPC status codes
   consistently.

### Phase 4 — Membership gating for ticket purchases

1. Reuse `SubscriptionTierHelper.GetForUser(ONUser)` as the tier-lookup
   primitive — already exists, already the established pattern.
2. Use the tier-mapping field added to `TicketClassRecord` in Phase 2,
   configured per event by event managers.
3. **Enforcement point**: Eventbrite has no knowledge of your membership
   system, so gating must happen on your side before the user reaches
   Eventbrite checkout — your `EventService`/frontend checks eligibility
   before returning/redirecting to the checkout URL. Check whether
   Eventbrite's API supports discount codes or private/hidden ticket
   classes your backend can issue per-eligible-user — that would make this
   a hard server-side control instead of a soft UI-level one (a motivated
   user could otherwise hit the Eventbrite checkout URL directly).
4. Introduce one reusable "does this `ONUser` meet tier X" check, used by
   both the new Events gate and the existing QR verification, instead of a
   third ad-hoc cents comparison.

### Phase 5 — Connect tickets to QR codes

Needs new work — today's QR system encodes a user badge, not a ticket.

1. New record type `TicketQRRecord` (or a generalized version of
   `UserQRRecord`) carrying `TicketId`, `EventId`, `UserId`, ticket-class/
   tier info, `ExpiresOnUTC` — signed the same way via a generalized
   `SignedQRHelper`.
2. New endpoints analogous to `signed-qr`/`verify-qr` but for tickets, e.g.
   `GET /api/events/ticket/{ticketId}/qr` (owner-only) and a check-in/verify
   endpoint gated by `RoleAbilities.ROLE_IS_EVENT_TICKET_MANAGER_OR_HIGHER`
   (that role already exists for exactly this purpose per
   `SignedQRHelper.VerifySignedQR`'s current gate, just not wired to
   tickets).
3. **Dependency direction**: Events only references `Base` today. Extract
   `SignedQRHelper` into a shared project (e.g. `IT.WebServices.Base` or a
   new small `IT.WebServices.QR`), parameterized over a generic signed
   payload rather than hardcoded to `UserQRRecord`, so both Authentication
   (user badges) and Events (tickets) can consume it without Events taking
   a dependency on the whole Authentication service.
4. **Trigger point**: generate/issue ticket QR once the ticket exists
   locally — i.e., after Phase 6's inbound Eventbrite order sync creates/
   updates the local `EventTicketRecord`, not at Eventbrite-purchase time.

### Phase 6 — Sync mechanism

Given tickets must exist locally (for QR issuance) as soon as someone buys
on Eventbrite, latency matters more here than it did for Payments' pure
back-office reconciliation.

- Primary: Eventbrite webhooks (order placed/updated) — requires a public
  endpoint, signature verification, and idempotency handling.
- Safety net: a reconciliation `IBulkJob`, mirroring Payments'
  `LookForNewPaymentsOneDay`-style jobs, to catch missed/failed webhook
  deliveries.
- Both are worth having regardless of which is primary, since a member
  waiting on their QR code after checkout is a live UX path, not just
  bookkeeping.

### Cross-cutting production-readiness

- **Observability**: structured logging + metrics around sync jobs and
  Eventbrite API calls.
- **Config validation at startup**: fail fast if Eventbrite is enabled but
  credentials are missing, rather than failing per-request.
- **Data storage**: consider whether filesystem storage is still
  appropriate for Events at scale, or whether ticket/event records should
  move to SQL like Payments already does for some record types
  (`SqlPaymentRecordProvider`) — filesystem-per-event doesn't scale well
  for high ticket volume/concurrent reservation.
- **Tests**: unit tests per provider (mock `IGenericEventProvider`),
  integration tests for `EventbriteClient` (mocked HTTP layer), and
  reconciliation-job tests — build alongside the refactor, not after.

## Suggested execution order

1. Phase 0 — fix broken Events code + baseline tests (unblocks everything
   else safely).
2. Phase 1 — structural split into Base/FileSystem/Combined (pure refactor,
   no new provider yet, verified by Phase 0's tests).
3. Phase 2 — restructure Events settings for multi-provider support (needed
   before Phase 3 has anywhere to put Eventbrite config).
4. Phase 3 — Eventbrite provider (additive, isolated in its own project).
5. Phase 4 — membership gating.
6. Phase 5 — ticket QR codes.
7. Phase 6 — sync mechanism (webhook + reconciliation job), needed to feed
   Phase 5.
8. Cross-cutting hardening, applied to both the new Eventbrite path and
   retrofitted to FileSystem where reasonable.

## Open items to decide before implementation

1. **User identity matching**: Eventbrite orders are identified by buyer
   email/name, not your internal `UserId`. Need a matching strategy
   (buyer must be logged in and email pre-filled into the Eventbrite
   checkout URL, or a post-purchase claim/link flow) so the inbound webhook
   can associate the order with the right local user for gating
   verification and QR issuance. Once matched, the local `UserId` plus the
   new `ProcessorName`/`ProcessorTicketID` fields (see Phase 3 §2) are what
   get stored on `EventTicketRecord` — the matching problem is about
   finding the right `UserId` to write into the existing/new record, not a
   separate identity system.
2. **Gate enforcement without Eventbrite cooperation**: confirm whether
   Eventbrite's API supports discount codes/private ticket types your
   backend can programmatically issue per eligible user — this determines
   whether the membership gate is a hard server-side control or a soft
   UI-level one.
