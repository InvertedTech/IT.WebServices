# Discord Service — Roadmap

Items are ordered by priority. The critical path to a working bot is Phase 1 → Phase 2 → Phase 3.

---

## Phase 1 — Core Bot Interactions (Blocking)

Nothing the bot does works without these.

- [ ] Implement `RespondToInteractionAsync` in `DiscordRestClient`
  - `POST /interactions/{id}/{token}/callback`
- [ ] Implement `DeferInteractionAsync` in `DiscordRestClient`
  - `POST /interactions/{id}/{token}/callback` with type 5 (deferred)
- [ ] Implement `EditInteractionResponseAsync` in `DiscordRestClient`
  - `PATCH /webhooks/{app_id}/{token}/messages/@original`

---

## Phase 2 — Role Reconciliation (Blocking)

The whole point of the linked roles integration.

- [ ] Implement `AddRoleAsync` in `DiscordRestClient`
  - `PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}` with bot token
- [ ] Implement `RemoveRoleAsync` in `DiscordRestClient`
  - `DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}` with bot token
- [ ] Implement `ReconcileRolesAsync` logic (call from `HandleLink`)
  - Map subscription `AmountCents` to tier role IDs via `_settings.GetRoleId(key)`
  - Add roles the user qualifies for, remove roles they no longer qualify for
  - Call after `PushLinkedRoleMetadataAsync`

---

## Phase 3 — Slash Command Handlers

### User Commands

- [ ] `/link` — redirect user to `/api/discord/oauth/link` (send ephemeral link)
- [ ] `/unlink` — unlink Discord from platform account
- [ ] `/status` — show user's current subscription tier and linked role status

### Moderation Commands

- [ ] `/shun` — assign shun role, optionally post to shun channel
- [ ] `/unshun` — remove shun role
- [ ] `/verify` — manually verify a user

### Subscription / Member Management Commands

- [ ] `/sub-lookup` — look up a user's subscription status
- [ ] `/sub-set` — manually set subscription tier for a user
- [ ] `/member-lookup` — look up platform member info
- [ ] `/member-reset-password` — trigger password reset
- [ ] `/member-reset-totp` — trigger TOTP reset

### Owner / Admin Config Commands

- [ ] `/set-role` — update a role ID in settings (e.g. `/set-role subscriber @SubscriberRole`)
- [ ] `/set-command` — override a command display name in settings
- [ ] `/set-path` — override a redirect URL in settings
- [ ] `/show-settings` — display current Discord settings as an ephemeral embed

---

## Phase 4 — Token Refresh

- [ ] Implement `RefreshTokenAsync` in `DiscordRestClient`
  - `POST /oauth2/token` with `grant_type=refresh_token`
- [ ] Background job or on-demand refresh: before calling Discord on behalf of a user, check `AccessTokenExpiresOnUTC` and refresh if needed
- [ ] Update stored `AccessToken`, `RefreshToken`, `AccessTokenExpiresOnUTC` in `DiscordMemberRecord` after refresh

---

## Phase 5 — UX Improvements

- [ ] **Intermediate landing page** — after sign-in, land on a page (e.g. `/discord/linked-roles`) that explains Linked Roles and has an explicit "Connect" button instead of immediately redirecting into the second Discord OAuth prompt. See note in Plan.md Phase 5.
- [ ] **Unlink success redirect** — wire `UnlinkSuccessRedirect` from settings through the `Unlink` endpoint

---

## Phase 6 — Cleanup / Nice-to-Have

- [ ] **Full token revocation on unlink** — `UserServerRecord` Discord tokens are not currently exposed via gRPC; once they are, call `RevokeTokenAsync` in `DiscordController.Unlink`
- [ ] **Thread helpers** — implement `CreateThreadAsync`, `LockThreadAsync`, `ArchiveThreadAsync` in `DiscordRestClient` (needed for support channel workflows)
- [ ] **DB migration** — run `ALTER TABLE Discord_Member ADD COLUMN AccessToken/RefreshToken/AccessTokenExpiresOnUTC` on any existing deployed instance (see `sql.md`)
