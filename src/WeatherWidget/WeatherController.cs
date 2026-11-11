using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;
using WeatherWidget.Core;

namespace WeatherWidget;

internal sealed class WeatherController : IDisposable
{
    public WeatherClient Client { get; }
    public WeatherSnapshot? Snapshot { get; private set; }
    public bool Offline { get; private set; }
    public string? Error { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;
    private readonly AppSettings settings;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMinutes(15) };
    private readonly Dispatcher dispatcher;
    private CancellationTokenSource? request;
    private int failures;
    private bool disposed;
    private readonly string cachePath;
    private readonly Action<string> log;
    private string CachePath => cachePath;
    public WeatherController(AppSettings settings, Dispatcher dispatcher, WeatherClient? client = null, string? cachePath = null, Action<string>? logger = null)
    {
        this.settings = settings; this.dispatcher = dispatcher;
        Client = client ?? new WeatherClient();
        this.cachePath = cachePath ?? Path.Combine(AppSettings.DataDirectory, "weather-cache.json");
        log = logger ?? AppSettings.Log;
        LoadCache();
        timer.Tick += async (_, _) => await RefreshAsync();
        timer.Start();
    }
    public async Task RefreshAsync(bool changedCity = false)
    {
        if (disposed || (IsLoading && !changedCity)) return;
        if (changedCity) { request?.Cancel(); Snapshot = null; failures = 0; }
        var own = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        request = own; IsLoading = true; Error = null; Changed?.Invoke();
        var location = new WeatherLocation(settings.CityName, settings.Latitude, settings.Longitude);
        try
        {
            var result = await Client.FetchAsync(location, own.Token);
            if (disposed || request != own) return;
            Snapshot = result; Offline = false; Error = null; failures = 0;
            SaveCache(result);
        }
        catch (OperationCanceledException)
        {
            if (disposed || request != own) return;
            Offline = true; Error = UiText.T(settings.Language, "Zahtjev je istekao. Ponovni pokušaj slijedi automatski.", "Request timed out. Retrying automatically."); failures++;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidDataException or ArgumentException or InvalidTimeZoneException)
        {
            if (disposed || request != own) return;
            Offline = true; Error = ex.Message; failures++;
            log("Weather refresh failed: " + ex.Message);
        }
        finally
        {
            if (request == own && !disposed)
            {
                IsLoading = false; request = null;
                timer.Interval = failures == 0 ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(Math.Min(900, 60 * Math.Pow(2, Math.Min(4, failures - 1))));
                timer.Stop(); timer.Start(); Changed?.Invoke();
            }
            own.Dispose();
        }
    }
    public void ScheduleRefresh()
    {
        if (!disposed) dispatcher.BeginInvoke(async () => await RefreshAsync());
    }
    private void LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return;
            var cached = JsonSerializer.Deserialize<WeatherSnapshot>(File.ReadAllText(CachePath));
            if (cached == null || cached.Location == null || cached.Current == null || cached.Hourly == null || cached.Daily == null ||
                cached.Hourly.Any(hour => hour == null) || cached.Daily.Any(day => day == null) ||
                cached.FetchedAtUtc == default || cached.Current.TimeUtc == default || string.IsNullOrWhiteSpace(cached.TimeZoneId) ||
                cached.FetchedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5) ||
                !double.IsFinite(cached.Location.Latitude) || cached.Location.Latitude is < -90 or > 90 ||
                !double.IsFinite(cached.Location.Longitude) || cached.Location.Longitude is < -180 or > 180 ||
                Math.Abs(cached.Location.Latitude - settings.Latitude) > .00001 || Math.Abs(cached.Location.Longitude - settings.Longitude) > .00001) return;
            _ = TimeZoneInfo.FindSystemTimeZoneById(cached.TimeZoneId);
            if (cached.Current.TimeUtc > DateTimeOffset.UtcNow.AddMinutes(5)) return;
            Snapshot = cached; Offline = true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { log("Cache ignored: " + ex.Message); }
    }
    private void SaveCache(WeatherSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(CachePath))!);
            File.WriteAllText(CachePath + ".tmp", JsonSerializer.Serialize(snapshot));
            File.Move(CachePath + ".tmp", CachePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log("Cache save failed: " + ex.Message); }
    }
    public void Dispose()
    {
        disposed = true; timer.Stop(); request?.Cancel(); Client.Dispose();
    }
}
