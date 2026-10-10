param([string]$Python = 'D:\conda\envs\PEICD100\python.exe', [switch]$SkipCompression)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
if (-not $SkipCompression) {
    & $Python tests/smoke.py
    if ($LASTEXITCODE -ne 0) { throw 'Compression smoke tests failed' }
}
$clang = (Resolve-Path build/tools/llvm-mingw-20261006-ucrt-x86_64/bin/clang++.exe).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force 'build/shell test 中文' | Out-Null
Copy-Item build/package/TarGzShell.dll 'build/shell test 中文/TarGzShell.dll' -Force
& $csc /nologo /out:'build\shell test 中文\TarGz.exe' tests\SelectionProbe.cs
if ($LASTEXITCODE -ne 0) { throw 'Probe compilation failed' }
& $clang -O2 -static -municode tests/ShellHarness.cpp -o build/ShellHarness.exe -lole32 -lshell32 -luuid
if ($LASTEXITCODE -ne 0) { throw 'Harness compilation failed' }
$one = Join-Path (Resolve-Path 'build/shell test 中文').Path '甲 & 空白.txt'
$two = Join-Path (Resolve-Path 'build/shell test 中文').Path '乙 [資料夾]'
[IO.File]::WriteAllText($one,'test')
New-Item -ItemType Directory -Force -Path $two | Out-Null
$report = Join-Path (Resolve-Path build).Path 'shell-selection.txt'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
& build/ShellHarness.exe (Resolve-Path 'build/shell test 中文/TarGzShell.dll').Path $one $two $report
if ($LASTEXITCODE -ne 0) { throw 'Shell COM test failed' }
$deadline = [DateTime]::UtcNow.AddSeconds(10)
while (-not (Test-Path -LiteralPath $report)) {
    if ([DateTime]::UtcNow -gt $deadline) { throw 'Shell selection probe timeout' }
    Start-Sleep -Milliseconds 100
}
$selected = [IO.File]::ReadAllLines($report)
if ($selected.Count -ne 2 -or $selected[0] -ne $one -or $selected[1] -ne $two) { throw 'Selection transfer mismatch' }
Write-Output 'Native shell end-to-end selection transfer PASS'
& $csc /nologo /target:winexe /out:build\RenderUi.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll tests\RenderUi.cs
if ($LASTEXITCODE -ne 0) { throw 'UI renderer compilation failed' }
$render = Start-Process -FilePath (Resolve-Path build/RenderUi.exe).Path -ArgumentList @('build/package/TarGz.exe','Peicd.TarGz.ArchiveWindow','build/app-preview.png') -PassThru -WindowStyle Hidden -Wait
if ($render.ExitCode -ne 0) { throw 'App UI render failed' }
$render = Start-Process -FilePath (Resolve-Path build/RenderUi.exe).Path -ArgumentList @('dist/Setup.exe','Peicd.TarGz.SetupWindow','build/setup-preview.png') -PassThru -WindowStyle Hidden -Wait
if ($render.ExitCode -ne 0) { throw 'Setup UI render failed' }
Write-Output 'App and installer UI rendering PASS (no installation performed)'
