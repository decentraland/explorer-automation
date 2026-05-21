import { AltObjectHandle, By, CommandRequest, CommandResponse, ConnectionConfig } from './protocol.js'

/**
 * Forward-declared WebSocket handle. The follow-up session will replace this
 * with the actual `ws` package import + `npm install ws @types/ws`. Keeping
 * it as `unknown` for now so the scaffold typechecks without adding a
 * dependency we don't yet use at runtime.
 */
type WebSocketHandle = unknown

/**
 * Minimal AltTester WebSocket client — vendor implementation targeting
 * AltTester Desktop v2.3.x. Used by the `@cross` Playwright specs as the
 * orchestration observability layer (Playwright watches Unity scene state
 * directly while C# stages perform actions + assertions).
 *
 * Designed to be *narrow*: commands are added only when a spec needs them.
 * The current set (`findObject`, `waitForObject`, `getText`, `tap`, `pressKey`)
 * covers Flow 1's observation + simple-action needs.
 *
 * **Not implemented yet** — this file is the scaffold + documented signatures.
 * The actual WebSocket transport and command serialization will be filled in
 * during a focused follow-up session. The decisions captured here:
 *
 *   - Uses the `ws` npm package (lightweight, well-maintained, MIT). Add to
 *     web/package.json as a `dependencies` entry (NOT devDependencies — the
 *     client is runtime code Playwright tests import at runtime).
 *   - Message correlation by UUID (`messageId`). A pending-promise map keyed
 *     by messageId resolves each response to its caller.
 *   - Reconnection: not built in — tests should treat a connection drop as
 *     a hard failure. The Desktop server doesn't retry on its end either.
 *   - Concurrency: a single AltDriver instance is single-flight-per-command
 *     (commands serialize via the message-id map). Tests should construct
 *     one AltDriver and reuse it, mirroring the C# pattern.
 *
 * @see ./protocol.ts for wire shapes and close-code → exception mapping.
 */
export class AltDriver {
  private ws: WebSocketHandle | null = null
  private readonly pending = new Map<string, { resolve: (r: CommandResponse) => void; reject: (e: Error) => void }>()

  constructor(private readonly config: ConnectionConfig) {}

  /**
   * Opens the WebSocket to AltTester Desktop and waits for the
   * driver-registered ack before resolving. Throws typed exceptions if Desktop
   * closes with a known protocol-level code (NoAppConnected, MultipleDrivers).
   *
   * Default config for our @cross flows:
   *   host: '127.0.0.1', port: 13000, appName: '__default__',
   *   platform: 'Standalone', platformVersion: 'macOS',
   *   deviceInstanceId: <unique>, driverType: 'SDK'
   *
   * @throws NoAppConnectedException if no Unity app is currently attached to Desktop.
   */
  async connect(): Promise<void> {
    throw new Error('AltDriver.connect not yet implemented — see follow-up issue')
    // Implementation outline:
    //   1. Build URI: `ws://${host}:${port}/altws?appName=...&platform=...&...`
    //      (URI-encode each value; see protocol.ts for the full param list)
    //   2. Open WebSocket via `import('ws')`.
    //   3. wire 'message' → JSON.parse → dispatch to pending.get(messageId).resolve
    //   4. wire 'close' → if code in {4001,4002,4005,4007}, throw typed exception;
    //      else reject all pending promises with a generic AltTesterError.
    //   5. await registration ack (Desktop sends a "driverConnected" message after URL accept).
  }

  /** Idempotent close — safe to call in test afterEach hooks. */
  async disconnect(): Promise<void> {
    throw new Error('AltDriver.disconnect not yet implemented')
  }

  /**
   * Finds a single GameObject. Throws {@link NotFoundException} if no match.
   * Use {@link waitForObject} when the object may take time to appear.
   */
  async findObject(by: By, value: string): Promise<AltObjectHandle> {
    return this.send({ commandName: 'findObject', by, value }) as Promise<AltObjectHandle>
  }

  /**
   * Polls Unity-side until the object appears or `timeoutSec` elapses. Mirrors
   * `AltDriver.WaitForObject` in C# — Unity SDK does the polling, so the WS
   * round trip is a single command, not a loop on the client.
   */
  async waitForObject(by: By, value: string, timeoutSec = 20, intervalSec = 0.5): Promise<AltObjectHandle> {
    return this.send({
      commandName: 'waitForObject',
      by,
      value,
      timeout: timeoutSec,
      interval: intervalSec
    }) as Promise<AltObjectHandle>
  }

  /** Reads the Text/TMP_Text value on a previously-found object. */
  async getText(object: AltObjectHandle): Promise<string> {
    return this.send({ commandName: 'getText', objectId: object.id }) as Promise<string>
  }

  /** Simulates a tap (click) on the object. Equivalent to C# `AltObject.Click()`. */
  async tap(object: AltObjectHandle): Promise<void> {
    await this.send({ commandName: 'tap', objectId: object.id })
  }

  /**
   * Simulates a keyboard press. AltKeyCode values are the Unity `KeyCode`
   * enum names as strings (e.g. 'I', 'Escape', 'Alpha1') — keep this
   * stringly-typed for the wire; callers can use a const helper if useful.
   */
  async pressKey(keyCode: string, durationSec = 0.5): Promise<void> {
    await this.send({ commandName: 'pressKey', keyCode, duration: durationSec })
  }

  /**
   * Internal command dispatch. Generates a messageId, sends the JSON-encoded
   * request, registers a pending promise, returns when the matching response
   * arrives. Throws if `response.error` is set.
   */
  private async send(command: Omit<CommandRequest, 'messageId'>): Promise<unknown> {
    throw new Error(`AltDriver.send not yet implemented (command: ${command.commandName})`)
    // Implementation outline:
    //   const messageId = randomUUID()
    //   const payload = JSON.stringify({ ...command, messageId })
    //   const promise = new Promise<CommandResponse>((resolve, reject) => {
    //     this.pending.set(messageId, { resolve, reject })
    //     setTimeout(() => {
    //       this.pending.delete(messageId)
    //       reject(new AltTesterError(`Command ${command.commandName} timed out`))
    //     }, COMMAND_TIMEOUT_MS)
    //   })
    //   this.ws!.send(payload)
    //   const response = await promise
    //   if (response.error) {
    //     if (response.error.type === 'NotFound') throw new NotFoundException(response.error.message)
    //     throw new AltTesterError(`${response.error.type}: ${response.error.message}`)
    //   }
    //   return response.data
  }
}
