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

## The Windows leg is sharded on every path

One job builds the shard matrix — `plan` in `windows-inworld-custom-image.yml` — and every origin of
that leg goes through it. Callers say **what** to split (`scope`: `ALL` or `FIXTURES: A B C`), never
how it splits; the count and the packing live in `explorer/ci/ScopeInWorldTests`. Only a PR run
resolves a scope, so only a PR run has one to pass:

| origin | passes | splits |
| --- | --- | --- |
| `inworld-pr.yml` | the scope its resolver printed | that scope |
| `inworld-main.yml` | `scope: 'ALL'` | the whole category |
| `run-inworld-suite.yml` dispatch | nothing; `filter` is empty or `Category=InWorld` | the whole category |
| `windows-inworld-custom-image.yml` dispatch | a `scope`, or a narrower `test_filter` | that scope, else one runner |
| a cross-repo caller (unity-explorer) | nothing but a `tests_ref` | the whole category |

The rules that keep it that way, all of them load-bearing:

- **A new caller that forgets `scope` still shards.** An empty scope is read back from `test_filter`:
  empty or `Category=InWorld` means the whole category. Only a genuinely narrower filter runs unsplit,
  because a vstest expression cannot be mapped back to the fixtures the packer weighs.
- **Nothing outside the tool may choose a shard count.** `Shards.DefaultCount` is the only one, and
  `--self-test` asserts the covers at exactly that number. A workflow passing `--shards` would be a
  second answer to the same question.
- **The unsplit case is still a one-shard matrix**, so no step downstream needs a special case, and
  `1/1` in a job name is the visible sign that a run did not split.
- **A plan is never passed between workflows as a matrix.** Handing a resolved *scope* to the planner
  is what removed the "is this plan still valid for this scope" check that used to guard the seam.
- **The planner checks out this repo explicitly**, at `tests_ref`, exactly as the leg's own checkout
  does. A bare checkout inside a `workflow_call` run fetches the *caller*, which has no
  `explorer/ci` — and since a missing planner costs only the split, that would degrade in silence.

Every degradation is one runner, never fewer tests: the tool writes no plan on any doubt (an unknown
fixture name, untrusted counts, a failed self-test), and a missing plan falls back to the caller's own
filter on a single shard. When touching any of this, run `--self-test` and check that a `1/1` job name
is a decision you can point at rather than a default nobody chose.

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
