# Events Module: Target Structure Design

Companion to [EVENTS_EVENTBRITE_PLAN.md](./EVENTS_EVENTBRITE_PLAN.md) (why/phasing)
and [EVENTS_CURRENT_STATE_ARCHIVE.md](./EVENTS_CURRENT_STATE_ARCHIVE.md) (what
existed before it was deleted on this branch). This document tracks the
_current, actual_ shape of the rebuild as it's being scaffolded — solution/
project layout, proto layout, and the settings structure. Updated as
decisions land; it is not a snapshot of an initial plan.

**Revision note (2026-08-07).** An earlier version of this document argued
there should be no first-party events project, no per-provider protos or gRPC
services, and that `Events.Combined` should implement `EventInterface`/
`AdminEventInterface` directly. That reasoning came from reading only
`Payment.Manual` and concluding Events had no equivalent need. A fuller read of
`Authorization/Payment` shows the pattern is broader than that — see §0. Those
decisions are reversed here.

---

## 0. What Payments actually does

The pattern being mirrored has four distinct mechanisms, not one. Getting this
right matters because the earlier draft of this document generalized from a
single one of them.

**Project layout.** `Authorization/Payment/` contains `Base`, `Combined`,
`Fortis`, `Manual`, `Paypal`, `Stripe`, `Tax` (plus `FortisAPI.Standard`, a
vendor API wrapper with no bearing on the pattern). Three kinds of sibling:

- `Base` — generic contracts, generic records, generic data providers.
- `Fortis`/`Manual`/`Paypal`/`Stripe` — one project per provider.
- `Tax` — a cross-cutting domain service, not a provider. Lives here because it
  operates on payment data, not because it processes payments.
- `Combined` — generic gRPC surface, dispatch, claims, bulk jobs. Composes; does
  not implement a provider.

**Every provider gets its own proto and its own gRPC service.**
`Combined/DIExtensions.MapPaymentGrpcServices` calls
`MapManualPaymentGrpcServices()`, `MapFortisGrpcServices()`,
`MapPaypalGrpcServices()`, `MapStripeGrpcServices()`, `MapTaxGrpcServices()`
_before_ mapping the generic services. `StripeService :
StripeInterface.StripeInterfaceBase` is a full provider gRPC service carrying
Stripe-only operations (`StripeFinishOwnSubscription`,
`StripeStartUpdateOwnCard`, `StripeFinishUpdateOwnCard`). `ManualPaymentInterface`
is not a special case for out-of-band entry — it is one instance of a rule that
applies to all four providers.

**Three call patterns coexist in `Combined`, chosen per operation:**

1. _Dispatch by record._ `PaymentService.CancelOwnSubscription` loads the
   record, calls `genericProcessorProvider.GetProcessor(record)`, invokes
   polymorphically. Used when the record already exists and names its processor.
2. _Fan-out by name._ `PaymentService.GetNewDetails` injects `FortisClient`,
   `PaypalClient`, and `StripeClient` directly and calls them by name, returning
   `{ Fortis = ..., Stripe = ... }` in one response for the frontend to choose
   between. Used when no record exists yet and the answer is "here are your
   options." `GetNewOneTimeDetails` is the degenerate case — Stripe-only,
   hardcoded.
3. _Parallel fields for non-generic records._ `GetSubscriptionRecordsResponse`
   carries `repeated Generic` _and_ `repeated Manual` as separate fields, merged
   in a single call. Used when a provider's records don't fit the generic shape.

**Data providers live in `Base` when the records fit the generic shape.**
Stripe, Fortis, and Paypal all produce `GenericSubscriptionRecord`/
`GenericPaymentRecord` and share `Payment.Generic.Data`'s providers. Manual has
its own (`ManualD.ISubscriptionRecordProvider`) because a manually-recorded
payment has no processor, no processor IDs, and nothing external to reconcile
against. The rule is about record shape, not about being a provider.

---

## 1. Solution / project structure

