"""Ad-hoc analysis of PerfSampler artifacts downloaded from CI runs.

Usage:
    gh run download <run-id> --repo decentraland/explorer-automation --dir /tmp/perf/<run-id>
    python docs/perf-analysis.py /tmp/perf

Reads every explorer-perf/<fixture>/perf.csv it can find under the given root and
recomputes statistics from the raw per-frame data, ignoring the eight scalars
PerfSampler writes into perf-summary.txt.

Reported: sample count, CPU p50 (level) and CPU p90 (tail). Everything else was
measured and dropped, using run-to-run spread across three runs of the *same*
Explorer build as the criterion (mean spread, % of median):

    p50 18.8 | p90 34.8 | p95 41.4 | 1% worst 51.6 | max 74.1

  * "0.1% worst" is identically equal to max on every capture we have (12/12):
    PerfSampler computes max(1, (int)(n * 0.001)) worst frames, which floors to 1
    for any n < 1000, and our fixtures produce 88-407 frames.
  * "1% worst" rests on 1-4 frames at these sample sizes and inherits max's noise.
  * max is unusable outright (one fixture moved 558 -> 769 -> 1792 ms on one build).
  * GPU is dropped entirely: the macOS paravirt runner has no real GPU and every
    GPU metric spreads 47-97% run to run. The column is still captured in perf.csv,
    because on the Windows chassis (dedicated T4) it should carry signal.

These cutoffs are properties of THIS chassis and these capture lengths, not of the
metrics. A longer capture at a higher frame rate (Windows: ~3600 frames instead of
~200) makes p99 and 1% worst legitimate again -- re-run this comparison there before
settling on a metric set.
"""
import csv
import os
import statistics
import sys
from collections import defaultdict


def percentile(sorted_values, q):
    """Nearest-rank percentile. No numpy dependency on purpose -- this runs anywhere."""
    if not sorted_values:
        return float("nan")
    k = max(0, min(len(sorted_values) - 1, int(round(q / 100.0 * (len(sorted_values) - 1)))))
    return sorted_values[k]


def read_cpu_times(path):
    """Column 1 only. Column 2 (GPU) is deliberately ignored -- see module docstring."""
    cpu = []
    with open(path, newline="") as fh:
        for row in csv.reader(fh):
            if not row or row[1].strip().strip('"') == "CPU Time":
                continue
            cpu.append(float(row[1]))
    return cpu


def stats(values):
    s = sorted(values)
    return {
        # n is reported because it is the reason the tail metrics were dropped: a
        # 88-frame capture and a 407-frame one do not support the same claims.
        "n": len(s),
        "p50": percentile(s, 50),
        "p90": percentile(s, 90),
    }


def collect(root):
    runs = defaultdict(dict)
    for dirpath, _, filenames in os.walk(root):
        if "perf.csv" not in filenames or "explorer-perf" not in dirpath.replace("\\", "/"):
            continue
        parts = dirpath.replace("\\", "/").split("/")
        fixture = parts[-1].replace("ExplorerAutomation.Tests.Tests.", "")
        run_id = next((p.split("-")[-1] for p in parts if p.startswith("runner-validation")), "unknown")
        runs[run_id][fixture] = stats(read_cpu_times(os.path.join(dirpath, "perf.csv")))
    return runs


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "/tmp/perf"
    runs = collect(root)
    if not runs:
        sys.exit(f"No perf.csv found under {root}")

    run_ids = sorted(runs)
    fixtures = sorted({f for r in runs.values() for f in r})
    metrics = ["n", "p50", "p90"]

    print("\n=== CPU frame time, ms ===")
    header = f"{'fixture':<22}{'run':<14}" + "".join(f"{m:>10}" for m in metrics)
    print(header)
    print("-" * len(header))
    for fixture in fixtures:
        for run_id in run_ids:
            s = runs[run_id].get(fixture)
            if not s:
                continue
            cells = "".join(f"{s[m]:>10.0f}" if m == "n" else f"{s[m]:>10.1f}" for m in metrics)
            print(f"{fixture:<22}{run_id:<14}{cells}")
        print()

    # Run-to-run deviation: the number that decides whether this chassis can detect
    # a regression at all. Spread is (max - min) / median across runs of the SAME build.
    # Only meaningful when the runs compared share an Explorer build -- mixing builds
    # folds real code changes into what reads as chassis noise.
    print("\n=== run-to-run spread across identical builds, % of median ===")
    header = f"{'fixture':<22}" + "".join(f"{m:>10}" for m in metrics[1:])
    print(header)
    print("-" * len(header))
    for fixture in fixtures:
        series = [runs[r][fixture] for r in run_ids if fixture in runs[r]]
        if len(series) < 2:
            continue
        cells = ""
        for m in metrics[1:]:
            vals = [s[m] for s in series]
            med = statistics.median(vals)
            cells += f"{(max(vals) - min(vals)) / med * 100:>10.1f}" if med else f"{'n/a':>10}"
        print(f"{fixture:<22}{cells}")


if __name__ == "__main__":
    main()
