param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:RUNNER_TEMP) {
    throw 'This preflight only records on an assigned GitHub Actions runner.'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$instance = 'ExplorerWait-' + [Guid]::NewGuid().ToString('N')
$tempDirectory = Join-Path $env:RUNNER_TEMP $instance
[IO.Directory]::CreateDirectory($tempDirectory) | Out-Null
$wpr = Join-Path $env:WINDIR 'System32/wpr.exe'
$profile = Join-Path $PSScriptRoot 'NativeWait.wprp'
$probe = $null
$owned = $false
$result = [ordered]@{
    started_utc = [DateTime]::UtcNow.ToString('o')
    instance = $instance
    status = 'failed'
    duration_limit_seconds = 20
    temporary_size_stop_mib = 256
    poll_interval_seconds = 1
    buffer_mib = 64
}
function Invoke-Recorder([string[]]$Arguments, [string]$LogName, [int]$TimeoutSeconds = 30) {
    $command = New-Object Diagnostics.Process
    $command.StartInfo.FileName = $wpr
    $command.StartInfo.Arguments = $Arguments -join ' '
    $command.StartInfo.UseShellExecute = $false
    $command.StartInfo.CreateNoWindow = $true
    $command.StartInfo.RedirectStandardOutput = $true
    $command.StartInfo.RedirectStandardError = $true
    try {
        if (-not $command.Start()) { throw "Could not start WPR $LogName" }
        $stdout = $command.StandardOutput.ReadToEndAsync()
        $stderr = $command.StandardError.ReadToEndAsync()
        if (-not $command.WaitForExit($TimeoutSeconds * 1000)) {
            $command.Kill()
            throw "WPR $LogName exceeded $TimeoutSeconds seconds"
        }
        $command.WaitForExit()
        [IO.File]::WriteAllText((Join-Path $OutputDirectory ($LogName + '.log')), $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $OutputDirectory ($LogName + '-error.log')), $stderr.GetAwaiter().GetResult())
        if ($command.ExitCode -ne 0) { throw "WPR $LogName exit code $($command.ExitCode)" }
    } finally { $command.Dispose() }
}
try {
    if (-not (Test-Path -LiteralPath $wpr)) { throw 'WPR is not installed.' }
    $result.wpr_version = (Get-Item -LiteralPath $wpr).VersionInfo.FileVersion
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    $result.elevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $result.elevated) { throw 'Runner token is not elevated; no privilege changes attempted.' }
    if (Get-Process -Name 'Decentraland*','testhost*','AltTester*' -ErrorAction SilentlyContinue) {
        throw 'A player or test process is already present; refusing idle preflight.'
    }
    $sessions = & logman.exe query -ets 2>&1
    $sessionExit = $LASTEXITCODE
    $sessions | Out-File (Join-Path $OutputDirectory 'sessions-before.log')
    if ($sessionExit -ne 0) { throw 'Cannot inventory existing ETW sessions.' }
    if (($sessions -join "`n") -match 'WPR_|NT Kernel Logger|Explorer Native Wait') {
        throw 'Another kernel/WPR recording is present; it will not be changed.'
    }
    Invoke-Recorder @('-profiledetails', ('"' + $profile + '!NativeWait"'), '-filemode') 'profile'
    $probeExe = Join-Path $tempDirectory 'NativeWaitProbe.exe'
    Add-Type -TypeDefinition @"
using System;
using System.Diagnostics;
using System.Threading;
public class NativeWaitProbe {
    public static void Main() {
        var total = Stopwatch.StartNew();
        while (total.ElapsedMilliseconds < 18000) {
            var busy = Stopwatch.StartNew();
            while (busy.ElapsedMilliseconds < 100) { Thread.SpinWait(1000); }
            Thread.Sleep(400);
        }
    }
}
"@ -OutputAssembly $probeExe -OutputType ConsoleApplication
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'instance.txt'), $instance)
    # Mark only our unique instance for cleanup, including an interrupted start command.
    $owned = $true
    Invoke-Recorder @('-start', ('"' + $profile + '!NativeWait"'), '-filemode', '-recordtempto', ('"' + $tempDirectory + '"'), '-instancename', $instance) 'start'
    $result.recording_started_utc = [DateTime]::UtcNow.ToString('o')
    $probe = Start-Process $probeExe -WindowStyle Hidden -PassThru
    $result.probe_pid = $probe.Id
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -Seconds 1
        $size = (Get-ChildItem -LiteralPath $tempDirectory -Recurse -File | Measure-Object Length -Sum).Sum
        if ($size -gt 256MB) { throw 'Temporary trace exceeded the 256 MiB stop threshold.' }
    } while ($watch.Elapsed.TotalSeconds -lt 20 -and -not $probe.HasExited)
    Invoke-Recorder @('-status', 'collectors', '-details', '-instancename', $instance) 'status'
    $trace = Join-Path $OutputDirectory 'idle-probe.etl'
    Invoke-Recorder @('-stop', ('"' + $trace + '"'), '-skipPdbGen', '-instancename', $instance) 'stop' 60
    $owned = $false
    if (-not (Test-Path -LiteralPath $trace) -or (Get-Item -LiteralPath $trace).Length -eq 0) {
        throw 'WPR returned no nonempty trace.'
    }
    $result.trace_bytes = (Get-Item -LiteralPath $trace).Length
    $result.trace_sha256 = (Get-FileHash -LiteralPath $trace -Algorithm SHA256).Hash
    $result.status = 'recorded_requires_event_validation'
} catch {
    $result.error = $_.Exception.Message
    throw
} finally {
    if ($probe) {
        try { if (-not $probe.HasExited) { $probe.Kill() } } finally { $probe.Dispose() }
    }
    if ($owned) {
        try { Invoke-Recorder @('-cancel', '-instancename', $instance) 'cancel' } catch { Write-Warning $_ }
    }
    $result.finished_utc = [DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'result.json'), ($result | ConvertTo-Json -Depth 5))
}
