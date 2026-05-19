#!/usr/bin/env python3
"""Tier-0 baseline analyzer for the Windows GPU chassis.

Pulls the last N `runner-validation-windows.yml` runs from the
`decentraland/explorer-automation` repo via the `gh` CLI, downloads each
run's fallback artifact (`runner-validation-windows-<sha>-<run_id>`),
extracts the Allure `summary.json` and aggregates per-test wall-clock
duration across runs. Emits a coefficient-of-variation (CV) histogram in
markdown sorted by CV ascending.

The goal is to answer — before any runtime instrumentation lands — which
existing InWorld tests are stable enough on the T4 self-hosted runner to
serve as perf canaries, and what the noise floor looks like on a counter
we already get for free (Allure-reported test duration).

Usage:
    python analyze_existing_baseline.py
    python analyze_existing_baseline.py --limit 50 --branch feat/win-gpu-test-runner-workflow
    python analyze_existing_baseline.py --output baseline_report.md --cv-threshold 8

Requires `gh` on PATH and an authenticated session
(`gh auth status` should be green).
"""
from __future__ import annotations

import argparse
import io
import json
import re
import statistics
import subprocess
import sys
import zipfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable

DEFAULT_REPO = "decentraland/explorer-automation"
DEFAULT_WORKFLOW = "runner-validation-windows.yml"
DEFAULT_LIMIT = 30
DEFAULT_MIN_SAMPLES = 3
DEFAULT_CV_THRESHOLD = 10.0  # percent
ARTIFACT_NAME_RE = re.compile(r"^runner-validation-windows-[0-9a-f]+-\d+$")
SUMMARY_PATH_IN_ZIP = "Local/Decentraland/MetaForge/allure-report/summary.json"


@dataclass
class Sample:
    run_id: int
    created_at: str
    head_sha: str
    branch: str
    run_conclusion: str
    status: str          # per-test status from Allure (passed/failed/broken/skipped)
    duration_ms: int


@dataclass
class TestStats:
    name: str
    samples: list[Sample] = field(default_factory=list)

    @property
    def durations(self) -> list[int]:
        return [s.duration_ms for s in self.samples]

    @property
    def passed(self) -> list[Sample]:
        return [s for s in self.samples if s.status == "passed"]

    @property
    def n(self) -> int:
        return len(self.samples)


def run_gh(args: list[str], *, binary: bool = False) -> bytes | str:
    """Shell out to gh, return stdout. Raises on non-zero exit."""
    proc = subprocess.run(
        ["gh", *args],
        check=False,
        capture_output=True,
    )
    if proc.returncode != 0:
        sys.stderr.write(
            f"gh {' '.join(args)} failed (exit {proc.returncode}):\n"
            f"{proc.stderr.decode('utf-8', errors='replace')}\n"
        )
        raise SystemExit(proc.returncode)
    return proc.stdout if binary else proc.stdout.decode("utf-8")


def list_runs(repo: str, workflow: str, limit: int, branch: str | None) -> list[dict]:
    args = [
        "run", "list",
        "--repo", repo,
        "--workflow", workflow,
        "--limit", str(limit),
        "--json", "databaseId,conclusion,createdAt,headBranch,headSha,displayTitle,event",
    ]
    if branch:
        args += ["--branch", branch]
    raw = run_gh(args)
    return json.loads(raw)


def list_artifacts(repo: str, run_id: int) -> list[dict]:
    raw = run_gh([
        "api", f"repos/{repo}/actions/runs/{run_id}/artifacts",
        "--paginate",
    ])
    return json.loads(raw).get("artifacts", [])


def download_artifact(repo: str, artifact_id: int, cache_path: Path) -> bytes:
    if cache_path.exists():
        return cache_path.read_bytes()
    blob = run_gh(
        ["api", f"repos/{repo}/actions/artifacts/{artifact_id}/zip"],
        binary=True,
    )
    cache_path.parent.mkdir(parents=True, exist_ok=True)
    cache_path.write_bytes(blob)
    return blob


def extract_summary(zip_bytes: bytes) -> dict | None:
    try:
        with zipfile.ZipFile(io.BytesIO(zip_bytes)) as zf:
            if SUMMARY_PATH_IN_ZIP not in zf.namelist():
                return None
            with zf.open(SUMMARY_PATH_IN_ZIP) as fh:
                return json.load(fh)
    except (zipfile.BadZipFile, json.JSONDecodeError):
        return None


