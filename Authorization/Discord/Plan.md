# Discord Bot Implementation Workflow

Reimplementation of `beanie-bot-v3` (TypeScript/discord.js) as **InvertedBot** — a native ASP.NET Core service
following the IT.WebServices microservice patterns. Uses Discord's HTTP API + Interaction
Endpoint webhook — no Discord library, no persistent Gateway WebSocket.

---

## Key Architectural Differences from beanie-bot-v3

| Concern | beanie-bot-v3 | This Service |
|---------|---------------|--------------|
| Discord comms | discord.js + Gateway WebSocket | Raw `HttpClient` + Interaction Endpoint webhook |
| Modal handling | Stateful `awaitModalSubmit()` | Stateless: correlate via `custom_id` encoding |
| Database | Prisma + SQLite | FileSystem Protobuf (same as Events/Payment) |
| Auth | Separate `API_USER`/`API_PASS` login | `ONUserHelper` + JWT (shared auth project) |
| Role assignment | `member.roles.add(roleId)` via Gateway | `PATCH /guilds/{id}/members/{userId}` via REST |
| User identity key | Timcast email | `UserPublicRecord.UserID` (Guid from auth service) |
| Account linking | `/link` + email verification code | Discord Linked Roles OAuth2 flow |
| DI | Inversify | `Microsoft.Extensions.DependencyInjection` |
| Timeouts ("Goofs") | "Goof" / `DiscordGoofRecord` | "Shun" / `DiscordShunRecord` |

### Why `/link` and `/unlink` Are Removed

The original bot used `/link` (email → verification code) and `/verify` (code → create member record)
to associate a Discord account with a Timcast account. **Discord Linked Roles replaces this entirely.**

The OAuth2 flow gives the service the user's Discord identity directly — no email entry, no verification
codes, no `DiscordAuthLogRecord`. The user clicks "Connect" in the Discord server's Linked Roles UI,
goes through OAuth2, and your service receives their Discord user ID and stores the linkage automatically.

`/admin-link` and `/admin-unlink` are kept for staff overrides.

### Modal State Correlation

Since there is no persistent connection, modals cannot be "awaited". Instead, encode state
directly in the modal's `custom_id` using a delimited format:

```
{action}:{discordUserId}:{encodedPayload}

Examples:
  close-ticket:123456789:threadId123   (ticket close confirm)
```

The interaction controller routes on the prefix of `custom_id` to the correct handler.

---

## Phase 1: Project Setup

**Update `IT.WebServices.Authorization.Discord.csproj`:**
- Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- Add project references: `IT.WebServices.Fragments`, `IT.WebServices.Authentication.Shared`, `IT.WebServices.Base`
- NuGet packages:
  - `Grpc.AspNetCore`
  - `NSec.Cryptography` — Ed25519 signature verification (Discord interaction security)

---

## Phase 2: Proto Definitions (Actual)

Proto files are at `Fragments/Protos/.../Authorization/Discord/`.

### `DiscordRecords.proto`

```proto
// TODO: Add BannedOnUTC, BannedByDiscordId, and BannedReason to Record
// TODO: Split Into DiscordMemberPublicRecord and DiscordMemberPrivateRecord
message DiscordMemberRecord {
  string PlatformUserId = 1;           // References UserRecord.UserID
  string DiscordUserId = 2;            // Discord snowflake ID
  string DiscordUserName = 3;
  uint32 CurrentLevelCents = 4;
  google.protobuf.Timestamp ExpiresOnUTC = 5;
  string InternalSubscriptionId = 6;   // Reference to internal subscription record
  repeated string Tiers = 7;           // e.g. ["VIP", "Elite"]
  google.protobuf.Timestamp CreatedOnUTC = 8;
  google.protobuf.Timestamp ModifiedOnUTC = 9;
}

message DiscordAuthLogRecord {
  string PlatformUserId = 1;
  string DiscordUserId = 2;
  string DiscordUserName = 3;
  string AuthCode = 6;
  google.protobuf.Timestamp CreatedOnUTC = 5;
}

enum ShunStatus {
  SHUN_ACTIVE = 0;
  SHUN_INACTIVE = 1;
}

message DiscordShunRecord {
  string ShunId = 1;                   // Guid
  string PlatformUserId = 2;           // References UserRecord.UserID
  string DiscordUserId = 3;
  string ShunnedByDiscordId = 4;       // Discord ID of mod/admin who shunned
  string Reason = 5;                   // Reason for shun
  ShunStatus Status = 6;
  google.protobuf.Timestamp CreatedOnUTC = 7;
  google.protobuf.Timestamp ModifiedOnUTC = 8;
  google.protobuf.Timestamp UnShunnedOnUTC = 9;
  string UnShunnedByDiscordID = 10;    // Discord ID of mod/admin who unshunned
}

enum TicketStatus {
  TICKET_OPEN = 0;
  TICKET_CLOSED = 1;
}

// TODO: Add Messages sent in thread
message DiscordTicketRecord {
  string TicketId = 1;                 // Guid
  string DiscordUserId = 2;
  string DiscordUserName = 3;
  string PlatformUserId = 4;           // Empty if account is not linked
  string ThreadId = 5;                 // Discord thread snowflake ID
  TicketStatus Status = 6;
  string Subject = 7;
  string Text = 8;
  google.protobuf.Timestamp CreatedOnUTC = 9;
  google.protobuf.Timestamp ModifiedOnUTC = 10;
  google.protobuf.Timestamp ClosedOnUTC = 11;
  string ClosedByDiscordId = 12;
}
```

