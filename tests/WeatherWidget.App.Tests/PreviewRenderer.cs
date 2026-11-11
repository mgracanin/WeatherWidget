using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WeatherWidget;
using WeatherWidget.Core;

internal static class PreviewRenderer
{
    private sealed record PreviewCase(string Name, string Theme, string City, double TemperatureC, int Code, bool IsDay);

    public static async Task<int> RenderAsync(string providerFixture)
    {
        var repositoryRoot = FindRepositoryRoot();
        var outputDirectory = Path.Combine(repositoryRoot, "artifacts", "previews");
        Directory.CreateDirectory(outputDirectory);

        var seed = await LoadFixtureAsync(providerFixture);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var cases = new[]
        {
            new PreviewCase("modern-rain", "modern", "Zagreb", 12.4, 61, true),
            new PreviewCase("classic-sun", "classic", "Split", 25.6, 0, true),
            new PreviewCase("modern-night", "modern", "Rijeka", 5.1, 2, false),
            new PreviewCase("modern-long-city-negative", "modern", "Gornji Grad – Medveščak i okolica (iznimno dugo ime)", -12.8, 71, false)
        };

        foreach (var previewCase in cases)
        {
            var snapshot = MakeSnapshot(seed, previewCase);
            await RenderWidgetAsync(app, previewCase, snapshot, outputDirectory);
        }

        await RenderForecastAsync(MakeSnapshot(seed, cases[0]), "hourly-rain", showDaily: false, outputDirectory);
        await RenderForecastAsync(MakeSnapshot(seed, cases[1]), "daily-sun", showDaily: true, outputDirectory);

        Console.WriteLine($"Rendered fixture previews to {outputDirectory}");
        return 0;
    }

    public static async Task VerifyCompactCardLayoutAsync(string providerFixture)
    {
        var seed = await LoadFixtureAsync(providerFixture);
        var fixtureCase = new PreviewCase("cloudy", "modern", "Zagreb", 12.4, 3, false);
        var snapshot = MakeSnapshot(seed, fixtureCase);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        foreach (var theme in new[] { "modern", "classic" })
        {
            var settings = new AppSettings
            {
                CityName = "Zagreb", Latitude = snapshot.Location.Latitude, Longitude = snapshot.Location.Longitude,
                Theme = theme, Scale = 1, Language = "en"
            };
            using var workspace = new PreviewWorkspace();
            var cachePath = await workspace.WriteCacheAsync(snapshot);
            using var http = new HttpClient(new NoNetworkHandler());
            using var client = new WeatherClient(http);
            var controller = new WeatherController(settings, Dispatcher.CurrentDispatcher, client, cachePath, _ => { });
            WidgetWindow? window = null;
            try
            {
                SetAppState(app, settings, controller);
                window = new WidgetWindow(app);
                Equal(130d, window.Width);
                Equal(67d, window.Height);
                var viewbox = (Viewbox)window.Content;
                var card = (Canvas)viewbox.Child;
                Equal(130d, card.Width);
                Equal(67d, card.Height);
                var art = card.Children.OfType<Image>().Single();
                Equal("docked_black_cloudy.png", Path.GetFileName(((BitmapImage)art.Source!).UriSource.LocalPath));
                Equal(130, ((BitmapSource)art.Source).PixelWidth);
                Equal(67, ((BitmapSource)art.Source).PixelHeight);
            }
            finally
            {
                window?.Close();
                controller.Dispose();
            }
        }
    }

    private static async Task<WeatherSnapshot> LoadFixtureAsync(string providerFixture)
    {
        using var http = new HttpClient(new FixtureHandler(providerFixture));
        using var client = new WeatherClient(http);
        return await client.FetchAsync(new WeatherLocation("Zagreb", 45.815, 15.982, "City of Zagreb", "Croatia"));
    }

