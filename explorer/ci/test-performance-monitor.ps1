$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$scriptPath = Join-Path $PSScriptRoot 'watch-explorer-performance.ps1'
[Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count) { throw "Monitor parse errors: $parseErrors" }
$directory = Join-Path ([IO.Path]::GetTempPath()) ('performance-monitor-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
[IO.File]::WriteAllText((Join-Path $directory 'active-test.txt'), 'test,"quoted"')
$child = Start-Process powershell.exe -WindowStyle Hidden -ArgumentList '-NoProfile', '-Command', 'Start-Sleep -Seconds 3' -PassThru
try {
    & $scriptPath -ExplorerProcessId $child.Id -OwnerProcessId $PID -OutputDirectory $directory -IntervalMilliseconds 100
    $rows = Import-Csv (Join-Path $directory 'process.csv')
    if ($rows.Count -lt 3) { throw 'Missing live process samples' }
    if ($rows[-1].alive -ne 'false') { throw 'Missing process exit sample' }
    if ($rows[0].test -ne 'test,"quoted"') { throw 'Test marker CSV escaping failed' }
    if ([double]$rows[0].working_set_bytes -le 0) { throw 'Working set was not captured' }
    if ($rows[1].cpu_delta_seconds -eq '') { throw 'CPU delta was not captured' }
    Write-Host "Monitor test passed: $($rows.Count) samples, CPU/memory, test marker and process exit."
} finally {
    if (-not $child.HasExited) { Stop-Process -Id $child.Id -ErrorAction SilentlyContinue }
}
