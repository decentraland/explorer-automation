# UiDump

Small CLI to inspect the **live instrumented Explorer client** through AltTester
(connects to AltTester Desktop at `127.0.0.1:13000`, app name `__default__` — same as the
test suite). Use it to discover locators (names, ids, hierarchy paths, key components)
before writing views, and to click through the UI so hidden panels can be dumped.

It is a standalone project — it is **not** part of the `explorer/Tests` compilation.

## Prerequisites

- AltTester Desktop listening on 13000 and the instrumented Explorer connected
  (`lsof -nP -iTCP:13000 | grep -c Explorer` returns >= 1).
- Do not run it while a `dotnet test` run is in progress — a second driver connection
  can be refused with "No app connected that has the given tags".

## Usage

```bash
cd explorer/tools/UiDump

# List all enabled UI elements (name, id, parent path, key components)
dotnet run -- tree

# Filter by name, case-insensitive substring match
dotnet run -- tree Sidebar

# Include disabled objects
dotnet run -- tree Backpack --all

# Save a screenshot of the live client
dotnet run -- shot /tmp/ui.png

# Click an object by GameObject name (exact), falling back to AltTester id.
# Use the numeric id from `tree` output when the name is ambiguous.
dotnet run -- click SidebarSettingsButton
dotnet run -- click -550714

# List objects matching an AltTester By.PATH query. Much cheaper than `tree` on heavy
# worlds (no full-scene enumeration) and the only safe way to browse subtrees at
# Genesis Plaza — a timed-out `tree` can wedge the app's AltTester connection.
dotnet run -- sub "//BackpackSection//TabSelector/*" --all

# Double-click (grid items treat clickCount == 2 as Equip)
dotnet run -- dclick -863224

# PointerEnter an object (reveals hover-only overlays). Hover state only lasts for the
# lifetime of one driver session, so combine it with a click via hoverclick.
dotnet run -- hover OutfitSlot_1

# Hover one object then click/tap another within a single driver session.
# Targets accept a name, a numeric id, or a By.PATH query starting with //.
dotnet run -- hoverclick OutfitSlot_2 "//OutfitsView//OutfitSlot_2/LoadedState/Hover"
dotnet run -- hovertap  OutfitSlot_2 "//OutfitsView//OutfitSlot_2/LoadedState/Hover"

# Press a key (AltKeyCode name) / set text on an input field
dotnet run -- key I
dotnet run -- settext "//BackpackSection//SearchBar" "Punk"

# settext fires the submit path (which chat handles as "send", twice on this build);
# settextns sets the text without submitting.
dotnet run -- settextns "//ChatInputBox//CustomInputField" "draft text"

# Read / write a component property (value is parsed as number, bool, then string).
# Writing UnityEngine.UI.Slider.value fires onValueChanged like a user drag would.
dotnet run -- getprop TimeSlider UnityEngine.UI.Slider value UnityEngine.UI
dotnet run -- setprop TimeSlider UnityEngine.UI.Slider value 0.75 UnityEngine.UI
```

Add `--no-build` after `run` to skip the rebuild once the tool is built.

## Output format

```
SidebarSettingsButton  id=-13980  path=/MainUIContainer(Clone)/UILayout/Sidebar/SidebarView/UpperLayout/SidebarSettingsButton  comps=HoverableAndSelectableButtonWithAnimator,Button,ShowTooltipOnHoverElement
```

- `id` — AltTester runtime instance id. Stable for the lifetime of the object only; do
  not hardcode into views (views use `By.ID` **UUIDs** from the AltId component, or names/paths).
- `path` — parent chain reconstructed from `transformParentId`, root first.
- `comps` — components matching key markers (Button, TMP_InputField, Toggle,
  TMP_Text/TextMeshProUGUI, InputField, Slider, Dropdown, ScrollRect). Component lookup is
  one RPC per element, so it is skipped when more than 400 elements match — narrow the
  pattern to get components. For text-bearing elements (<= 150 matches) the displayed
  `text="..."` is included too.

## Implementation notes

- Screenshots use `AltDriver.GetScreenshot()` + SkiaSharp PNG re-encode, mirroring
  `Tests/Common/Snapshots/ScreenshotCapture.cs`. `GetPNGScreenshot` is avoided on purpose —
  it has a known StackOverflow bug in AltTester 2.3.x.
- A full `tree` dump enumerates the whole scene (60k+ objects at Genesis Plaza) — always
  pass a name pattern when you can.
- Scene-embedded UI (SDK scene popups, e.g. quest/minigame modals) is **not** uGUI and does
  not appear in the dump, but it can still block raycasts and eat clicks on HUD buttons.
  If a click reports success but nothing opens, screenshot first and look for a scene popup.
- Backpack grids pool their tiles: after a search or on partial pages, stale tiles stay
  **enabled** but mask-clipped, so `BackpackItem(Clone)` sibling counts and indexes lie.
  Only tiles with an enabled `FullBackpack` child carry real content — anchor queries and
  clicks there (e.g. `//BackpackGrid/BackpackItem(Clone)/FullBackpack`).
- The grid hover overlay's Equip/Unequip Buttons do not respond to synthetic clicks/taps
  in this build; equip via double-click (`dclick`) instead. The outfit slots' hover
  buttons (Save/Equip/Delete) DO respond when hovered and clicked in one session.

## Batch mode (`repl`) — use this, not repeated single-shots

Every single-shot invocation pays MSBuild + process spawn + driver handshake (~5-20s).
`repl` reads commands line-by-line from stdin over ONE driver connection:

```bash
printf 'click SidebarChatButton\nwaitfor ChatPanel 10\nsub //ChatPanel//* --all\nshot /tmp/chat.png\nquit\n' \
  | ./uidump.sh repl
```

- `./uidump.sh` runs the prebuilt Release DLL (rebuilds automatically when Program.cs changed).
- Extra commands available inside a batch: `sleep <seconds>`, `waitfor <name|//path> [timeoutS]`
  (prefer `waitfor` over blind `sleep`), `echo <marker>`, `quit`.
- Each command echoes `-- done (<rc>): <line>` so output can be correlated with input.
- Errors don't abort the batch; they print `ERR: ...` and continue.
- The driver connection closes at EOF — the one-pairing-at-a-time rule still holds; never run
  a batch while `dotnet test` is running.