```
Authorization/Events/
├── Base/
│   └── IT.WebServices.Authorization.Events.Base.csproj
├── Manual/
│   └── IT.WebServices.Authorization.Events.Manual.csproj
├── Eventbrite/
│   └── IT.WebServices.Authorization.Events.Eventbrite.csproj
├── QR/
│   └── IT.WebServices.Authorization.Events.QR.csproj
└── Combined/
    └── IT.WebServices.Authorization.Events.Combined.csproj
```

Reference graph: `Manual` → `Base`, `Eventbrite` → `Base`, `QR` → `Base`,
`Combined` → `Base` + `Manual` + `Eventbrite` + `QR`. `Services/Combined`
(the top-level app host) references only `Events.Combined`, same as it
references only `Authorization.Payment.Combined` today.

### Why `Events.Manual` exists as its own project

The first-party ticketing engine is a provider like any other. It sells tickets,
takes money (via one-time Payments), issues tickets, and cancels/refunds them —
it is `Stripe`'s analogue, not a "default path" that lives in `Combined`.

The earlier draft's argument was that forcing the built-in path through
`IGenericEventProvider` means "implementing that interface twice for something
that's really just no external processor involved." That holds only while
first-party tickets are free and synchronously issued. Once one-time Payments
back a paid first-party ticket class:

- reservation becomes a payment handoff, not a synchronous write — the same
  shape as Eventbrite's checkout redirect;
- cancellation becomes a refund through `IGenericPaymentProcessor`;
- ticket issuance becomes asynchronous, completing on payment confirmation.

So the generic interface gets _fuller_, and the free-ticket case is the
degenerate one. `Combined` implementing this directly would put a payment client,
first-party data providers, and a check-in surface next to the dispatch logic.

**Naming.** The project is `Events.Manual`, matching `Payment.Manual`'s slot in
the folder. The implication is not exact — in Payments, "Manual" means an admin
recording a payment that happened out-of-band, whereas here it means the
first-party ticketing engine — but positional consistency across the two modules
is worth more than precision in the name. Anyone navigating `Authorization/`
finds `Base`, `Manual`, `<vendor>`, `Combined` in both places.

Two consequences to keep in mind:

- If genuine out-of-band ticket entry is wanted later (comping a ticket, cash at
  the door), it belongs _inside_ `Events.Manual` as additional RPCs on
  `ManualEventInterface`, not as a separate project. There is no second `Manual`
  slot.
- `ProcessorName = "manual"` therefore covers both free and paid first-party
  tickets, and would cover comped ones too. If that becomes too coarse — for
  reporting, or for gating — split on ticket class rather than on
  `ProcessorName`, since the processor is genuinely the same in all three cases.

### `Events.Base`

Namespace `IT.WebServices.Authorization.Events`.

```
Base/
├── Generic/
│   ├── IGenericEventProvider.cs          — provider contract (§2)
│   ├── GenericEventProviderProvider.cs   — registry over IEnumerable<IGenericEventProvider>
│   ├── EventConstants.cs                 — PROCESSOR_NAME_* constants
│   └── Data/
│       ├── IGenericEventRecordProvider.cs
│       └── IGenericEventTicketRecordProvider.cs
├── Helpers/
│   └── EventVenueHelper.cs               — reads settings-configured venues (§4)
└── DIExtensions.cs
```

The generic data providers live here because both providers produce
`GenericEventRecord`/`GenericEventTicketRecord` — the Stripe/Fortis case, not the
Manual case. Eventbrite uses them as a synced local cache; Manual uses them
as its source of truth. Same records, same contract.

`EventTicketClassHelper` is gone — ticket classes are embedded on the event, not
in settings (§3.1).

### `Events.Manual`

Namespace `IT.WebServices.Authorization.Events.Manual`.

```
Manual/
├── ManualGenericEventProvider.cs   — implements IGenericEventProvider,
│                                            ProcessorName = "manual"
├── Services/
│   └── ManualEventService.cs       — ManualEventInterface impl (§2.2)
├── Helpers/
│   └── ManualEventSettingsHelper.cs
└── DIExtensions.cs                       — AddManualEventClasses() /
                                             MapManualEventGrpcServices()
```

