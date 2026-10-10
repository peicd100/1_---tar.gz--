$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /out:build\ProgressHarness.exe src\Archive.cs tests\ProgressHarness.cs
if ($LASTEXITCODE -ne 0) { throw 'Progress harness compilation failed' }
$run = Join-Path (Resolve-Path build).Path ('progress-tests/' + (Get-Date -Format yyyyMMdd-HHmmss))
& build/ProgressHarness.exe $run
if ($LASTEXITCODE -ne 0) { throw 'Progress tests failed' }
