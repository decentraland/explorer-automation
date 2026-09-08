param(
    [Parameter(Mandatory=$true)][int]$ExplorerProcessId,
    [Parameter(Mandatory=$true)][string]$LogPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$PublicKey,
    [int]$StallSeconds = 60,
    [int]$MaxDumps = 2
)
$ErrorActionPreference = 'Stop'
$target = Get-Process -Id $ExplorerProcessId
if ($target.ProcessName -ne 'Decentraland') { throw 'Expected the owned Explorer process' }
$started = $target.StartTime
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.FromXmlString([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($PublicKey)))
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ExplorerHangDump {
    [DllImport("dbghelp.dll", SetLastError=true)]
    public static extern bool MiniDumpWriteDump(IntPtr process, uint processId,
        IntPtr file, uint flags, IntPtr exception, IntPtr streams, IntPtr callback);
}
"@
function Save-EncryptedDump($process, [string]$stem) {
    $rawPath = Join-Path ([IO.Path]::GetTempPath()) ($stem + '-' + [Guid]::NewGuid().ToString('N') + '.dmp')
    $encryptedPath = Join-Path $OutputDirectory ($stem + '.dmp.enc')
    try {
        $file = [IO.File]::Create($rawPath)
        try {
            # Thread stacks and module metadata, without the process heap.
            $ok = [ExplorerHangDump]::MiniDumpWriteDump($process.Handle, $process.Id,
                $file.SafeFileHandle.DangerousGetHandle(), 0x1020,
                [IntPtr]::Zero, [IntPtr]::Zero, [IntPtr]::Zero)
            if (-not $ok) { throw "MiniDumpWriteDump failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())" }
        } finally { $file.Dispose() }
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
        Save-EncryptedDump $target ("explorer-hang-{0}-{1}" -f $ExplorerProcessId, $count)
        $lastDump = $now
    }
    Start-Sleep -Seconds 2
}
$rsa.Dispose()
