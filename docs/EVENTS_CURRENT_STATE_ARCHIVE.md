# Events Module — Current-State Archive (pre-rewrite snapshot)

Captured 2026-08-03, prior to any rewrite/refactor of `Authorization/Events`. This
is a documentation snapshot only — a full inventory of every proto message and
service in the module as it exists today, so the current design can be
referenced or resurrected even if the module is rebuilt from scratch. See
[EVENTS_EVENTBRITE_PLAN.md](./EVENTS_EVENTBRITE_PLAN.md) for the refactor plan
this snapshot was taken ahead of.

## Layout

Two physical locations:

- **Protos + proto-partial C# classes**:
  `Fragments/Protos/IT/WebServices/Fragments/Authorization/Events/`
- **Service implementation project**: `Authorization/Events/`
  (`IT.WebServices.Authorization.Events.csproj`, `net10.0`, `Nullable=enable`,
  references only `Base/IT.WebServices.Base.csproj`)

Proto namespace/package for all of them: `IT.WebServices.Fragments.Authorization.Events`.

Generated TS artifacts also exist (not source of truth): `Fragments/ts-gen/Authorization/Events`,
`Fragments/dist/esm/Authorization/Events`, `Fragments/dist/protos/Authorization/Events`.

Registered in `Fragments/IT.WebServices.Fragments.csproj` lines 16-21 (`<None Remove>`)
and 98-103 (`<Protobuf Include>`) — all six protos compiled into the Fragments assembly.

---

# Part 1 — Proto files

## 1.1 `Events/EventRecord.proto` (197 lines)

Imports: `google/protobuf/timestamp.proto`, `CommonTypes.proto`, `Events/TicketClassRecord.proto`

### Enums

**`RecurrenceFrequency`**: REPEAT_NONE=0, REPEAT_DAILY=1, REPEAT_WEEKLY=2,
`REPEATE_MONTHLY`=3 (typo is in the source), REPEAT_YEARLY=4

**`EventRecordOneOfType`**: EVENT_ONE_OF_SINGLE=0, EVENT_ONE_OF_RECURRING=2
(note: value 1 is skipped)

**`EventVenueOneOfType`**: VENUE_ONE_OF_PHYSICAL=0, VENUE_ONE_OF_VIRTUAL=1

### Messages

**`PhysicalEventVenue`**: Name(1) string, Address(2) string, City(3) string,
StateOrProvince(4) string, PostalCode(5) string, Country(6) string,
PhoneNumber(7) string, EmailAddress(8) string

**`VirtualEventVenue`**: Name(1) string, Url(2) string,
AccessInstructions(3) string, ContactEmailAddress(4) string

**`EventVenue`**: VenueId(1) string, OneOfType(2) EventVenueOneOfType,
oneof `VenueOneOf` { Physical(3) PhysicalEventVenue, Virtual(4) VirtualEventVenue }

**`EventRecurrenceRule`**: Frequency(1) RecurrenceFrequency, Interval(2) uint32,
ByWeekday(3) repeated WeekdayEnum, oneof `EndCondition` { Count(4) uint32,
RepeatUntilUTC(5) Timestamp }, ExcludeDatesUTC(6) repeated Timestamp

**`SingleEventPublicRecord`**: EventId(1) string, Title(2) string,
Description(3) string, Location(4) string, StartOnUTC(5) Timestamp,
EndOnUTC(6) Timestamp, Tags(7) repeated string, TicketClasses(8) repeated
EventTicketClass, IsCanceled(9) bool, Venue(10) EventVenue, MaxTickets(11)
uint32, CanceledOnUTC(20) Timestamp, CreatedOnUTC(21) Timestamp,
ModifiedOnUTC(22) Timestamp

**`SingleEventPrivateRecord`**: CreatedById(1) string, ModifiedById(2) string,
ExternalSystemId(3) string, InternalNotes(4) string, CanceledById(5) string,
ExtraMetadata(6) map<string,string>, CanceledForReason(7) string

