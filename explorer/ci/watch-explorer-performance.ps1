param(
    [Parameter(Mandatory=$true)][int]$ExplorerProcessId,
    [Parameter(Mandatory=$true)][int]$OwnerProcessId,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [int]$IntervalMilliseconds = 2000
)
$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::InvariantCulture
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$writer = New-Object IO.StreamWriter((Join-Path $OutputDirectory 'process.csv'), $false, (New-Object Text.UTF8Encoding($false)))
$writer.AutoFlush = $true
$writer.WriteLine('utc,elapsed_seconds,alive,cpu_seconds,cpu_delta_seconds,working_set_bytes,private_bytes,threads,test')
$clock = [Diagnostics.Stopwatch]::StartNew()
$previousCpu = $null
try {
    while (Get-Process -Id $OwnerProcessId -ErrorAction SilentlyContinue) {
        $process = Get-Process -Id $ExplorerProcessId -ErrorAction SilentlyContinue
        $test = 'startup'
        $marker = Join-Path $OutputDirectory 'active-test.txt'
        if (Test-Path -LiteralPath $marker) {
            try { $test = [IO.File]::ReadAllText($marker) } catch { $test = 'marker unavailable' }
        }
        $test = '"' + ($test -replace '"','""' -replace '[\r\n]',' ') + '"'
        $utc = [DateTime]::UtcNow.ToString('o')
        $elapsed = $clock.Elapsed.TotalSeconds.ToString('F3', $culture)
        if (-not $process) {
            $writer.WriteLine("$utc,$elapsed,false,,,,,,$test")
            break
        }
        try {
            $cpu = $process.TotalProcessorTime.TotalSeconds
            $delta = if ($null -eq $previousCpu) { '' } else { ($cpu - $previousCpu).ToString('F6', $culture) }
            $writer.WriteLine("$utc,$elapsed,true,$($cpu.ToString('F6', $culture)),$delta,$($process.WorkingSet64),$($process.PrivateMemorySize64),$($process.Threads.Count),$test")
            $previousCpu = $cpu
        } catch {
            # Process exit races must leave the samples already collected intact.
            Write-Warning "Process sample unavailable: $_"
        }
        Start-Sleep -Milliseconds $IntervalMilliseconds
    }
} finally { $writer.Dispose() }
