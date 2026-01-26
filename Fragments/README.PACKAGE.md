# @inverted-tech/fragments

TypeScript types and ESM runtime for IT WebServices Protobufs.

This package is ESM-only and ships `.d.ts` declaration files plus generated JS under `dist/esm/`.

Install
```bash
npm install @inverted-tech/fragments
```

## Imports
We expose convenient subpaths for each module and file-level entries. You don’t need to append `/index`.

Examples
```ts
// Settings models
import type { CMSPublicRecord, ChannelRecord, CategoryRecord } from '@inverted-tech/fragments/Settings';
// or with a trailing slash
import type { CMSPublicMenuRecord } from '@inverted-tech/fragments/Settings/';

// Specific files
import type { UserRecord } from '@inverted-tech/fragments/Authentication/UserRecord';
import { UserInterfaceClient } from '@inverted-tech/fragments/Authentication/UserInterface_connect';

// Protos bundle
import * as Protos from '@inverted-tech/fragments/protos';
// or file-level
import '@inverted-tech/fragments/protos/Authentication/UserInterface_connect';
```

Available namespaces: Authentication, Authorization, Comment, Content, CreatorDashboard, Generic, Notification, Page, Settings.

## Notes
- ESM-only; no CommonJS entry points.
- Built from protobuf sources using Buf and TypeScript generators.

## Support
- Node.js 18+
- Modern browsers (ES2020)

## Changelog
This package uses Changesets; see release notes on npm.

## License
MIT — see `LICENSE`.