No `Clients/` folder — there is no external API to wrap. When paid first-party
tickets land, this project takes a dependency on Payments' one-time payment
surface; that coupling is deliberate and belongs here rather than in `Base`.

`ProcessorName = "manual"`, not `""`. An empty string as a sentinel makes
"unset" and "first-party" indistinguishable, which matters the first time a
record is written by a code path that forgot to set it. Records created before
this convention (if any survive) migrate on read.

### `Events.Eventbrite`

Namespace `IT.WebServices.Authorization.Events.Eventbrite`.

```
Eventbrite/
├── Clients/
│   └── EventbriteClient.cs               — HTTP wrapper, rate limiting + Polly retry
├── EventbriteGenericEventProvider.cs     — implements IGenericEventProvider,
│                                            ProcessorName = "eventbrite"
├── Services/
│   └── EventbriteEventService.cs         — EventbriteEventInterface impl (§2.2)
├── Data/                                  — Eventbrite-only state (§5.1)
│   ├── IEventbriteSyncStateProvider.cs
│   ├── IEventbriteIdMapProvider.cs
│   └── IEventbriteWebhookLogProvider.cs
├── Helpers/
│   └── EventbriteSettingsHelper.cs
└── DIExtensions.cs                       — AddEventbriteEventClasses() /
                                             MapEventbriteEventGrpcServices()
```

### `Events.QR`

Namespace `IT.WebServices.Authorization.Events.QR`. Modeled on `Payment.Tax` — a
cross-cutting capability with its own project and gRPC surface, not a per-provider
concern. Ticket QR issuance and door check-in apply to any ticket held locally
regardless of which provider produced it.

```
QR/
├── Services/
│   └── TicketQRService.cs                — TicketQRInterface impl
├── Helpers/
│   └── TicketQRHelper.cs                 — signs/verifies TicketQRRecord
└── DIExtensions.cs
```

This resolves the plan's Phase 5 §3 dependency-direction question: `SignedQRHelper`
gets generalized over a signed payload and moved somewhere shared (`IT.WebServices.Base`
or a small `IT.WebServices.QR`), and `Events.QR` consumes it. Events still does not
reference the Authentication _service_.

### `Events.Combined`

Namespace `IT.WebServices.Authorization.Events.Combined`.

```
Combined/
├── DIExtensions.cs                       — AddEventsClasses() / MapEventsGrpcServices()
├── Services/
│   ├── EventService.cs                   — EventInterface, generic surface
│   ├── AdminEventService.cs              — AdminEventInterface, generic surface
│   └── ClaimsService.cs                  — ClaimsInterface impl
├── Helpers/
│   ├── SyncHelper.cs                     — mirrors Payment's BulkHelper
│   └── BulkJobs/
│       ├── LookForNewEventsOneDay.cs     — inbound event pull
│       ├── LookForNewTicketsOneDay.cs
│       └── ReconcileAll.cs
└── Controllers/
    └── EventbriteWebhookController.cs    — placement still open, see §5.4
```

`DIExtensions` follows Payments' shape exactly:

```csharp
public static IServiceCollection AddEventsClasses(this IServiceCollection services)
{
    services.AddManualEventClasses();
    services.AddEventbriteEventClasses();
    services.AddEventQRClasses();

    services.AddSingleton<GenericEventProviderProvider>();
    services.AddSingleton<SyncHelper>();
    // bulk jobs as AddTransient
    return services;
}

public static void MapEventsGrpcServices(this IEndpointRouteBuilder endpoints)
{
    endpoints.MapManualEventGrpcServices();
    endpoints.MapEventbriteEventGrpcServices();
    endpoints.MapEventQRGrpcServices();

    endpoints.MapGrpcService<AdminEventService>();
    endpoints.MapGrpcService<ClaimsService>();
    endpoints.MapGrpcService<EventService>();
}
```

---

## 2. Contracts

### 2.1 `IGenericEventProvider`