**`RecurringEventPublicRecord`**: EventId(1) string, Title(2) string,
Description(3) string, Location(4) string, MaxTickets(5) uint32,
TemplateStartOnUTC(6) Timestamp, TemplateEndOnUTC(7) Timestamp, Tags(8)
repeated string, TicketClasses(9) repeated EventTicketClass, Recurrence(10)
EventRecurrenceRule, IsCanceled(11) bool, RecurrenceHash(12) string, Venue(13)
EventVenue, CanceledOnUTC(21) Timestamp, CreatedOnUTC(22) Timestamp,
ModifiedOnUTC(23) Timestamp

**`RecurringEventPrivateRecord`** — identical shape to `SingleEventPrivateRecord`
(same field names/numbers).

**`EventRecord`**: EventId(1) string, OneOfType(2) EventRecordOneOfType,
oneof `EventPublicRecordOneOf` { SinglePublic(3), RecurringPublic(4) },
oneof `EventPrivateRecordOneOf` { SinglePrivate(5), RecurringPrivate(6) }

**`EventPublicRecord`**: EventId(1) string, OneOfType(2) EventRecordOneOfType,
oneof `EventPublicRecordOneOf` { SinglePublic(3), RecurringPublic(4) }

## 1.2 `Events/TicketClassRecord.proto` (28 lines)

**Enum `EventTicketClassType`**: TICKET_GENERAL_ACCESS=0,
TICKET_ALL_MEMBER_ACCESS=1, TICKET_MEMBER_LEVEL_ACCESS=2

**`TicketClassRecord`**: TicketClassId(1) string, Type(2)
EventTicketClassType, Name(3) string, AmountAvailable(4) uint32,
CountTowardEventMax(5) bool, MaxTicketsPerUser(6) uint32, IsTransferrable(7)
bool, PricePerTicketCents(8) uint32

**`EventTicketClass`**: TicketClassId(1) string, EventId(2) string, Public(3)
TicketClassRecord, SaleStartOnUTC(21) Timestamp, SaleEndOnUTC(22) Timestamp

## 1.3 `Events/EventTicketRecord.proto` (37 lines)

**Enum `EventTicketStatus`**: TICKET_STATUS_AVAILABLE=0, TICKET_STATUS_USED=1,
TICKET_STATUS_EXPIRED=2, TICKET_STATUS_CANCELED=3

**`EventTicketPublicRecord`**: TicketClassId(1) string, Title(2) string,
EventId(3) string, Status(4) EventTicketStatus, CreatedOnUTC(21) Timestamp,
ModifiedOnUTC(22) Timestamp, UsedOnUTC(23) Timestamp, ExpiredOnUTC(24)
Timestamp, CanceledOnUTC(25) Timestamp

**`EventTicketPrivateRecord`**: UserId(1) string, CreatedById(2) string,
ModifiedById(3) string, UsedById(4) string, CanceledById(5) string,
CanceledForReason(6) string

**`EventTicketRecord`**: TicketId(1) string, Public(2) EventTicketPublicRecord,
Private(3) EventTicketPrivateRecord

## 1.4 `Events/EventsSettings.proto` (16 lines)

**`EventPublicSettings`**: TicketClasses(1) repeated TicketClassRecord
**`EventPrivateSettings`**: Venues(1) repeated EventVenue
**`EventOwnerSettings`**: IsEnabled(1) bool

Wired into `Settings/SettingsRecord.proto`: `EventPublicSettings Events = 15`
(public, line 34), `EventPrivateSettings Events = 15` (private, line 45),
`EventOwnerSettings Events = 15` (owner, line 55).

## 1.5 `Events/EventInterface.proto` (124 lines) — public gRPC surface

service `EventInterface`:

