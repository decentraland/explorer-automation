# Visual Regression Scenes

npm-workspace monorepo of SDK7 scenes used by the C# AltTester suite for visual regression testing.

Each `packages/*` is a standalone SDK7 scene built and deployed once into a single "main" scene host inside the Explorer. The C# fixtures hot-swap the built `bin/index.js` (and assets) into the host, sdk-commands' file watcher fires a reload, and the test snapshots the rendered frame.

## Scripts

```bash
# Install (run from this directory)
npm install

# Build every scene in parallel with bounded concurrency + fail-fast
npm run build

# Scaffold a new test scene + matching C# fixture stub
npm run new-scene -- <scene-id>
# example: npm run new-scene -- lighting-baseline-front

# Lint / format
npm run format
npm run format:fix

# Keep @dcl/* versions aligned across all scenes
npm run syncpack:list
npm run syncpack:fix
```

## Layout

```
scenes/
├── package.json
├── tsconfig.base.json
├── .syncpackrc.json
├── scripts/
│   ├── pool.ts
│   ├── build-all.ts
│   └── new-scene.ts
├── templates/
│   └── scene/
└── packages/
    ├── _host/
    └── <scene-id>/
```

### `scripts/`
TypeScript helpers run via `tsx` from the npm scripts in `package.json`. `pool.ts` is a generic concurrency-limited runner with fail-fast semantics, used by `build-all.ts` to build every scene in parallel. `new-scene.ts` scaffolds a new scene package from `templates/scene/` and writes a matching C# fixture stub under `../Tests/Tests/Visual/`.

### `templates/scene/`
Source-of-truth template that `new-scene.ts` copies on each scaffold. Edits here propagate to every newly-created scene; existing scenes keep their forked copy and don't auto-update.

### `packages/_host/`
The "host" scene that the visual-host server actually serves. Its `bin/index.js` is overwritten on every fixture's `[OneTimeSetUp]` with the test scene's compiled bundle, triggering a hot reload. Its `scene.json` controls realm-wide config (skybox `fixedTime`, base parcel, spawn point) for ALL visual tests — change it here to affect every scene in one PR.

### `packages/<scene-id>/`
Each is a standalone SDK7 scene authored to exercise a specific rendering path. Build artifacts (`bin/`, `main.crdt`, `assets/` mirrored from test scenes) are gitignored. The matching C# fixture lives at `../Tests/Tests/Visual/<Pascal>Fixture.cs`.

## Conventions

- **One scene = one camera pose.** Multi-pose concepts live as `<concept>-<pose>` packages (e.g. `lighting-baseline-front`, `lighting-baseline-side`). This keeps scenes deterministic and avoids in-scene camera switching.
- **Scene name = test ID = baseline filename root.** The C# fixture references the scene by package name.
- **No animations or non-deterministic effects** unless the scene exists specifically to test them. Fix time-of-day, freeze tweens, avoid network-loaded media.
- **Parcel coords are always `0,0`.** All scenes load one at a time into the same host.
