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
    & $dotnet publish src\WeatherWidget -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts\win-x64 --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item README.md,NOTICE.md -Destination artifacts\win-x64
    $sdkDirectory = Split-Path $dotnet
    foreach ($notice in @("LICENSE.txt", "ThirdPartyNotices.txt")) {
        $noticePath = Join-Path $sdkDirectory $notice
        if (Test-Path -LiteralPath $noticePath) { Copy-Item -LiteralPath $noticePath -Destination (Join-Path "artifacts\win-x64" ("dotnet-" + $notice)) }
    }
    Write-Host 'Portable app: artifacts\win-x64\WeatherWidget.exe (keep Assets beside it)'
}