### `DiscordInterface.proto`

```proto
// TODO: Split Into DiscordInterface and AdminDiscordInterface
service DiscordInterface {
  rpc CreateMemberRecord (CreateMemberRecordRequest) returns (CreateMemberRecordResponse);
  rpc ModifyMemberRecord (ModifyMemberRecordRequest) returns (ModifyMemberRecordResponse);
  rpc GetMemberByPlatformId (GetMemberByPlatformIdRequest) returns (GetMemberResponse);
  rpc GetMemberByDiscordId (GetMemberByDiscordIdRequest) returns (GetMemberResponse);
  rpc GetMembers (GetMembersRequest) returns (GetMembersResponse);
  rpc BanMember (BanMemberRequest) returns (BanMemberResponse);
  rpc ShunDiscordMember (ShunDiscordMemberRequest) returns (ShunDiscordMemberResponse);
  rpc UnShunDiscordMember (UnShunDiscordMemberRequest) returns (UnShunDiscordMemberResponse);
  rpc GetUserShuns (GetUserShunsRequest) returns (GetUserShunsResponse);
  rpc CreateTicket (CreateTicketRequest) returns (CreateTicketResponse);
  rpc CloseTicket (CloseTicketRequest) returns (CloseTicketResponse);
  rpc GetTickets (GetTicketsRequest) returns (GetTicketsResponse);
}
```

Key request/response shapes:
- **GetMembers** — filterable by `PossibleUserPlatformIds`, `PossibleUserDiscordIds`, `IncludeShunned`, `IncludeBanned`, `ExpiredOnUTC`; paginated via `PageSize`/`PageOffset`
- **BanMember** — takes `PlatformUserId`, `DiscordUserId`, `BannedReason`
- **GetUserShuns** — takes `PlatformUserId`, `IncludeInactive`; paginated
- **GetTickets** — filterable by `DiscordUserId`, `IncludeClosed`; paginated

---

## Phase 3: Discord REST Client

**`Helpers/DiscordRestClient.cs`** — thin `HttpClient` wrapper against `https://discord.com/api/v10`.
All requests use `Authorization: Bot {DISCORD_BOT_TOKEN}`. Configured via `DiscordSettings`.

#### Interactions

```csharp
// PUT /applications/{appId}/commands  (global)
// PUT /applications/{appId}/guilds/{guildId}/commands  (guild-scoped, instant)
ValueTask RegisterCommandsAsync(IEnumerable<DiscordApplicationCommand> commands);

// POST /interactions/{interactionId}/{interactionToken}/callback
ValueTask RespondToInteractionAsync(string interactionId, string interactionToken, InteractionResponse response);

// POST /interactions/{interactionId}/{interactionToken}/callback  { type: 5 }
ValueTask DeferInteractionAsync(string interactionId, string interactionToken, bool ephemeral = true);

// PATCH /webhooks/{appId}/{interactionToken}/messages/@original
ValueTask EditInteractionResponseAsync(string interactionToken, InteractionCallbackData data);
```

#### Guild Members & Roles

