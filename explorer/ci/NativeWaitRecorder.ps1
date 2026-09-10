function Invoke-Recorder([string[]]$Arguments, [string]$LogName, [int]$TimeoutSeconds = 30, [string]$Executable = $wpr) {
    $command = New-Object Diagnostics.Process
    $command.StartInfo.FileName = $Executable
    $command.StartInfo.Arguments = $Arguments -join ' '
    $command.StartInfo.UseShellExecute = $false
    $command.StartInfo.CreateNoWindow = $true
    $command.StartInfo.RedirectStandardOutput = $true
    $command.StartInfo.RedirectStandardError = $true
    $stdout = $null
    $stderr = $null
    $timedOut = $false
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        if (-not $command.Start()) { throw "Could not start Recorder $LogName" }
        $stdout = $command.StandardOutput.ReadToEndAsync()
        $stderr = $command.StandardError.ReadToEndAsync()
        if (-not $command.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            $command.Kill()
            [void]$command.WaitForExit(5000)
            throw "Recorder $LogName exceeded $TimeoutSeconds seconds"
        }
        $command.WaitForExit()
        if ($command.ExitCode -ne 0) { throw "Recorder $LogName exit code $($command.ExitCode)" }
    } finally {
        try {
            if ($stdout -and $stderr) {
                [void][Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 5000)
            }
            # Preserve completed output even when the command failed or was terminated.
            if ($stdout -and $stdout.IsCompleted) {
                [IO.File]::WriteAllText((Join-Path $OutputDirectory ($LogName + '.log')), $stdout.GetAwaiter().GetResult())
            }
            if ($stderr -and $stderr.IsCompleted) {
                [IO.File]::WriteAllText((Join-Path $OutputDirectory ($LogName + '-error.log')), $stderr.GetAwaiter().GetResult())
            }
            $details = [ordered]@{
                elapsed_seconds = $watch.Elapsed.TotalSeconds
                timeout_seconds = $TimeoutSeconds
                timed_out = $timedOut
                stdout_complete = [bool]($stdout -and $stdout.IsCompleted)
                stderr_complete = [bool]($stderr -and $stderr.IsCompleted)
            }
            [IO.File]::WriteAllText((Join-Path $OutputDirectory ($LogName + '-command.json')), ($details | ConvertTo-Json))
        } finally { $command.Dispose() }
    }
}

function Get-RecorderTemporaryFiles([string]$Directory) {
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File -Force) {
        $handle = $null
        try {
            # Directory metadata can lag behind a file that ETW keeps open.
            $share = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
            $handle = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, $share)
            [pscustomobject]@{ Name = $file.Name; Length = $handle.Length; DirectoryLength = $file.Length; Attributes = [string]$file.Attributes }
        } finally { if ($handle) { $handle.Dispose() } }
    }
}
function Save-RecorderUnmergedTrace([string]$Instance, [string]$Directory, [string]$Destination) {
    if ($Instance -notmatch '^ExplorerWait-[0-9a-f]{32}$') { throw 'Invalid owned recorder instance.' }
    $collector = 'WPR_initiated_' + $Instance + '_WPR System Collector'
    $logman = Join-Path $env:WINDIR 'System32/logman.exe'
    Invoke-Recorder @('stop', ('"' + $collector + '"'), '-ets') 'collector-stop' 30 $logman
    $source = Join-Path $Directory ($collector + '.etl')
    if (-not (Test-Path -LiteralPath $source)) { throw 'Owned collector did not leave an ETL file.' }
    Copy-Item -LiteralPath $source -Destination $Destination -ErrorAction Stop
    if ((Get-Item -LiteralPath $Destination).Length -eq 0) { throw 'Owned collector left an empty trace.' }
}