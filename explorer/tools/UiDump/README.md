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
