# Discord SQL

## New Tables

### Discord_Member

```sql
CREATE TABLE Discord_Member (
    UserID                  VARCHAR(36)     NOT NULL,
    DiscordUserId           VARCHAR(50)     NOT NULL,
    DiscordUserName         VARCHAR(100)    NOT NULL DEFAULT '',
    Tiers                   TEXT            NOT NULL DEFAULT '',     -- comma-separated
    BannedReason            TEXT            NOT NULL DEFAULT '',
    CreatedOnUTC            DATETIME        NOT NULL,
    ModifiedOnUTC           DATETIME        NULL,
    BannedOnUTC             DATETIME        NULL,
    CreatedById             VARCHAR(36)     NOT NULL DEFAULT '',
    ModifiedById            VARCHAR(36)     NOT NULL DEFAULT '',
    BannedByDiscordId       VARCHAR(50)     NOT NULL DEFAULT '',
    InternalSubscriptionId  VARCHAR(36)     NOT NULL DEFAULT '',
    AccessToken             TEXT            NOT NULL DEFAULT '',     -- Linked Roles OAuth access token
    RefreshToken            TEXT            NOT NULL DEFAULT '',     -- Linked Roles OAuth refresh token
    AccessTokenExpiresOnUTC DATETIME        NULL,
    PRIMARY KEY (UserID),
    UNIQUE KEY UQ_Discord_Member_DiscordUserId (DiscordUserId)
);
```

### Discord_Shun

```sql
CREATE TABLE Discord_Shun (
    ShunId                  VARCHAR(36)     NOT NULL,
    UserId                  VARCHAR(36)     NOT NULL,
    ShunnedByDiscordId      VARCHAR(50)     NOT NULL DEFAULT '',
    Reason                  TEXT            NOT NULL DEFAULT '',
    Status                  TINYINT         NOT NULL DEFAULT 0,     -- 0=ACTIVE, 1=INACTIVE
    CreatedOnUTC            DATETIME        NOT NULL,
    UnShunnedOnUTC          DATETIME        NULL,
    UnShunnedByDiscordId    VARCHAR(50)     NOT NULL DEFAULT '',
    PRIMARY KEY (ShunId),
    KEY IX_Discord_Shun_UserId (UserId)
);
```

### Discord_Ticket

```sql
CREATE TABLE Discord_Ticket (
    TicketId                VARCHAR(36)     NOT NULL,
    UserId                  VARCHAR(36)     NOT NULL,
    ThreadId                VARCHAR(50)     NOT NULL DEFAULT '',
    Status                  TINYINT         NOT NULL DEFAULT 0,     -- 0=OPEN, 1=CLOSED
    Subject                 VARCHAR(500)    NOT NULL DEFAULT '',
    Text                    TEXT            NOT NULL DEFAULT '',
    CreatedOnUTC            DATETIME        NOT NULL,
    ClosedOnUTC             DATETIME        NULL,
    ClosedByDiscordId       VARCHAR(50)     NOT NULL DEFAULT '',
    PRIMARY KEY (TicketId),
    KEY IX_Discord_Ticket_UserId (UserId)
);
```

### Discord_TicketMessage

```sql
CREATE TABLE Discord_TicketMessage (
    MessageId               VARCHAR(36)     NOT NULL,
    TicketId                VARCHAR(36)     NOT NULL,
    UserId                  VARCHAR(36)     NOT NULL DEFAULT '',
    SentByDiscordId         VARCHAR(50)     NOT NULL DEFAULT '',
    Text                    TEXT            NOT NULL DEFAULT '',
    CreatedOnUTC            DATETIME        NOT NULL,
    PRIMARY KEY (MessageId),
    KEY IX_Discord_TicketMessage_TicketId (TicketId)
);
```

---

## Discord_Member Additions

```sql
ALTER TABLE Discord_Member
    ADD COLUMN AccessToken             TEXT        NOT NULL DEFAULT '',
    ADD COLUMN RefreshToken            TEXT        NOT NULL DEFAULT '',
    ADD COLUMN AccessTokenExpiresOnUTC DATETIME    NULL;
```

---

## Auth_User Additions

```sql
ALTER TABLE Auth_User
    ADD COLUMN DiscordAuthProviderUserId       VARCHAR(50)     NULL,
    ADD COLUMN DiscordAccessToken              TEXT            NULL,
    ADD COLUMN DiscordRefreshToken             TEXT            NULL,
    ADD COLUMN DiscordAccessTokenExpiresOnUTC  DATETIME        NULL;
```
