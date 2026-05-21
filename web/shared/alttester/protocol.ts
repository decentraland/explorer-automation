/**
 * Wire-protocol primitives for AltTester WebSocket Desktop (v2.3.x).
 *
 * This module owns ONLY the shapes that travel over the websocket. The
 * higher-level driver (`./client.ts`) builds and parses these.
 *
 * Reference: the C# driver source at
 * `~/.nuget/packages/alttester-driver/2.3.0/` and the Unity-side SDK at
 * `Library/PackageCache/com.alttester.sdk@*` (sibling repo `unity-explorer/`).
 *
 * @see https://alttester.com/docs/ for the official protocol docs.
 */

/**
 * Handshake URL is `ws://<host>:<port>/altws` with query parameters:
 *   appName            — required, matches the app's AltTesterPrefab.appName
 *   platform           — "Editor" / "Standalone" / "Android" / ...
 *   platformVersion    — free-form
 *   deviceInstanceId   — free-form, used for multi-device runs
 *   driverType         — "SDK" (test client) or "Proxy"
 *   appId              — optional, present when targeting a specific connected app
 *
 * Close codes Desktop sends:
 *   4001 NoAppConnected             — no app with the given tags is connected
 *   4002 AppDisconnected            — the connected app went away mid-session
 *   4005 MultipleDrivers            — multiple drivers tried to claim the same tags
 *   4007 MultipleDriversTryingToConnect
 *
 * The handshake itself is a plain WS open; the first protocol-level message is
 * the driver-registered ack. After that, every command/response carries a
 * unique `messageId`.
 */
export interface ConnectionConfig {
  host: string
  port: number
  appName: string
  platform: string
  platformVersion: string
  deviceInstanceId: string
  driverType: 'SDK' | 'Proxy'
  appId?: string
  /** Whether to use `wss://` instead of `ws://`. */
  secure?: boolean
}

/**
 * Every command sent to Desktop has this envelope. Subtypes carry additional
 * fields named by the command (e.g., `findObject` carries `by`, `value`).
 */
export interface CommandRequest {
  /** Unique per command — Desktop echoes this back to correlate responses. */
  messageId: string
  /** Discriminator that selects the handler on the Unity side. */
  commandName: AltCommandName
  /** Command params, shape varies per commandName. JSON-encoded as siblings of these fields. */
  [param: string]: unknown
}

/** Response envelope. `data` carries the payload; `error` is set on failure. */
export interface CommandResponse {
  messageId: string
  commandName: AltCommandName
  data?: unknown
  error?: AltErrorPayload
}

export interface AltErrorPayload {
  type: string
  message: string
  trace?: string
}

/**
 * Supported commands in this minimal client. Add new entries as flows demand —
 * keep this enum tight to make it obvious what's wired up. Each entry maps to
 * a `<commandName>Command.cs` file in the Unity SDK source.
 */
export type AltCommandName =
  | 'findObject' // -> AltObject handle (single match) or NotFoundException
  | 'findObjects' // -> AltObject[] (all matches)
  | 'waitForObject' // -> AltObject handle; polls Unity-side until found or timeout
  | 'getText' // -> string; reads Text/TMP_Text on a previously-found handle
  | 'tap' // -> void; clicks an element by handle
  | 'pressKey' // -> void; simulates AltKeyCode press

/**
 * Object location strategies — match the C# `By` enum verbatim.
 * - `NAME`      — GameObject.name
 * - `PATH`      — slash-separated path through transform parents
 * - `COMPONENT` — find by a MonoBehaviour type name
 * - `ID`        — internal AltTester id (the value Unity returns in handles)
 * - `TAG`       — Unity GameObject.tag
 */
export enum By {
  NAME = 'NAME',
  PATH = 'PATH',
  COMPONENT = 'COMPONENT',
  ID = 'ID',
  TAG = 'TAG'
}

/**
 * The handle Unity returns for a found object. Future commands operating on the
 * same object reference it by `id`. AltTester encodes it as a flat JSON shape
 * — fields beyond `id` are convenience (name/transform info captured at
 * find-time, useful for debugging).
 */
export interface AltObjectHandle {
  id: number
  name: string
  // The C# driver carries more fields here (parentId, transformId, screen
  // coordinates, etc.) — add them as commands need them, not preemptively.
}

/**
 * Wraps Desktop's protocol-level close codes as typed exceptions so callers
 * can `catch (e: NoAppConnectedException)` rather than parsing close reasons.
 */
export class AltTesterError extends Error {
  constructor(
    message: string,
    public readonly closeCode?: number
  ) {
    super(message)
    this.name = 'AltTesterError'
  }
}

export class NoAppConnectedException extends AltTesterError {
  constructor(reason: string) {
    super(`No app connected: ${reason}`, 4001)
    this.name = 'NoAppConnectedException'
  }
}

export class AppDisconnectedException extends AltTesterError {
  constructor(reason: string) {
    super(`App disconnected mid-session: ${reason}`, 4002)
    this.name = 'AppDisconnectedException'
  }
}

export class MultipleDriversException extends AltTesterError {
  constructor(reason: string) {
    super(`Multiple drivers with same tags: ${reason}`, 4005)
    this.name = 'MultipleDriversException'
  }
}

export class NotFoundException extends AltTesterError {
  constructor(message: string) {
    super(message)
    this.name = 'NotFoundException'
  }
}