```csharp
// GET /guilds/{guildId}/members/{userId}
ValueTask<DiscordInteractionMember> GetGuildMemberAsync(string guildId, string userId);

// PUT /guilds/{guildId}/members/{userId}/roles/{roleId}
ValueTask AddRoleAsync(string guildId, string userId, string roleId);

// DELETE /guilds/{guildId}/members/{userId}/roles/{roleId}
ValueTask RemoveRoleAsync(string guildId, string userId, string roleId);
```

#### Threads & Channels

```csharp
// POST /channels/{channelId}/threads  { name, type: 11 (PRIVATE_THREAD) }
ValueTask<string> CreateThreadAsync(string channelId, string name);  // returns threadId

// PATCH /channels/{threadId}  { locked: true }
ValueTask LockThreadAsync(string threadId);

// PATCH /channels/{threadId}  { archived: true }
ValueTask ArchiveThreadAsync(string threadId);
```

#### OAuth2 (Linked Roles)

```csharp
// POST /oauth2/token  grant_type=authorization_code
ValueTask<OAuthTokenResponse> ExchangeCodeAsync(string code, string redirectUri);

// POST /oauth2/token  grant_type=refresh_token
ValueTask<OAuthTokenResponse> RefreshTokenAsync(string refreshToken);

// PUT /users/@me/applications/{appId}/role-connection
// Uses user's OAuth2 access token, not bot token
ValueTask PushLinkedRoleMetadataAsync(string userAccessToken, LinkedRoleMetadata metadata);

// PUT /applications/{appId}/role-connections/metadata
ValueTask RegisterRoleMetadataAsync(IEnumerable<RoleMetadataSchema> schema);
```

Additional DTOs needed for OAuth2 methods (also in `Models/Discord/`):

```csharp
class OAuthTokenResponse {
    string AccessToken;
    string RefreshToken;
    int ExpiresIn;          // seconds
    string TokenType;
    string Scope;
}

class LinkedRoleMetadata {
    string PlatformName;             // e.g. "Timcast"
    string PlatformUsername;         // display name on the platform
    Dictionary<string, string> Metadata;  // keys match RegisterRoleMetadataAsync schema
}

class RoleMetadataSchema {
    int Type;               // 2=number_gt, 7=boolean_equal
    string Key;             // e.g. "subscription_level"
    string Name;            // display name shown in Discord
    string Description;
}
```

### DTOs (`Models/Discord/`)

All Discord API JSON payloads are represented as C# DTOs in `Models/Discord/`.
Serialize/deserialize with `System.Text.Json` using `JsonPropertyName` attributes to match Discord's snake_case API.

#### Inbound DTOs (Discord → Service)

These map the JSON body Discord POSTs to `/api/discord/interactions`.

```csharp
// Top-level interaction payload
class DiscordInteraction {
    int Type;                          // 1=PING, 2=COMMAND, 3=COMPONENT, 5=MODAL_SUBMIT
    string Id;
    string Token;                      // Used to respond via REST
    string ApplicationId;
    DiscordInteractionData? Data;
    DiscordInteractionMember? Member;  // Present in guild interactions
    string? GuildId;
    string? ChannelId;
}

class DiscordInteractionData {
    string? Id;                        // Command ID (type 2 only)
    string? Name;                      // Command name (type 2 only)
    string? CustomId;                  // Component/modal custom_id (type 3, 5)
    int? ComponentType;                // 2=Button (type 3 only)
    List<DiscordInteractionOption>? Options;    // Slash command options (type 2)
    List<DiscordModalActionRow>? Components;   // Modal fields (type 5)
}

class DiscordInteractionOption {
    string Name;
    int Type;                          // 3=string, 4=int, 5=bool, 6=user
    string? Value;
}

class DiscordModalActionRow {
    int Type;                          // Always 1
    List<DiscordModalTextInput> Components;
}

class DiscordModalTextInput {
    string CustomId;
    string Value;
}

class DiscordInteractionMember {
    DiscordInteractionUser User;
    List<string> Roles;                // Snowflake role IDs — used for permission checks
}

class DiscordInteractionUser {
    string Id;                         // Discord snowflake
    string Username;
    string GlobalName;
}
```

#### Outbound DTOs (Service → Discord)

**Interaction responses** — returned from `POST /api/discord/interactions`:

