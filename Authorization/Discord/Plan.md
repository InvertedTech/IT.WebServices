# Discord Bot Implementation — Source of Truth

## New Configs (IT.WebServices.Fragments.Settings)

```json
{
	"Public": {
		"...": {},
		"Discord": {
			"Enabled": true
		}
	},
	"Private": {
		"...": {},
		"Discord": {
			"AppId": "YOUR_APP_ID",
			"OAuthRedirectUri": "YOUR_OAUTH_REDIRECT_URI",
			"PublicKey": "YOUR_PUBLIC_KEY",
			"SignInSuccessRedirect": "YOUR_SIGN_IN_SUCCESS_REDIRECT",
			"LinkedRoleSuccessRedirect": "YOUR_LINKED_ROLE_SUCCESS_REDIRECT",
			"GuildId": "YOUR_GUILD_ID",
			"SupportChannelId": "YOUR_SUPPORT_CHANNEL_ID",
			"ShunChannelId": "YOUR_SHUN_CHANNEL_ID",
			"Roles": {
				"lounge": { "RoleId": "ROLE_ID", "NameOverride": "" },
				"vip": { "RoleId": "ROLE_ID", "NameOverride": "" },
				"silver": { "RoleId": "ROLE_ID", "NameOverride": "" },
				"elite": { "RoleId": "ROLE_ID", "NameOverride": "" },
				"shun": { "RoleId": "ROLE_ID", "NameOverride": "" },
				"staff": { "RoleId": "ROLE_ID", "NameOverride": "" },
				"admin": { "RoleId": "ROLE_ID", "NameOverride": "" },
				"chief-coordinator": { "RoleId": "ROLE_ID", "NameOverride": "" },
				"core": { "RoleId": "ROLE_ID", "NameOverride": "" }
			},
			"CommandNameOverrides": {
				"ping": "ping",
				"check": "check",
				"create-ticket": "create-ticket",
				"close-ticket": "close-ticket",
				"tickets": "tickets",
				"shun": "shun",
				"unshun": "unshun",
				"admin-link": "admin-link",
				"admin-unlink": "admin-unlink",
				"audit-user": "audit-user",
				"export-expired": "export-expired",
				"admin-refresh": "admin-refresh",
				"set-role": "set-role",
				"set-command": "set-command",
				"set-path": "set-path",
				"show-settings": "show-settings"
			}
		}
	},
	"Owner": {
		"...": {},
		"Discord": {
			"ClientSecret": "YOUR_CLIENT_SECRET",
			"DiscordStateSecret": "YOUR_DISCORD_STATE_SECRET",
			"BotToken": "YOUR_BOT_TOKEN"
		}
	}
}
```

### Modal State Correlation

Encode state directly in the modal's `custom_id`:

```
{action}:{discordUserId}:{encodedPayload}
Examples:
  close-ticket:123456789:threadId123
```

---

## Status Legend

- ✅ Complete
- 🔲 Not started
- 🔧 In progress / partial

---

## Phase 1: Infrastructure ✅

- Project setup, package references, DI wiring
- `AddAuthenticationClasses()` registered (provides `TokenHelper`, `IUserDataProvider`, `ClaimsClient`)
- JWT env vars (`JWT_PRIV_KEY`, `JWT_PUB_KEY`) in launchSettings
- ngrok dev tunnel service
- SQL tables created:
  - `Discord_Member`, `Discord_Shun`, `Discord_Ticket`, `Discord_TicketMessage`
  - `Auth_User` altered: `DiscordAuthProviderUserId`, `DiscordAccessToken`, `DiscordRefreshToken`, `DiscordAccessTokenExpiresOnUTC`
- `SqlUserDataProvider` updated to persist/read Discord OAuth columns
- `ParserExtensions` updated to read Discord OAuth columns

---

## Phase 2: Proto Definitions ✅

Files at `Fragments/Protos/.../Authorization/Discord/`.