```csharp
public interface IGenericEventProvider
{
    string ProcessorName { get; }
    bool IsEnabled { get; }

    bool CreateEventSupported { get; }
    bool CancelEventSupported { get; }
    bool CancelTicketSupported { get; }
    bool SyncEventsSupported { get; }

    Task<CreateEventResult> CreateEvent(GenericEventRecord evt, CancellationToken ct);
    Task<CancelEventResult> CancelEvent(GenericEventRecord evt, ONUser actor, string reason, CancellationToken ct);

    Task<ReserveTicketResult> ReserveTicket(GenericEventRecord evt, GenericTicketClassRecord ticketClass, ONUser user, uint quantity, CancellationToken ct);
    Task<CancelTicketResult> CancelTicket(GenericEventTicketRecord ticket, ONUser actor, string reason, CancellationToken ct);

    Task<SyncResult> SyncTicket(string processorEventID, string processorTicketID, CancellationToken ct);
    Task<EventSyncResult> SyncEvents(DateTime changedSince, CancellationToken ct);
}
```

Changes from the previous version and why:

- **`*Supported` bools added**, matching `IGenericPaymentProcessor`
  (`GetAllSubscriptionsSupported`, `GetAllPaymentsBetweenDatesSupported`). Lets
  `AdminEventService` check rather than catch. Eventbrite reports
  `CancelTicketSupported = false` — the public v3 API has no order-cancel or
  refund endpoint. Manual reports `CreateEventSupported`/`SyncEventsSupported
= false` — nothing external to push to or pull from.
- **`CancelEvent` added.** Previously admin cancel bypassed dispatch entirely.
  That is wrong for two of three cases: canceling a paid first-party event must
  issue refunds, and canceling an Eventbrite-backed event must fail loudly rather
  than diverge silently. Only free first-party cancel is a pure local write.
- **`CreateEvent` returns `CreateEventResult`, not `string`.** It needs to return
  the published event URL (for checkout redirect) and the provider-assigned ticket
  class IDs (without which inbound sync cannot map an attendee's
  `ticket_class_id` back to a local `TicketClassID`).
- **`SyncTicket` takes `processorEventID` as well.** Eventbrite's attendee
  retrieve is `GET /events/{event_id}/attendees/{attendee_id}/` — a bare ticket ID
  cannot address it. Both are processor-side identifiers so the provider never
  needs the local data providers; the caller resolves them. This also serves the
  webhook path, where no local ticket exists yet to pass in.
- **`SyncEvents` added.** `EventBulkAction` already declares
  `LookForNewEventsOneDay/Week/Month`, which is an inbound pull with no interface
  method behind it. Pull is also the simpler Eventbrite milestone — no venue push,
  no currency mapping, no compensating delete.
- **`CancelTicket` returns a result, not `bool`.** A `false` cannot distinguish
  "not supported" from "already canceled" from "provider errored."

Result types follow `ReserveTicketResult`'s existing shape — `Success` plus
`APIError? Error` plus payload:

```csharp
public class CreateEventResult
{
    public bool Success { get; set; }
    public string ProcessorEventID { get; set; } = "";
    public string Url { get; set; } = "";
    public Dictionary<string, string> ProcessorTicketClassIDs { get; set; } = new();
    public APIError? Error { get; set; }
}

public class CancelEventResult   { public bool Success { get; set; } public APIError? Error { get; set; } }
public class CancelTicketResult  { public bool Success { get; set; } public APIError? Error { get; set; } }

public class EventSyncResult
{
    public bool Success { get; set; }
    public List<GenericEventRecord> Records { get; set; } = new();
    public bool HasMore { get; set; }
    public APIError? Error { get; set; }
}
```

`EventSyncResult` returns records rather than counters so the provider stays
storage-free (consistent with `CreateEvent`/`SyncTicket` handing data back for the
caller to persist), and so the bulk job has something to report progress against
for `EventBulkActionProgress`.

### 2.2 Provider-specific gRPC surfaces

Each provider project gets its own proto and service, per §0.

**`EventbriteEventInterface`** — owner/admin gated. Operations that only make
sense for Eventbrite and have no generic analogue:

- connect/verify token, list visible organizations, select one
- pre-provision venue mappings (local `VenueID` → Eventbrite `venue_id`)
- trigger resync, read sync watermark/status
- list unmatched orders (an Eventbrite order whose buyer email resolves to no
  local user)