    private static WeatherSnapshot MakeSnapshot(WeatherSnapshot seed, PreviewCase previewCase)
    {
        var now = DateTimeOffset.UtcNow;
        var hourFloor = new DateTimeOffset(now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day,
            now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(seed.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var location = seed.Location with { Name = previewCase.City };
        var current = seed.Current with
        {
            TimeUtc = now,
            TemperatureC = previewCase.TemperatureC,
            FeelsLikeC = previewCase.TemperatureC - 1.2,
            WeatherCode = previewCase.Code,
            IsDay = previewCase.IsDay
        };
        var hourly = Enumerable.Range(0, 48).Select(index => seed.Hourly[index % seed.Hourly.Count] with
        {
            TimeUtc = hourFloor.AddHours(index - 1),
            TemperatureC = previewCase.TemperatureC + (index / 6d),
            WeatherCode = previewCase.Code,
            IsDay = previewCase.IsDay,
            PrecipitationProbability = previewCase.Code is 51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 ? 72 : seed.Hourly[index % seed.Hourly.Count].PrecipitationProbability
        }).ToArray();
        var daily = Enumerable.Range(0, 7).Select(index => seed.Daily[index % seed.Daily.Count] with
        {
            Date = today.AddDays(index),
            HighC = previewCase.TemperatureC + 4 + index,
            LowC = previewCase.TemperatureC - 3 - index,
            WeatherCode = previewCase.Code,
            PrecipitationProbability = index == 0 ? 72 : seed.Daily[index % seed.Daily.Count].PrecipitationProbability
        }).ToArray();
        return seed with { Location = location, FetchedAtUtc = now, Current = current, Hourly = hourly, Daily = daily };
    }

    private static async Task RenderWidgetAsync(App app, PreviewCase previewCase, WeatherSnapshot snapshot, string outputDirectory)
    {
        var settings = new AppSettings
        {
            CityName = previewCase.City,
            Latitude = snapshot.Location.Latitude,
            Longitude = snapshot.Location.Longitude,
            Theme = previewCase.Theme,
            Scale = 1,
            TemperatureUnit = "celsius",
            WindUnit = "kmh",
            Language = "en"
        };
        using var workspace = new PreviewWorkspace();
        var cachePath = await workspace.WriteCacheAsync(snapshot);
        using var http = new HttpClient(new NoNetworkHandler());
        using var client = new WeatherClient(http);
        var controller = new WeatherController(settings, Dispatcher.CurrentDispatcher, client, cachePath, _ => { });
        WidgetWindow? window = null;
        try
        {
            SetAppState(app, settings, controller);
            window = new WidgetWindow(app);
            window.UpdateWeatherPresentation();
            RenderContent(window.Content, window.Width, window.Height,
                Path.Combine(outputDirectory, $"fixture-widget-{previewCase.Name}.png"));
        }
        finally
        {
            window?.Close();
            controller.Dispose();
        }
    }

    private static async Task RenderForecastAsync(WeatherSnapshot snapshot, string name, bool showDaily, string outputDirectory)
    {
        var window = new ForecastWindow();
        try
        {
            window.Update(snapshot, "en", "F", "mph", isOffline: true, lastError: "Fixture preview; no network request.");
            if (showDaily)
            {
                var button = Descendants<Button>((DependencyObject)window.Content!).FirstOrDefault(candidate =>
                    AutomationProperties.GetName(candidate).Contains("daily", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Content?.ToString()?.Contains("5", StringComparison.Ordinal) == true)
                    ?? throw new InvalidOperationException("Could not find the daily forecast control for the preview.");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
            }
            RenderContent(window.Content, window.Width, window.Height,
                Path.Combine(outputDirectory, $"fixture-forecast-{name}.png"));
        }
        finally { window.RequestClose(); }
    }

    private static void SetAppState(App app, AppSettings settings, WeatherController controller)
    {
        typeof(App).GetProperty(nameof(App.Settings), BindingFlags.Instance | BindingFlags.Public)!.SetValue(app, settings);
        typeof(App).GetProperty("Weather", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, controller);
    }

    private static void RenderContent(object? content, double logicalWidth, double logicalHeight, string outputPath)
    {
        if (content is not FrameworkElement visual) throw new InvalidOperationException("The WPF window has no renderable content.");
        if (!double.IsFinite(logicalWidth) || logicalWidth <= 0 || !double.IsFinite(logicalHeight) || logicalHeight <= 0)
            throw new InvalidOperationException("The WPF window has invalid logical dimensions.");
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(logicalWidth));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(logicalHeight));
        visual.Measure(new Size(logicalWidth, logicalHeight));
        visual.Arrange(new Rect(0, 0, logicalWidth, logicalHeight));
        visual.UpdateLayout();

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
        Console.WriteLine($"{Path.GetFileName(outputPath)} — {pixelWidth}×{pixelHeight} px (fixture)");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T result) yield return result;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, index)))
            yield return child;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets")) && File.Exists(Path.Combine(directory.FullName, "tasks", "plan.md")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the WeatherWidget repository root.");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }

    private sealed class PreviewWorkspace : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "WeatherWidget.Previews-" + Guid.NewGuid().ToString("N"));
        public async Task<string> WriteCacheAsync(WeatherSnapshot snapshot)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "weather-cache.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(snapshot));
            return path;
        }
        public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private sealed class FixtureHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private sealed class NoNetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Preview controller must use only the captured cache and must not send a request.");
    }
}