```csharp
class InteractionResponse {
    int Type;                          // 1=PONG, 4=CHANNEL_MESSAGE, 5=DEFERRED, 6=DEFERRED_UPDATE
    InteractionCallbackData? Data;
}

class InteractionCallbackData {
    string? Content;
    List<DiscordEmbed>? Embeds;
    List<DiscordActionRow>? Components;
    int? Flags;                        // 64 = ephemeral
}
```

**Embeds:**

```csharp
class DiscordEmbed {
    string? Title;
    string? Description;
    int? Color;                        // RGB as int
    List<DiscordEmbedField>? Fields;
    string? Timestamp;                 // ISO 8601
}

class DiscordEmbedField {
    string Name;
    string Value;
    bool Inline;
}
```

**Components (buttons):**

```csharp
class DiscordActionRow {
    int Type = 1;
    List<DiscordButton> Components;
}

class DiscordButton {
    int Type = 2;
    int Style;                         // 1=Primary, 2=Secondary, 4=Danger
    string Label;
    string CustomId;
}
```

**Command registration manifest** — sent to Discord on startup:

```csharp
class DiscordApplicationCommand {
    string Name;
    string Description;
    int Type = 1;                      // 1 = CHAT_INPUT (slash command)
    List<DiscordCommandOption>? Options;
    bool DefaultMemberPermissions;     // false = admin only
}

class DiscordCommandOption {
    int Type;                          // 3=string, 6=user
    string Name;
    string Description;
    bool Required;
}
```

---

## Phase 4: Slash Command Registration

On startup (via `IHostedService`), call `RegisterCommandsAsync` with the full command manifest.
If `DISCORD_GUILD_ID` env var is set, register guild-scoped (instant); otherwise global (up to 1hr propagation).

### Command Manifest

#### Public Commands
| Command | Options | Description |
|---------|---------|-------------|
| `/ping` | None | Health check — responds "Pong!" |
| `/check` | None | Shows current membership status for the calling user's linked account |
| `/create-ticket` | None | Shows modal: subject + details → opens support thread |

#### Staff Commands
| Command | Options | Description |
|---------|---------|-------------|
| `/close-ticket` | `id` (string) | Closes a ticket by ID — locks and archives thread |
| `/tickets` | `user` (user, optional) | Lists open tickets, optionally filtered by user |

#### Admin Commands
| Command | Options | Description |
|---------|---------|-------------|
| `/shun` | `user` (user), `reason` (string) | Shuns a user (timeout) — assigns `DISCORD_ROLE_SHUN` |
| `/unshun` | `user` (user) | Removes shun status |
| `/admin-link` | None | Modal: email + Discord ID + username → resolves `UserID`, force-links account |
| `/admin-unlink` | None | Modal: Discord ID → force-unlinks account |
| `/audit-user` | None | Modal: Discord ID → compares stored vs. live server membership data |
| `/export-expired` | None | Exports all expired members to CSV, uploads as file attachment |
| `/admin-refresh` | None | Batch-processes all expired members, refreshes from backend (deferred response) |

---

## Phase 5: Linked Roles OAuth2 Flow

Replaces `/link`, `/unlink`, and `/verify` from beanie-bot-v3. Users connect their Timcast
account via Discord's native Linked Roles UI — no bot commands required.

**`Controllers/DiscordOAuthController.cs`:**

| Route | Auth | Purpose |
|-------|------|---------|
| `GET /api/discord/oauth/callback` | None (Discord redirects here) | Exchange code → resolve platform user → create `DiscordMemberRecord` → push metadata |
| `DELETE /api/discord/link` | JWT required (`ONUserHelper`) | Admin/user-initiated unlink |

**Linked Role Metadata Schema** — registered on startup via `RegisterRoleMetadataAsync`:
```json
[
  { "key": "subscription_level", "name": "Subscription Level", "type": 2 },
  { "key": "is_subscriber",      "name": "Subscriber",         "type": 7 }
]
```

**OAuth2 Flow:**
```
User clicks "Connect" in Discord server Linked Roles UI
  → Discord redirects to GET /api/discord/oauth/callback?code=...&state=...
  → ExchangeCodeAsync(code) → Discord access token + Discord user info
  → Resolve PlatformUserId: call auth service GetUserByIdentity(discordId or stored email)
  → GetSubscriptionByUserId(platformUserId) → level, expiry, tiers
  → CreateMemberRecord gRPC call → DiscordMemberRecord stored
  → PushLinkedRoleMetadataAsync → Discord assigns linked role automatically
  → Assign tier roles via AddRoleAsync
  → Redirect to success page
```