- inspect/replay failed webhook deliveries

**`ManualEventInterface`** — first-party operations with no external
analogue. Thinner, but not empty; grows with paid ticketing.

Ticket check-in does **not** live here — it goes in `Events.QR`, since it applies
to any locally-held ticket regardless of provider.

### 2.3 Which call pattern for which operation

Per §0's three mechanisms:

| Operation                                  | Pattern                           | Why                                                                                        |
| ------------------------------------------ | --------------------------------- | ------------------------------------------------------------------------------------------ |
| `GetEvents`/`GetEvent`/`GetOwnTickets`     | direct read from `Base` providers | local storage holds both providers' records; see §5.6 for the staleness caveat             |
| `ReserveTicket`                            | dispatch by record                | event exists and names its processor                                                       |
| `CancelOwnTicket`/`AdminCancelOtherTicket` | dispatch by record                | ticket carries denormalized `ProcessorName`                                                |
| `AdminCancelEvent`                         | dispatch by record                | refunds (first-party) or explicit failure (Eventbrite)                                     |
| `AdminCreateEvent`                         | fan-out by name                   | no record exists yet; deciding the processor _is_ the call                                 |
| purchase options for an event              | fan-out by name                   | `GetNewDetails` precedent — return each enabled provider's option, let the frontend choose |

The fan-out pattern is what dissolves `ReserveTicket`'s union-return awkwardness.
`ReserveTicketResult` still carries either tickets or a `CheckoutUrl`, but that is
no longer a provider asymmetry — a paid first-party ticket class also returns a
payment handoff. The free-ticket synchronous case is the exception, not the rule.

---

## 3. Proto layout

```
Fragments/Protos/IT/WebServices/Fragments/Authorization/Events/
├── DataRecords.proto              — shared Generic-prefixed records + enums (§3.1)
├── EventSettings.proto            — settings envelope + per-provider sub-messages (§4)
├── EventInterface.proto           — public generic gRPC surface
├── AdminEventInterface.proto      — admin generic gRPC surface
├── SharedTypes.proto              — EventBulkAction enum + progress record
├── Manual/
│   ├── ManualEventInterface.proto
│   └── ManualEventSettings.proto
├── Eventbrite/
│   ├── EventbriteEventInterface.proto
│   └── EventbriteEventSettings.proto
└── QR/
    └── TicketQRInterface.proto
```

### 3.1 `DataRecords.proto` — required changes

Current shape is mostly right. Fields that must be added before the Eventbrite
create/sync path can function:

```
GenericTicketClassRecord
  + string ProcessorTicketClassID          // provider-assigned; without it, inbound
                                           // sync cannot map attendee → ticket class,
                                           // and access-code discounts cannot be scoped
  + uint32 RequiredSubscriptionAmountCents // TICKET_MEMBER_LEVEL_ACCESS has nothing
                                           // behind it today; keys on AmountCents to
                                           // match SubscriptionTierHelper

GenericEventRecord
  + string ProcessorUrl                    // public checkout URL, available only after
                                           // publish; avoids a GET per reserve call
  - GenericEventRecurrenceRule Recurrence  // removed, see §5.8
  - string RecurrenceHash                  // removed, see §5.8
  reserved 23, 24;
  reserved "Recurrence", "RecurrenceHash";

removed entirely (§5.8)
  - message GenericEventRecurrenceRule
  - enum GenericEventRecurrenceFrequency

AdminEventInterface.proto / EventData
  - GenericEventRecurrenceRule Recurrence  // removed, see §5.8
```

`WeekdayEnum` from `CommonTypes.proto` was only imported for
`GenericEventRecurrenceRule.ByWeekday` — check whether the import is still needed
after removal.

`GenericEventVenueRecord` gets **no** `ProcessorVenueID`. Venues live in
`EventPrivateSettings.Venues`, which is written only through
`SettingsService.ModifyEventPrivateSettings` — a full read-modify-write of the
whole `SettingsRecord` with `VersionNum++`. Writing a venue mapping during event
creation would put a cross-service settings write on the create path and race two
concurrent creates against the entire record. Instead the mapping is Eventbrite-owned
state in `IEventbriteIdMapProvider` (§5.1), pre-provisioned through
`EventbriteEventInterface` rather than discovered on the hot path.