| RPC | Request | Response | HTTP |
|---|---|---|---|
| GetEvent | GetEventRequest | GetEventResponse | GET `/api/events/{EventId}` |
| GetEvents | GetEventsRequest | GetEventsResponse | GET `/api/events` |
| GetOwnTicket | GetOwnTicketRequest | GetOwnTicketResponse | GET `/api/events/{EventId}/tickets/{TicketId}` |
| GetOwnTickets | GetOwnTicketsRequest | GetOwnTicketsResponse | GET `/api/events/{EventId}/tickets` |
| CancelOwnTicket | CancelOwnTicketRequest | CancelOwnTicketResponse | POST `/api/events/{EventId}/tickets/{TicketId}/cancel` |
| ReserveTicketForEvent | ReserveTicketForEventRequest | ReserveTicketForEventResponse | POST `/api/events/{EventId}/tickets/reserve` |
| UseTicket | UseTicketRequest | UseTicketResponse | POST `/api/events/tickets/use` |

Messages: GetEventRequest{EventId}, GetEventResponse{Error,Event},
GetEventsRequest{RecurrenceHash,IncludeCanceled}, GetEventsResponse{Error,
Events,Pagination}, GetOwnTicketRequest{TicketId,EventId},
GetOwnTicketResponse{Record,Error}, GetOwnTicketsRequest{EventId,
IncludeCanceled,IncludeUsed,IncludeExpired}, GetOwnTicketsResponse{Records,
Error}, CancelOwnTicketRequest{EventId,TicketId,Reason},
CancelOwnTicketResponse{Error}, ReserveTicketForEventRequest{EventId,
TicketClassId,Quantity}, ReserveTicketForEventResponse{Error,Tickets},
UseTicketRequest{TicketId}, UseTicketResponse{Error}

## 1.6 `Events/AdminEventInterface.proto` (206 lines) — admin gRPC surface

service `AdminEventInterface`:

| RPC | Request | Response | HTTP |
|---|---|---|---|
| AdminCreateEvent | AdminCreateEventRequest | AdminCreateEventResponse | POST `/api/admin/events/create` |
| AdminCreateRecurringEvent | AdminCreateRecurringEventRequest | AdminCreateEventResponse (reused) | POST `/api/admin/events/create-recurring` |
| AdminGetEvent | AdminGetEventRequest | AdminGetEventResponse | GET `/api/admin/events/{EventId}` |
| AdminGetEvents | AdminGetEventsRequest | AdminGetEventsResponse | GET `/api/admin/events` |
| AdminModifyEvent | AdminModifyEventRequest | AdminModifyEventResponse | POST `/api/admin/events/modify` |
| AdminCancelEvent | AdminCancelEventRequest | AdminCancelEventResponse | POST `/api/admin/events/cancel` |
| AdminCancelAllRecurringEvents | AdminCancelAllRecurringEventsRequest | AdminCancelAllRecurringEventsResponse | POST `/api/admin/events/cancel-all-recurring` |
| AdminGetTicket | AdminGetTicketRequest | AdminGetTicketResponse | GET `/api/admin/events/{EventId}/tickets/{TicketId}` |
| AdminGetTicketsForEvent | AdminGetTicketsForEventRequest | AdminGetTicketsForEventResponse | GET `/api/admin/events/{EventId}/tickets` |
| AdminCancelOtherTicket | AdminCancelOtherTicketRequest | AdminCancelOtherTicketResponse | POST `/api/admin/events/{EventId}/tickets/{TicketId}/cancel` |
| AdminReserveEventTicketForUser | AdminReserveEventTicketForUserRequest | AdminReserveEventTicketForUserResponse | POST `/api/admin/events/{EventId}/tickets/reserve` |

Note: `AdminModifyRecurringEventRequest`/`Response` messages exist in the proto
but have **no RPC** — orphaned.

