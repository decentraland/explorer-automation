# `web/shared/alttester/` — Node AltTester WebSocket client

Vendor client for AltTester Desktop, used by `@cross` Playwright specs as an
observability layer. The C# `dotnet test` stages remain the authoritative
Unity-side test runners (actions + assertions); this client lets Playwright
**observe scene state in real time** to gate orchestration decisions without
spamming file-flag artifacts.

## Status

**Scaffold only — not yet functional.** The protocol shapes, public API,
exception types, and architectural decisions are committed; the actual
WebSocket transport and command serialization (`AltDriver.connect`,
`AltDriver.send`) are stubbed with documented implementation outlines. A
focused follow-up session should:

1. Add `ws` (≥ ^8.0.0) to `web/package.json` `dependencies`.
2. Implement the body of `AltDriver.connect` per the comment in `client.ts`.
3. Implement `AltDriver.send` (message-id dispatch + pending-promise map).
4. Add a `__tests__/alttester-smoke.spec.ts` that connects to a running
   AltTester Desktop + Explorer and runs the 5 supported commands.
5. Wire one orchestration-observability check into
   `tests/auth/specs/client-to-web-handoff.spec.ts` (e.g. `await
alt.waitForObject(By.NAME, 'Verification.Dapp.Screen')`) so the spec
   gates web-side navigation on observed Unity state rather than the
   `auth-handoff.json` file alone.

## Why vendor rather than depend?

- `@alttester/websocket-client` doesn't exist on npm (the AltTester team
  publishes only C# and Python first-party clients).
- `appium-altunity-plugin` is a heavyweight Appium plugin that drags the
  Appium runtime into our test deps.
- `open-alttester-server` is a community **server**, not a client.
- The wire protocol is small (~6 commands for our needs), MIT-friendly
  (the C# SDK is GPL-3, but we're reimplementing the wire format which is
  documented in their public docs — not derivative code), and stable across
  Desktop minor versions.

## Scope

Implemented (or scaffolded for implementation):

- `connect()` / `disconnect()` — WS handshake against `/altws` with full
  query-param set (`appName`, `platform`, `driverType`, etc.).
- `findObject(by, value)` — single-match locator.
- `waitForObject(by, value, timeoutSec)` — Unity-side polling locator.
- `getText(object)` — read Text/TMP_Text content.
- `tap(object)` — click-equivalent action.
- `pressKey(keyCode)` — simulate AltKeyCode press.

NOT implemented (deliberately):

- Screenshots, performance metrics, custom inputs (`tapCoordinates`,
  `multiPointSwipe`).
- Component reflection (`getComponentProperty`, `setComponentProperty`).
- Asset bundle / scene-load helpers.
- Reverse port forwarding for mobile-device proxying.

Add these as flows demand. Keep the surface tight so the implementation
stays auditable.

## Wire-protocol references

- `protocol.ts` — full type definitions, close-code → exception mapping.
- C# reference: `~/.nuget/packages/alttester-driver/2.3.0/` (NuGet source).
- Unity-side reference:
  `~/Projects/unity-explorer/Explorer/Library/PackageCache/com.alttester.sdk@*/Runtime/AltDriver/Commands/`.
  Each `<CommandName>Command.cs` defines the JSON shape for that command.

## Integration pattern (target)

```ts
import { AltDriver, By } from '../../../shared/alttester/index.js'

const alt = new AltDriver({
  host: '127.0.0.1',
  port: 13000,
  appName: '__default__',
  platform: 'Standalone',
  platformVersion: 'macOS',
  deviceInstanceId: 'playwright-orchestrator',
  driverType: 'SDK'
})
await alt.connect()

// Fire-and-forget the C# capture (which clicks Metamask)
const unityCapture = runExplorerTest('TestCaptureWalletAuthHandoff')

// Watch the Unity scene from the orchestrator side
const verificationScreen = await alt.waitForObject(By.NAME, 'Verification.Dapp.Screen', 60)
const code = await alt.getText(await alt.findObject(By.PATH, '//Verification.Dapp.Screen//Code'))

// Now Playwright can proceed without waiting for the C# test to fully exit
// (dotnet test cleanup takes ~5s after the assertion passes).
await alt.disconnect()
await unityCapture // still await for the file-flag fallback / exit code
```