- `DiscordRecords.proto` — `DiscordMemberRecord` (Public/Private split), `DiscordShunRecord`, `DiscordTicketRecord`, `DiscordTicketThreadMessage`
- `DiscordSettings.proto` — `DiscordPublicRecord`, `DiscordPrivateRecord` (incl. `SignInSuccessRedirect`), `DiscordOwnerRecord`
- `UserRecord.proto` — `DiscordAuthProvider` has `DiscordId`, `AccessToken`, `RefreshToken`, `AccessTokenExpiresOnUTC`

---

## Phase 3: Discord REST Client 🔧

File: `Helpers/DiscordRestClient.cs`

| Method                                                   | Status |
| -------------------------------------------------------- | ------ |
| `RegisterCommandsAsync`                                  | ✅     |
| `GetCurrentUserAsync(accessToken)`                       | ✅     |
| `ExchangeCodeAsync(code)`                                | ✅     |
| `RefreshTokenAsync(refreshToken)`                        | 🔲     |
| `RevokeTokenAsync(token)`                                | 🔲     |
| `PushLinkedRoleMetadataAsync(userAccessToken, metadata)` | 🔲     |
| `RegisterRoleMetadataAsync(schema)`                      | 🔲     |
| `GetGuildMemberAsync(guildId, userId)`                   | 🔲     |
| `AddRoleAsync(guildId, userId, roleId)`                  | 🔲     |
| `RemoveRoleAsync(guildId, userId, roleId)`               | 🔲     |
| `CreateThreadAsync(channelId, name)`                     | 🔲     |
| `LockThreadAsync(threadId)`                              | 🔲     |
| `ArchiveThreadAsync(threadId)`                           | 🔲     |
| `RespondToInteractionAsync`                              | 🔲     |
| `DeferInteractionAsync`                                  | 🔲     |
| `EditInteractionResponseAsync`                           | 🔲     |

---

## Phase 4: Slash Command Registration ✅

`DiscordCommandRegistrationService` registers commands on startup (guild-scoped if `DISCORD_GUILD_ID` is set).
`RegisterRoleMetadataAsync` called on startup for Linked Roles schema — **needs implementation in REST client**.

Linked Role Metadata Schema:

```json
[
	{ "key": "subscription_level", "name": "Subscription Level", "type": 2 },
	{ "key": "is_subscriber", "name": "Subscriber", "type": 7 }
]
```

---

## Phase 5: OAuth2 🔧

### OAuth2 Overview

Two flows share a single callback. Both use Authorization Code Grant.

| Flow                 | Scopes                            | Purpose                                                                |
| -------------------- | --------------------------------- | ---------------------------------------------------------------------- |
| Sign in with Discord | `identify email`                  | Log into platform using Discord account                                |
| Linked Roles         | `identify role_connections.write` | Link logged-in platform account to Discord; push subscription metadata |

### State Parameter

Signed HMAC payload — built by `CryptoHelper`:

```
state = Base64Url( HEX(HMAC-SHA256(payload, DISCORD_STATE_SECRET)) + ":" + payload )

payload = "signin"          → Sign in flow (platformUserId = null on decode)
payload = "{platformGuid}"  → Link flow (platformUserId = the GUID on decode)
```

### Routes

| Route                                                | Status |
| ---------------------------------------------------- | ------ |
| `GET /api/discord/oauth/signin`                      | ✅     |
| `GET /api/discord/oauth/callback` — state validation | ✅     |
| `GET /api/discord/oauth/callback` — code exchange    | ✅     |
| `GET /api/discord/oauth/callback` — get Discord user | ✅     |
| `GET /api/discord/oauth/callback` — flow branch      | ✅     |
| `DELETE /api/discord/link`                           | 🔲     |

### Sign in with Discord Flow ✅