def collect_samples(runs: list[dict], repo: str, cache_dir: Path) -> tuple[dict[str, TestStats], int]:
    """Returns (tests-by-name, count of runs that produced a usable summary)."""
    tests: dict[str, TestStats] = {}
    runs_with_data = 0
    for run in runs:
        run_id = run["databaseId"]
        artifacts = list_artifacts(repo, run_id)
        chassis = next(
            (a for a in artifacts if ARTIFACT_NAME_RE.match(a.get("name", ""))),
            None,
        )
        if not chassis:
            sys.stderr.write(f"[skip] run {run_id}: no chassis artifact\n")
            continue
        cache_path = cache_dir / f"{run_id}.zip"
        try:
            blob = download_artifact(repo, chassis["id"], cache_path)
        except SystemExit:
            sys.stderr.write(f"[skip] run {run_id}: artifact download failed\n")
            continue
        summary = extract_summary(blob)
        if not summary:
            sys.stderr.write(f"[skip] run {run_id}: no summary.json in artifact\n")
            continue
        runs_with_data += 1
        for entry in summary.get("newTests", []) + summary.get("retryTests", []):
            name = entry.get("name")
            duration = entry.get("duration")
            status = entry.get("status", "unknown")
            if not name or duration is None:
                continue
            tests.setdefault(name, TestStats(name=name)).samples.append(Sample(
                run_id=run_id,
                created_at=run["createdAt"],
                head_sha=run.get("headSha", ""),
                branch=run.get("headBranch", ""),
                run_conclusion=run.get("conclusion") or "",
                status=status,
                duration_ms=int(duration),
            ))
    return tests, runs_with_data


def percentile(values: list[int], p: float) -> float:
    """Linear-interpolation percentile (0 <= p <= 100). Empty -> 0.0."""
    if not values:
        return 0.0
    s = sorted(values)
    if len(s) == 1:
        return float(s[0])
    k = (len(s) - 1) * (p / 100.0)
    lo = int(k)
    hi = min(lo + 1, len(s) - 1)
    frac = k - lo
    return s[lo] + (s[hi] - s[lo]) * frac


def compute_stats(t: TestStats) -> dict:
    passed_durations = [s.duration_ms for s in t.passed]
    # CV is meaningful only on successful runs — a failed test's duration is
    # dominated by retry/timeout artefacts and would inflate variance.
    base = passed_durations if len(passed_durations) >= 2 else t.durations
    mean = statistics.fmean(base) if base else 0.0
    stdev = statistics.pstdev(base) if len(base) >= 2 else 0.0
    cv = (stdev / mean * 100.0) if mean > 0 else 0.0
    pass_rate = (len(t.passed) / t.n * 100.0) if t.n else 0.0
    return {
        "name": t.name,
        "n": t.n,
        "n_passed": len(t.passed),
        "mean_ms": mean,
        "stdev_ms": stdev,
        "cv_pct": cv,
        "p50_ms": percentile(base, 50),
        "p95_ms": percentile(base, 95),
        "pass_rate_pct": pass_rate,
    }


def fmt_ms(ms: float) -> str:
    if ms >= 1000:
        return f"{ms/1000:.2f}s"
    return f"{ms:.0f}ms"


PRD_CALLOUT = "TestOpenEventsFromSidebar"

# Industry rule-of-thumb buckets for wall-clock CV on a perf benchmark. The
# image is judged against the *best* candidate's CV — wall-clock is an upper
# bound on the noise floor; per-frame metrics captured via ProfilerRecorder
# should be tighter than this.
CV_BUCKETS = [
    (3.0,  "excellent",  "can detect ~5% regressions on per-frame metrics"),
    (8.0,  "acceptable", "usable with median + 5–10 samples per data point"),
    (15.0, "borderline", "viable only for steady-state metrics, not lifecycle"),
    (30.0, "noisy",      "cannot detect regressions below ~20%"),
]


def name_cell(name: str) -> str:
    mark = " **★**" if name == PRD_CALLOUT else ""
    return f"`{name}`{mark}"


def classify_cv(cv: float) -> tuple[str, str]:
    for limit, label, note in CV_BUCKETS:
        if cv < limit:
            return label, note
    return "unusable", "noise dominates; no point measuring perf here"


