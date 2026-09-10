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
