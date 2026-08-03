# Events Module: Target Structure Design

Companion to [EVENTS_EVENTBRITE_PLAN.md](./EVENTS_EVENTBRITE_PLAN.md) (why/phasing)
and [EVENTS_CURRENT_STATE_ARCHIVE.md](./EVENTS_CURRENT_STATE_ARCHIVE.md) (what
existed before it was deleted on this branch). This document hammers out the
*shape* of the rebuild: solution/project layout, proto layout, the
provider-abstraction contract, and the settings structure — mirroring
`Authorization/Payment`'s existing multi-provider pattern as closely as makes
sense for Events. No implementation yet; this is the structural agreement to
build against.

## Naming note up front

Payments' `Manual` project means "an admin manually records that a payment
happened out-of-band" (cash, check, comp) — it is not a payment *processor* in
the sense of actually handling checkout. `Events.Manual` means something
larger: the actual self-hosted ticketing engine (today's filesystem-backed
event/ticket CRUD), i.e. what happens when there's no third-party platform
involved at all. Same directory role (the always-available, no-external-
dependency provider) but a bigger scope than its Payments namesake. Worth
being aware of so the analogy isn't taken too literally when porting code —
the *pattern* mirrors Payments, not the specific responsibilities of `Manual`.

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
└── Combined/
    └── IT.WebServices.Authorization.Events.Combined.csproj
