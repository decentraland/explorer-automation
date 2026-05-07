# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working anywhere in this repository.

## Repository Layout

This repo hosts **two independent test stacks** that share a single test identity and credentials:

- **`explorer/`** — C# / .NET 10 / NUnit / AltTester suite for the Decentraland Explorer **desktop client** (Unity). See [explorer/CLAUDE.md](explorer/CLAUDE.md) for build commands, architecture, and conventions.
- **`web/`** — TypeScript / Playwright suite for two web surfaces: the Decentraland auth/landing flows at `https://decentraland.org` + `/auth` (including the cross-platform handoff into the desktop client) and the **marketplace dapp** at `/marketplace/` (off-chain browsing + on-chain buy/sell flows on Polygon Amoy testnet). See [web/CLAUDE.md](web/CLAUDE.md).

### Shared at the root

- **`.env`** — IMAP credentials for OTP retrieval; loaded by both stacks. Template in `.env.example`.
- **`scripts/setup-test-identity.sh`** — provisions the BIP39 wallet identity used by all in-world tests (both `@cross` Playwright tests and the C# `InWorld` category).
- **`.claude/`** — agents and skills shared across both stacks. The `view-writer` and `test-writer` skills are C#-specific and apply only inside `explorer/`. The `dcl-testing-playwright`, `playwright-best-practices`, and `dcl-testing` skills apply inside `web/` (auth + marketplace).

## When to Read Which CLAUDE.md

- Working on a `.cs` file, a Unity-side flow, or anything under `explorer/` → read [explorer/CLAUDE.md](explorer/CLAUDE.md).
- Working on a `.ts`/`.spec.ts` file, a browser flow, or anything under `web/` → read [web/CLAUDE.md](web/CLAUDE.md).
- Touching shared files (`.env`, `scripts/`, root `README.md`, this file) → no extra context needed beyond this file.

The two stacks integrate via the **`auth-token-bridge.txt`** file written by the dapp and consumed by the desktop client. The cross-platform Playwright tests verify the in-world handoff by shelling out to `dotnet test` against a fixture in `explorer/Tests/`.