Messages: CreateEventData{Title,Description,Venue,StartTimeUTC,EndTimeUTC,
Tags,TicketClasses,ExtraData map<string,string>,MaxTickets},
AdminCreateEventRequest{Data}, AdminCreateEventResponse{Error,Event},
AdminCreateRecurringEventRequest{Data(1),RecurrenceHash(2),
RecurrenceRule(9) — fields 3-8 unused}, AdminGetEventRequest{EventId},
AdminGetEventResponse{Error,Event}, AdminGetEventsRequest{RecurrenceHash,
IncludeCanceled,Pagination(10)}, AdminGetEventsResponse{Error,Events,
Pagination}, AdminModifyEventRequest{EventId,Data},
AdminModifyEventResponse{Error}, AdminCancelEventRequest{EventId,Reason},
AdminCancelEventResponse{Error}, AdminCancelAllRecurringEventsRequest{
RecurrenceHash,Reason}, AdminCancelAllRecurringEventsResponse{Error},
AdminGetTicketRequest{EventId,TicketId}, AdminGetTicketResponse{Record}
(**no Error field**), AdminGetTicketsForEventRequest{EventId},
AdminGetTicketsForEventResponse{Records} (**no Error field**),
AdminCancelOtherTicketRequest{EventId,TicketId,UserId,Reason},
AdminCancelOtherTicketResponse{Error}, AdminReserveEventTicketForUserRequest{
EventId,UserId,TicketClassId,Quantity **(int32, inconsistent with uint32
elsewhere)**}, AdminReserveEventTicketForUserResponse{Error,Tickets}

## 1.7 Referenced external proto types

- `CommonTypes.proto`: `WeekdayEnum` (Sunday=0..Saturday=6), `Pagination`
  {PageOffsetStart,PageOffsetEnd,PageTotalItems}
- `Errors.proto`: `APIError`, `APIErrorReason`
- `Authorization/Claims.proto`: `ClaimsInterface` service
  (`GetClaims(GetClaimsRequest) returns (GetClaimsResponse)`),
  `ClaimRecord{Name,Value,ExpiresOnUTC}`, `GetClaimsRequest{UserID}`,
  `GetClaimsResponse{Claims repeated}`

---

# Part 2 — C# files

## 2.1 Proto-partial extension classes (in Fragments)

**`Events/EventRecord.cs`** (63 lines) — `partial class EventRecord`
- `EventRecord(AdminCreateRecurringEventRequest request, string userId, string recurrenceHash)` —
  builds a recurring `EventRecord`, new Guid EventId, populates
  `RecurringPublic`/`RecurringPrivate`.
- `EventPublicRecord? GetPublicRecord()` — maps the oneof to an
  `EventPublicRecord`. **Bug: does not copy `EventId` or `OneOfType`** onto
  the returned record.
- `EventRecord.cs:25` — commented-out `// Location = request.Data.Venue?.Name ?? "",`

**`Events/EventTicketClass.cs`** (33 lines) — `partial class EventTicketClass`
- `bool HasRequestedAmount(int numToReserve)` — `numToReserve > 0 && <= Public.AmountAvailable`.
  Dead local `maxPerUser` at line 17.
- `bool HitReservationLimit(int numToReserve, int numReservedAlready = 0)` —
  `numToReserve > maxPerUser || numToReserve <= numReservedAlready` (second
  clause looks suspicious).
- `bool IsOnSale()` — `SaleStartOnUTC <= now && SaleEndOnUTC >= now`.

**`Events/EventTicketRecord.cs`** (99 lines) — `partial class EventTicketRecord`
- `EventTicketRecord Cancel(string canceledById, string reason = "")`
- `EventTicketRecord MarkAsUsed(string usedById)`
- `static List<EventTicketRecord> GenerateRecords(int numToGenerate, EventRecord eventRecord, string userId, EventTicketClass ticketClass)` —
  Title = `ticketClass.Public.Name + " " + event title`; ExpiredOnUTC = event
  end (single) or template end (recurring).

## 2.2 `Authorization/Events/Data/IEventDataProvider.cs` (20 lines)

```csharp
Task<bool> Create(EventRecord record);
Task<bool> CreateRecurring(IEnumerable<EventRecord> records);
Task<EventRecord?> GetById(Guid id);
IAsyncEnumerable<EventRecord> GetEvents();
Task<bool> Update(EventRecord record);
Task<bool> UpdateRecurring(IEnumerable<EventRecord> records);
Task<bool> Exists(Guid eventId);
```

## 2.3 `Authorization/Events/Data/ITicketDataProvider.cs` (20 lines)

