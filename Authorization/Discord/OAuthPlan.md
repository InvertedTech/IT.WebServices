# Discord OAuth2 — Linked Roles Flow (InvertedBot)

This project uses the **Authorization Code Grant** exclusively, scoped to Linked Roles.
No implicit grant, no client credentials. Bot operations use the bot token directly.

---

## Scopes Required

| Scope | Why |
|-------|-----|
| `identify` | Get Discord user ID + username after code exchange |
| `role_connections.write` | Push subscription metadata to Discord Linked Roles |

---

## Endpoints

| Purpose | Method | URL |
|---------|--------|-----|
| Send user to authorize | `GET` (redirect) | `https://discord.com/oauth2/authorize` |
| Exchange code for tokens | `POST` | `https://discord.com/api/oauth2/token` |
| Refresh access token | `POST` | `https://discord.com/api/oauth2/token` |
| Revoke token | `POST` | `https://discord.com/api/oauth2/token/revoke` |
| Get Discord user info | `GET` | `https://discord.com/api/users/@me` |
| Push linked role metadata | `PUT` | `https://discord.com/api/users/@me/applications/{app_id}/role-connection` |

> All token endpoints require `Content-Type: application/x-www-form-urlencoded`
> and HTTP Basic Auth (`client_id:client_secret` in `Authorization` header).

---

## Full Flow

```
1. User clicks "Connect" in Discord server's Linked Roles UI
       ↓
   Discord redirects to GET /api/discord/oauth/callback?code=...&state=...

2. Controller: validate state → exchange code → get user info → resolve platform user

3. ExchangeCodeAsync(code, redirectUri)
   POST /oauth2/token
     grant_type=authorization_code
     code=...
     redirect_uri=DISCORD_OAUTH_REDIRECT
   ← { access_token, refresh_token, expires_in, scope }

4. GET /users/@me  (Bearer {access_token})
   ← { id, username, global_name, ... }

5. Resolve PlatformUserId
   - Check existing DiscordMemberRecord by Discord ID → found: update
   - Not found: extract platformUserId from signed `state` param (user was logged into platform)
     → call auth service: GetUserByIdentity(discordId)

6. GetSubscriptionByUserId(platformUserId)
   ← { CurrentLevelCents, ExpiresOnUTC, Tiers, InternalSubscriptionId }

7. CreateMemberRecord (or ModifyMemberRecord) gRPC call
   → stores DiscordMemberRecord { PlatformUserId, DiscordUserId, DiscordUserName,
                                   CurrentLevelCents, ExpiresOnUTC, Tiers,
                                   access_token, refresh_token, token_expiry }

8. PushLinkedRoleMetadataAsync(access_token)
   PUT /users/@me/applications/{appId}/role-connection
     { platform_name, platform_username, metadata: { subscription_level, is_subscriber } }

9. AddRoleAsync for each applicable tier role

10. Redirect user to success page
```

---

## State Parameter

The `state` param carries a **signed platform user ID** so we can link accounts without
asking the user to log in twice.

```
Build authorize URL:
  state = Base64Url( HMAC-SHA256( platformUserId, DISCORD_STATE_SECRET ) + ":" + platformUserId )

On callback:
  1. Decode state → extract platformUserId + signature
  2. Recompute HMAC → compare → reject if mismatch (CSRF protection)
  3. Use platformUserId to look up / create the DiscordMemberRecord
```

> `DISCORD_STATE_SECRET` is a new env var — add it alongside the others.

---

## Token Storage

Access + refresh tokens must be stored on `DiscordMemberRecord`.
Add these fields to the proto:

```proto
string OAuthAccessToken  = 13;
string OAuthRefreshToken = 14;
google.protobuf.Timestamp OAuthTokenExpiresOnUTC = 15;
```

---

## Token Refresh

Tokens expire in ~7 days (`expires_in: 604800`). Refresh before pushing metadata:

```
if (tokenExpiry < now + 5min):
    POST /oauth2/token  grant_type=refresh_token  refresh_token=...
    ← new access_token + refresh_token + expires_in
    → update DiscordMemberRecord with new tokens
```

Refresh is needed in two places:
- **OAuth callback** (first link — tokens are fresh, no refresh needed)
- **Metadata push on subscription change** (BulkHelper / admin-refresh — may need refresh)

---

## Controller: `GET /api/discord/oauth/callback`

```csharp
[AllowAnonymous]
[HttpGet("oauth/callback")]
public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string state)
{
    if (!ValidateState(state, out var platformUserId))
        return BadRequest("Invalid state");

    var tokens    = await _discord.ExchangeCodeAsync(code, _settings.OAuthRedirect);
    var discordUser = await _discord.GetCurrentUserAsync(tokens.AccessToken);

    var member = await ResolveOrCreateMember(platformUserId, discordUser, tokens);
    await _discord.PushLinkedRoleMetadataAsync(tokens.AccessToken, BuildMetadata(member));
    await ReconcileRoles(discordUser.Id, member);

    return Redirect("/linked-role-success");
}
```

---

## Controller: `DELETE /api/discord/link`

JWT-authenticated (ONUserHelper). Unlinks the calling user's Discord account:
1. Look up `DiscordMemberRecord` by `PlatformUserId` from JWT
2. Revoke token: `POST /oauth2/token/revoke`
3. Delete or clear the record

---

## New REST Client Methods Needed

```csharp
// GET /users/@me  — requires Bearer token, NOT bot token
ValueTask<DiscordCurrentUser> GetCurrentUserAsync(string accessToken);

// POST /oauth2/token  grant_type=authorization_code
ValueTask<OAuthTokenResponse> ExchangeCodeAsync(string code, string redirectUri);

// POST /oauth2/token  grant_type=refresh_token
ValueTask<OAuthTokenResponse> RefreshTokenAsync(string refreshToken);

// POST /oauth2/token/revoke
ValueTask RevokeTokenAsync(string token);
```

> `GetCurrentUserAsync` must use a per-call `Authorization: Bearer {token}` header,
> not the default bot token set on the `HttpClient`.

---

## New DTO: `DiscordCurrentUser`

```csharp
class DiscordCurrentUser {
    [JsonPropertyName("id")]         string Id         { get; set; }
    [JsonPropertyName("username")]   string Username   { get; set; }
    [JsonPropertyName("global_name")]string GlobalName { get; set; }
    [JsonPropertyName("email")]      string? Email     { get; set; }
}
```

---

## New Environment Variables

```
DISCORD_OAUTH_REDIRECT    # e.g. https://yoursite.com/api/discord/oauth/callback
DISCORD_STATE_SECRET      # Random secret for HMAC signing the state param
```

---

## Metadata Push on Subscription Change

When a user's subscription changes (BulkHelper / admin-refresh):

```
1. Load DiscordMemberRecord (has stored tokens)
2. Refresh token if within 5 min of expiry
3. PushLinkedRoleMetadataAsync with updated level/expiry
4. AddRoleAsync / RemoveRoleAsync to reconcile tier roles
5. Save updated tokens back to record
```

---

## Linked Role Metadata Schema (registered on startup)

```json
[
  { "key": "subscription_level", "name": "Subscription Level", "type": 2, "description": "Subscription level in cents" },
  { "key": "is_subscriber",      "name": "Subscriber",         "type": 7, "description": "Has an active subscription" }
]
```

Registered via `RegisterRoleMetadataAsync` in `DiscordStartupService.StartAsync`.