Also unchanged but worth restating: `ProcessorName` is denormalized onto
`GenericEventTicketRecord` from its parent event at creation and never re-derived.
Ticket-only operations dispatch without loading the event.

---

## 4. Settings structure

```proto
message EventPublicSettings {
  bool Enabled = 1;                        // module-wide switch
  ManualEventPublicSettings Manual = 9;
  EventbriteEventPublicSettings Eventbrite = 10;
}

message EventPrivateSettings {
  repeated GenericEventVenueRecord Venues = 1;
  ManualEventPrivateSettings Manual = 9;
  EventbriteEventPrivateSettings Eventbrite = 10;
}

message EventOwnerSettings {
  ManualEventOwnerSettings Manual = 9;
  EventbriteEventOwnerSettings Eventbrite = 10;
}
```

The earlier version had no `Manual` sub-message on the grounds that
"there's no scenario where the built-in path is independently disabled while
Eventbrite keeps running." That is not true once both are real providers — running
Eventbrite-only during a migration, or first-party-only while Eventbrite
credentials are being rotated, are both ordinary states. Both providers source
`IsEnabled` from their own `Enabled` flag, exactly as Stripe/Fortis/Paypal do.

`EventbriteEventOwnerSettings` — corrected credential shape:

```proto
message EventbriteEventOwnerSettings {
  string PrivateToken = 1;                 // was ApiKey/ApiSecret
  string WebhookVerificationToken = 2;     // ours, not Eventbrite's — see §5.4
}
```

`ApiKey`/`ApiSecret` are OAuth _app_ credentials, used only to obtain a token via
the authorization-code flow. API calls authenticate with a private token as
`Authorization: Bearer`. For a single-tenant server-to-server integration against
your own organization, the OAuth dance never runs — the token is pasted from
Eventbrite's API keys page. `ApiKey`/`ApiSecret` only become relevant for per-user
OAuth, which nothing here needs.

`EventbriteEventPrivateSettings` gains `OrganizationID`, `OrganizerID`, and
`Currency` (ISO 4217). Currency is required on event create and on ticket cost
(`"USD,1000"` string form), and nothing in the generic records carries it —
`PricePerTicketCents` is a bare uint32. Org-wide default is the right scope.

**Cache-invalidation caveat unchanged**: `PublicSettingsClient`/`SettingsClient`
cache indefinitely per-process and only invalidate on a write through that same
instance. `Enabled` flags read on the purchase hot path have unbounded staleness
across replicas. This structure does not fix that; see the plan's Phase 2 §4.

---

## 5. Decisions

**5.1 — Generic data providers in `Base`; provider-specific state in the
provider project.** The rule is record shape, not provider identity (§0). Both
providers produce `GenericEventRecord`/`GenericEventTicketRecord`, so both share
`Base`'s providers. Eventbrite additionally owns state that has no generic home
and that no other provider will ever have:

- sync watermarks per event (`changed_since` / `last_item_seen`)
- ID maps: local `VenueID` → Eventbrite `venue_id`, local `TicketClassID` →
  Eventbrite `ticket_class_id`
- webhook delivery log, for idempotency and replay
- unmatched orders
- issued access codes, if the hard membership gate lands (§5.7)

These are `Data/I*Provider` interfaces in `Events.Eventbrite`, surfaced through
`EventbriteEventInterface`. If a future need genuinely cannot be made generic,
the escape hatch is a parallel field on the combined response —
`GetSubscriptionRecordsResponse`'s `Generic` + `Manual` dual-field shape — rather
than nullable provider-specific fields on the generic record.

**5.2 — `Events.Manual` is a peer project implementing
`IGenericEventProvider`.** Reverses the previous "no first-party project"
decision. See §1.

**5.3 — Every provider gets a proto and a gRPC service.** Reverses the previous
"nothing provider-specific exists at the proto level" decision. See §0 and §2.2.