```csharp
Task<bool> Create(EventTicketRecord record);
Task<bool> Create(List<EventTicketRecord> records);
Task<EventTicketRecord?> GetById(Guid ticketId, Guid eventId);
IAsyncEnumerable<EventTicketRecord> GetAllByEvent(Guid eventId);
IAsyncEnumerable<EventTicketRecord> GetAllByUser(Guid userId);
IAsyncEnumerable<EventTicketRecord> GetAllByUserAndEvent(Guid userId, Guid eventId);
Task<bool> Update(EventTicketRecord record);
```

## 2.4 `Authorization/Events/Data/FileSystemEventDataProvider.cs` (192 lines)

`IEventDataProvider` impl. Storage root: `settings.DataStore/event/events/`,
one file per event (protobuf bytes). `Create`/`CreateRecurring` refuse to
overwrite existing files; `CreateRecurring` has **no transactionality** —
a failure partway through a batch leaves partial writes. `GetEvents()` does a
full recursive directory scan every call, no filtering/paging. `Exists`,
`Update`, `UpdateRecurring` round out the interface.

## 2.5 `Authorization/Events/Data/FileSystemTicketDataProvider.cs` (180 lines)

`ITicketDataProvider` impl. Layout: `event/tickets/{eventId}/{ticketId}`.
`Create(List<>)` breaks on first failure but returns `allSucceeded` — **partial-write
hazard**. `GetById(ticketId, eventId)` is **broken**: it enumerates files using
`eventId` as a filename search pattern rather than as a directory segment
(lines 81-84), so it will not match the actual on-disk layout.
`GetAllByUser`/`GetAllByUserAndEvent` do full scans and **swallow parse
exceptions** (`catch { continue; }`).

## 2.6 `Authorization/Events/Helpers/EventTicketClassHelper.cs` (32 lines)

Reads ticket classes from **global public settings**
(`SettingsHelper.Public.Events.TicketClasses`), not from the event record —
architectural mismatch with `EventRecord.TicketClasses`, which is a separate,
competing source of truth. `GetAll()`, `GetById(string id)`.

## 2.7 `Authorization/Events/Helpers/EventVenueHelper.cs` (31 lines)

Reads venues from global private settings (`SettingsHelper.Private.Events.Venues`).
`GetAll()`, `GetById(string id)`. **Registered in DI but never injected into
any service** — dead code path.

## 2.8 `Authorization/Events/Helpers/RecurrenceHelper.cs` (108 lines)

`static class RecurrenceHelper`:
- `record EventInstance(DateTime Start, DateTime End)`
- `GenerateRecurrenceHash(string input)` — SHA256 → lowercase hex
- `GenerateInstances(EventRecord eventRecord)` — walks from
  `TemplateStartOnUTC` by frequency×interval, honors `ExcludeDatesUTC`,
  `ByWeekday`, `Count`; duration preserved
- `GetEndConditionLimit(...)` — hardcoded 5-year safety cap when no
  `RepeatUntilUTC`/`Count` given (line 90)
- `ToWeekdayEnum(DayOfWeek day)` — falls back to Monday on unmapped input

## 2.9 `Authorization/Events/Extensions/DIExtensions.cs` (35 lines)

```csharp
AddEventsClasses(services):
  AddSingleton<ITicketDataProvider, FileSystemTicketDataProvider>()
  AddSingleton<IEventDataProvider, FileSystemEventDataProvider>()
  AddScoped<EventTicketClassHelper>()
  AddScoped<EventVenueHelper>()

MapEventsGrpcServices(endpoints):
  MapGrpcService<ClaimsService>()
  MapGrpcService<EventService>()
  MapGrpcService<AdminEventService>()
```

Called from `Services/Combined/Startup.cs:104` and `:148`.

## 2.10 `Authorization/Events/Extensions/EventTicketRecordExtensions.cs` (22 lines)

`ToClaimRecords(this IEnumerable<EventTicketRecord>)` maps `Name = TicketId`,
`Value = Public.EventId`, `ExpiresOnUTC = Public.ExpiredOnUTC`. **Dead code** —
`ClaimsService` builds `ClaimRecord`s inline with a *different* mapping and
never calls this extension.

## 2.11 `Authorization/Events/Extensions/FluentExtensions.cs` (17 lines)