**Metadata Push on Subscription Change:**
The payment service's `BulkHelper` pattern triggers `PushLinkedRoleMetadataAsync` when a
user's subscription level changes, keeping linked role metadata in sync automatically.

---

## Phase 6: Interaction Endpoint Controller

**`Controllers/DiscordInteractionController.cs`** — `POST /api/discord/interactions`

Must respond within **3 seconds** or Discord shows an error to the user.

### Request Pipeline

```
POST /api/discord/interactions
  1. Read raw body bytes (needed for signature verification before deserialization)
  2. Verify Ed25519 signature using DISCORD_PUBLIC_KEY — return 401 if invalid
  3. Deserialize JSON body to DiscordInteraction
  4. Route by interaction type:
       type 1 (PING)                → return { type: 1 }
       type 2 (APPLICATION_COMMAND) → CommandRouter.HandleAsync(interaction)
       type 3 (MESSAGE_COMPONENT)   → ComponentRouter.HandleAsync(interaction)  [buttons]
       type 5 (MODAL_SUBMIT)        → ModalRouter.HandleAsync(interaction)
```

### Permission Checks

```csharp
bool IsStaff(DiscordInteraction i) =>
    i.Member.Roles.Contains(CAST_MEMBER_ROLE_ID) ||
    i.Member.Roles.Contains(ADMIN_ROLE_ID) ||
    i.Member.Roles.Contains(CHIEF_COORDINATOR_ROLE_ID);

bool IsAdmin(DiscordInteraction i) =>
    i.Member.Roles.Contains(ADMIN_ROLE_ID);
```

### Ed25519 Signature Verification

```csharp
// NSec.Cryptography
var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519,
    Convert.FromHexString(Environment.GetEnvironmentVariable("DISCORD_PUBLIC_KEY")),
    KeyBlobFormat.RawPublicKey);

var message = Encoding.UTF8.GetBytes(timestamp + rawBody);
var signature = Convert.FromHexString(signatureHeader);

bool valid = SignatureAlgorithm.Ed25519.Verify(publicKey, message, signature);
```

---

## Phase 7: Command Handlers

Each handler lives in `Handlers/Commands/`. They receive the `DiscordInteraction` and return
a serialized `InteractionResponse`.

### Check (`/check`)

```
/check
  → look up DiscordMemberRecord by DiscordUserId (from interaction)
  → if not found: reply ephemeral "Your account is not linked. Connect via server Linked Roles."
  → reply ephemeral: membership status embed
```

### Shun Flow (`/shun` → `/unshun`)

```
/shun (admin only)
  → look up DiscordMemberRecord for target user
  → ShunDiscordMember gRPC call → DiscordShunRecord { Reason, ShunnedByDiscordId, SHUN_ACTIVE }
  → AddRoleAsync(targetDiscordUserId, DISCORD_ROLE_SHUN)
  → post to DISCORD_SHUN_CHANNEL_ID: "{user} has been shunned 🫢 — {reason}"
  → reply ephemeral: "Shunned."

/unshun (admin only)
  → GetUserShuns(IncludeInactive: false) → find active shun
  → UnShunDiscordMember gRPC call → sets UnShunnedOnUTC, UnShunnedByDiscordID, SHUN_INACTIVE
  → RemoveRoleAsync(targetDiscordUserId, DISCORD_ROLE_SHUN)
  → reply ephemeral: "Unshunned."
```

### Ticket Flow (`/create-ticket` → button close)

```
/create-ticket
  → show modal (custom_id: "ticket-create", fields: subject, body)

MODAL_SUBMIT (custom_id: "ticket-create")
  → optionally resolve PlatformUserId from linked DiscordMemberRecord — leave empty if not linked
  → CreateTicket gRPC call → DiscordTicketRecord { Subject, Text, PlatformUserId (if known) }
  → create Discord thread in SUPPORT_CHANNEL_ID via CreateThreadAsync
  → post embed to thread (title: "Support Ticket", fields: subject/details)
  → post "Close Ticket" button (custom_id: "ticket-close:{threadId}")
  → reply ephemeral: "Ticket opened: {thread link}"

BUTTON (custom_id prefix: "ticket-close")
  → GetTickets filtered by threadId to find ticket
  → validate: user is ticket owner OR IsStaff()
  → CloseTicket gRPC call → sets ClosedOnUTC, ClosedByDiscordId, TICKET_CLOSED
  → LockThreadAsync + ArchiveThreadAsync
  → reply ephemeral: "Ticket closed"
```

