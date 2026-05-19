#!/usr/bin/env python3
"""Track A (Slice 3) aggregator — variance across N AutoPilot summary files.

Reads N `perf-summary-*.txt` plain-text files emitted by AutoPilot
(`Explorer/Assets/DCL/PerformanceAndDiagnostics/AutoPilot/AutoPilot.cs`),
parses the eight standardised fields, and computes per-metric mean,
stddev, coefficient of variation (CV = sigma / mu * 100), min, max, and
sample count across the N runs.

Emits a markdown table (stdout or --markdown <path>) and an optional CSV
(--csv <path>). When --github-step-summary is set or GITHUB_STEP_SUMMARY
is in env, the markdown is also appended there so it shows up directly on
the workflow run page.

Usage:
    python aggregate_cv.py path/to/probe-bundle/
    python aggregate_cv.py run-*/perf-summary.txt
    python aggregate_cv.py probe-bundle/ --csv out.csv --markdown out.md
"""
from __future__ import annotations

import argparse
import csv
import glob
import io
import os
import statistics
import sys
from dataclasses import dataclass
from pathlib import Path

# The eight fields AutoPilot.WriteSummaryAsync emits, in order. We match by
# exact label prefix so any future additions land in "unknown" instead of
# silently mangling our table.
METRIC_LABELS = (
    "CPU average",
    "CPU 1% worst",
    "CPU 0.1% worst",
    "CPU worst",
    "GPU average",
    "GPU 1% worst",
    "GPU 0.1% worst",
    "GPU worst",
)


@dataclass
class MetricStats:
    label: str
    values: list[float]

    @property
    def n(self) -> int:
        return len(self.values)

    @property
    def mean(self) -> float:
        return statistics.fmean(self.values)

    @property
    def stdev(self) -> float:
        # statistics.stdev requires n >= 2; for n == 1 stdev is undefined
        # (single observation has no variance) — surface as 0.0 with a CV of 0.
        return statistics.stdev(self.values) if self.n >= 2 else 0.0

    @property
    def cv_pct(self) -> float:
        if self.mean == 0 or self.n < 2:
            return 0.0
        return self.stdev / self.mean * 100.0

    @property
    def min(self) -> float:
        return min(self.values)

    @property
    def max(self) -> float:
        return max(self.values)


def parse_summary(path: Path) -> dict[str, float]:
    """Parse one AutoPilot summary file. Returns a {label: value} dict for
    whichever labels were present and parseable. Silently skips lines that
    don't match — the goal is best-effort aggregation across partial runs."""
    out: dict[str, float] = {}
    text = path.read_text(encoding="utf-8", errors="replace")
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line or ":" not in line:
            continue
        label, _, value_str = line.partition(":")
        label = label.strip()
        value_str = value_str.strip()
        if label not in METRIC_LABELS:
            continue
        try:
            out[label] = float(value_str)
        except ValueError:
            continue
    return out


def expand_inputs(inputs: list[str]) -> list[Path]:
    """Each input may be:
      - a directory (we glob *.txt inside, non-recursive then recursive),
      - a glob pattern (we expand it),
      - a literal path (we take it).
    Order is preserved per input; deduplicated globally by absolute path."""
    seen: set[Path] = set()
    result: list[Path] = []

    def _add(p: Path) -> None:
        ap = p.resolve()
        if ap not in seen:
            seen.add(ap)
            result.append(p)

    for raw in inputs:
        p = Path(raw)
        if p.is_dir():
            # Non-recursive first (favours flat probe-bundle/perf-summary-*.txt),
            # then recursive to also pick up nested run-N/perf-summary.txt layouts.
            matches = sorted(p.glob("perf-summary*.txt"))
            if not matches:
                matches = sorted(p.rglob("perf-summary*.txt"))
            for m in matches:
                _add(m)
        else:
            matches = sorted(Path(m) for m in glob.glob(raw))
            if not matches and p.exists():
                _add(p)
            else:
                for m in matches:
                    _add(m)
    return result


def render_markdown(stats: list[MetricStats], file_count: int, missing: dict[str, int]) -> str:
    buf = io.StringIO()
    buf.write(f"# AutoPilot variance probe — {file_count} runs\n\n")
    buf.write("| Metric | n | Mean | StdDev | CV % | Min | Max |\n")
    buf.write("|---|---|---|---|---|---|---|\n")
    for s in stats:
        buf.write(
            f"| `{s.label}` | {s.n} | {s.mean:.3f} | {s.stdev:.3f} "
            f"| {s.cv_pct:.2f} | {s.min:.3f} | {s.max:.3f} |\n"
        )
    if any(missing.values()):
        buf.write("\n**Missing samples per metric** (file parsed but label not present):\n\n")
        for label, n_missing in missing.items():
            if n_missing > 0:
                buf.write(f"- `{label}`: {n_missing} of {file_count}\n")
    buf.write("\n_Units are whatever AutoPilot writes (currently ms per "
              "`profiler.LastFrameTimeValueNs * 0.000001f` in `AutoPilot.SamplePerFrame`)._\n")
    return buf.getvalue()


def write_csv(stats: list[MetricStats], path: Path) -> None:
    with path.open("w", newline="", encoding="utf-8") as fp:
        w = csv.writer(fp)
        w.writerow(["metric", "n", "mean", "stdev", "cv_pct", "min", "max"])
        for s in stats:
            w.writerow([
                s.label, s.n,
                f"{s.mean:.6f}", f"{s.stdev:.6f}",
                f"{s.cv_pct:.4f}", f"{s.min:.6f}", f"{s.max:.6f}",
            ])


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("inputs", nargs="+",
                    help="Directories, glob patterns, or individual perf-summary*.txt files.")
    ap.add_argument("--markdown", type=Path, default=None,
                    help="Write markdown to this file (in addition to stdout).")
    ap.add_argument("--csv", type=Path, default=None,
                    help="Write CSV to this file.")
    ap.add_argument("--github-step-summary", action="store_true",
                    help=("Append markdown to $GITHUB_STEP_SUMMARY (also done "
                          "automatically when that env var is set in CI)."))
    args = ap.parse_args(argv)

    paths = expand_inputs(args.inputs)
    if not paths:
        print("error: no perf-summary files matched.", file=sys.stderr)
        return 2

    accum: dict[str, list[float]] = {label: [] for label in METRIC_LABELS}
    missing: dict[str, int] = {label: 0 for label in METRIC_LABELS}
    file_count = 0

    for path in paths:
        parsed = parse_summary(path)
        if not parsed:
            print(f"warn: no recognised labels in {path}", file=sys.stderr)
            continue
        file_count += 1
        for label in METRIC_LABELS:
            if label in parsed:
                accum[label].append(parsed[label])
            else:
                missing[label] += 1

    if file_count == 0:
        print("error: no parseable summary files found.", file=sys.stderr)
        return 2

    stats = [MetricStats(label, accum[label]) for label in METRIC_LABELS if accum[label]]
    markdown = render_markdown(stats, file_count, missing)

    sys.stdout.write(markdown)

    if args.markdown:
        args.markdown.write_text(markdown, encoding="utf-8")
    if args.csv:
        write_csv(stats, args.csv)

    step_summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if args.github_step_summary or step_summary_path:
        if step_summary_path:
            with open(step_summary_path, "a", encoding="utf-8") as fp:
                fp.write(markdown)
        else:
            print("warn: --github-step-summary set but $GITHUB_STEP_SUMMARY is empty.", file=sys.stderr)

    return 0


if __name__ == "__main__":
    sys.exit(main())
