# WeatherWidget.App controller tests

Run on Windows from the repository root:

```powershell
& .\tools\dotnet\dotnet.exe run --project .\tests\WeatherWidget.App.Tests\WeatherWidget.App.Tests.csproj -c Release
```

The harness uses an STA WPF dispatcher and only local HTTP fixtures. Each controller receives a unique temporary cache path and a no-op test logger, so these tests do not read or write the user's WeatherWidget cache or log.
