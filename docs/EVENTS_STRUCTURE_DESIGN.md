# Events Module: Target Structure Design

Companion to [EVENTS_EVENTBRITE_PLAN.md](./EVENTS_EVENTBRITE_PLAN.md) (why/phasing)
and [EVENTS_CURRENT_STATE_ARCHIVE.md](./EVENTS_CURRENT_STATE_ARCHIVE.md) (what
existed before it was deleted on this branch). This document tracks the
*current, actual* shape of the rebuild as it's being scaffolded — solution/
project layout, proto layout, and the settings structure. Updated as
decisions land; it is not a snapshot of an initial plan.

## Why there's no `Events.Manual` project

Payments' `Manual` project means "an admin manually records that a payment
happened out-of-band" (cash, check, comp) — a narrow, additive operation the
generic `PaymentInterface` has no equivalent for, which is why it needs its
own project and its own interface proto (`ManualPaymentInterface.proto`).

Events has no equivalent need. What would have been `Events.Manual` is the
app's own built-in, self-hosted ticketing engine — not a swappable backend
alongside Eventbrite, but the default/reference implementation that *is* the
generic surface. Rather than force it through the same
`IGenericEventProvider` abstraction Eventbrite needs (which would mean
implementing that interface twice for something that's really just "no
external processor involved"), **`Events.Combined` implements
`EventInterface`/`AdminEventInterface` directly**, using `Events.Base`'s data
providers. `IGenericEventProvider` exists for genuinely pluggable external
integrations — today just Eventbrite, potentially more later — and Combined
dispatches to it only when an event's `ProcessorName` names one.

This also means there's no `ManualEventInterface.proto` and no
`ManualEventSettings.proto` — nothing Manual-specific exists at the proto
level, because "Manual" isn't a provider, it's the default path.

---

## 1. Solution / project structure

```
Authorization/Events/
├── Base/
│   └── IT.WebServices.Authorization.Events.Base.csproj
├── Eventbrite/
│   └── IT.WebServices.Authorization.Events.Eventbrite.csproj
└── Combined/
    └── IT.WebServices.Authorization.Events.Combined.csproj
```

Reference graph: `Eventbrite` → `Base`, `Combined` → `Base` + `Eventbrite`.
`Services/Combined` (the top-level app host) references only
`Events.Combined`, same as it references only
`Authorization.Payment.Combined` today.

### `Events.Base`

Namespace `IT.WebServices.Authorization.Events`.

```
Base/
├── Generic/
│   ├── IGenericEventProvider.cs          — the pluggable-external-provider contract (§2)
│   ├── GenericEventProviderProvider.cs   — registry over IEnumerable<IGenericEventProvider>
│   ├── Data/
│   │   ├── IEventDataProvider.cs         — local persistence contract
│   │   └── ITicketDataProvider.cs
├── Helpers/
│   ├── RecurrenceHelper.cs               — pure date-math, no provider dependency
│   ├── EventTicketClassHelper.cs         — reads settings-configured venues (§4)
│   └── EventVenueHelper.cs
├── DIExtensions.cs
```

`IEventDataProvider`/`ITicketDataProvider` live here (not per-provider) —
the built-in path in `Combined` uses them as its actual source of truth;
Eventbrite uses them as a synced local cache (needed for QR issuance
latency, per the Eventbrite plan's Phase 5). Both are "store a record
locally, look it up by ticket/event ID" at the interface level, so one
shared contract avoids duplicating that shape.

### `Events.Eventbrite`

Namespace `IT.WebServices.Authorization.Events.Eventbrite`.

```
Eventbrite/
├── Clients/
│   └── EventbriteClient.cs               — HTTP wrapper over the Eventbrite REST API,
│                                            rate limiting + Polly retry (plan Phase 3 §3)
├── EventbriteGenericEventProvider.cs     — implements IGenericEventProvider,
│                                            ProcessorName = "eventbrite"
├── Helpers/
│   └── EventbriteSettingsHelper.cs       — reads EventbriteEventPublicSettings/OwnerSettings (§4)
├── DIExtensions.cs                       — AddEventbriteClasses()
```

### `Events.Combined`

Namespace `IT.WebServices.Authorization.Events.Combined`.

```
Combined/
├── DIExtensions.cs                       — AddEventsClasses() / MapEventsGrpcServices()
│                                            composes AddEventbriteClasses(), registers
│                                            GenericEventProviderProvider
├── Services/
│   ├── EventService.cs                   — implements EventInterface directly (built-in path),
│   │                                        dispatches to IGenericEventProvider when an event's
│   │                                        ProcessorName names an external provider
│   ├── AdminEventService.cs              — implements AdminEventInterface directly, same dispatch rule
│   └── ClaimsService.cs                  — ClaimsInterface impl
├── Helpers/
│   ├── SyncHelper.cs                     — mirrors Payment's BulkHelper: dispatch to whichever
│   │                                        provider(s) need reconciliation
│   └── BulkJobs/
│       └── ReconcileEventbriteOrders.cs  — mirrors LookForNewPaymentsOneDay-style jobs
├── Controllers/ (if the webhook receiver needs a plain HTTP endpoint rather than gRPC — see §5.3)
│   └── EventbriteWebhookController.cs
```

`Services/Combined/Startup.cs` calls `services.AddEventsClasses()` and
`endpoints.MapEventsGrpcServices()` — the app host's integration point
doesn't change shape, only what's behind it.

---

## 2. `IGenericEventProvider` contract

Mirrors Payments' actual pattern, not a set of parallel gRPC "service"
classes: **one** `EventService`/`AdminEventService` stays the only
gRPC-facing surface, and provider-specific behavior is reached by looking
up an `IGenericEventProvider` implementation and calling it polymorphically
— the same shape as `PaymentService.cs` doing
`genericProcessorProvider.GetProcessor(record).CancelSubscription(...)`
rather than branching to a `ManualPaymentService`/`StripePaymentService`.

**Reads stay off this interface entirely.** `GetEvents`/`GetEvent`/
`GetOwnTickets` in `EventService` go straight against `Base`'s
`IGenericEventRecordProvider`/`IGenericEventTicketRecordProvider` regardless
of which provider an event belongs to — local storage is the single source
of truth for both the built-in path and Eventbrite-synced events (synced in
via Phase 6), so there's nothing to dispatch for a read. `IGenericEventProvider`
only covers operations where behavior genuinely diverges by provider:

```csharp
public interface IGenericEventProvider
{
    string ProcessorName { get; }
    bool IsEnabled { get; }

    // Eventbrite returns a CheckoutUrl instead of reserving for real; the
    // built-in provider reserves synchronously. See §5.5.
    Task<ReserveTicketResult> ReserveTicket(GenericEventRecord evt, GenericTicketClassRecord ticketClass, ONUser user, uint quantity, CancellationToken cancellationToken);

    Task<bool> CancelTicket(GenericEventTicketRecord ticket, ONUser actor, string reason, CancellationToken cancellationToken);

    // Inbound sync — order/attendee/cancellation updates from the provider's system of record
    Task<SyncResult> SyncTicket(string processorTicketId, CancellationToken cancellationToken);
}
```

**The built-in path is itself a registered `IGenericEventProvider`**
(`BuiltInGenericEventProvider`, `ProcessorName = ""`), not a special case
callers branch around. `GenericEventProviderProvider.GetProcessor(record)`
does the same `AllProviders.FirstOrDefault(p => p.ProcessorName == record.ProcessorName)`
lookup `GenericPaymentProcessorProvider` does — since built-in records
already carry `ProcessorName == ""`, that lookup resolves them to
`BuiltInGenericEventProvider` the same way it resolves an Eventbrite record
to `EventbriteGenericEventProvider`, with zero `if (string.IsNullOrEmpty(...))`
branching anywhere in `EventService`/`AdminEventService`.

`ReserveTicketResult`/`SyncResult` are small plain result types (not proto
messages): per §5.5, `ReserveTicketResult` carries a `CheckoutUrl`
(Eventbrite-only) plus success/error info; `SyncResult` carries the
created/updated `GenericEventTicketRecord` (or enough to build one) and an
error reason mappable to `APIError`.

As of this writing, `BuiltInGenericEventProvider`'s three methods are
`NotImplementedException` stubs — the shape is scaffolded and registered in
DI, but reservation logic is deliberately deferred (see §5.5).

---

## 3. Proto layout

```
Fragments/Protos/IT/WebServices/Fragments/Authorization/Events/
├── DataRecords.proto              — shared, Generic-prefixed records + enums (§3.1)
├── EventSettings.proto            — settings envelope + Eventbrite sub-message (§4)
├── EventInterface.proto           — public gRPC surface, implemented directly by Combined
├── AdminEventInterface.proto      — admin gRPC surface, implemented directly by Combined
├── SharedTypes.proto              — EventBulkAction enum + progress record (reconciliation jobs)
└── Eventbrite/
    └── EventbriteEventSettings.proto   — Eventbrite-only: EventbriteEventPublicSettings,
                                           EventbriteEventPrivateSettings, EventbriteEventOwnerSettings
```

No `Authorization/Events/Manual/` folder and no `ManualEventInterface.proto`
— see the note at the top of this document.

### 3.1 `DataRecords.proto` — shared records

All flat, `Generic`-prefixed, mirroring Payments' `DataRecords.proto` style
(no public/private split at the storage-record level — that's handled by
`EventInterface.proto`'s response messages instead):

- **`GenericEventRecord`** — `ProcessorName`/`ProcessorEventID` (empty
  `ProcessorName` = built-in/no external processor), title/description/
  location, `VenueID` (references `EventPrivateSettings.Venues`, not
  embedded — see below), tags, embedded `TicketClasses`, optional
  `Recurrence` (unset = single event; replaces what was a Single/Recurring
  oneof in the old proto), cancel/audit fields.
- **`GenericTicketClassRecord`** — merges the old `TicketClassRecord` +
  `EventTicketClass` into one flat record. Lives only embedded on
  `GenericEventRecord.TicketClasses` — ticket classes are priced/capacity-
  limited per event, so there is deliberately no global/reusable ticket-
  class list in settings (removed after review — see git history on this
  file for the reasoning).
- **`GenericEventTicketRecord`** — merges old public/private ticket fields;
  `ProcessorName` denormalized from its parent event at creation time (see
  §5.2), `ProcessorTicketID` for the external order/attendee ID.
- **`GenericEventVenueRecord`** — `oneof VenueOneOf { Physical, Virtual }`
  with no separate `Type` discriminator field (the oneof case is the
  discriminator — a parallel enum field would just be a second, driftable
  source of truth, which is what the old proto's
  `EventRecordOneOfType`/`EventVenueOneOfType` companion fields were).
  Lives in `EventPrivateSettings.Venues` as the sole copy; events reference
  it by `VenueID` rather than embedding it, since venues are genuinely
  reusable across events (unlike ticket classes).
- **`GenericEventRecurrenceRule`** — `oneof EndCondition { Count, RepeatUntilUTC }`,
  a legitimate use of oneof (no parallel discriminator needed or present).
- Enums: `GenericEventTicketClassType`, `GenericEventTicketStatus`,
  `GenericEventRecurrenceFrequency`.

### Fields carried over from the Eventbrite plan's identity-matching discussion

Mirroring Payments' `ProcessorName`/`ProcessorCustomerID` pattern (plain
`string`, not an enum — see `EVENTS_EVENTBRITE_PLAN.md` Phase 3 §2):
`GenericEventRecord.ProcessorName`/`ProcessorEventID`,
`GenericEventTicketRecord.ProcessorName`/`ProcessorTicketID`.

---

## 4. Settings structure

`EventSettings.proto` (as it exists now):

```proto
import "Protos/IT/WebServices/Fragments/Authorization/Events/DataRecords.proto";
import "Protos/IT/WebServices/Fragments/Authorization/Events/Eventbrite/EventbriteEventSettings.proto";

message EventPublicSettings {
  bool Enabled = 1;             // built-in/module-wide switch — no separate "Manual.Enabled"
  IT.WebServices.Fragments.Authorization.Events.Eventbrite.EventbriteEventPublicSettings Eventbrite = 10;
}

message EventPrivateSettings {
  repeated GenericEventVenueRecord Venues = 1;   // shared — sole source of truth for venues, see §3.1
  IT.WebServices.Fragments.Authorization.Events.Eventbrite.EventbriteEventPrivateSettings Eventbrite = 10;
}

message EventOwnerSettings {
  IT.WebServices.Fragments.Authorization.Events.Eventbrite.EventbriteEventOwnerSettings Eventbrite = 10;
}
```

Notably **no `TicketClasses` here** (removed — see §3.1) and **no `Manual`
sub-message** (removed — see the note at the top of this document; the
top-level `Enabled` already covers "is the Events module on at all," and
there's no scenario where the built-in path is independently disabled
while Eventbrite keeps running, unlike Eventbrite's own `Enabled`, which is
genuinely independent).

This slots into `Settings/SettingsRecord.proto` as `SettingsPublicData.Events = 17`,
`SettingsPrivateData.Events = 17`, `SettingsOwnerData.Events = 17`.

`SettingsInterface.proto`'s three RPCs (`ModifyEventPublicSettings`/
`ModifyEventPrivateSettings`/`ModifyEventOwnerSettings`) are implemented in
`Settings/Services/SettingsService.cs`, following the same
read-modify-write + `VersionNum++` + audit-log pattern as every other
settings section (e.g. Merch).

**Cache-invalidation note carried over from the Eventbrite plan (Phase 2 §4)
still applies unchanged**: `Enabled` flags read on the hot path of ticket
purchase need to not be stale across replicas — this settings structure
doesn't fix that on its own.

---

## 5. Decisions

1. **`ITicketDataProvider`/`IEventDataProvider` live in `Base`.** The
   built-in path in `Combined` uses them as the actual source of truth;
   Eventbrite uses them as a synced local cache. If Eventbrite's caching
   needs grow a field the built-in path doesn't use (e.g.
   `LastSyncedAtUTC`), add it as an optional field on the shared record
   rather than forking the interface.