`Tap<T>(this T obj, Action<T> action)` — generic fluent helper.

## 2.12 `Authorization/Events/Extensions/ParserExtensions.cs` (205 lines)

**Entire class body is commented out** (lines 14-203) — two dead SQL-reader
parsers referencing a long-gone schema (`EventAccessData`,
`EventLocationData`, `LifecycleMetadataPublic/Private`, `EventSettings`,
`EventTicketRecord.Price/MaxAttendees/MaxPerUser/QuantityAvailible` — none of
which exist in the current proto). Compiles to an empty static class; prime
deletion candidate regardless of rewrite-vs-refactor.

## 2.13 `Authorization/Events/Services/EventService.cs` (315 lines) — public gRPC surface

`[Authorize]` (class-level, any authenticated user, no method-level role
gates anywhere in this service).

ctor: `(ILogger, IEventDataProvider, ITicketDataProvider, ONUserHelper, EventTicketClassHelper)` —
`_userHelper` injected but never used (methods call static
`ONUserHelper.ParseUser(context.GetHttpContext())` instead).

| Method | Behavior |
|---|---|
| GetEvent | Guid-parse → InvalidRequest; lookup; NotFound; else `GetPublicRecord()` |
| GetEvents | Enumerates **all** events. **Ignores `RecurrenceHash` and `IncludeCanceled`; never populates `Pagination`.** Dead local `classes` at line 77. |
| GetOwnTicket | Finds ticket in user's tickets by TicketId. **No error when not found; ignores `EventId`.** |
| GetOwnTickets | All user tickets. **Ignores EventId/IncludeCanceled/IncludeUsed/IncludeExpired filters.** |
| CancelOwnTicket | Finds own ticket, `.Cancel(user.Id, reason)`, persists |
| ReserveTicketForEvent | **Effectively a no-op** — validates inputs then returns success without creating any ticket; full reservation logic is commented out |
| UseTicket | NotFound/Conflict(used)/Conflict(canceled)/InvalidDate(expired); `MarkAsUsed`, persists. No ticket-manager role gate — scoped implicitly by looking up only the caller's own tickets. |

Error helpers used inconsistently: `GenericErrorExtensions.Create(...)` and
`.CreateError(...)` both appear in the same file. Also imports
`System.Diagnostics.Eventing.Reader` (Windows-only namespace) — almost
certainly an accidental auto-import, unused.

**TODOs/commented-out**: `:81` "make this more efficient"; `:166` "Handle
Event Count Update"; `:198` "Rework generation to include the
EventTicketClass"; `:199-262` the entire reservation body (availability
check, per-user limit check, sale-window check, `GenerateRecords`,
`ticketProvider.Create`) commented out, referencing a now-nonexistent
`TicketError`/`ReserveTicketErrorType` type.

## 2.14 `Authorization/Events/Services/AdminEventService.cs` (648 lines) — admin gRPC surface

Namespace is `IT.WebServices.Authorization.Events.Services.Services`
(doubled `Services` — inconsistent with EventService/ClaimsService).

Class-level `[Authorize(Roles = RoleAbilities.ROLE_IS_EVENT_MANAGER_OR_HIGHER)]`.

ctor: `(ILogger, ITicketDataProvider, IEventDataProvider, ONUserHelper, EventTicketClassHelper, IAuditLogService)` —
`_ticketClassHelper` and `auditLogHelper` stored but never used; all audit
logging is commented out.

