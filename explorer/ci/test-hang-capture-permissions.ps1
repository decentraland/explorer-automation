param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$started = Get-Date
$toolRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$toolDirectory = Join-Path $toolRoot 'explorer-dump-tools'
[IO.Directory]::CreateDirectory($toolDirectory) | Out-Null
Start-Transcript -Path (Join-Path $OutputDirectory 'preflight.log') | Out-Null
$probe = $null
$monitor = $null
$rsa = $null
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
    $probeDirectory = Join-Path $toolDirectory ([Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($probeDirectory) | Out-Null
    $probeExe = Join-Path $probeDirectory 'Decentraland.exe'
    Add-Type -TypeDefinition 'public class CaptureProbe { public static void Main() { System.Threading.Thread.Sleep(180000); } }' -OutputAssembly $probeExe -OutputType ConsoleApplication
    $probe = Start-Process $probeExe -WindowStyle Hidden -PassThru
    $rawDump = Join-Path $toolDirectory 'probe.dmp'
    $capture = Start-Process $tool -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        '-accepteula', '-r', '1', '-a', '-at', '20', '-mc', '1020', $probe.Id, "`"$rawDump`""
    ) -RedirectStandardOutput (Join-Path $OutputDirectory 'procdump-capture.log') `
        -RedirectStandardError (Join-Path $OutputDirectory 'procdump-capture-error.log')
    try { Write-Output "Capture exit code: $($capture.ExitCode)" } finally { $capture.Dispose() }
    if (-not (Test-Path -LiteralPath $rawDump)) { throw 'No probe dump created' }
    Write-Output "PASS: owned idle process clone captured ($((Get-Item -LiteralPath $rawDump).Length) bytes)"
    $rsa = New-Object Security.Cryptography.RSACryptoServiceProvider 2048
    $publicKey = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($rsa.ToXmlString($false)))
    $monitorOutput = Join-Path $probeDirectory 'encrypted'
    $monitorScript = Join-Path $PSScriptRoot 'watch-explorer-hang.ps1'
    $monitor = Start-Process powershell.exe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-NoProfile', '-File', "`"$monitorScript`"", '-ExplorerProcessId', $probe.Id,
        '-LogPath', "`"$probeDirectory\idle.log`"", '-OutputDirectory', "`"$monitorOutput`"",
        '-PublicKey', $publicKey, '-ProcDumpPath', "`"$tool`"", '-StallSeconds', '1', '-MaxDumps', '1'
    ) -RedirectStandardOutput (Join-Path $OutputDirectory 'monitor.log') `
        -RedirectStandardError (Join-Path $OutputDirectory 'monitor-error.log')
    if (-not $monitor.WaitForExit(120000)) { throw 'Child monitor did not finish within 120 seconds' }
    Get-ChildItem $monitorOutput -Filter '*.log' | Copy-Item -Destination $OutputDirectory
    $encryptedDumps = @(Get-ChildItem $monitorOutput -Filter '*.dmp.enc')
    if ($encryptedDumps.Count -ne 1) { throw 'Child monitor did not create an encrypted dump' }
    foreach ($encryptedDump in $encryptedDumps) { Remove-Item -LiteralPath $encryptedDump.FullName -Force }
    Write-Output 'PASS: actual background monitor captured and encrypted the owned idle process'

} catch {
    $_ | Format-List * -Force
    for ($exception = $_.Exception; $exception; $exception = $exception.InnerException) {
        Write-Output "Exception=$($exception.GetType().FullName) HResult=$($exception.HResult) NativeErrorCode=$($exception.NativeErrorCode) Message=$($exception.Message)"
    }
    throw
} finally {
    if ($monitor) { if (-not $monitor.HasExited) { Stop-Process -Id $monitor.Id -ErrorAction SilentlyContinue }; $monitor.Dispose() }
    if ($rsa) { $rsa.Dispose() }
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
