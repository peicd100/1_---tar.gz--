param(
    [string]$Version = '1.0.2',
    [string]$Python = 'D:\conda\envs\PEICD100\python.exe'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be major.minor.patch' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$native = Join-Path $root 'build\tools\llvm-mingw-20261006-ucrt-x86_64\bin\clang++.exe'
$sdk = Join-Path $root 'build\tools\sdk\bin\10.0.26100.0\x64'
foreach ($tool in @($compiler,$native,(Join-Path $sdk 'makeappx.exe'),(Join-Path $sdk 'signtool.exe'),$Python)) {
    if (-not (Test-Path -LiteralPath $tool)) { throw "Missing tool: $tool. See tools/bootstrap.ps1." }
}
function Run-Native([string]$Tool, [string[]]$Arguments) {
    if ($Tool -eq $compiler) {
        $Arguments = @($Arguments | ForEach-Object {
            if ($_.StartsWith('/')) { '/' + $_.Substring(1).Replace('/','\') }
            else { $_.Replace('/','\') }
        })
        $Arguments = @($Arguments | Where-Object { $_.StartsWith('/') }) + @($Arguments | Where-Object { -not $_.StartsWith('/') })
    }
    if ($Tool -eq $compiler) {
        $response = Join-Path $root 'build/csc.rsp'
        [IO.File]::WriteAllLines($response, @($Arguments | ForEach-Object { '"' + $_ + '"' }), (New-Object System.Text.UTF8Encoding($true)))
        & $Tool "@$response"
    } else { & $Tool @Arguments }
    if ($LASTEXITCODE -ne 0) { throw "Tool failed ($LASTEXITCODE): $Tool" }
}
New-Item -ItemType Directory -Force build/package/Assets,dist | Out-Null
Run-Native $Python @('tools/prepare_assets.py')
Run-Native $Python @('tools/create_certificate.py')
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $root 'build/AssemblyInfo.cs'), "using System.Reflection; [assembly:AssemblyVersion(`"$Version.0`")] [assembly:AssemblyFileVersion(`"$Version.0`")] [assembly:AssemblyProduct(`"PEICD TAR.GZ`")]")
$common = @('/nologo','/target:winexe','/platform:x64','/optimize+', '/r:System.Windows.Forms.dll','/r:System.Drawing.dll',
    '/win32icon:assets/app.ico','/resource:assets/app.ico,app.ico','/resource:assets/JasonHandwriting9.ttf,font.ttf', 'src/Common.cs','src/Archive.cs','build/AssemblyInfo.cs')
Run-Native $compiler ($common + @('/out:build/package/TarGz.exe','src/App.cs'))
Run-Native $native @('-shared','-O2','-std=c++17','-static','-finput-charset=UTF-8','src/Shell.cpp','-o','build/package/TarGzShell.dll','-lole32','-luuid','-lshlwapi')
Run-Native (Join-Path $sdk 'signtool.exe') @('sign','/fd','SHA256','/f','build/signing/local-signing.pfx','build/package/TarGz.exe','build/package/TarGzShell.dll')
$manifest = [IO.File]::ReadAllText((Join-Path $root 'src/AppxManifest.xml')).Replace('Version="1.0.0.0"', "Version=`"$Version.0`"")
[IO.File]::WriteAllText((Join-Path $root 'build/package/AppxManifest.xml'),$manifest,$utf8)
Copy-Item assets/*Logo.png build/package/Assets -Force
Copy-Item assets/FONT-LICENSE.txt,assets/FONT-AUTHOR.md build/package -Force
$msix = Join-Path $root "dist/TarGz-$Version-x64.msix"
Run-Native (Join-Path $sdk 'makeappx.exe') @('pack','/d','build/package','/p',$msix,'/o')
Run-Native (Join-Path $sdk 'signtool.exe') @('sign','/fd','SHA256','/f','build/signing/local-signing.pfx',$msix)
$hash = (Get-FileHash -LiteralPath $msix -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $root 'build/PayloadHash.cs'),"namespace Peicd.TarGz { internal static class PayloadHash { internal const string Value = `"$hash`"; } }",$utf8)
Run-Native $compiler ($common + @('/out:dist/Setup.exe',"/resource:$msix,payload.msix",'/resource:build/signing/local-signing.cer,signer.cer','src/Setup.cs','build/PayloadHash.cs'))
# Sign bootstrapper too; trust is still explicit on first installation.
Run-Native (Join-Path $sdk 'signtool.exe') @('sign','/fd','SHA256','/f','build/signing/local-signing.pfx','dist/Setup.exe')
Copy-Item build/signing/local-signing.cer dist/signer.cer -Force
Get-FileHash dist/Setup.exe,$msix -Algorithm SHA256 | ForEach-Object { $_.Hash + '  ' + (Split-Path $_.Path -Leaf) } | Set-Content dist/SHA256SUMS.txt -Encoding ASCII
Write-Output "Build complete: dist/Setup.exe ($Version)"