2. **`ProcessorName` is denormalized onto `GenericEventTicketRecord`, set
   from the parent event at ticket-creation time and never re-derived.**
   Ticket-only operations (`CancelTicket`, `SyncTicket`) need to know
   whether to dispatch to `IGenericEventProvider` (and which one) without
   necessarily having the parent event loaded — consistent with how
   `EventTicketPublicRecord.EventId` already duplicated onto the ticket in
   the old proto without issue. No code path should ever write a ticket's
   `ProcessorName` independently of its event's.

3. **There is no `Events.Manual` project, no `ManualEventInterface.proto`,
   and no `ManualEventSettings.proto`.** `Events.Combined` implements
   `EventInterface`/`AdminEventInterface` directly against `Events.Base`'s
   data providers for reads and for admin create/edit/cancel — those never
   go through `IGenericEventProvider` at all, for either the built-in path
   or Eventbrite, since you can't administratively create/edit an event
   that lives on Eventbrite through your own backend (you'd do that on
   Eventbrite and let it flow back in via sync). `IGenericEventProvider` is
   used only where behavior genuinely diverges by provider — currently just
   ticket reservation/cancellation/sync — and the built-in path implements
   it too (`BuiltInGenericEventProvider`, see §2), rather than being a
   special case the caller branches around.

4. **Webhook endpoint placement is deferred** — decide when Phase 3/6
   implementation starts, once the Eventbrite auth model and webhook
   signature-verification approach are chosen. Not blocking the structural
   scaffolding in §1-§4.

5. **`ReserveTicket` returns a backend-issued checkout URL for Eventbrite,
   not a real server-side reservation.** `ReserveTicketResult` carries (at
   minimum) a `CheckoutUrl` string plus success/error info — no ticket is
   created locally at this step; Eventbrite remains the system of record
   for the purchase until Phase 6's inbound sync creates/updates the local
   `GenericEventTicketRecord` (and, per Phase 5, triggers QR issuance). The
   built-in path, by contrast, reserves for real synchronously. Revisit if
   Eventbrite turns out to support backend-issuable discount codes/private
   ticket types (plan's open item #2) — that would upgrade this from a
   redirect to a hard server-side gate.
