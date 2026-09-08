param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$started = Get-Date
$toolDirectory = Join-Path ([IO.Path]::GetTempPath()) ('capture-preflight-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($toolDirectory) | Out-Null
Start-Transcript -Path (Join-Path $OutputDirectory 'preflight.log') | Out-Null
$probe = $null
try {
    $zip = Join-Path $toolDirectory 'procdump.zip'
    Invoke-WebRequest 'https://download.sysinternals.com/files/Procdump.zip' -OutFile $zip -UseBasicParsing
    Expand-Archive $zip $toolDirectory -Force
    $tool = Join-Path $toolDirectory 'procdump64.exe'
    $signature = Get-AuthenticodeSignature $tool
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Microsoft Corporation') {
        throw 'ProcDump did not have a valid Microsoft signature'
    }
    Get-Item -LiteralPath $tool | Select-Object FullName, Length, LastWriteTime | Format-List
    Get-FileHash -LiteralPath $tool -Algorithm SHA256 | Format-List
    Get-Acl -LiteralPath $tool | Select-Object Owner, AccessToString | Format-List
    Write-Output "User: $([Security.Principal.WindowsIdentity]::GetCurrent().Name)"
    Write-Output "Tool launch at $([DateTime]::UtcNow.ToString('o'))"
    $help = Start-Process $tool -WindowStyle Hidden -Wait -PassThru -ArgumentList '-accepteula', '-?' `
        -RedirectStandardOutput (Join-Path $OutputDirectory 'procdump-help.log') `
        -RedirectStandardError (Join-Path $OutputDirectory 'procdump-help-error.log')
    try { Write-Output "Help exit code: $($help.ExitCode)" } finally { $help.Dispose() }
    # The owned idle process contains no Explorer session; raw dumps stay outside artifacts.
    $probe = Start-Process powershell.exe -WindowStyle Hidden -PassThru -ArgumentList '-NoProfile', '-Command', 'Start-Sleep -Seconds 90'
    $rawDump = Join-Path $toolDirectory 'probe.dmp'
    $capture = Start-Process $tool -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        '-accepteula', '-r', '1', '-a', '-at', '20', '-mc', '1020', $probe.Id, "`"$rawDump`""
    ) -RedirectStandardOutput (Join-Path $OutputDirectory 'procdump-capture.log') `
        -RedirectStandardError (Join-Path $OutputDirectory 'procdump-capture-error.log')
    try { Write-Output "Capture exit code: $($capture.ExitCode)" } finally { $capture.Dispose() }
    if (-not (Test-Path -LiteralPath $rawDump)) { throw 'No probe dump created' }
    Write-Output "PASS: owned idle process clone captured ($((Get-Item -LiteralPath $rawDump).Length) bytes)"
} catch {
    $_ | Format-List * -Force
    for ($exception = $_.Exception; $exception; $exception = $exception.InnerException) {
        Write-Output "Exception=$($exception.GetType().FullName) HResult=$($exception.HResult) NativeErrorCode=$($exception.NativeErrorCode) Message=$($exception.Message)"
    }
    throw
} finally {
    if ($probe) { Stop-Process -Id $probe.Id -ErrorAction SilentlyContinue; $probe.Dispose() }
    foreach ($channel in @('Microsoft-Windows-Windows Defender/Operational', 'Microsoft-Windows-CodeIntegrity/Operational', 'Microsoft-Windows-AppLocker/EXE and DLL')) {
        $eventPath = Join-Path $OutputDirectory (($channel -replace '[/ ]', '-') + '.log')
        try {
            $events = @(Get-WinEvent -FilterHashtable @{LogName=$channel; StartTime=$started.AddMinutes(-1)} -ErrorAction Stop |
                Where-Object { $_.Message -match 'procdump|capture-preflight' })
            $events | Select-Object TimeCreated, Id, ProviderName, Message | Format-List | Out-File $eventPath
            Write-Output "$channel : $($events.Count) related events"
        } catch { [IO.File]::WriteAllText($eventPath, $_.Exception.Message) }
    }
    # Remove only files in the unique temporary directory this script created.
    if ($rawDump -and (Test-Path -LiteralPath $rawDump)) { Remove-Item -LiteralPath $rawDump -Force }
    Stop-Transcript | Out-Null
}