| Method | Auth | Notes |
|---|---|---|
| AdminCreateEvent | EVENT_MANAGER_OR_HIGHER | Builds `SingleEventPublicRecord` directly — **does not set `Location`, `MaxTickets`, or `OneOfType`** |
| AdminCreateRecurringEvent | EVENT_MANAGER_OR_HIGHER | Hash from JSON of rule+eventId+venueId; one persisted `EventRecord` per `RecurrenceHelper` instance, sharing `RecurrenceHash`; returns first as "template" |
| AdminGetEvent | EVENT_MANAGER_OR_HIGHER | |
| AdminGetEvents | EVENT_MANAGER_OR_HIGHER | **Never populates `Pagination`**, ignores request's `Pagination` |
| AdminModifyEvent | EVENT_MANAGER_OR_HIGHER | Only handles single events (rejects recurring); sets `ModifiedById` from HTTP context identity name rather than `ONUserHelper` |
| AdminCancelEvent | EVENT_MANAGER_OR_HIGHER | |
| AdminCancelAllRecurringEvents | EVENT_MANAGER_OR_HIGHER | **Never sets `CanceledForReason` from request** |
| AdminGetTicket | EVENT_TICKET_MANAGER_OR_HIGHER | Response has no Error field; empty on bad Guid |
| AdminGetTicketsForEvent | **no method attribute** — falls back to class-level EVENT_MANAGER_OR_HIGHER, inconsistent with sibling ticket methods | |
| AdminCancelOtherTicket | EVENT_TICKET_MANAGER_OR_HIGHER | `throw new NotImplementedException();` |
| AdminReserveEventTicketForUser | EVENT_TICKET_MANAGER_OR_HIGHER | Stub — returns `base.AdminReserveEventTicketForUser(...)` → gRPC Unimplemented |

Private helpers `GetSingleEvents(...)` / `GetRecurringEvents(...)` (lines 591,
613) — **logic bug**: when `includeCanceled == true` they return *only*
canceled events (inverted semantics), and neither checks `OneOfType` before
dereferencing `SinglePublic`, so a recurring record passed through the
single-event path will throw/null.

**TODOs/NotImplemented/commented-out**: five separate `// TODO: Add Actor` +
commented `auditLogHelper.TryLogEvent` blocks (lines ~80-92, 190-205,
338-352, 435-449, 514-528); `:581-582` NotImplementedException for
`AdminCancelOtherTicket`; `:588-589` stub for
`AdminReserveEventTicketForUser`; `:305` commented-out Location assignment.

## 2.15 `Authorization/Events/Services/ClaimsService.cs` (56 lines)

