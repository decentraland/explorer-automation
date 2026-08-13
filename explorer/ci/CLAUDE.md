# CLAUDE.md

Guidance for the CI plumbing under `explorer/ci/` and the workflows that call it. Test-authoring
conventions live in [explorer/CLAUDE.md](../CLAUDE.md).

## The Explorer's lifetime is bounded by an Actions step

The runner terminates a step's tracked child processes when the step ends, so the AltTester server
and the test command have to share one `run:` block — split them and the server dies before NUnit
connects. And with AltTester already listening, MetaForge assumes an app is connected and skips
launching the Explorer, so the workflow launches the pre-installed build itself and waits for
`command received` in `AltTester-Server.log` before handing over.

## PowerShell 5.1 on the Windows runner

The generated `.ps1` is read as ANSI, so a `run:` body must stay ASCII — an em dash is a parse
error that kills the step. `Out-File -Encoding utf8` writes a BOM, which breaks any downstream
parser reading the file as a stream; use `WriteAllText` where a later step parses the output.
Native-argument passing also mangles embedded double quotes: `gh api … --jq '… "\(.x)" …'` arrives
as two positional arguments and fails. Parse with `ConvertFrom-Json` instead of an interpolating
jq filter.

A job-level `env:` key beats a value written to `$GITHUB_ENV`, silently. When a step resolves
something later steps consume, the resolved name must not also exist in the job's `env:` block —
`BUILD_URL_INPUT` and `BUILD_URL` are split for exactly that reason.

## Reading a leg's result

Matrix jobs cannot carry per-instance outputs, so each shard writes its row as an artifact and a
final job assembles the table. MetaForge closes with either `All N tests passed.` or
`Tests finished: …`, so matching only the latter reports no count for a green run — and a loss
check that goes inert on an absent count is worse than none. A filter matching nothing exits
clean, which is why every shard reconciles the count MetaForge reports against the count it
planned.

## Scripts

`Resolve-ExplorerRenderSize.ps1` and `Resolve-ExplorerBuildUrl.ps1` take parameters and only write
`$GITHUB_ENV` when it exists, so both run by hand against a candidate runner image — which is how
their display enumeration and build probing were checked without a push. Keep that property.
