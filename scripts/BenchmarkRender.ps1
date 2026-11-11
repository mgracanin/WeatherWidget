param([string]$BaselineRef='f97c265')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
Set-Location $root
$dotnet=Join-Path $root 'tools\dotnet\dotnet.exe'
if(-not(Test-Path $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$baseline=Join-Path $root 'artifacts\baseline'
if(-not(Test-Path $baseline)){git worktree add --detach $baseline $BaselineRef;if($LASTEXITCODE -ne 0){throw 'Cannot create baseline checkout'}}
$expected=git rev-parse $BaselineRef
$actual=git -C $baseline rev-parse HEAD
if($actual -ne $expected){throw 'Existing baseline checkout has a different commit; refusing to overwrite it.'}
Copy-Item tests\WeatherWidget.App.Tests\PreviewRenderer.cs (Join-Path $baseline 'tests\WeatherWidget.App.Tests\PreviewRenderer.cs')
$program=Join-Path $baseline 'tests\WeatherWidget.App.Tests\Program.cs'
$text=[IO.File]::ReadAllText($program)
if(-not $text.Contains('_ = PreviewRenderer.FixtureApp;')){$text=$text.Replace('        fixture = File.ReadAllText(FixturePath);',"        _ = PreviewRenderer.FixtureApp;`r`n        fixture = File.ReadAllText(FixturePath);")}
if(-not $text.Contains('--benchmark-render')){$text=$text.Replace('try { exitCode = args.Contains("--render-previews", StringComparer.OrdinalIgnoreCase)','try { exitCode = args.Contains("--benchmark-render", StringComparer.OrdinalIgnoreCase) ? await PreviewRenderer.BenchmarkAsync(fixture,args.Last()) : args.Contains("--render-previews", StringComparer.OrdinalIgnoreCase)')}
[IO.File]::WriteAllText($program,$text)
$oldTiering=$env:DOTNET_TieredCompilation
try {
    $env:DOTNET_TieredCompilation='0'
    & $dotnet run --project (Join-Path $baseline 'tests\WeatherWidget.App.Tests') -c Release -- --benchmark-render (Join-Path $root 'artifacts\perf-render-before.json')
    if($LASTEXITCODE -ne 0){throw 'Baseline benchmark failed'}
    & $dotnet run --project tests\WeatherWidget.App.Tests -c Release -- --benchmark-render (Join-Path $root 'artifacts\perf-render-after.json')
    if($LASTEXITCODE -ne 0){throw 'Current benchmark failed'}
} finally {$env:DOTNET_TieredCompilation=$oldTiering}