`ClaimsInterface.ClaimsInterfaceBase` — **no `[Authorize]` at all** (internal
service-to-service claims aggregation, consistent with Payment's equivalent).

`GetClaims` → Guid-parse `UserID`, empty on failure, else
`GetEventClaims(userId)` — filters user's tickets to
`Private.UserId == userId`, `Public.CreatedOnUTC == null`,
`Public.UsedOnUTC == null`; maps `Name = Public.Title`, `Value = TicketId`,
`ExpiresOnUTC = Public.ExpiredOnUTC`.

**Logic bug (line 47)**: filters `Public.CreatedOnUTC == null` — but
`GenerateRecords` always sets `CreatedOnUTC`, so this predicate excludes
every real ticket. Almost certainly meant `CanceledOnUTC == null`. Net
effect: this service always returns zero claims today.

---

# Part 3 — gRPC method / auth summary

Roles (`Authentication/Shared/RoleAbilities.cs`):
- `ROLE_EVENT_MANAGER = "evt_manager"` (line 46)
- `ROLE_EVENT_TICKET_MANAGER = "evt_tkt_manager"` (line 49)
- `ROLE_IS_EVENT_MANAGER_OR_HIGHER` = owner, admin, evt_manager (line 71)
- `ROLE_IS_EVENT_TICKET_MANAGER_OR_HIGHER` = owner, admin, evt_manager,
  evt_tkt_manager (line 72)
- `ONUser.IsEventManager` / `IsEventTicketManager` helpers (lines 94, 96)

- **EventService** — every method gated only by class-level `[Authorize]`
  (any authenticated user); no method-level attributes.
- **AdminEventService** — class-level EVENT_MANAGER_OR_HIGHER; ticket-specific
  methods raised to EVENT_TICKET_MANAGER_OR_HIGHER except
  `AdminGetTicketsForEvent`, which has no method attribute (inconsistency).
- **ClaimsService** — ungated (internal use only).

---

# Part 4 — Client-side code

**There is no Events gRPC client wrapper.** `Clients/` has `CMS`, `Merch`,
`Notifications`, `Payment`, `Settings` subfolders — no `Events/` directory,
no `EventInterfaceClient`/`AdminEventInterfaceClient` usage anywhere in the
repo. This confirms nothing on the frontend currently consumes this API.

The only Events-adjacent client code is settings-related, in
`Clients/Settings/SettingsClient.cs`:
- `ModifyEventOwnerSettings` (line 97)
- `ModifyEventPrivateSettings` (line 106)
- `ModifyEventPublicSettings` (line 115)

Each calls the settings gRPC client, invalidates cache, returns
`res.Error`. Server side (`Settings/Services/SettingsService.cs`, all
`[Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]`, fully
implemented with audit logging): `ModifyEventPublicSettings` (line 955),
`ModifyEventPrivateSettings` (line 994), `ModifyEventOwnerSettings` (line 1032).

The Events module *reads* those settings via `EventTicketClassHelper`/
`EventVenueHelper`. **Nothing in the Events module reads
`EventOwnerSettings.IsEnabled` — the enable/disable flag is defined but
never checked anywhere.**

---

# Part 5 — Consolidated TODO / NotImplemented / commented-out index

| File:Line | Item |
|---|---|
| `AdminEventService.cs:80,190,338,435,514` | `// TODO: Add Actor` (×5, each followed by a commented-out audit log block) |
| `AdminEventService.cs:305` | commented-out Location assignment |
| `AdminEventService.cs:581-582` | `// TODO Implement` + `throw new NotImplementedException();` (AdminCancelOtherTicket) |
| `AdminEventService.cs:588-589` | `// TODO Implement` + stub returning gRPC Unimplemented (AdminReserveEventTicketForUser) |
| `EventService.cs:81` | `// TODO: Make this more efficient` |
| `EventService.cs:166` | `// TODO: Handle Event Count Update` |
| `EventService.cs:198-262` | entire ticket-reservation implementation commented out |
| `Extensions/ParserExtensions.cs:14-203` | entire class body commented out (dead SQL parsers for a schema that no longer exists) |
| `Fragments/.../EventRecord.cs:25` | commented-out Location assignment |

---

# Part 6 — Notable current-state gaps (for rewrite planning)

1. **Ticket reservation is entirely non-functional** — the module can
   create/read/cancel events but cannot issue a ticket through any public
   or admin path today.
2. **Claims never emitted** — `ClaimsService`'s filter excludes every real
   ticket, so event tickets never surface as claims (e.g. to gate content).
3. **Ticket classes live in global settings, not on the event** — yet
   `EventRecord` also carries its own `TicketClasses` list; two competing
   sources of truth for the same concept.
4. **Pagination is defined in the proto but implemented nowhere.**
5. **Request filter fields are widely ignored**: `IncludeCanceled`,
   `IncludeUsed`, `IncludeExpired`, `RecurrenceHash`, `EventId` scoping on
   ticket lookups.
6. **`includeCanceled` semantics are inverted** in both
   `AdminEventService` list helpers.
7. **`FileSystemTicketDataProvider.GetById` is broken** — searches by
   filename pattern instead of directory path, won't match the real layout.
8. **No audit logging is active** despite `IAuditLogService` being injected
   into `AdminEventService`.
9. **Storage is full-scan flat-file protobuf** — every list/lookup-by-user
   call reads every record on disk; no index; no transaction/rollback on
   partial recurring-event writes.
10. **Assorted inconsistencies**: doubled `Services.Services` namespace on
    `AdminEventService`; `Create`/`CreateError` error-helper naming
    inconsistency; `int32` vs `uint32` Quantity field inconsistency;
    `EventRecordOneOfType` skips value 1; `REPEATE_MONTHLY` typo;
    `AdminModifyRecurringEvent{Request,Response}` messages defined with no
    RPC to use them.
11. **Dead/unused code**: `EventVenueHelper` (registered, never injected),
    `EventTicketRecordExtensions.ToClaimRecords` (defined, never called),
    `ParserExtensions.cs` (205 lines, fully commented out).
12. **No test project** exists for this module.
13. **No frontend client exists** — no `Clients/Events/` folder, confirming
    nothing currently consumes this API outside the settings-modification
    path.
