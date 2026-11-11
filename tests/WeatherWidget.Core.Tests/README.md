# WeatherWidget.Core tests

Run the dependency-free fixture assertion harness from the repository root:

```powershell
& .\tools\dotnet\dotnet.exe run --project .\tests\WeatherWidget.Core.Tests\WeatherWidget.Core.Tests.csproj -c Release
```

The Open-Meteo adapter keeps every hourly entry and all daily entries returned by the provider. The forecast UI can select the next 12 hours from the current local hour and the next five city-local dates without losing timezone context.

Production requests five forecast days; the captured parser fixture contains seven.