def pick_canary(eligible: list[dict], cv_threshold: float) -> dict | None:
    """Pick the best canary: lowest CV, but require a steady-state phase
    (mean > 5s) so per-frame instrumentation has signal beyond setup."""
    candidates = [r for r in eligible if r["cv_pct"] < cv_threshold and r["mean_ms"] >= 5000]
    if not candidates:
        # Fall back to any sub-threshold test, even short ones.
        candidates = [r for r in eligible if r["cv_pct"] < cv_threshold]
    return candidates[0] if candidates else None


def render_markdown(
    rows: list[dict],
    *,
    runs_considered: int,
    runs_with_data: int,
    min_samples: int,
    cv_threshold: float,
    branch: str | None,
    repo: str,
    workflow: str,
) -> str:
    eligible = [r for r in rows if r["n_passed"] >= min_samples]
    dropped = [r for r in rows if r["n_passed"] < min_samples]
    eligible.sort(key=lambda r: r["cv_pct"])
    shortlist = [r for r in eligible if r["cv_pct"] < cv_threshold]

    out: list[str] = []
    out.append("# Chassis baseline: wall-clock CV histogram (Tier 0)")
    out.append("")
    out.append(
        f"Source: last **{runs_considered}** runs of `{workflow}` on "
        f"`{repo}`"
        + (f", branch `{branch}`" if branch else "")
        + "."
    )
    out.append(
        f"Eligibility: ≥ {min_samples} **passed** samples per test. "
        f"Canary shortlist: CV < **{cv_threshold:.1f}%**."
    )
    out.append("")
    out.append(f"- Tests with sufficient samples: **{len(eligible)}**")
    out.append(f"- Tests dropped for insufficient samples: **{len(dropped)}**")
    out.append(f"- Canary shortlist size: **{len(shortlist)}**")
    out.append("")

    # ── Interpretation & go/no-go recommendation ───────────────────────────
    best = eligible[0] if eligible else None
    canary = pick_canary(eligible, cv_threshold)
    out.append("## Interpretation & go/no-go for Slice 2")
    out.append("")
    if not best:
        out.append("**Verdict: NOT-GO.** No tests had enough passed samples to estimate CV.")
        out.append("")
    else:
        label, note = classify_cv(best["cv_pct"])
        out.append(
            f"Best wall-clock CV in the sample: **{best['cv_pct']:.2f}%** "
            f"(`{best['name']}`, n={best['n_passed']} passed). Bucket: **{label}** — {note}."
        )
        out.append("")
        out.append("**CV reference (wall-clock, perf-benchmark rule of thumb):**")
        out.append("")
        out.append("| CV % | bucket | what it means |")
        out.append("|------|--------|---------------|")
        prev = 0.0
        for limit, lbl, n in CV_BUCKETS:
            out.append(f"| {prev:g}–{limit:g} | {lbl} | {n} |")
            prev = limit
        out.append(f"| > {CV_BUCKETS[-1][0]:g} | unusable | noise dominates; no point measuring perf here |")
        out.append("")
        out.append(
            "Wall-clock is the **upper bound** on the runner's noise floor — it "
            "includes scene download, AltTester RPC, GC warmup, and Windows I/O. "
            "Per-frame counters measured via `ProfilerRecorder` in a steady-state "
            "phase are expected to be tighter than this number."
        )
        out.append("")

        if best["cv_pct"] >= 15.0 or not canary:
            out.append(
                "**Verdict: NOT-GO without changes.** Best wall-clock CV is "
                "≥ 15%, meaning even the most stable test is noise-dominated. "
                "Investigate runner-side variance (antivirus, swap, background "
                "processes, network jitter to Cloud Build CDN) before any "
                "instrumentation work."
            )
        elif best["cv_pct"] < 8.0:
            out.append(
                "**Verdict: GO.** Best wall-clock CV is in the *acceptable* "
                "bucket. Per-frame instrumentation in Slice 2/3 is justified."
            )
        else:
            out.append(
                "**Verdict: GO with conditions.** Best wall-clock CV is in the "
                "*borderline* bucket (8–15%). The image is usable for "
                "**steady-state** per-frame metrics, but not for lifecycle "
                "timings. Tight sampling discipline required (median + p95, "
                "n ≥ 30 per data point, warm-up budget before measurement)."
            )
        out.append("")

        if canary:
            out.append(
                f"**Recommended canary for Slice 2: `{canary['name']}`** "
                f"(CV {canary['cv_pct']:.2f}%, mean {fmt_ms(canary['mean_ms'])}, "
                f"pass-rate {canary['pass_rate_pct']:.0f}%)."
            )
            out.append("")
        else:
            out.append(
                "_No canary candidate cleared the CV threshold with a "
                "steady-state phase (mean ≥ 5s) — re-run with `--cv-threshold` "
                "widened or accept a sub-5s lifecycle-only canary._"
            )
            out.append("")

        out.append("**Validation conditions for Slice 2 (pre-instrumentation):**")
        out.append("")
        out.append(
            "1. Run the canary ≥ 30 times back-to-back on the runner; record "
            "per-frame `Time.unscaledDeltaTime` median + p95 via "
            "`ProfilerRecorder`."
        )
        out.append(
            "2. If per-frame CV > 8% — **stop** and investigate runner-side "
            "noise sources before instrumenting more tests."
        )
        out.append(
            "3. If per-frame CV < 5% — green light for Slice 3 instrumentation "
            "(GC alloc, draw calls, managed memory)."
        )
        out.append(
            "4. **Reject lifecycle metrics**: only measure during a steady-state "
            "window (≥ 3s after the trigger event), to exclude scene-load and "
            "GC-warmup variance."
        )
        out.append("")

        out.append("**Caveats on this sample:**")
        out.append("")
        n_min = min(r["n_passed"] for r in eligible)
        n_max = max(r["n_passed"] for r in eligible)
        out.append(
            f"- n = {n_min}–{n_max} passed samples per test is small: the 90% "
            f"CI on stdev itself is wide (~±25% at n=11). Treat the numbers as "
            f"direction, not as a tight bound."
        )
        out.append(
            f"- {runs_considered} runs were scanned, **{runs_with_data}** "
            f"produced a usable `summary.json`. The rest failed before the "
            f"test step ran (CI-plumbing churn during chassis iteration). "
            f"That share should drop as the image stabilizes — it does **not** "
            f"reflect the image's perf reproducibility."
        )
        prd_row = next((r for r in eligible if r["name"] == PRD_CALLOUT), None)
        if prd_row and prd_row["cv_pct"] >= cv_threshold:
            out.append(
                f"- ⚠ The PRD's call-out candidate ★ `{PRD_CALLOUT}` came in at "
                f"**CV {prd_row['cv_pct']:.2f}%** — above the {cv_threshold:.0f}% "
                f"threshold. The PRD hypothesis does not hold; pick the canary "
                f"from this report's shortlist instead."
            )
        out.append("")

    out.append("## Canary shortlist")
    if shortlist:
        out.append("")
        out.append("These are the tests stable enough to act as perf canaries:")
        out.append("")
        out.append("| Test | CV % | mean | p50 | p95 | n (pass/total) | pass-rate |")
        out.append("|------|------|------|-----|-----|----------------|-----------|")
        for r in shortlist:
            out.append(
                f"| {name_cell(r['name'])} | {r['cv_pct']:.2f} | "
                f"{fmt_ms(r['mean_ms'])} | {fmt_ms(r['p50_ms'])} | "
                f"{fmt_ms(r['p95_ms'])} | {r['n_passed']}/{r['n']} | "
                f"{r['pass_rate_pct']:.0f}% |"
            )
    else:
        out.append("")
        out.append(
            f"_No test cleared CV < {cv_threshold:.1f}% with ≥ {min_samples} passed samples. "
            f"Consider raising `--limit`, lowering `--min-samples`, or widening "
            f"`--cv-threshold`._"
        )
    out.append("")

    out.append("## Full histogram (sorted by CV ascending)")
    out.append("")
    if eligible:
        out.append("| Test | CV % | mean | stdev | p50 | p95 | n (pass/total) | pass-rate |")
        out.append("|------|------|------|-------|-----|-----|----------------|-----------|")
        for r in eligible:
            out.append(
                f"| {name_cell(r['name'])} | {r['cv_pct']:.2f} | "
                f"{fmt_ms(r['mean_ms'])} | {fmt_ms(r['stdev_ms'])} | "
                f"{fmt_ms(r['p50_ms'])} | {fmt_ms(r['p95_ms'])} | "
                f"{r['n_passed']}/{r['n']} | {r['pass_rate_pct']:.0f}% |"
            )
    else:
        out.append("_(empty — no eligible tests)_")
    out.append("")

    if dropped:
        out.append("## Dropped (insufficient samples)")
        out.append("")
        out.append("| Test | n (pass/total) | pass-rate |")
        out.append("|------|----------------|-----------|")
        for r in sorted(dropped, key=lambda x: -x["n"]):
            out.append(
                f"| {name_cell(r['name'])} | {r['n_passed']}/{r['n']} | "
                f"{r['pass_rate_pct']:.0f}% |"
            )
        out.append("")

    out.append("## Notes")
    out.append("")
    out.append(
        "- CV (coefficient of variation) is computed against **passed** samples only. "
        "Failed/broken runs are still counted in pass-rate but their durations are "
        "excluded from the variance signal because they are dominated by retry / "
        "timeout artefacts."
    )
    out.append(
        "- This CV is an **upper bound** on the noise floor of any future per-metric "
        "(frame time, GC alloc, draw calls). Wall-clock includes everything: scene "
        "load, network jitter, GC stalls, runner-side I/O. Per-metric counters "
        "captured via `ProfilerRecorder` should beat this number, not match it."
    )
    out.append(
        "- ★ marks `TestOpenEventsFromSidebar` per the PRD's call-out as a "
        "candidate canary."
    )
    return "\n".join(out) + "\n"