```
GET /api/discord/oauth/signin?returnUrl=...
  → store returnUrl in 10-min HttpOnly cookie
  → state = CryptoHelper.GenerateSignInState(secret)
  → redirect to discord.com/oauth2/authorize?scope=identify+email&state=...

Callback (platformUserId == null):
  → GetUserByEmailAsync(discordUser.Email)  via SearchUsersAdmin (ROLE_SERVICE allowed)
      not found → 404 APIError
  → LinkDiscordAsync(userId, discordId, accessToken, refreshToken, expiry)
      via ModifyOtherUserAuthProviders (ROLE_SERVICE allowed)
  → TokenHelper.GenerateToken(userId)
  → set JWT as HttpOnly cookie (21 days)
  → redirect to returnUrl cookie ?? SignInSuccessRedirect ?? "/"
```

### Linked Roles Flow 🔧

```
User clicks "Connect" in Discord server Linked Roles UI
  → Discord redirects to GET /api/discord/oauth/callback?code=...&state={platformGuid}

Callback (platformUserId != null):  ← HandleLink — NOT YET IMPLEMENTED
  1. ExchangeCodeAsync(code)
     ← { AccessToken, RefreshToken, ExpiresIn }

  2. GetCurrentUserAsync(accessToken)
     ← { id, username, global_name }

  3. GetSubscriptionByUserId(platformUserId)
     ← { CurrentLevelCents, ExpiresOnUTC, Tiers, InternalSubscriptionId }

  4. CreateMemberRecord or ModifyMemberRecord gRPC
     → store DiscordMemberRecord {
         UserId, DiscordUserId, DiscordUserName,
         Tiers, InternalSubscriptionId,
         AccessToken, RefreshToken, TokenExpiry
       }

  5. PushLinkedRoleMetadataAsync(accessToken, metadata)
     PUT /users/@me/applications/{appId}/role-connection
     { platform_name, platform_username,
       metadata: { subscription_level, is_subscriber } }

  6. Reconcile tier roles via AddRoleAsync / RemoveRoleAsync

  7. Redirect to success page
```

### Token Refresh (needed for Linked Roles metadata push)

```
if (tokenExpiry < now + 5min):
    POST /oauth2/token  grant_type=refresh_token
    ← new access_token + refresh_token + expires_in
    → update DiscordMemberRecord with new tokens
```

Refresh needed in two places:

- **OAuth callback** (first link — tokens are fresh, no refresh needed)
- **Metadata push on subscription change** (BulkHelper / admin-refresh — may need refresh)

### DELETE /api/discord/link 🔲

JWT-authenticated. Unlinks the calling user's Discord account:

1. Look up `DiscordMemberRecord` by `PlatformUserId` from JWT
2. Revoke token: `POST /oauth2/token/revoke`
3. Clear `DiscordAuthProvider` on `UserRecord` via `ModifyOtherUserAuthProviders`
4. Delete or clear `DiscordMemberRecord`

---

## Phase 6: Interaction Endpoint ✅

`POST /api/discord/interactions` — Ed25519 signature validation, routes by type:

- Type 1 (PING) → `{ type: 1 }`
- Type 2 (COMMAND) → `DiscordCommandRouter`
- Type 3 (COMPONENT) → `DiscordComponentRouter` 🔲
- Type 5 (MODAL_SUBMIT) → `DiscordModalRouter` 🔲

---

## Phase 7: Command Handlers 🔧

| Command           | Handler                                                             | Status |
| ----------------- | ------------------------------------------------------------------- | ------ |
| `/ping`           | —                                                                   | 🔲     |
| `/check`          | Check membership status                                             | 🔲     |
| `/create-ticket`  | Show modal                                                          | 🔲     |
| `/close-ticket`   | Close + archive thread                                              | 🔲     |
| `/tickets`        | List open tickets                                                   | 🔲     |
| `/shun`           | Assign shun role + record                                           | 🔲     |
| `/unshun`         | Remove shun role + record                                           | 🔲     |
| `/admin-link`     | Force-link account                                                  | 🔲     |
| `/admin-unlink`   | Force-unlink account                                                | 🔲     |
| `/audit-user`     | Compare stored vs live                                              | 🔲     |
| `/export-expired` | CSV of expired members                                              | 🔲     |
| `/admin-refresh`  | Batch metadata push                                                 | 🔲     |
| `/set-role`       | Map a tier key to a Discord role ID                                 | 🔲     |
| `/set-command`    | Override a slash command's display name                             | 🔲     |
| `/set-path`       | Override a redirect URL (signin-success, linked-role-success, etc.) | 🔲     |
| `/show-settings`  | Display current name/path override config                           | 🔲     |

