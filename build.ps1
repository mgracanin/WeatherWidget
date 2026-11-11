param([switch]$Test, [switch]$Publish)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$localSdk = Join-Path $PSScriptRoot 'tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
if ((& $dotnet --version) -notmatch '^10\.') { throw 'A .NET 10 SDK is required.' }
& $dotnet build src\WeatherWidget -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
    foreach ($project in @('tests\WeatherWidget.Core.Tests', 'tests\WeatherWidget.App.Tests')) {
        & $dotnet run --project $project -c Release
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $project" }
    }
}
if ($Publish) {
    $publishedExe=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts\win-x64\WeatherWidget.exe'))
    if (Get-Process -Name WeatherWidget -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $publishedExe }) {
        throw 'Close the packaged WeatherWidget before publishing over it.'
    }
    # Publish does not remove obsolete content files from an existing output directory.
    $assetOutput=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts\win-x64\Assets'))
    $workspacePrefix=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')+'\'
    if (!$assetOutput.StartsWith($workspacePrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Asset output is outside the workspace.' }
    if (Test-Path -LiteralPath $assetOutput) {
        if ((Get-Item -LiteralPath $assetOutput).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing to clean a linked asset directory.' }
        Remove-Item -LiteralPath $assetOutput -Recurse -Force
    }
    & $dotnet publish src\WeatherWidget -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts\win-x64 --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item README.md,NOTICE.md -Destination artifacts\win-x64
    $docsOutput=Join-Path $PSScriptRoot 'artifacts\win-x64\docs'
    New-Item -ItemType Directory -Force -Path (Join-Path $docsOutput 'images') | Out-Null
    foreach($name in @('assets.md','locations.md','legacy-artwork.md')) {Copy-Item -LiteralPath (Join-Path (Join-Path $PSScriptRoot 'docs') $name) -Destination $docsOutput}
    Copy-Item -Path (Join-Path $PSScriptRoot 'docs\images\*.png') -Destination (Join-Path $docsOutput 'images')
    $sdkDirectory = Split-Path $dotnet
    foreach ($notice in @("LICENSE.txt", "ThirdPartyNotices.txt")) {
        $noticePath = Join-Path $sdkDirectory $notice
        if (Test-Path -LiteralPath $noticePath) { Copy-Item -LiteralPath $noticePath -Destination (Join-Path "artifacts\win-x64" ("dotnet-" + $notice)) }
    }
    Write-Host 'Portable app: artifacts\win-x64\WeatherWidget.exe (keep Assets beside it)'
}
