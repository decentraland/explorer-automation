# Chassis baseline: wall-clock CV histogram (Tier 0)

Source: last **30** runs of `runner-validation-windows.yml` on `decentraland/explorer-automation`.
Eligibility: ≥ 3 **passed** samples per test. Canary shortlist: CV < **10.0%**.

- Tests with sufficient samples: **16**
- Tests dropped for insufficient samples: **1**
- Canary shortlist size: **3**

## Interpretation & go/no-go for Slice 2

Best wall-clock CV in the sample: **9.04%** (`TestSearchAndEquipFistPump`, n=11 passed). Bucket: **borderline** — viable only for steady-state metrics, not lifecycle.

**CV reference (wall-clock, perf-benchmark rule of thumb):**

| CV % | bucket | what it means |
|------|--------|---------------|
| 0–3 | excellent | can detect ~5% regressions on per-frame metrics |
| 3–8 | acceptable | usable with median + 5–10 samples per data point |
| 8–15 | borderline | viable only for steady-state metrics, not lifecycle |
| 15–30 | noisy | cannot detect regressions below ~20% |
| > 30 | unusable | noise dominates; no point measuring perf here |

Wall-clock is the **upper bound** on the runner's noise floor — it includes scene download, AltTester RPC, GC warmup, and Windows I/O. Per-frame counters measured via `ProfilerRecorder` in a steady-state phase are expected to be tighter than this number.

**Verdict: GO with conditions.** Best wall-clock CV is in the *borderline* bucket (8–15%). The image is usable for **steady-state** per-frame metrics, but not for lifecycle timings. Tight sampling discipline required (median + p95, n ≥ 30 per data point, warm-up budget before measurement).

**Recommended canary for Slice 2: `TestSearchAndEquipFistPump`** (CV 9.04%, mean 11.98s, pass-rate 100%).

**Validation conditions for Slice 2 (pre-instrumentation):**

1. Run the canary ≥ 30 times back-to-back on the runner; record per-frame `Time.unscaledDeltaTime` median + p95 via `ProfilerRecorder`.
2. If per-frame CV > 8% — **stop** and investigate runner-side noise sources before instrumenting more tests.
3. If per-frame CV < 5% — green light for Slice 3 instrumentation (GC alloc, draw calls, managed memory).
4. **Reject lifecycle metrics**: only measure during a steady-state window (≥ 3s after the trigger event), to exclude scene-load and GC-warmup variance.

**Caveats on this sample:**

- n = 10–13 passed samples per test is small: the 90% CI on stdev itself is wide (~±25% at n=11). Treat the numbers as direction, not as a tight bound.
- 30 runs were scanned, **16** produced a usable `summary.json`. The rest failed before the test step ran (CI-plumbing churn during chassis iteration). That share should drop as the image stabilizes — it does **not** reflect the image's perf reproducibility.
- ⚠ The PRD's call-out candidate ★ `TestOpenEventsFromSidebar` came in at **CV 15.29%** — above the 10% threshold. The PRD hypothesis does not hold; pick the canary from this report's shortlist instead.

## Canary shortlist

These are the tests stable enough to act as perf canaries:

| Test | CV % | mean | p50 | p95 | n (pass/total) | pass-rate |
|------|------|------|-----|-----|----------------|-----------|
| `TestSearchAndEquipFistPump` | 9.04 | 11.98s | 12.07s | 13.03s | 11/11 | 100% |
| `TestOpenPlacesWithShortcut` | 9.29 | 4.41s | 4.40s | 5.05s | 13/13 | 100% |
| `TestUnequipAndEquipAllEmoteSlots` | 9.80 | 51.41s | 52.58s | 54.88s | 11/11 | 100% |

## Full histogram (sorted by CV ascending)

| Test | CV % | mean | stdev | p50 | p95 | n (pass/total) | pass-rate |
|------|------|------|-------|-----|-----|----------------|-----------|
| `TestSearchAndEquipFistPump` | 9.04 | 11.98s | 1.08s | 12.07s | 13.03s | 11/11 | 100% |
| `TestOpenPlacesWithShortcut` | 9.29 | 4.41s | 409ms | 4.40s | 5.05s | 13/13 | 100% |
| `TestUnequipAndEquipAllEmoteSlots` | 9.80 | 51.41s | 5.04s | 52.58s | 54.88s | 11/11 | 100% |
| `TestOpenSettingsWithShortcut` | 11.16 | 4.36s | 487ms | 4.29s | 5.08s | 11/11 | 100% |
| `TestOpenMapWithShortcut` | 12.56 | 4.44s | 558ms | 4.30s | 5.28s | 11/11 | 100% |
| `TestOpenPlacesFromSidebar` | 14.47 | 4.59s | 664ms | 4.59s | 5.51s | 11/11 | 100% |
| `TestOpenSettingsFromSidebar` | 14.51 | 4.66s | 676ms | 4.70s | 5.56s | 11/11 | 100% |
| `TestOpenBackpackFromSidebar` | 15.18 | 4.76s | 723ms | 4.90s | 5.66s | 11/11 | 100% |
| `TestOpenEventsFromSidebar` **★** | 15.29 | 4.62s | 707ms | 4.49s | 5.55s | 11/11 | 100% |
| `TestOpenCommunitiesFromSidebar` | 16.12 | 4.66s | 752ms | 4.56s | 5.62s | 11/11 | 100% |
| `TestSwitchBetweenAllTabs` | 17.72 | 19.35s | 3.43s | 18.77s | 24.34s | 11/11 | 100% |
| `TestOpenEventsWithShortcut` | 18.53 | 3.73s | 690ms | 3.73s | 4.77s | 11/11 | 100% |
| `TestOpenGalleryWithShortcut` | 21.22 | 4.57s | 971ms | 4.37s | 6.11s | 10/11 | 91% |
| `TestOpenCommunitiesWithShortcut` | 31.26 | 4.74s | 1.48s | 4.38s | 6.97s | 11/11 | 100% |
| `TestOpenGalleryFromSidebar` | 37.67 | 9.54s | 3.59s | 11.60s | 12.40s | 10/11 | 91% |
| `TestOpenBackpackWithShortcut` | 90.62 | 6.08s | 5.51s | 4.39s | 14.09s | 11/11 | 100% |

## Dropped (insufficient samples)

| Test | n (pass/total) | pass-rate |
|------|----------------|-----------|
| `TestOpenMapFromSidebar` | 0/11 | 0% |

## Notes

- CV (coefficient of variation) is computed against **passed** samples only. Failed/broken runs are still counted in pass-rate but their durations are excluded from the variance signal because they are dominated by retry / timeout artefacts.
- This CV is an **upper bound** on the noise floor of any future per-metric (frame time, GC alloc, draw calls). Wall-clock includes everything: scene load, network jitter, GC stalls, runner-side I/O. Per-metric counters captured via `ProfilerRecorder` should beat this number, not match it.
- ★ marks `TestOpenEventsFromSidebar` per the PRD's call-out as a candidate canary.