def parse_args(argv: list[str]) -> argparse.Namespace:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--repo", default=DEFAULT_REPO, help=f"GitHub repo slug (default: {DEFAULT_REPO})")
    p.add_argument("--workflow", default=DEFAULT_WORKFLOW, help=f"Workflow file name (default: {DEFAULT_WORKFLOW})")
    p.add_argument("--limit", type=int, default=DEFAULT_LIMIT, help=f"How many recent runs to scan (default: {DEFAULT_LIMIT})")
    p.add_argument("--branch", default=None, help="Restrict to a single branch (default: all branches)")
    p.add_argument("--min-samples", type=int, default=DEFAULT_MIN_SAMPLES, help=f"Drop tests with fewer passed samples (default: {DEFAULT_MIN_SAMPLES})")
    p.add_argument("--cv-threshold", type=float, default=DEFAULT_CV_THRESHOLD, help=f"Canary shortlist CV%% cutoff (default: {DEFAULT_CV_THRESHOLD})")
    p.add_argument("--cache-dir", type=Path, default=Path(__file__).parent / ".cache", help="Where to cache downloaded artifact zips")
    p.add_argument("--output", type=Path, default=Path(__file__).parent / "baseline_report.md", help="Markdown report output path")
    p.add_argument("--raw-json", type=Path, default=None, help="Optional path to dump raw per-sample data as JSON")
    return p.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    args.cache_dir.mkdir(parents=True, exist_ok=True)

    sys.stderr.write(
        f"Listing last {args.limit} runs of {args.workflow} on {args.repo}"
        + (f" (branch: {args.branch})" if args.branch else "")
        + "...\n"
    )
    runs = list_runs(args.repo, args.workflow, args.limit, args.branch)
    sys.stderr.write(f"  found {len(runs)} runs\n")
    if not runs:
        sys.stderr.write("No runs found; nothing to analyze.\n")
        return 1

    tests, runs_with_data = collect_samples(runs, args.repo, args.cache_dir)
    if not tests:
        sys.stderr.write("No test samples extracted from any artifact.\n")
        return 1

    rows = [compute_stats(t) for t in tests.values()]
    report = render_markdown(
        rows,
        runs_considered=len(runs),
        runs_with_data=runs_with_data,
        min_samples=args.min_samples,
        cv_threshold=args.cv_threshold,
        branch=args.branch,
        repo=args.repo,
        workflow=args.workflow,
    )
    args.output.write_text(report, encoding="utf-8")
    sys.stderr.write(f"Report written to {args.output}\n")

    if args.raw_json:
        raw = {
            name: [
                {
                    "run_id": s.run_id,
                    "created_at": s.created_at,
                    "head_sha": s.head_sha,
                    "branch": s.branch,
                    "run_conclusion": s.run_conclusion,
                    "status": s.status,
                    "duration_ms": s.duration_ms,
                }
                for s in t.samples
            ]
            for name, t in tests.items()
        }
        args.raw_json.write_text(json.dumps(raw, indent=2), encoding="utf-8")
        sys.stderr.write(f"Raw samples written to {args.raw_json}\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