---

## Phase 8: Data Layer 🔧

SQL tables created. `IDiscordDataProvider` and `SqlMemberDataProvider` exist but need full implementation.

| Operation                | Status |
| ------------------------ | ------ |
| `GetMemberByDiscordId`   | 🔲     |
| `GetMemberByPlatformId`  | 🔲     |
| `GetMembers` (paginated) | 🔲     |
| `CreateMember`           | 🔲     |
| `ModifyMember`           | 🔲     |
| `BanMember`              | 🔲     |
| `ShunMember`             | 🔲     |
| `UnShunMember`           | 🔲     |
| `GetUserShuns`           | 🔲     |
| `CreateTicket`           | 🔲     |
| `CloseTicket`            | 🔲     |
| `GetTickets`             | 🔲     |

---

## Phase 9: Name & Path Overrides 🔲

Rather than hardcoding Discord role names, command names, and redirect URLs, these should be configurable via `DiscordSettings` so deployments can customize without recompilation.

### Role Name Overrides ✅

Tier names from the subscription system are mapped to Discord role IDs via `DiscordPrivateRecord.Roles` — a `map<string, DiscordRoleRecord>` stored in the settings service.

```
DiscordPrivateRecord.Roles:
  "lounge" → DiscordRoleRecord { RoleId, NameOverride }
  "vip"    → DiscordRoleRecord { RoleId, NameOverride }
  "shun"   → DiscordRoleRecord { RoleId, NameOverride }
  "admin"  → DiscordRoleRecord { RoleId, NameOverride }
  ...
```

`DiscordSettings.GetRoleId(key)` looks up the role ID, returning empty string if not configured.
`DiscordRoleRecord.NameOverride` allows customizing the displayed name of a role without renaming it in Discord.

### Command Name Overrides ✅

Slash command names are overrideable via `DiscordPrivateRecord.CommandNameOverrides` — a `map<string, string>` in the settings service.

```
DiscordPrivateRecord.CommandNameOverrides:
  "check"         → "membership"
  "create-ticket" → "support"
```

`DiscordSettings.GetCommandName(defaultName)` returns the override if set, otherwise the default. If a key is absent, the default command name is used.

### Path / URL Overrides

Redirect and callback URLs that appear in the OAuth flow should be settings-driven rather than hardcoded:

| Setting                     | Description                                   | Already in Settings? |
| --------------------------- | --------------------------------------------- | -------------------- |
| `OAuthRedirect`             | Full callback URL sent to Discord             | ✅                   |
| `SignInSuccessRedirect`     | Where to send user after Sign in with Discord | ✅                   |
| `LinkedRoleSuccessRedirect` | Where to send user after Linked Roles link    | ✅                   |
| `UnlinkSuccessRedirect`     | Where to send user after unlinking            | 🔲                   |

These should all live in `DiscordPrivateRecord` so they are operator-controlled and not compiled in.

### Owner Commands for Managing Overrides

Server owners can manage overrides at runtime via slash commands (see Phase 7), without touching config files or restarting the service:

| Command          | Action                                               |
| ---------------- | ---------------------------------------------------- |
| `/set-role`      | Set `RoleOverrides[tier]` to a given Discord role ID |
| `/set-command`   | Set `CommandNameOverrides[command]` to a new name    |
| `/set-path`      | Set a named redirect URL (e.g. `signin-success`)     |
| `/show-settings` | Display all current overrides in an ephemeral reply  |

All write commands are restricted to guild owner (or a configurable admin role). Changes persist by writing back to `DiscordPrivateRecord` via the Settings service.

---

## Phase 10: Backend Integration 🔧

### UserClient (in `Clients/Authentication/UserClient.cs`)

