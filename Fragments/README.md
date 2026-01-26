# Fragments

Monorepo folder for the IT WebServices protocol buffer definitions and generated artifacts.

This directory contains the canonical .proto files, generation scripts, and the npm package `@inverted-tech/fragments` built from these sources.

## Layout
- `Protos/` — protobuf sources organized by domain (Authentication, Authorization, Comment, Content, CreatorDashboard, Generic, Notification, Page, Settings)
- `buf.yaml` / `buf.gen.v2.yaml` — Buf configuration and codegen pipeline
- `generate-ts.mjs` — TypeScript generation entrypoint
- `ts-gen/` — intermediate TypeScript barrels (source for the package build)
- `dist/` — build output (created during package builds)

## Code Generation
- C# code: handled by Grpc.Tools via project csproj includes elsewhere in the repo
- TypeScript/JS package: built from the protos using Buf + TS generators

Common tasks (from `Fragments/`):
```bash
npm run build      # build TS types and ESM outputs
npm run rebuild    # clean and rebuild
npm run gen        # regenerate TS barrels from protos
```

## Validation
We annotate protos using ProtoValidate where useful to enforce shapes (e.g., `string.min_len`, `string.email`, `repeated.unique`). See files under `Protos/` (for example Settings protos) for current annotations.

## NPM Package Usage
For TypeScript/JavaScript import patterns and subpath exports, see `README.PACKAGE.md` in this folder. That document covers how to consume `@inverted-tech/fragments`.

## Releases (Changesets)
We use Changesets for versioning and publishing of the npm package.

Prereqs:
- `npm login` with access to the `@inverted-tech` scope
- 2FA if required (`npm profile enable-2fa auth-and-writes`)

Workflow:
```bash
npm run changeset           # author a changeset
npm run release:version     # apply versions
npm run release:publish     # build and publish
```
