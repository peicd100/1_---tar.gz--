param([string]$Version = '1.0.2')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
Add-Type -AssemblyName System.Security.Cryptography.Pkcs
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path "dist/TarGz-$Version-x64.msix").Path)
try {
    $entry = $archive.GetEntry('AppxSignature.p7x')
    $stream = $entry.Open(); $memory = New-Object IO.MemoryStream
    $stream.CopyTo($memory); $stream.Dispose()
    $bytes = $memory.ToArray(); $memory.Dispose()
    $cms = New-Object System.Security.Cryptography.Pkcs.SignedCms
    $cms.Decode([byte[]]$bytes[4..($bytes.Length-1)])
    $cms.CheckSignature($true) # Cryptographic signature only; never modify system trust.
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2((Resolve-Path dist/signer.cer).Path)
    if ($cms.SignerInfos[0].Certificate.Thumbprint -ne $cert.Thumbprint) { throw 'Signer mismatch' }
    # Verify signed content binds the exact blockmap bytes.
    $entry = $archive.GetEntry('AppxBlockMap.xml'); $stream = $entry.Open(); $hash = [Security.Cryptography.SHA256]::Create()
    $blockDigest = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-',''); $stream.Dispose(); $hash.Dispose()
    $signed = [BitConverter]::ToString($cms.ContentInfo.Content).Replace('-','')
    if (-not $signed.Contains('4158424D' + $blockDigest)) { throw 'Signed AXBM blockmap digest mismatch' }
    Write-Output 'CMS signature, expected signer, signed blockmap digest PASS (trust not installed)'
} finally { $archive.Dispose() }