| Method                | Status |
| --------------------- | ------ |
| `GetOtherUserAsync`   | ✅     |
| `GetUserByEmailAsync` | ✅     |
| `LinkDiscordAsync`    | ✅     |

### Auth Service Changes

| Change                                                | Status |
| ----------------------------------------------------- | ------ |
| `SearchUsersAdmin` — allow `ROLE_SERVICE`             | ✅     |
| `ModifyOtherUserAuthProviders` — allow `ROLE_SERVICE` | ✅     |

### Subscription Tiers → Discord Roles

When a user's subscription changes (Linked Roles callback or `/admin-refresh`):

```
Tiers from subscription record → map to Discord role IDs via RoleOverrides setting:

  settings.RoleOverrides["lounge"] → role ID
  settings.RoleOverrides["vip"]    → role ID
  ...etc

For each tier role:
  - User has tier + does not have role → AddRoleAsync
  - User does not have tier + has role → RemoveRoleAsync
```

Current server roles fetched via `GetGuildMemberAsync` to diff against stored tiers.

---

## Phase 11: Startup Service ✅

`DiscordCommandRegistrationService` registers slash commands on startup.
`RegisterRoleMetadataAsync` needs to be called here once REST client method is implemented.

---

## Phase 12: DIExtensions ✅

`AddDiscordClasses()` and `MapDiscordGrpcServices()` wired into `Program.cs`.

---

## Environment Variables

Most configuration lives in the Settings service (`DiscordPrivateRecord` / `DiscordOwnerRecord`) and is loaded at runtime — no rebuild required. Only bootstrap secrets that are needed before the settings service is reachable stay as env vars.

```
# JWT (same keys as Combined service)
JWT_PRIV_KEY
JWT_PUB_KEY
```

### Moved to Settings Service (`DiscordPrivateRecord` / `DiscordOwnerRecord`)

| Was env var                            | Now in                                           |
| -------------------------------------- | ------------------------------------------------ |
| `DISCORD_BOT_TOKEN`                    | `DiscordOwnerRecord.BotToken`                    |
| `DISCORD_APP_ID`                       | `DiscordPrivateRecord.AppId`                     |
| `DISCORD_PUBLIC_KEY`                   | `DiscordPrivateRecord.PublicKey`                 |
| `DISCORD_CLIENT_SECRET`                | `DiscordOwnerRecord.ClientSecret`                |
| `DISCORD_STATE_SECRET`                 | `DiscordOwnerRecord.DiscordStateSecret`          |
| `DISCORD_OAUTH_REDIRECT`               | `DiscordPrivateRecord.OAuthRedirectUri`          |
| `DISCORD_SIGNIN_SUCCESS_REDIRECT`      | `DiscordPrivateRecord.SignInSuccessRedirect`     |
| `DISCORD_LINKED_ROLE_SUCCESS_REDIRECT` | `DiscordPrivateRecord.LinkedRoleSuccessRedirect` |
| `DISCORD_GUILD_ID`                     | `DiscordPrivateRecord.GuildId`                   |
| `DISCORD_SUPPORT_CHANNEL_ID`           | `DiscordPrivateRecord.SupportChannelId`          |
| `DISCORD_SHUN_CHANNEL_ID`              | `DiscordPrivateRecord.ShunChannelId`             |
| `DISCORD_ROLE_*`                       | `DiscordPrivateRecord.Roles[key].RoleId`         |

---

## Open TODOs

- Implement `HandleLink` in `DiscordController`
- Implement `PushLinkedRoleMetadataAsync`, `RefreshTokenAsync`, `RevokeTokenAsync`, `AddRoleAsync`, `RemoveRoleAsync` in `DiscordRestClient`
- Implement `RegisterRoleMetadataAsync` and call it from `DiscordCommandRegistrationService`
- Implement `SqlMemberDataProvider` CRUD methods
- Implement all slash command handlers
- Implement `DELETE /api/discord/link`
- Wire subscription tier → Discord role reconciliation
- `DiscordInterface` gRPC service split into user-facing and admin interfaces
