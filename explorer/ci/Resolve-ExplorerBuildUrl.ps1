<#
.SYNOPSIS
Finds the Windows Explorer artifact for the newest reachable unity-explorer dev build.

.DESCRIPTION
Mirrors the macOS leg's resolver in run-inworld-suite.yml, down to the candidate list, so both
legs of a run exercise the same client. A pinned URL goes stale silently: the leg keeps passing
until a test starts depending on something the pinned build predates, and then fails in a way that
looks like a platform difference.

dev is the integration branch, and branch builds keep the AltTester scripting define that release
artifacts strip. Unity Cloud Build publishes push-event builds under a pu- prefix.

Prints the URL, and writes BUILD_URL and BUILD_TAG to GITHUB_ENV when running under Actions.
#>
[CmdletBinding()]
param(
    # Caller-supplied URL. Skips resolution entirely.
    [string]$BuildUrl = "",

    [string]$Artifact = "Decentraland_windows64.zip",
    [int]$Candidates = 5
)

$ErrorActionPreference = "Stop"

function Publish-BuildUrl([string]$url) {
    $tag = Split-Path (Split-Path $url -Parent) -Leaf
    Write-Host "Build: $url"
    if ($env:GITHUB_ENV) {
        "BUILD_URL=$url" | Out-File $env:GITHUB_ENV -Append -Encoding utf8
        "BUILD_TAG=$tag" | Out-File $env:GITHUB_ENV -Append -Encoding utf8
    }
    return $tag
}

if ($BuildUrl) {
    Write-Host "Using the caller-supplied build URL."
    Publish-BuildUrl $BuildUrl | Out-Null
    return
}

$base = "https://explorer-artifacts.decentraland.org/@dcl/unity-explorer/branch/dev"
# Parsed here rather than with --jq: PowerShell 5.1 mangles the double quotes an interpolating
# jq filter needs, and gh then sees the filter as a second positional argument.
# The runs API has answered identical queries with different pages inside a single run:
# 33616155055 resolved its three legs to three builds, the oldest from May. Union a few
# attempts and keep the highest run_number, so one thin page cannot decide the client.
$query = "repos/decentraland/unity-explorer/actions/workflows/build-unitycloud.yml/runs?branch=dev&event=push&status=success&per_page=10"
$seen = @{}
foreach ($attempt in 1..3) {
    $json = gh api $query
    if ($LASTEXITCODE -ne 0) { continue }
    foreach ($r in ($json | ConvertFrom-Json).workflow_runs) {
        if (-not $seen.ContainsKey($r.run_number)) { $seen[$r.run_number] = $r }
    }
}
if ($seen.Count -eq 0) { throw "Could not list unity-explorer dev builds" }
$runs = $seen.Values | Sort-Object -Property run_number -Descending

# A stale page installs a months-old client, and the suite then fails in ways that read as
# client regressions. Refuse it by age instead.
# ConvertFrom-Json hands back a local-kind DateTime, so normalize before subtracting.
$newestUtc = ([datetime]$runs[0].created_at).ToUniversalTime()
$ageDays = ([datetime]::UtcNow - $newestUtc).TotalDays
if ($ageDays -gt 7) {
    throw ("Newest dev build offered by the runs API is {0:N0} days old (pu-{1}). Refusing to test a stale client." -f $ageDays, $runs[0].run_number)
}

$tried = 0
foreach ($run in $runs) {
    if ($tried -ge $Candidates) { break }
    $tried++

    $tag = "pu-$($run.run_number)-$($run.head_sha.Substring(0, 7))"
    $url = "$base/$tag/$Artifact"

    # A build can be published for one platform before the other, so a candidate that answers for
    # macOS may still 404 here.
    $status = 0
    try {
        $status = (Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -TimeoutSec 30).StatusCode
    } catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
    }

    if ($status -eq 200) {
        Publish-BuildUrl $url | Out-Null
        return
    }

    Write-Warning "$tag returned HTTP $status for $Artifact - trying next."
}

throw "No reachable unity-explorer dev build with $Artifact in the last $Candidates successful dev push builds."