**5.4 — Eventbrite does not sign webhooks.** The delivery body is a thin
envelope, roughly `{ "api_url": "...", "config": { "action": "order.placed", ... } }`
— no HMAC header, no shared secret. Consequences, which are requirements not
suggestions:

- **Never fetch `api_url` as given.** Parse the resource type and ID out of it,
  verify the host is exactly `www.eventbriteapi.com`, and reconstruct the URL
  against our own base. Otherwise this is an SSRF primitive aimed at the service
  mesh.
- Treat the webhook as a cache-invalidation ping carrying no trustworthy data —
  "re-read this ID from the real API with our own token."
- Authenticate the endpoint out-of-band with an unguessable path segment or query
  token in the registered `endpoint_url` (`WebhookVerificationToken`, §4). Weak,
  but it is what the platform allows.
- Reject any resource that does not belong to our organization after the fetch.
- Idempotency on `(action, resource_id, changed)`; Eventbrite retries.

Because the webhook is untrustworthy and unauthenticated, the reconciliation bulk
job is **co-primary, not a safety net**.
`GET /events/{id}/attendees/?changed_since=&last_item_seen=` is purpose-built for
this and is the more trustworthy path.

**5.5 — `ReserveTicket` returns either tickets or a checkout handoff, and this is
the general shape.** Previously framed as an Eventbrite-only concession. It is
not: a paid first-party ticket class also hands off to payment before a ticket
exists. Free synchronous first-party reservation is the degenerate case.

**5.6 — Reads come from local storage, with a staleness caveat for external
providers.** For Manual, local storage is authoritative. For Eventbrite, it
is a cache, and Eventbrite owns exactly the fields that matter on the purchase
path — remaining capacity, sales status, sold-out state. Serving a stale
Eventbrite event and then returning a checkout URL for a sold-out class is the
normal outcome once sync lags, not an edge case. Resolution is open: refresh-on-read
with a short TTL for external providers, or an explicit staleness marker on the
record that the frontend surfaces. Decide before Eventbrite reads go live.

**5.7 — The membership gate can be a hard server-side control.** Eventbrite
supports `hidden: true` ticket classes plus `type: "access"` discount codes,
which unlock hidden tickets. A per-user, single-use access code issued at
eligibility-check time makes the member-only class invisible and unpurchasable
without it. It also gives an exact `code → UserID` binding: the attendee's
`variant_id` is `T{ticket_class}-D{discount}`, so the discount ID resolves back
to the user without trusting the buyer email. This answers the plan's open item
#2 affirmatively and upgrades §5.5's Eventbrite path from a soft redirect to a
real gate. Requires a `GenericEventAccessCodeRecord` and provider — Eventbrite-owned
state per §5.1.

**5.8 — Recurrence is dropped entirely; series membership is a frontend
copy-event affordance.** `GenericEventRecurrenceRule`,
`GenericEventRecurrenceFrequency`, `GenericEventRecord.Recurrence`/
`RecurrenceHash`, `EventData.Recurrence`, and `RecurrenceHelper` are all removed.
Reasoning:

- Nothing consumes recurrence today — there is no frontend client and no
  released behavior to preserve, so this costs nothing to remove now and gets
  expensive to remove later.
- It was the one feature that structurally could not push to Eventbrite. Their
  model is a series parent (`is_series: true`) plus `POST /events/{id}/schedules/`
  occurrences; `RecurrenceHelper` expanded to N concrete local records. Dropping
  it removes a rejection branch from `CreateEvent` rather than adding one.
- The old `AdminCreateRecurringEvent` wrote N events with no transactionality —
  a mid-batch failure left partial state with no cleanup path.
- It forced the Single/Recurring oneof that was already flattened away, and it
  left the event list with an unanswerable grouping question (N rows, or one
  grouped row per `RecurrenceHash`?).
- For the realistic case — an admin duplicating last month's show and moving the
  date — copy-event is a _better_ product answer, since it allows per-instance
  ticket classes and capacity. Recurrence rules only fit genuinely identical,
  numerous instances.

