using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WeatherWidget;
using WeatherWidget.Core;

internal static class Program
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "Zagreb-live.json");
    private static string fixture = "";

    [STAThread]
    private static void Main()
    {
        fixture = File.ReadAllText(FixturePath);
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var exitCode = 0;
        dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
        {
            try { exitCode = await RunAll(); }
            catch (Exception exception) { Console.Error.WriteLine($"Harness error: {exception}"); exitCode = 1; }
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        }));
        Dispatcher.Run();
        Environment.ExitCode = exitCode;
    }

    private static async Task<int> RunAll()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("matching cache loads offline and mismatching coordinates are ignored", CacheCoordinateMatch),
            ("corrupt and schema-invalid caches are ignored", InvalidCachesAreIgnored),
            ("HTTP failure preserves the previous snapshot", FailurePreservesSnapshot),
            ("city change cancels the obsolete request", CityChangeSupersedesRequest),
            ("malformed provider data is surfaced and remains recoverable", MalformedResponseIsNonfatal),
            ("provider IO failure is surfaced as offline", IoFailureIsNonfatal),
            ("disposal cancels pending work", DisposalCancelsRequest),
            ("settings round-trip and atomically replace the file", SettingsRoundTrip),
            ("corrupt settings recover defaults without app-data logging", CorruptSettingsRecover),
            ("invalid settings normalize and valid option values are preserved", SettingsNormalizeValues),
            ("tray icon is embedded with all Windows sizes", EmbeddedIconFrames),
            ("weather artwork assets load and forecast layout has 12-hour and 5-day views", ForecastLayoutAndAssets),
            ("forecast close is safe when closing triggers deactivation or another close", ForecastCloseRegression)
        };
        var failures = 0;
        foreach (var test in tests)
        {
            try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
            catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}"); }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} controller checks passed");
        return failures == 0 ? 0 : 1;
    }

    private static async Task ForecastCloseRegression()
    {
        var window = new ForecastWindow();
        int closingCount = 0, closedCount = 0;
        window.Closing += (_, _) =>
        {
            closingCount++;
            window.DismissWhenInactive();
            window.RequestClose();
        };
        window.Closed += (_, _) => closedCount++;
        window.RequestClose();
        window.RequestClose();
        window.DismissWhenInactive();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Equal(1, closingCount);
        Equal(1, closedCount);
    }
    private static async Task CacheCoordinateMatch()
    {
        var snapshot = await BuildFixtureSnapshot();
        using var workspace = new TestWorkspace();
        await workspace.WriteCache(JsonSerializer.Serialize(snapshot));

        using (var matching = NewController(workspace.CachePath, Settings(), new FixtureHandler(_ => Ok("{}"))))
        {
            Equal(snapshot.Location.Latitude, matching.Controller.Snapshot!.Location.Latitude);
            True(matching.Controller.Offline);
        }

        using var mismatched = NewController(workspace.CachePath, Settings("Split", 43.508, 16.44), new FixtureHandler(_ => Ok("{}")));
        True(mismatched.Controller.Snapshot is null);
        True(!mismatched.Controller.Offline);
    }

    private static async Task InvalidCachesAreIgnored()
    {
        var snapshot = await BuildFixtureSnapshot();
        var valid = JsonSerializer.Serialize(snapshot);
        var invalidCases = new List<string> { "{ broken json" };

        var nullCurrent = JsonNode.Parse(valid)!.AsObject();
        nullCurrent["Current"] = null;
        invalidCases.Add(nullCurrent.ToJsonString());

        var nullHourlyEntry = JsonNode.Parse(valid)!.AsObject();
        nullHourlyEntry["Hourly"]!.AsArray().Insert(0, null);
        invalidCases.Add(nullHourlyEntry.ToJsonString());

        var missingTimestamp = JsonNode.Parse(valid)!.AsObject();
        missingTimestamp["FetchedAtUtc"] = DateTimeOffset.MinValue;
        invalidCases.Add(missingTimestamp.ToJsonString());

        var invalidSourceTime = JsonNode.Parse(valid)!.AsObject();
        invalidSourceTime["Current"]!["TimeUtc"] = DateTimeOffset.MinValue;
        invalidCases.Add(invalidSourceTime.ToJsonString());

        foreach (var invalid in invalidCases)
        {
            using var workspace = new TestWorkspace();
            await workspace.WriteCache(invalid);
            using var harness = NewController(workspace.CachePath, Settings(), new FixtureHandler(_ => Ok("{}")));
            True(harness.Controller.Snapshot is null);
            True(!harness.Controller.Offline);
        }
    }

    private static async Task FailurePreservesSnapshot()
    {
        var snapshot = await BuildFixtureSnapshot();
        using var workspace = new TestWorkspace();
        await workspace.WriteCache(JsonSerializer.Serialize(snapshot));
        using var harness = NewController(workspace.CachePath, Settings(), new FixtureHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("temporary outage") }));
        var previous = harness.Controller.Snapshot;

        await harness.Controller.RefreshAsync();

        Same(previous, harness.Controller.Snapshot);
        True(harness.Controller.Offline);
        True(!string.IsNullOrWhiteSpace(harness.Controller.Error));
        True(!harness.Controller.IsLoading);
    }

    private static async Task CityChangeSupersedesRequest()
    {
        using var workspace = new TestWorkspace();
        var handler = new SupersedingHandler(fixture);
        using var harness = NewController(workspace.CachePath, Settings(), handler);
        var obsoleteRequest = harness.Controller.RefreshAsync();
        await handler.FirstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        harness.UpdateSettings("Split", 43.508, 16.44);
        var selectedCityRequest = harness.Controller.RefreshAsync(changedCity: true);
        await selectedCityRequest.WaitAsync(TimeSpan.FromSeconds(5));
        await obsoleteRequest.WaitAsync(TimeSpan.FromSeconds(5));

        Equal("Split", harness.Controller.Snapshot!.Location.Name);
        Equal(43.508, harness.Controller.Snapshot.Location.Latitude);
        True(!harness.Controller.Offline);
        Equal(2, handler.CallCount);
    }

    private static async Task MalformedResponseIsNonfatal()
    {
        using var workspace = new TestWorkspace();
        var calls = 0;
        var handler = new FixtureHandler(_ => Interlocked.Increment(ref calls) == 1 ? Ok("{\"timezone\":\"Europe/Zagreb\",\"current\":null}") : Ok(fixture));
        using var harness = NewController(workspace.CachePath, Settings(), handler);

        await harness.Controller.RefreshAsync();
        True(harness.Controller.Offline);
        True(harness.Controller.Snapshot is null);
        True(!string.IsNullOrWhiteSpace(harness.Controller.Error));
        True(!harness.Controller.IsLoading);

        await harness.Controller.RefreshAsync();
        True(harness.Controller.Snapshot is not null);
        True(!harness.Controller.Offline);
        True(harness.Controller.Error is null);
    }

    private static async Task IoFailureIsNonfatal()
    {
        using var workspace = new TestWorkspace();
        var calls = 0;
        var handler = new FixtureHandler(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new IOException("fixture read failed");
            return Ok(fixture);
        });
        using var harness = NewController(workspace.CachePath, Settings(), handler);

        await harness.Controller.RefreshAsync();
        True(harness.Controller.Offline);
        True(harness.Controller.Snapshot is null);
        True(!string.IsNullOrWhiteSpace(harness.Controller.Error));
        True(!harness.Controller.IsLoading);

        await harness.Controller.RefreshAsync();
        True(harness.Controller.Snapshot is not null);
        True(!harness.Controller.Offline);
    }

    private static async Task DisposalCancelsRequest()
    {
        using var workspace = new TestWorkspace();
        var handler = new BlockingHandler();
        var harness = NewController(workspace.CachePath, Settings(), handler);
        var pending = harness.Controller.RefreshAsync();
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        harness.Controller.Dispose();
        await pending.WaitAsync(TimeSpan.FromSeconds(3));
        True(handler.CancellationObserved.Task.IsCompleted);
        harness.DisposeHttp();
    }

    private static Task SettingsRoundTrip()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.SettingsPath;
        var logs = new List<string>();
        var settings = Settings("Rijeka", 45.327, 14.442);
        settings.TemperatureUnit = "fahrenheit";
        settings.WindUnit = "mph";
        settings.Theme = "classic";
        settings.Scale = 1.75;
        settings.Language = "hr";
        settings.Locked = true;
        settings.AlwaysOnTop = true;
        settings.StartWithWindows = true;
        settings.Left = -2560;
        settings.Top = 120;

        settings.Save(path, logs.Add);
        True(File.Exists(path));
        True(!File.Exists(path + ".tmp"));
        var loaded = AppSettings.Load(path, logs.Add);
        Equal("Rijeka", loaded.CityName);
        Equal(45.327, loaded.Latitude);
        Equal(14.442, loaded.Longitude);
        Equal("fahrenheit", loaded.TemperatureUnit);
        Equal("mph", loaded.WindUnit);
        Equal("classic", loaded.Theme);
        Equal(1.75, loaded.Scale);
        Equal("hr", loaded.Language);
        True(loaded.Locked && loaded.AlwaysOnTop && loaded.StartWithWindows);
        Equal(-2560d, loaded.Left);
        Equal(120d, loaded.Top);

        settings.CityName = "Osijek";
        settings.Save(path, logs.Add);
        True(!File.Exists(path + ".tmp"));
        Equal("Osijek", AppSettings.Load(path, logs.Add).CityName);
        Equal(0, logs.Count);
        return Task.CompletedTask;
    }

    private static async Task CorruptSettingsRecover()
    {
        using var workspace = new TestWorkspace();
        workspace.EnsureDirectory();
        await File.WriteAllTextAsync(workspace.SettingsPath, "not json {");
        var logs = new List<string>();
        var settings = AppSettings.Load(workspace.SettingsPath, logs.Add);
        Equal("Zagreb", settings.CityName);
        Equal(45.81444, settings.Latitude);
        Equal(15.97798, settings.Longitude);
        Equal(1d, settings.Scale);
        Equal("modern", settings.Theme);
        Equal(1, logs.Count);
    }

    private static async Task SettingsNormalizeValues()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.SettingsPath;
        workspace.EnsureDirectory();
        var logs = new List<string>();
        await File.WriteAllTextAsync(path, """
            {"CityName":null,"TemperatureUnit":"kelvin","WindUnit":"feet","Theme":"dark","Scale":4,"Language":"de","Left":-100001,"Top":null,"Latitude":91,"Longitude":16}
            """);
        var invalid = AppSettings.Load(path, logs.Add);
        Equal("Zagreb", invalid.CityName);
        Equal(45.81444, invalid.Latitude);
        Equal(15.97798, invalid.Longitude);
        Equal("celsius", invalid.TemperatureUnit);
        Equal("kmh", invalid.WindUnit);
        Equal("modern", invalid.Theme);
        Equal(1d, invalid.Scale);
        Equal("system", invalid.Language);
        True(invalid.Left is null && invalid.Top is null);

        var allowedCases = new (string Json, string Temperature, string Wind, string Theme, double Scale, string Language)[]
        {
            ("\"TemperatureUnit\":\"fahrenheit\",\"WindUnit\":\"mph\",\"Theme\":\"classic\",\"Scale\":3,\"Language\":\"hr\"", "fahrenheit", "mph", "classic", 3, "hr"),
            ("\"TemperatureUnit\":\"F\",\"WindUnit\":\"m/s\",\"Theme\":\"modern\",\"Scale\":0.5,\"Language\":\"en\"", "fahrenheit", "ms", "modern", .5, "en"),
            ("\"TemperatureUnit\":\"celsius\",\"WindUnit\":\"kmh\",\"Theme\":\"modern\",\"Scale\":1,\"Language\":\"system\"", "celsius", "kmh", "modern", 1, "system")
        };
        foreach (var item in allowedCases)
        {
            await File.WriteAllTextAsync(path, "{" + item.Json + "}");
            var valid = AppSettings.Load(path, logs.Add);
            Equal(item.Temperature, valid.TemperatureUnit);
            Equal(item.Wind, valid.WindUnit);
            Equal(item.Theme, valid.Theme);
            Equal(item.Scale, valid.Scale);
            Equal(item.Language, valid.Language);
        }
        Equal(0, logs.Count);
    }

    private static async Task ForecastLayoutAndAssets()
    {
        var smallAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var largeAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var code = 0; code <= 99; code++)
        foreach (var day in new[] { false, true })
        {
            var small = WeatherArt.Background(code, day, expanded: false);
            var large = WeatherArt.Background(code, day, expanded: true);
            True(File.Exists(small.UriSource!.LocalPath), $"small asset missing for code {code}, day={day}: {small.UriSource.LocalPath}");
            True(File.Exists(large.UriSource!.LocalPath), $"large asset missing for code {code}, day={day}: {large.UriSource.LocalPath}");
            True(small.IsFrozen && small.PixelWidth > 0 && small.PixelHeight > 0, $"small bitmap did not load for code {code}, day={day}");
            True(large.IsFrozen && large.PixelWidth > 0 && large.PixelHeight > 0, $"large bitmap did not load for code {code}, day={day}");
            smallAssets.Add(small.UriSource.LocalPath);
            largeAssets.Add(large.UriSource.LocalPath);
        }
        True(smallAssets.Count > 1 && largeAssets.Count > 1, $"Expected mapped backgrounds, found {smallAssets.Count} small and {largeAssets.Count} large assets.");
        var largeBackground = WeatherArt.Background(0, true, expanded: true);
        Equal(264, largeBackground.PixelWidth);
        Equal(194, largeBackground.PixelHeight);

        var source = await BuildFixtureSnapshot();
        var now = DateTimeOffset.UtcNow;
        var hourFloor = new DateTimeOffset(now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day, now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(source.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var hourly = Enumerable.Range(0, 24 * 7).Select(i => source.Hourly[i % source.Hourly.Count] with { TimeUtc = hourFloor.AddHours(i) }).ToArray();
        var daily = Enumerable.Range(0, 7).Select(i => source.Daily[i % source.Daily.Count] with { Date = today.AddDays(i) }).ToArray();
        var current = source.Current with { TimeUtc = now };
        var currentSnapshot = source with { FetchedAtUtc = now, Current = current, Hourly = hourly, Daily = daily };

        var window = new ForecastWindow();
        try
        {
            window.Update(currentSnapshot, "en", "F", "mph", isOffline: true, lastError: null);
            var viewbox = (Viewbox)window.Content;
            var scene = (Canvas)viewbox.Child;
            viewbox.Measure(new Size(528, 388));
            viewbox.Arrange(new Rect(0, 0, 528, 388));
            viewbox.UpdateLayout();
            Equal(528d, scene.Width);
            Equal(388d, scene.Height);
            var background = scene.Children.OfType<Image>().First();
            Equal(264, ((System.Windows.Media.Imaging.BitmapSource)background.Source).PixelWidth);
            Equal(194, ((System.Windows.Media.Imaging.BitmapSource)background.Source).PixelHeight);
            var scroll = scene.Children.OfType<ScrollViewer>().Single();
            Equal(12, ((StackPanel)scroll.Content).Children.Count);
            var labels = CollectText(scene);
            True(labels.Contains("°F", StringComparison.Ordinal), "Temperature unit label missing.");
            True(labels.Contains("mph", StringComparison.Ordinal), "Wind unit label missing.");
            True(labels.Contains("OFFLINE", StringComparison.Ordinal), "Offline label missing.");
            True(labels.Contains("Next 12 hours", StringComparison.Ordinal), "Hourly view control missing.");
            True(labels.Contains("5-day forecast", StringComparison.Ordinal), "Daily view control missing.");

            var dailyButton = scene.Children.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Show daily forecast");
            dailyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, dailyButton));
            scroll = scene.Children.OfType<ScrollViewer>().Single();
            Equal(5, ((StackPanel)scroll.Content).Children.Count);
        }
        finally { window.Close(); }
    }

    private static Task EmbeddedIconFrames()
    {
        using var stream = typeof(App).Assembly.GetManifestResourceStream("WeatherWidget.Brand.Icon")
            ?? throw new InvalidOperationException("Embedded tray icon resource was not found in the app assembly.");
        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream,
            System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        var sizes = decoder.Frames.Select(frame => $"{frame.PixelWidth}x{frame.PixelHeight}").ToHashSet(StringComparer.Ordinal);
        Equal(9, decoder.Frames.Count);
        foreach (var size in new[] { "16x16", "20x20", "24x24", "32x32", "40x40", "48x48", "64x64", "128x128", "256x256" })
            True(sizes.Contains(size), $"Embedded icon frame {size} was missing.");
        return Task.CompletedTask;
    }

    private static string CollectText(DependencyObject root)
    {
        var result = new StringBuilder();
        Walk(root);
        return result.ToString();

        void Walk(DependencyObject node)
        {
            if (node is TextBlock text) result.Append(text.Text).Append('\n');
            if (node is Panel panel)
            {
                foreach (UIElement child in panel.Children) Walk(child);
            }
            else if (node is ContentControl content)
            {
                if (content.Content is string value) result.Append(value).Append('\n');
                else if (content.Content is DependencyObject contentNode) Walk(contentNode);
            }
            else if (node is ScrollViewer scroll && scroll.Content is DependencyObject scrollNode) Walk(scrollNode);
            else if (node is Viewbox viewbox && viewbox.Child is DependencyObject viewboxChild) Walk(viewboxChild);
        }
    }

    private static async Task<WeatherSnapshot> BuildFixtureSnapshot()
    {
        using var http = new HttpClient(new FixtureHandler(_ => Ok(fixture)));
        using var client = new WeatherClient(http);
        return await client.FetchAsync(new WeatherLocation("Zagreb", 45.815, 15.982, null, "Croatia"));
    }

    private static AppSettings Settings(string name = "Zagreb", double latitude = 45.815, double longitude = 15.982) =>
        new() { CityName = name, Latitude = latitude, Longitude = longitude, Language = "en" };

    private static ControllerHarness NewController(string cachePath, AppSettings settings, HttpMessageHandler handler)
    {
        var http = new HttpClient(handler);
        var weather = new WeatherClient(http);
        var controller = new WeatherController(settings, Dispatcher.CurrentDispatcher, weather, cachePath, _ => { });
        return new ControllerHarness(controller, http, settings);
    }

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private static void True(bool value, string? message = null) { if (!value) throw new InvalidOperationException(message ?? "Expected true."); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected '{expected}', got '{actual}'."); }
    private static void Same(object? expected, object? actual) { if (!ReferenceEquals(expected, actual)) throw new InvalidOperationException("Expected the original object instance to remain unchanged."); }

    private sealed class ControllerHarness(WeatherController controller, HttpClient http, AppSettings settings) : IDisposable
    {
        public WeatherController Controller { get; } = controller;
        public AppSettings Settings { get; } = settings;
        public void UpdateSettings(string name, double latitude, double longitude)
        {
            Settings.CityName = name; Settings.Latitude = latitude; Settings.Longitude = longitude;
        }
        public void DisposeHttp() => http.Dispose();
        public void Dispose() { Controller.Dispose(); http.Dispose(); }
    }

    private sealed class TestWorkspace : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "WeatherWidget.App.Tests-" + Guid.NewGuid().ToString("N"));
        public string CachePath => Path.Combine(root, "weather-cache.json");
        public string SettingsPath => Path.Combine(root, "nested", "settings.json");
        public void EnsureDirectory() => Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        public async Task WriteCache(string content)
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(CachePath, content);
        }
        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class SupersedingHandler(string body) : HttpMessageHandler
    {
        private int count;
        public int CallCount => Volatile.Read(ref count);
        public TaskCompletionSource FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref count) == 1)
            {
                FirstRequestStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return Ok(body);
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestStarted.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { CancellationObserved.TrySetResult(); throw; }
            throw new InvalidOperationException("Unreachable");
        }
    }
}
