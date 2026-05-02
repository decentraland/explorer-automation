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
├── package.json          # workspace root
├── tsconfig.base.json
├── .syncpackrc.json
├── scripts/
│   ├── pool.ts           # generic concurrency-limited runner with fail-fast
│   ├── build-all.ts      # builds every scene via the pool
│   └── new-scene.ts      # scaffolds a scene package + C# fixture
├── templates/
│   └── scene/            # source-of-truth scene template
└── packages/
    └── <scene-id>/
```

## Conventions

- **One scene = one camera pose.** Multi-pose concepts live as `<concept>-<pose>` packages (e.g. `lighting-baseline-front`, `lighting-baseline-side`). This keeps scenes deterministic and avoids in-scene camera switching.
- **Scene name = test ID = baseline filename root.** The C# fixture references the scene by package name.
- **No animations or non-deterministic effects** unless the scene exists specifically to test them. Fix time-of-day, freeze tweens, avoid network-loaded media.
- **Parcel coords are always `0,0`.** All scenes load one at a time into the same host.