**Copy event** requires no backend work. `AdminCreateEvent` already accepts a
full `EventData`, so the frontend prefills the create form from an existing
event, clears IDs and audit timestamps, and shifts the dates.

Field numbers are reserved rather than freed, since the schema is real even if
unreleased:

```proto
message GenericEventRecord {
  reserved 23, 24;
  reserved "Recurrence", "RecurrenceHash";
}
```

If series membership ever needs to be queryable, the answer is a plain
`string SeriesID` propagated by the copy operation — cheaper than
`RecurrenceHash` (no rule-JSON hashing, no rehash-on-edit problem) and it
carries no implication that instances are identical. Not needed for MVP; worth
reserving a field number for while the proto is already being edited.

**5.9 — `CreateEvent` is non-transactional and needs compensation.** Up to five
sequential calls (org lookup, venue, event, ticket classes, publish). On failure
after event creation, attempt `DELETE /events/{id}/`; if that also fails, log the
orphaned Eventbrite event ID at error level with the local event data. Otherwise
half-created draft events accumulate invisibly. Distinct error reasons worth
carrying: `INSUFFICIENT_PACKAGE`/`NO_PACKAGE_SELECTED` (a free Eventbrite plan
cannot have more than one ticket class — this breaks most multi-tier designs),
`SUMMARY_DESCRIPTION_CONFLICT` (`summary` and `description` are mutually
exclusive; `description` is deprecated, so `GenericEventRecord.Description` maps
to `summary` truncated to 140 chars, with rich bodies deferred to the Structured
Content API), `NO_DEFAULT_ORGANIZER`.

---

## 6. Known defects in scaffolded code

Carried here so they are not lost between documents.

1. **`BuiltInGenericEventProvider.ReserveTicket` has an unguarded read-then-write
   capacity race.** Load all tickets → count → decide → save in a loop, with no
   lock and no transaction. Two concurrent requests both pass. On filesystem
   storage this is the expected outcome under load, not an edge case — and once
   one-time Payments back the first-party path it oversells _and_ takes money.
   The plan's Phase 0 §6 said to settle the concurrency approach before
   implementing reservation; it was implemented without one. Options, least-bad
   first: per-event `SemaphoreSlim` (correct single-process only), lock file /
   atomic rename in the data provider, or move tickets to SQL and make the
   capacity check a conditional insert.
2. **Partial write on `quantity > 1`.** Tickets are saved one at a time; a failure
   mid-loop leaves earlier tickets persisted while the caller sees an exception.
   `ticketProvider.Save` return value is also discarded — if it returns `bool`
   rather than throwing, failures are silent and `result.Tickets` reports tickets
   that do not exist.
3. **`CountTowardEventMax` is applied backwards.** `activeForEvent.Count` counts
   every active ticket including classes with `CountTowardEventMax = false`. The
   flag is checked on the incoming class but not used to filter the existing
   tally.
4. **`ExpiredOnUTC` is never set.** The field exists and the old `GenerateRecords`
   populated it from event end. Without it `TICKET_STATUS_EXPIRED` is underivable
   and ticket QR has no expiry to encode.

---

## 7. Open questions

1. **Paid first-party ticketing: same provider or separate?** Does
   `ManualGenericEventProvider` branch on `PricePerTicketCents > 0`, or does
   paid get its own `ProcessorName`? If the former, `Events.Manual` takes a
   dependency on Payments' one-time surface — a notable cross-module coupling
   worth deciding before the interface shape locks.
2. **Push-first or pull-first for Eventbrite.** Pull (`SyncEvents`) is
   substantially simpler and is arguably the more valuable direction — it lets a
   producer who lives in Eventbrite's UI get their events onto the site. Push
   (`CreateEvent`) is the hardest path in the integration.
3. **Read staleness resolution for Eventbrite events** (§5.6).
4. **Webhook endpoint placement** — `Combined/Controllers` vs. inside
   `Events.Eventbrite`. The receiver only reconstructs an ID and calls
   `SyncTicket`, so either works; Eventbrite-local keeps the provider
   self-contained.
5. **Reservation concurrency approach** (§6.1) — blocking for correctness, not
   just for Eventbrite.
