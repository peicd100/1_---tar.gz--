param([string]$Python = 'D:\conda\envs\PEICD100\python.exe')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
New-Item -ItemType Directory -Force build/tools,assets | Out-Null
# Project-local tools only: no global installation or PATH changes.
if (-not (Test-Path build/tools/llvm-mingw-20261006-ucrt-x86_64/bin/clang++.exe)) {
    Invoke-WebRequest 'https://github.com/mstorsjo/llvm-mingw/releases/download/20261006/llvm-mingw-20261006-ucrt-x86_64.zip' -OutFile build/tools/llvm-mingw.zip
    Expand-Archive -LiteralPath build/tools/llvm-mingw.zip -DestinationPath build/tools -Force
}
if (-not (Test-Path build/tools/sdk/bin/10.0.26100.0/x64/makeappx.exe)) {
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/10.0.26100.4948/microsoft.windows.sdk.buildtools.10.0.26100.4948.nupkg' -OutFile build/tools/sdk.zip
    Expand-Archive -LiteralPath build/tools/sdk.zip -DestinationPath build/tools/sdk -Force
}
if (-not (Test-Path assets/JasonHandwriting9.ttf)) {
    Invoke-WebRequest 'https://raw.githubusercontent.com/jasonhandwriting/JasonHandwriting/master/JasonHandwriting9.ttf' -OutFile assets/JasonHandwriting9.ttf
}
& $Python -c 'import PIL, fontTools, cryptography'
if ($LASTEXITCODE -ne 0) { throw 'Python requires Pillow, fonttools, cryptography; no environment changes were made.' }
Write-Output 'Build tools ready.'
