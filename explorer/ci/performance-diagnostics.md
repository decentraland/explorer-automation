# In-world performance diagnostics

Set `record_perf: true` when dispatching `run-inworld-suite.yml` or
`windows-inworld-custom-image.yml`. It defaults to false and adds no performance
pass/fail threshold. Pin the Windows build URL and tests_ref when comparing runs.

Windows emits a `PERF:` progress line at each test teardown and attaches frame
CSV, summary and UTC test window to Allure. The standalone
`windows-inworld-perf-shard<N>-<run>` artifact uploads even after failure. Each
invocation gets a unique directory, including repeated test names. macOS keeps
its existing opt-in fixture capture.

- `process.csv`: UTC/elapsed time, liveness, cumulative CPU/CPU delta, working and
  private memory, thread count and current test. Sampling starts at Explorer
  launch every two seconds, without AltTester calls. Samples flush immediately
  and stop when Explorer or the owning workflow step exits.
- `<id>/test.json`: name, host start/end time, duration, NUnit outcome, capture
  completion and errors. A `running` entry without an end means interrupted.
- `<id>/perf.csv`: client frame number and CPU/GPU milliseconds.
- `<id>/report.txt`: valid counts, p50/p95/p99, maximum and counts over
  33/100/1000 ms. Percentiles require at least 2/20/100 samples respectively;
  sparse tails still need caution. Invalid/zero and incomplete rows are flagged.

The existing `PerfSampler.Begin/End` API requires no new client build when that
API is present. It adds two driver commands per test and client CSV writing.
The host window includes capture startup and test setup; capture ends before
screenshot cleanup. A sampler error disables subsequent captures. A known
transport failure skips End and preserves readable partial data without another
command. Missing samples are never interpreted as fast frames.

Frames have no UTC timestamps. Associate data with its test window, not an exact
wall-clock instant. Client buffering can lose the last samples on a hang or
crash. Independent process samples cover startup and driver loss, but idle CPU
alone does not prove a hang or GPU saturation. Do not convert CPU measurements
into measured FPS or use an average to rule out a pause.

Compare failing and passing tests on the same build, runner type, render settings
and scene. Inspect CPU deltas/memory during the UTC window, frame tails, and the
first Player/AltTester error. Keep an uninstrumented control when attributing
flakes: diagnostic capture can itself affect timing.

Validation:

```text
dotnet test explorer/ci/PerformanceTests/PerformanceTests.csproj
powershell -NoProfile -File explorer/ci/test-performance-monitor.ps1
```
