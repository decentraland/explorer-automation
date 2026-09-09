param(
    [Parameter(Mandatory=$true)][int]$ExplorerProcessId,
    [Parameter(Mandatory=$true)][string]$LogPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$PublicKey,
    [Parameter(Mandatory=$true)][string]$ProcDumpPath,
    [switch]$CaptureOnStart,
    [int]$StallSeconds = 60,
    [int]$MaxDumps = 2
)
$ErrorActionPreference = 'Stop'
$target = Get-Process -Id $ExplorerProcessId
if ($target.ProcessName -ne 'Decentraland') { throw 'Expected the owned Explorer process' }
$started = $target.StartTime
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$toolFile = Get-Item -LiteralPath $ProcDumpPath
if ($toolFile.PSIsContainer) { throw 'Expected a ProcDump executable file' }
Write-Output "Capture tool: $($toolFile.FullName), $($toolFile.Length) bytes; user=$([Security.Principal.WindowsIdentity]::GetCurrent().Name)"
$rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.FromXmlString([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($PublicKey)))
function Save-EncryptedDump($process, [string]$stem) {
    $rawPath = Join-Path ([IO.Path]::GetTempPath()) ($stem + '-' + [Guid]::NewGuid().ToString('N') + '.dmp')
    $encryptedPath = Join-Path $OutputDirectory ($stem + '.dmp.enc')
    try {
        Write-Output "Starting dump capture $stem at $([DateTime]::UtcNow.ToString('o'))"
        # Capture a clone so writing the dump does not hold the live player's threads.
        $stdoutPath = Join-Path $OutputDirectory ($stem + '-procdump.log')
        $stderrPath = Join-Path $OutputDirectory ($stem + '-procdump-error.log')
        $capture = Start-Process -FilePath $ProcDumpPath -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
            '-accepteula', '-r', '1', '-a', '-at', '20', '-mc', '1020', $process.Id, "`"$rawPath`""
        ) -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        try { $exitCode = $capture.ExitCode } finally { $capture.Dispose() }
        $dumpExists = Test-Path -LiteralPath $rawPath
        Write-Output "ProcDump exited at $([DateTime]::UtcNow.ToString('o')): exit=$exitCode dumpExists=$dumpExists"
        if ($exitCode -notin @(0, 1) -or -not $dumpExists) {
            throw "Clone dump capture failed with exit code $exitCode; see $stdoutPath and $stderrPath"
        }
        Write-Output "Finished dump capture $stem at $([DateTime]::UtcNow.ToString('o'))"
        $aes = [Security.Cryptography.Aes]::Create()
        $aes.GenerateKey()
        $aes.GenerateIV()
        $macKey = New-Object byte[] 32
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        $rng.GetBytes($macKey)
        $rng.Dispose()
        $header = @{
            format = 'explorer-hang-v1'
            wrappedKeys = [Convert]::ToBase64String($rsa.Encrypt([byte[]]($aes.Key + $macKey), $true))
            iv = [Convert]::ToBase64String($aes.IV)
        } | ConvertTo-Json -Compress
        $headerBytes = [Text.Encoding]::UTF8.GetBytes($header + "`n")
        $output = [IO.File]::Create($encryptedPath)
        try {
            $output.Write($headerBytes, 0, $headerBytes.Length)
            $crypto = New-Object Security.Cryptography.CryptoStream($output, $aes.CreateEncryptor(), [Security.Cryptography.CryptoStreamMode]::Write, $true)
            $inputFile = [IO.File]::OpenRead($rawPath)
            try { $inputFile.CopyTo($crypto); $crypto.FlushFinalBlock() }
            finally { $inputFile.Dispose(); $crypto.Dispose() }
        } finally { $output.Dispose(); $aes.Dispose() }
        $hmac = New-Object Security.Cryptography.HMACSHA256
        $hmac.Key = $macKey
        $encrypted = [IO.File]::OpenRead($encryptedPath)
        try { $tag = $hmac.ComputeHash($encrypted) }
        finally { $encrypted.Dispose(); $hmac.Dispose() }
        $output = [IO.File]::Open($encryptedPath, [IO.FileMode]::Append)
        try { $output.Write($tag, 0, $tag.Length) } finally { $output.Dispose() }
        Write-Output "Captured encrypted stack dump: $stem"
    } finally {
        if (Test-Path -LiteralPath $rawPath) { Remove-Item -LiteralPath $rawPath -Force }
    }
}
function Write-CaptureFailure($failure, [string]$label) {
    Write-Warning "$label failed at $([DateTime]::UtcNow.ToString('o')): $($failure.Exception.Message)"
    $failure | Format-List * -Force | Out-String | Write-Output
    for ($exception = $failure.Exception; $exception; $exception = $exception.InnerException) {
        Write-Output "Exception=$($exception.GetType().FullName) HResult=$($exception.HResult) NativeErrorCode=$($exception.NativeErrorCode) Message=$($exception.Message)"
    }
    $since = (Get-Date).AddMinutes(-3)
    foreach ($channel in @('Microsoft-Windows-Windows Defender/Operational', 'Microsoft-Windows-CodeIntegrity/Operational', 'Microsoft-Windows-AppLocker/EXE and DLL')) {
        try {
            Get-WinEvent -FilterHashtable @{LogName=$channel; StartTime=$since} -ErrorAction Stop |
                Where-Object { $_.Message -match 'procdump|explorer-dump-tools' } |
                Select-Object TimeCreated, Id, ProviderName, Message | Format-List | Out-String | Write-Output
        } catch { Write-Output "$channel : $($_.Exception.Message)" }
    }
}
if ($CaptureOnStart) {
    try { Save-EncryptedDump $target ("explorer-startup-{0}" -f $ExplorerProcessId) }
    catch { Write-CaptureFailure $_ 'Startup capture' }
}
$csv = Join-Path $OutputDirectory 'explorer-progress.csv'
[IO.File]::WriteAllText($csv, "utc,cpu_seconds,working_set,private_bytes,threads,log_bytes,idle_seconds`n")
$lastLength = -1L
$lastProgress = [DateTime]::UtcNow
$lastDump = [DateTime]::MinValue
$deadline = [DateTime]::UtcNow.AddMinutes(45)
$count = 0
while ([DateTime]::UtcNow -lt $deadline -and $count -lt $MaxDumps) {
    $target = Get-Process -Id $ExplorerProcessId -ErrorAction SilentlyContinue
    if (-not $target -or $target.StartTime -ne $started) { break }
    $now = [DateTime]::UtcNow
    $log = Get-Item -LiteralPath $LogPath -ErrorAction SilentlyContinue
    $length = if ($log) { $log.Length } else { 0L }
    if ($length -ne $lastLength) { $lastProgress = $now; $lastLength = $length }
    $idle = ($now - $lastProgress).TotalSeconds
    $cpu = $target.TotalProcessorTime.TotalSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
    [IO.File]::AppendAllText($csv, "$($now.ToString('o')),$cpu,$($target.WorkingSet64),$($target.PrivateMemorySize64),$($target.Threads.Count),$length,$([int]$idle)`n")
    if ($idle -ge $StallSeconds -and ($now - $lastDump).TotalSeconds -ge $StallSeconds) {
        $count++
        try { Save-EncryptedDump $target ("explorer-hang-{0}-{1}" -f $ExplorerProcessId, $count) }
        catch { Write-CaptureFailure $_ "Dump attempt $count" }
        $lastDump = [DateTime]::UtcNow
    }
    Start-Sleep -Seconds 2
}
$rsa.Dispose()
