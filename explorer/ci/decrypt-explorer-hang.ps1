param(
    [Parameter(Mandatory=$true)][string]$EncryptedDump,
    [Parameter(Mandatory=$true)][string]$PrivateKey,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$data = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $EncryptedDump))
$newline = [Array]::IndexOf($data, [byte]10)
if ($newline -lt 0 -or $data.Length -lt $newline + 49) { throw 'Invalid dump envelope' }
$header = [Text.Encoding]::UTF8.GetString($data, 0, $newline) | ConvertFrom-Json
if ($header.format -ne 'explorer-hang-v1') { throw 'Unsupported dump format' }
$rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
try {
    $rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $PrivateKey)))
    $keys = $rsa.Decrypt([Convert]::FromBase64String($header.wrappedKeys), $true)
} finally { $rsa.Dispose() }
$hmac = New-Object Security.Cryptography.HMACSHA256
try {
    $hmac.Key = [byte[]]$keys[32..63]
    $tag = $hmac.ComputeHash($data, 0, $data.Length - 32)
} finally { $hmac.Dispose() }
$difference = 0
for ($i = 0; $i -lt 32; $i++) { $difference = $difference -bor ($tag[$i] -bxor $data[$data.Length - 32 + $i]) }
if ($difference -ne 0) { throw 'Dump authentication failed' }
$aes = [Security.Cryptography.Aes]::Create()
try {
    $aes.Key = [byte[]]$keys[0..31]
    $aes.IV = [Convert]::FromBase64String($header.iv)
    $decryptor = $aes.CreateDecryptor()
    try { $dump = $decryptor.TransformFinalBlock($data, $newline + 1, $data.Length - $newline - 33) }
    finally { $decryptor.Dispose() }
} finally { $aes.Dispose() }
if ([Text.Encoding]::ASCII.GetString($dump, 0, 4) -ne 'MDMP') { throw 'Decrypted content is not a minidump' }
[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($OutputPath), $dump)
Write-Output "Decrypted and authenticated dump: $OutputPath"
