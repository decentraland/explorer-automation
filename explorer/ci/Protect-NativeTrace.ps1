param(
    [Parameter(Mandatory=$true)][string]$InputPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [Parameter(Mandatory=$true)][string]$PublicKey
)
$ErrorActionPreference = 'Stop'
$rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
$aes = [Security.Cryptography.Aes]::Create()
$partial = $OutputPath + '.partial'
$partialOwned = $false
try {
    $rsa.FromXmlString([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($PublicKey)))
    if (-not $rsa.PublicOnly -or $rsa.KeySize -lt 2048) { throw 'Expected an RSA public key of at least 2048 bits.' }
    $aes.GenerateKey()
    $aes.GenerateIV()
    $macKey = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($macKey) } finally { $rng.Dispose() }
    $header = @{
        format = 'explorer-native-trace-v1'
        wrappedKeys = [Convert]::ToBase64String($rsa.Encrypt([byte[]]($aes.Key + $macKey), $true))
        iv = [Convert]::ToBase64String($aes.IV)
    } | ConvertTo-Json -Compress
    $headerBytes = [Text.Encoding]::UTF8.GetBytes($header + "`n")
    $output = [IO.File]::Open($partial, [IO.FileMode]::CreateNew)
    $partialOwned = $true
    try {
        $output.Write($headerBytes, 0, $headerBytes.Length)
        $crypto = New-Object Security.Cryptography.CryptoStream($output, $aes.CreateEncryptor(), [Security.Cryptography.CryptoStreamMode]::Write, $true)
        try {
            $inputFile = [IO.File]::OpenRead($InputPath)
            try { $inputFile.CopyTo($crypto); $crypto.FlushFinalBlock() } finally { $inputFile.Dispose() }
        } finally { $crypto.Dispose() }
    } finally { $output.Dispose() }
    $hmac = New-Object Security.Cryptography.HMACSHA256
    try {
        $hmac.Key = $macKey
        $encrypted = [IO.File]::OpenRead($partial)
        try { $tag = $hmac.ComputeHash($encrypted) } finally { $encrypted.Dispose() }
    } finally { $hmac.Dispose() }
    $output = [IO.File]::Open($partial, [IO.FileMode]::Append)
    try { $output.Write($tag, 0, $tag.Length) } finally { $output.Dispose() }
    [IO.File]::Move($partial, $OutputPath)
} finally {
    $rsa.Dispose()
    $aes.Dispose()
    if ($partialOwned -and (Test-Path -LiteralPath $partial)) { Remove-Item -LiteralPath $partial -Force }
}