### Admin Refresh (`/admin-refresh`)

Long-running — must defer:

```
/admin-refresh
  → DeferInteractionAsync (type 5, ephemeral)
  → run batch in background Task:
       GetMembers(ExpiredOnUTC: now, PageSize: 500, ...) — paginate through all expired
       for each record:
         call payment service gRPC: GetSubscriptionByUserId(PlatformUserId)
         ModifyMemberRecord gRPC call with updated level/expiry/tiers
         AddRoleAsync/RemoveRoleAsync to reconcile Discord roles
         PushLinkedRoleMetadataAsync to update linked role metadata
         track success/fail/skip counts
  → EditInteractionResponseAsync with result embed
```

---

## Phase 8: Data Layer

**`Data/IDiscordDataProvider.cs`** — maps directly to the `DiscordInterface` gRPC service.
The FileSystem provider stores Protobuf-serialized files per record type.

```csharp
// Members
Task<DiscordMemberRecord> GetMemberByDiscordId(string discordId);
Task<DiscordMemberRecord> GetMemberByPlatformId(Guid platformUserId);
Task<GetMembersResponse> GetMembers(GetMembersRequest request);       // paginated
Task<DiscordMemberRecord> CreateMember(CreateMemberRecordRequest request);
Task<DiscordMemberRecord> ModifyMember(ModifyMemberRecordRequest request);
Task BanMember(BanMemberRequest request);

// Shuns
Task<ShunDiscordMemberResponse> ShunMember(ShunDiscordMemberRequest request);
Task UnShunMember(UnShunDiscordMemberRequest request);
Task<GetUserShunsResponse> GetUserShuns(GetUserShunsRequest request);  // paginated

// Tickets
Task<DiscordTicketRecord> CreateTicket(CreateTicketRequest request);
Task<DiscordTicketRecord> CloseTicket(CloseTicketRequest request);
Task<GetTicketsResponse> GetTickets(GetTicketsRequest request);         // paginated
```

**FileSystem directory layout:**
```
{DISCORD_DATA_PATH}/
  members/    # keyed by discordUserId
  shuns/      # keyed by shunId (Guid)
  tickets/    # keyed by ticketId (Guid)
```

Note: `DiscordAuthLogRecord` is removed — no longer needed since email verification
is replaced by the Linked Roles OAuth2 flow.

---

## Phase 9: Backend Integration (Auth Service)

Runs inside Combined service — calls auth/payment/notification services via gRPC directly.

### User Lookup (Discord ID → PlatformUserId)

During OAuth2 callback, after exchanging the code, Discord returns the user's Discord ID.
Look up or create the platform association:

```csharp
// Try to find existing linked account first
var existing = await _discordDataProvider.GetMemberByDiscordId(discordUserId);
if (existing != null) { /* update subscription, push metadata */ return; }

// New link: Discord doesn't give us the email, so the user must be logged into
// the platform in the same session, or we prompt them to log in.
// State parameter in OAuth2 URL carries a signed platformUserId from the platform session.
var platformUserId = ValidateSignedState(state);
```

### Subscription Check

```csharp
var response = await _paymentClient.GetSubscriptionByUserId(new GetSubByUserIdRequest
{
    UserID = platformUserId.ToString()
});
// response.CurrentLevelCents, response.ExpiresOnUTC, response.Tiers, response.InternalSubscriptionId
```

### Claims Service

**`Services/ClaimsService.cs`** — implements `ClaimsInterface.ClaimsInterfaceBase`:
- Given `UserID` (Guid), look up `DiscordMemberRecord` by `PlatformUserId`
- Returns `ClaimRecord { Name = "discord_id", Value = discordUserId }` if linked

This feeds the Discord identity into the platform JWT so other services can use it.

---

## Phase 10: Command Registration + Metadata Hosted Service

**`Services/DiscordStartupService.cs`** — implements `IHostedService`, runs on startup:

```csharp
public async Task StartAsync(CancellationToken ct)
{
    await _discordRestClient.RegisterCommandsAsync(BuildCommandManifest());
    await _discordRestClient.RegisterRoleMetadataAsync(BuildRoleMetadataSchema());
}
```

Both are idempotent (Discord upserts by name/key).

---

