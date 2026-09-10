param(
    [Parameter(Mandatory=$true)][int]$ExplorerProcessId,
    [Parameter(Mandatory=$true)][int]$OwnerProcessId,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateRange(10,1200)][int]$MaxSeconds = 1200,
    [ValidateRange(64,4096)][int]$MaxTempMiB = 4096
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:RUNNER_TEMP) { throw 'Expected assigned CI runner.' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$instance = 'ExplorerWait-' + [Guid]::NewGuid().ToString('N')
$tempDirectory = Join-Path $env:RUNNER_TEMP $instance
[IO.Directory]::CreateDirectory($tempDirectory) | Out-Null
$wpr = Join-Path $env:WINDIR 'System32/wpr.exe'
. (Join-Path $PSScriptRoot 'NativeWaitRecorder.ps1')
$owned = $false
$gpuMonitor = $null
$rawPath = Join-Path $tempDirectory 'player.etl'
$result = [ordered]@{
    status = 'failed'
    instance = $instance
    started_utc = [DateTime]::UtcNow.ToString('o')
    max_seconds = $MaxSeconds
    temporary_size_stop_mib = $MaxTempMiB
    poll_seconds = 2
    minimum_start_free_gib = 18
    stop_free_gib = 10
    etl_compression = $false
    stop_timeout_seconds = 30
    capture_mode = 'unmerged-owned-collector'
    explorer_pid = $ExplorerProcessId
}
try {
    $target = Get-Process -Id $ExplorerProcessId
    $owner = Get-Process -Id $OwnerProcessId
    if ($target.ProcessName -ne 'Decentraland') { throw 'Expected owned Explorer process.' }
    $targetStart = $target.StartTime
    $ownerStart = $owner.StartTime
    $rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
    try {
        $rsa.FromXmlString([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($env:NATIVE_TRACE_PUBLIC_KEY)))
        if (-not $rsa.PublicOnly -or $rsa.KeySize -lt 2048) { throw 'Expected RSA public key of at least 2048 bits.' }
    } finally { $rsa.Dispose() }
    $result.explorer_started_utc = $targetStart.ToUniversalTime().ToString('o')
    $result.wpr_version = (Get-Item -LiteralPath $wpr).VersionInfo.FileVersion
    $sessions = & logman.exe query -ets 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inventory ETW sessions.' }
    $sessions | Out-File (Join-Path $OutputDirectory 'sessions-before.log')
    if (($sessions -join "`n") -match 'WPR_|NT Kernel Logger|Explorer Native Wait') { throw 'Another recording is already active.' }
    $drive = New-Object IO.DriveInfo([IO.Path]::GetPathRoot($tempDirectory))
    if ($drive.AvailableFreeSpace -lt 18GB) { throw 'Less than 18 GiB available for bounded trace and encryption.' }
    try {
        $contextScript = Join-Path $PSScriptRoot 'Write-GraphicsContext.ps1'
        $contextArgs = @('-NoProfile','-File',('"' + $contextScript + '"'),'-OutputDirectory',('"' + $OutputDirectory + '"'))
        $context = Start-Process powershell.exe -WindowStyle Hidden -PassThru -ArgumentList $contextArgs -RedirectStandardOutput (Join-Path $OutputDirectory 'graphics-context.log') -RedirectStandardError (Join-Path $OutputDirectory 'graphics-context-error.log')
        try {
            if (-not $context.WaitForExit(30000)) { $context.Kill(); [void]$context.WaitForExit(5000); $result.graphics_context_timeout = $true }
        } finally { $context.Dispose() }
    } catch { $result.graphics_context_error = $_.Exception.Message }
    $playerDirectory = Split-Path -Parent $target.Path
    $result.binaries = @(foreach ($name in @('Decentraland.exe','GameAssembly.dll','UnityPlayer.dll')) {
        $file = Join-Path $playerDirectory $name
        if (Test-Path -LiteralPath $file) { @{ name = $name; sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash; bytes = (Get-Item -LiteralPath $file).Length } }
    })
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'instance.txt'), $instance)
    $owned = $true
    $profile = Join-Path $PSScriptRoot 'NativeWait.wprp'
    Invoke-Recorder @('-start', ('"' + $profile + '!NativeWait"'), '-filemode', '-recordtempto', ('"' + $tempDirectory + '"'), '-instancename', $instance) 'start'
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $result.recording_started_utc = [DateTime]::UtcNow.ToString('o')
    try {
        $smi = (Get-Command nvidia-smi.exe -ErrorAction Stop).Source
        $gpuArgs = @('--query-gpu=timestamp,index,pstate,utilization.gpu,utilization.memory,memory.used,temperature.gpu,power.draw,clocks.current.graphics,clocks.current.sm,clocks.current.memory,clocks_throttle_reasons.active','--format=csv','-l','2')
        $gpuMonitor = Start-Process $smi -WindowStyle Hidden -PassThru -ArgumentList $gpuArgs -RedirectStandardOutput (Join-Path $OutputDirectory 'gpu-samples.csv') -RedirectStandardError (Join-Path $OutputDirectory 'gpu-samples-error.log')
        $result.gpu_sample_timezone = [TimeZoneInfo]::Local.Id
    } catch { $result.gpu_monitor_error = $_.Exception.Message }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'ready.txt'), $result.recording_started_utc)
    while ($true) {
        if (Test-Path -LiteralPath (Join-Path $OutputDirectory 'stop.txt')) { $result.stop_reason = 'test-step-finished'; break }
        if ($watch.Elapsed.TotalSeconds -ge $MaxSeconds) { $result.stop_reason = 'duration-limit'; break }
        $currentTarget = Get-Process -Id $ExplorerProcessId -ErrorAction SilentlyContinue
        $currentOwner = Get-Process -Id $OwnerProcessId -ErrorAction SilentlyContinue
        try {
            if (-not $currentOwner -or $currentOwner.StartTime -ne $ownerStart) { $result.stop_reason = 'owner-exited'; break }
            if (-not $currentTarget -or $currentTarget.StartTime -ne $targetStart) { $result.stop_reason = 'player-exited'; break }
        } finally {
            if ($currentOwner) { $currentOwner.Dispose() }
            if ($currentTarget) { $currentTarget.Dispose() }
        }
        $size = (Get-RecorderTemporaryFiles $tempDirectory | Measure-Object Length -Sum).Sum
        if ($size -ge $MaxTempMiB * 1MB -or $drive.AvailableFreeSpace -lt 10GB) { $result.stop_reason = 'disk-limit'; break }
        Start-Sleep -Seconds 2
    }
    Invoke-Recorder @('-status', 'collectors', '-details', '-instancename', $instance) 'status'
    $result.stop_started_utc = [DateTime]::UtcNow.ToString('o')
    $result.temporary_files_before_stop = @(Get-RecorderTemporaryFiles $tempDirectory)
    $result.temporary_bytes_before_stop = (Get-RecorderTemporaryFiles $tempDirectory | Measure-Object Length -Sum).Sum
    # Retain the kernel stream without a merge that can block after long stalls.
    Save-RecorderUnmergedTrace $instance $tempDirectory $rawPath
    $result.recording_stopped_utc = [DateTime]::UtcNow.ToString('o')
    try {
        Invoke-Recorder @('-cancel', '-instancename', $instance) 'cancel'
        $owned = $false
    } catch { $result.wpr_cleanup_error = $_.Exception.Message }
    $result.raw_bytes = (Get-Item -LiteralPath $rawPath).Length
    if ($result.raw_bytes -eq 0) { throw 'Empty trace.' }
    $result.raw_sha256 = (Get-FileHash -LiteralPath $rawPath -Algorithm SHA256).Hash
    & (Join-Path $PSScriptRoot 'Protect-NativeTrace.ps1') -InputPath $rawPath -OutputPath (Join-Path $OutputDirectory 'player.etl.enc') -PublicKey $env:NATIVE_TRACE_PUBLIC_KEY
    $result.status = 'encrypted-requires-event-validation'
} catch {
    $result.error = $_.Exception.Message
    if (Test-Path -LiteralPath $rawPath) { $result.incomplete_output_bytes = (Get-Item -LiteralPath $rawPath).Length }
    Write-Error $_ -ErrorAction Continue
} finally {
    if ($gpuMonitor) {
        try { if (-not $gpuMonitor.HasExited) { $gpuMonitor.Kill(); [void]$gpuMonitor.WaitForExit(5000) } } catch { $result.gpu_monitor_cleanup_error = $_.Exception.Message } finally { $gpuMonitor.Dispose() }
    }
    if ($owned) {
        try { Invoke-Recorder @('-cancel', '-instancename', $instance) 'cancel' } catch { Write-Warning $_ }
    }
    if (Test-Path -LiteralPath $rawPath) { Remove-Item -LiteralPath $rawPath -Force }
    $result.finished_utc = [DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'result.json'), ($result | ConvertTo-Json -Depth 6))
}