```

Reference graph (mirrors Payment exactly): `Manual` → `Base`, `Eventbrite` →
`Base`, `Combined` → `Base` + `Manual` + `Eventbrite`. `Services/Combined`
(the top-level app host) references only `Events.Combined`, same as it
references only `Authorization.Payment.Combined` today — nothing outside the
Events module ever needs to see `Base`/`Manual`/`Eventbrite` directly.

### `Events.Base`

Namespace `IT.WebServices.Authorization.Events` (top-level, no `.Base` suffix
in the namespace — matches how Payment's Base project uses
`IT.WebServices.Authorization.Payment.Generic` for the interface, not a
`.Base` namespace segment).

```
Base/
├── Generic/
│   ├── IGenericEventProvider.cs          — the provider contract (§2)
│   ├── GenericEventProviderProvider.cs   — registry over IEnumerable<IGenericEventProvider>
│   ├── Data/
│   │   ├── IEventDataProvider.cs         — local-cache/storage contract, provider-agnostic
│   │   └── ITicketDataProvider.cs
├── EventConstants.cs                     — PROCESSOR_NAME_MANUAL / PROCESSOR_NAME_EVENTBRITE
├── Helpers/
│   └── RecurrenceHelper.cs               — pure date-math, no provider dependency, stays here
├── DIExtensions.cs                       — no-op-ish; mostly exists so Manual/Eventbrite/Combined
│                                            have a common place to hang shared registrations if needed
```

`EventTicketClassHelper`/`EventVenueHelper` (today's settings-reading
helpers) move here too, since both public/private Events settings are
provider-agnostic (see §4) and every provider needs to read ticket-class and
venue config the same way.

Whether `IEventDataProvider`/`ITicketDataProvider` belong in `Base` at all is
an open question — see §5. They're today's *local persistence* contract
(filesystem-backed), which is Manual-specific in a pure provider-split world,
but Eventbrite still needs *some* local record of tickets it has synced (for
QR issuance, per the Eventbrite plan's Phase 5) so there may be a shared
local-cache contract both providers use underneath their respective
`IGenericEventProvider` implementations, distinct from Manual's storage being
the *entire* source of truth vs. Eventbrite's being a cache of Eventbrite's
own source of truth.

### `Events.Manual`

Namespace `IT.WebServices.Authorization.Events.Manual`.

```
Manual/
├── Data/
│   ├── FileSystemEventDataProvider.cs    — today's code, rehomed unchanged
│   └── FileSystemTicketDataProvider.cs
├── ManualGenericEventProvider.cs         — implements IGenericEventProvider,
│                                            ProcessorName = EventConstants.PROCESSOR_NAME_MANUAL
├── DIExtensions.cs                       — AddManualEventClasses()
```

### `Events.Eventbrite`

Namespace `IT.WebServices.Authorization.Events.Eventbrite`.

```
Eventbrite/
├── Clients/
│   └── EventbriteClient.cs               — HTTP wrapper over the Eventbrite REST API,
│                                            rate limiting + Polly retry (plan Phase 3 §3)
├── EventbriteGenericEventProvider.cs     — implements IGenericEventProvider,
│                                            ProcessorName = EventConstants.PROCESSOR_NAME_EVENTBRITE
├── Helpers/
│   └── EventbriteSettingsHelper.cs       — reads EventbritePublicSettings/OwnerSettings (§4)
├── DIExtensions.cs                       — AddEventbriteClasses()
```

Webhook receiver and reconciliation job (plan Phase 6) live in `Combined`,
not here — mirroring Payment, where sync/reconciliation is cross-cutting
(`Combined/Helpers/BulkHelper.cs`, `Combined/Helpers/BulkJobs/*`) rather than
owned by a single provider project. `Eventbrite` owns *talking to* Eventbrite;
`Combined` owns *when* to talk to it and what to do with the result.

### `Events.Combined`

Namespace `IT.WebServices.Authorization.Events.Combined`.

```
Combined/
├── DIExtensions.cs                       — AddEventsClasses() / MapEventsGrpcServices()
│                                            composes AddManualEventClasses() + AddEventbriteClasses(),
│                                            registers GenericEventProviderProvider
├── Services/
│   ├── EventService.cs                   — public gRPC surface (today's, rehomed)
│   ├── AdminEventService.cs              — admin gRPC surface (today's, rehomed)
│   └── ClaimsService.cs                  — ClaimsInterface impl (today's, rehomed + bug fixed)
├── Helpers/
│   ├── SyncHelper.cs                     — mirrors Payment's BulkHelper: dispatch to whichever
│   │                                        provider(s) need reconciliation
│   └── BulkJobs/
│       └── ReconcileEventbriteOrders.cs  — mirrors LookForNewPaymentsOneDay-style jobs
├── Controllers/ (if webhook receiver needs a plain HTTP endpoint rather than gRPC)
│   └── EventbriteWebhookController.cs
```

`Services/Combined/Startup.cs` calls `services.AddEventsClasses()` and
`endpoints.MapEventsGrpcServices()` exactly as it did before — the app host's
integration point doesn't change shape, only what's behind it.

---

## 2. `IGenericEventProvider` contract

Mirrors `IGenericPaymentProcessor`'s shape: a `ProcessorName`/`IsEnabled`
pair, operations paired with a `*Supported` bool wherever a provider might
legitimately not support something (Manual supports everything since it's
the reference implementation; Eventbrite may not support all of it, e.g. no
server-side "reserve a ticket" call if Eventbrite checkout must be a
redirect).

```csharp
public interface IGenericEventProvider
{
    string ProcessorName { get; }
    bool IsEnabled { get; }

    // Reads — used to render your own event pages regardless of provider
    IAsyncEnumerable<EventRecord> GetEvents(CancellationToken cancellationToken);
    Task<EventRecord?> GetEvent(string processorEventId, CancellationToken cancellationToken);
    Task<List<EventTicketClass>> GetTicketClasses(string processorEventId, CancellationToken cancellationToken);

    // Gate + handoff — for Eventbrite this returns a checkout URL rather than
    // reserving a ticket server-side (see §5.4); Manual reserves for real.
    Task<ReserveTicketResult> ReserveTicket(EventRecord evt, EventTicketClass ticketClass, ONUser user, int quantity, CancellationToken cancellationToken);
    bool ReserveTicketSupported { get; }           // true for both today, but "supported" means
                                                    // different things — see §5.4

    Task<bool> CancelTicket(EventTicketRecord ticket, ONUser actor, string reason, CancellationToken cancellationToken);

    // Inbound sync — order/attendee/cancellation updates from the provider's system of record
    Task<SyncResult> SyncTicket(string processorTicketId, CancellationToken cancellationToken);
    bool SyncSupported { get; }                   // false for Manual — nothing external to sync from
}
```

`GenericEventProviderProvider` mirrors `GenericPaymentProcessorProvider`
exactly: `AllProviders`, `AllEnabledProviders` (filtered by `IsEnabled`),
`GetProcessor(EventRecord record)` doing
`AllProviders.FirstOrDefault(p => p.ProcessorName == record.ProcessorName)`,
throwing `NotImplementedException` if not found — same failure mode as
Payments today.

`ReserveTicketResult`/`SyncResult` are new small result types (not in
Payments' vocabulary, since Payments doesn't have Events' "must exist
locally fast for QR issuance" latency requirement). Per §5.4,
`ReserveTicketResult` carries a `CheckoutUrl` (populated by Eventbrite, null/
unused for Manual) plus success/error info; `SyncResult` needs at least the
created/updated `EventTicketRecord` (or enough to build one) and an error
reason mappable to `APIError`.

---

## 3. Proto layout

```
Fragments/Protos/IT/WebServices/Fragments/Authorization/Events/
├── EventRecord.proto              — shared: EventRecord, EventVenue, EventRecurrenceRule, enums
├── TicketClassRecord.proto        — shared: TicketClassRecord, EventTicketClass
├── EventTicketRecord.proto        — shared: EventTicketRecord, EventTicketStatus
├── EventsSettings.proto           — shared envelope + per-provider sub-messages (§4)
├── EventInterface.proto           — shared public gRPC surface
├── AdminEventInterface.proto      — shared admin gRPC surface
└── Eventbrite/
    └── EventbriteSettings.proto   — Eventbrite-only: EventbritePublicSettings, EventbriteOwnerSettings
```

Rule of thumb, same one Payment already follows (`Authorization/Payment/`
top-level = shared, `Authorization/Payment/Stripe|Paypal|Fortis|Manual/` =
provider-specific): anything a frontend needs regardless of which provider
served a given event lives at the `Authorization/Events/` top level.
Anything that only makes sense if Eventbrite specifically is involved
(credentials shape, Eventbrite-specific webhook payload types if they need
proto representation) goes in `Authorization/Events/Eventbrite/`. There is
no `Authorization/Events/Manual/` proto folder — Manual has no
provider-specific wire format of its own; it's fully described by the shared
messages.

`Manual` doesn't need its own settings proto message the way
Stripe/Paypal/Fortis each get one, because Manual has no credentials and (per
plan Phase 2) is really just "the existing flat `IsEnabled`" — see §4 for
where that lands.

### Fields to add to shared records (from the Eventbrite plan's identity-matching discussion)

Mirroring Payments' `ProcessorName`/`ProcessorCustomerID` pattern (plain
`string`, not an enum — see `EVENTS_EVENTBRITE_PLAN.md` Phase 3 §2):

- `EventRecord` gains `string ProcessorName` + `string ProcessorEventID`
- `EventTicketRecord` gains `string ProcessorName` (denormalized from its
  parent event at creation time — see §5.2) + `string ProcessorTicketID`

---

## 4. Settings structure

Today's flat `EventOwnerSettings { bool IsEnabled }` becomes a per-provider
structure, mirroring exactly how `SubscriptionPublicRecord`/
`SubscriptionOwnerRecord` give Stripe/Paypal/Fortis/Manual each their own
numbered sub-field:

```proto
// Authorization/Events/EventsSettings.proto
message EventPublicSettings {
  repeated TicketClassRecord TicketClasses = 1;
  ManualEventPublicSettings Manual = 11;
  IT.WebServices.Fragments.Authorization.Events.Eventbrite.EventbritePublicSettings Eventbrite = 12;
}

message EventPrivateSettings {
  repeated EventVenue Venues = 1;              // shared — venues aren't provider-specific
}

message EventOwnerSettings {
  ManualEventOwnerSettings Manual = 11;
  IT.WebServices.Fragments.Authorization.Events.Eventbrite.EventbriteOwnerSettings Eventbrite = 12;
}

message ManualEventPublicSettings {
  bool Enabled = 1;                            // preserves today's EventOwnerSettings.IsEnabled semantics
}

message ManualEventOwnerSettings {
}
```

```proto
// Authorization/Events/Eventbrite/EventbriteSettings.proto
message EventbritePublicSettings {
  bool Enabled = 1;
  string Url = 2;
}

message EventbriteOwnerSettings {
  string ClientID = 1;      // or a single PrivateToken field — depends on Phase 3's auth-model decision
  string ClientSecret = 2;
}
```

Numbering starting at 11 leaves room below for any other fields that might
belong on the shared public/owner messages themselves (matching how
Payments' `SubscriptionPublicRecord` reserves 1-10 for cross-cutting fields
like `Tiers`/`AllowOther` before providers start at 11).

This slots into `Settings/SettingsRecord.proto` exactly where the old flat
version did — `SettingsPublicData.Events = 15`,
`SettingsPrivateData.Events = 15`, `SettingsOwnerData.Events = 15` — no
change needed at that level, since the per-provider nesting is internal to
`EventPublicSettings`/`EventOwnerSettings`.

`SettingsInterface.proto`'s three RPCs (`ModifyEventPublicSettings`/
`ModifyEventPrivateSettings`/`ModifyEventOwnerSettings`) also stay
structurally the same — they already operate on the whole
`EventPublicSettings`/`EventOwnerSettings` message, so they don't need
per-provider RPC variants the way nothing in Payments has
`ModifyStripeSettings` as a separate top-level RPC either (Payments' provider
settings ride inside `SubscriptionPublicRecord`/`SubscriptionOwnerRecord` via
the same generic `ModifySubscriptionPublicData`-style RPCs).

**Cache-invalidation note carried over from the Eventbrite plan (Phase 2 §4)
still applies unchanged**: `Enabled` flags read on the hot path of ticket
purchase need to not be stale across replicas — this settings restructuring
doesn't fix that on its own.

---

## 5. Decisions

1. **`ITicketDataProvider`/`IEventDataProvider` live in `Base`.** Both
   `Manual` and `Eventbrite` implement/consume the same shared local-storage
   contract — Manual's storage is its full source of truth, Eventbrite's is
   a synced cache, but both are "store a record locally, look it up by
   ticket/event ID" at the interface level. If Eventbrite's caching needs
   grow a field Manual doesn't use (e.g. `LastSyncedAtUTC`), add it as an
   optional/nullable field on the shared record rather than forking the
   interface.

2. **`ProcessorName` is denormalized onto `EventTicketRecord`, set from the
   parent event at ticket-creation time and never re-derived.** Ticket-only
   operations (`CancelTicket`, `SyncTicket`) need to dispatch to the right
   `IGenericEventProvider` without necessarily having the parent `EventRecord`
   loaded, so requiring a join back to the event on every ticket operation is
   worse than a small redundant field — consistent with how
   `EventTicketPublicRecord.EventId` already duplicates onto the ticket today
   without issue. No code path should ever write a ticket's `ProcessorName`
   independently of its event's.

3. **Webhook endpoint placement is deferred** — decide when Phase 3/6
   implementation starts, once the Eventbrite auth model and webhook
   signature-verification approach are chosen. Not blocking the structural
   scaffolding in §1-§4.

4. **`ReserveTicket` returns a backend-issued checkout URL for now, not a
   real server-side reservation.** `ReserveTicketResult` carries (at
   minimum) a `CheckoutUrl` string plus success/error info — no ticket is
   created locally at this step; Eventbrite remains the system of record for
   the purchase until Phase 6's inbound sync creates/updates the local
   `EventTicketRecord` (and, per Phase 5, triggers QR issuance). This means
   `ReserveTicketSupported` is effectively "supported but soft" for
   Eventbrite (returns a URL, doesn't gate/reserve) versus Manual, which can
   do a real server-side reservation directly. Revisit if Eventbrite turns
   out to support backend-issuable discount codes/private ticket types
   (plan's open item #2) — that would upgrade this from a redirect to a hard
   server-side gate.