## Phase 11: DIExtensions & Integration

**`Extensions/DIExtensions.cs`:**
```csharp
public static IServiceCollection AddDiscordClasses(this IServiceCollection services)
{
    services.AddSingleton<IDiscordDataProvider, FileSystemDiscordDataProvider>();
    services.AddSingleton<DiscordRestClient>();
    services.AddSingleton<DiscordCommandRouter>();
    services.AddSingleton<DiscordComponentRouter>();
    services.AddSingleton<DiscordModalRouter>();
    services.AddHostedService<DiscordStartupService>();
    return services;
}

public static void MapDiscordGrpcServices(this IEndpointRouteBuilder endpoints)
{
    endpoints.MapGrpcService<ClaimsService>();
    endpoints.MapGrpcService<DiscordService>();
}
```

In `Services/Combined/Startup.cs`:
- `services.AddDiscordClasses()` in `ConfigureServices`
- `endpoints.MapDiscordGrpcServices()` in `Configure`
- Controllers auto-map via `endpoints.MapControllers()`

---

## Environment Variables

```
DISCORD_BOT_TOKEN           # Bot token
DISCORD_APP_ID              # Application/client ID
DISCORD_PUBLIC_KEY          # Ed25519 public key for signature verification
DISCORD_CLIENT_SECRET       # OAuth2 client secret (linked roles)
DISCORD_OAUTH_REDIRECT      # e.g. https://yoursite.com/api/discord/oauth/callback
DISCORD_GUILD_ID            # Optional: if set, register commands guild-scoped
DISCORD_SUPPORT_CHANNEL_ID  # Channel for ticket threads
DISCORD_SHUN_CHANNEL_ID     # Channel for shun announcements
DISCORD_DATA_PATH           # FileSystem storage root

# Role IDs
DISCORD_ROLE_LOUNGE
DISCORD_ROLE_VIP
DISCORD_ROLE_SILVER
DISCORD_ROLE_ELITE
DISCORD_ROLE_SHUN           # Timeout role
DISCORD_ROLE_CAST_MEMBER
DISCORD_ROLE_ADMIN
DISCORD_ROLE_CHIEF_COORDINATOR
DISCORD_ROLE_CORE
```

---

## Embed Structures

All replies are ephemeral (`flags: 64`) unless posting to a channel/thread.

### Membership Status (`/check`)
```json
{
  "title": "Membership Status",
  "fields": [
    { "name": "Level",   "value": "VIP",        "inline": true },
    { "name": "Expires", "value": "2025-12-01", "inline": true },
    { "name": "Tiers",   "value": "VIP, Elite", "inline": true }
  ]
}
```

### Support Ticket (posted to thread)
```json
{
  "title": "Support Ticket",
  "fields": [
    { "name": "Subject", "value": "..." },
    { "name": "Details", "value": "..." }
  ]
}
```

### Membership Audit (`/audit-user`)
```json
{
  "title": "Membership Audit",
  "fields": [
    { "name": "Discord ID",                   "value": "...",            "inline": true },
    { "name": "Platform User ID",             "value": "guid...",        "inline": true },
    { "name": "Level (stored → server)",      "value": "VIP → VIP",      "inline": true },
    { "name": "Expiration (stored → server)", "value": "2025-12-01 → 2025-12-01" },
    { "name": "Stored Tiers",                 "value": "VIP, Elite" },
    { "name": "Server Roles",                 "value": "VIP, Elite" }
  ]
}
```

### Admin Refresh Result (`/admin-refresh`)
```json
{
  "title": "Admin Refresh — Expired Members",
  "fields": [
    { "name": "Expired Found", "value": "42", "inline": true },
    { "name": "Attempted",     "value": "42", "inline": true },
    { "name": "Success",       "value": "40", "inline": true },
    { "name": "Failed",        "value": "1",  "inline": true },
    { "name": "Skipped",       "value": "1",  "inline": true },
    { "name": "Updated",       "value": "38", "inline": true }
  ],
  "timestamp": "2025-03-21T00:00:00Z"
}
```

---

## Open TODOs (from proto files)

- `DiscordMemberRecord` — split into `DiscordMemberPublicRecord` / `DiscordMemberPrivateRecord`
- `DiscordTicketRecord` — add messages sent in thread
- `DiscordInterface` — split into `DiscordInterface` (user-facing) and `AdminDiscordInterface`
