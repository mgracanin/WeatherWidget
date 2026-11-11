using System.Globalization;
using System.Net;
using System.Text.Json;

namespace WeatherWidget.Core;

/// <summary>Open-Meteo adapter. Hourly contains the provider's complete returned range; consumers select the next 12 entries from the current local hour.</summary>
public sealed class WeatherClient : IDisposable
{
    private static readonly Uri ForecastEndpoint = new("https://api.open-meteo.com/v1/forecast");
    private static readonly Uri GeocodingEndpoint = new("https://geocoding-api.open-meteo.com/v1/search");
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public WeatherClient() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }, ownsHttp: true) { }

    public WeatherClient(HttpClient httpClient) : this(httpClient, ownsHttp: false) { }

    private WeatherClient(HttpClient httpClient, bool ownsHttp)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttp = ownsHttp;
    }

    public async Task<WeatherSnapshot> FetchAsync(WeatherLocation location, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        ValidateCoordinates(location.Latitude, location.Longitude);

        var url = ForecastEndpoint + "?" + string.Join("&", new Dictionary<string, string>
        {
            ["latitude"] = location.Latitude.ToString("R", CultureInfo.InvariantCulture),
            ["longitude"] = location.Longitude.ToString("R", CultureInfo.InvariantCulture),
            ["current"] = "temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code,is_day",
            ["hourly"] = "temperature_2m,precipitation_probability,weather_code,is_day",
            ["daily"] = "temperature_2m_max,temperature_2m_min,weather_code,precipitation_probability_max",
            ["forecast_days"] = "5",
            ["timeformat"] = "unixtime",
            ["timezone"] = "auto"
        }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = RequiredRootObject(document.RootElement, "forecast");
        var timeZone = RequiredString(root, "timezone");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (TimeZoneNotFoundException ex) { throw new InvalidDataException($"Open-Meteo returned unknown time zone '{timeZone}'.", ex); }

        var current = RequiredObject(root, "current");
        var currentConditions = new CurrentConditions(
            UnixSeconds(RequiredInt64(current, "time")), RequiredDouble(current, "temperature_2m"), RequiredDouble(current, "apparent_temperature"),
            RequiredInt(current, "relative_humidity_2m"), RequiredDouble(current, "wind_speed_10m"), RequiredInt(current, "weather_code"), RequiredBool(current, "is_day"));
        ValidateProbability(currentConditions.Humidity, "current.relative_humidity_2m");

        var hourly = RequiredObject(root, "hourly");
        var hourlyTimes = RequiredInt64Array(hourly, "time");
        var hourlyTemps = RequiredDoubleArray(hourly, "temperature_2m");
        var hourlyPrecip = RequiredIntArray(hourly, "precipitation_probability");
        var hourlyCodes = RequiredIntArray(hourly, "weather_code");
        var hourlyDay = RequiredIntArray(hourly, "is_day");
        RequireEqualLengths("hourly", hourlyTimes.Length, hourlyTemps.Length, hourlyPrecip.Length, hourlyCodes.Length, hourlyDay.Length);
        var hourlyForecast = new HourlyForecast[hourlyTimes.Length];
        for (var i = 0; i < hourlyTimes.Length; i++)
        {
            ValidateProbability(hourlyPrecip[i], $"hourly.precipitation_probability[{i}]");
            if (hourlyDay[i] is not (0 or 1)) throw new InvalidDataException($"hourly.is_day[{i}] must be 0 or 1.");
            hourlyForecast[i] = new HourlyForecast(UnixSeconds(hourlyTimes[i]), hourlyTemps[i], hourlyPrecip[i], hourlyCodes[i], hourlyDay[i] == 1);
        }
        if (hourlyForecast.Length == 0) throw new InvalidDataException("Open-Meteo returned no hourly forecast entries.");

        var daily = RequiredObject(root, "daily");
        var dailyTimes = RequiredInt64Array(daily, "time");
        var dailyHigh = RequiredDoubleArray(daily, "temperature_2m_max");
        var dailyLow = RequiredDoubleArray(daily, "temperature_2m_min");
        var dailyCodes = RequiredIntArray(daily, "weather_code");
        var dailyPrecip = RequiredIntArray(daily, "precipitation_probability_max");
        RequireEqualLengths("daily", dailyTimes.Length, dailyHigh.Length, dailyLow.Length, dailyCodes.Length, dailyPrecip.Length);
        if (dailyTimes.Length < 5) throw new InvalidDataException("Open-Meteo returned fewer than five daily forecast entries.");
        var dailyForecast = new DailyForecast[dailyTimes.Length];
        for (var i = 0; i < dailyForecast.Length; i++)
        {
            ValidateProbability(dailyPrecip[i], $"daily.precipitation_probability_max[{i}]");
            var localDate = TimeZoneInfo.ConvertTime(UnixSeconds(dailyTimes[i]), zone);
            dailyForecast[i] = new DailyForecast(DateOnly.FromDateTime(localDate.DateTime), dailyHigh[i], dailyLow[i], dailyCodes[i], dailyPrecip[i]);
        }

        return new WeatherSnapshot(location, DateTimeOffset.UtcNow, timeZone, currentConditions, hourlyForecast, dailyForecast);
    }

    public async Task<IReadOnlyList<WeatherLocation>> SearchAsync(string query, string language, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<WeatherLocation>();
        var url = GeocodingEndpoint + "?" + string.Join("&", new Dictionary<string, string>
        {
            ["name"] = query.Trim(), ["count"] = "10", ["language"] = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim(), ["format"] = "json"
        }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = RequiredRootObject(document.RootElement, "geocoding");
        if (!TryGetProperty(root, "results", out var results) || results.ValueKind == JsonValueKind.Null) return Array.Empty<WeatherLocation>();
        if (results.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Open-Meteo geocoding results must be an array.");
        var found = new List<WeatherLocation>();
        foreach (var result in results.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object) continue;
            var name = RequiredString(result, "name");
            var latitude = RequiredDouble(result, "latitude");
            var longitude = RequiredDouble(result, "longitude");
            ValidateProviderCoordinates(latitude, longitude);
            found.Add(new WeatherLocation(name, latitude, longitude, OptionalString(result, "admin1"), OptionalString(result, "country")));
        }
        return found;
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var excerpt = body.Length <= 300 ? body : body[..300];
        throw new HttpRequestException($"Open-Meteo returned {(int)response.StatusCode} ({response.StatusCode}): {excerpt}", null, response.StatusCode);
    }

    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
    }

    private static void ValidateProviderCoordinates(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 || !double.IsFinite(longitude) || longitude is < -180 or > 180)
            throw new InvalidDataException("Open-Meteo returned coordinates outside the valid latitude/longitude range.");
    }

    private static void ValidateProbability(int value, string field)
    {
        if (value is < 0 or > 100) throw new InvalidDataException($"{field} must be between 0 and 100.");
    }

    private static DateTimeOffset UnixSeconds(long value)
    {
        try { return DateTimeOffset.FromUnixTimeSeconds(value); }
        catch (ArgumentOutOfRangeException ex) { throw new InvalidDataException("Open-Meteo returned an invalid Unix timestamp.", ex); }
    }

    private static JsonElement RequiredObject(JsonElement element, string property)
    {
        if (!TryGetProperty(element, property, out var value) || value.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"Open-Meteo response is missing object '{property}'.");
        return value;
    }
    private static JsonElement RequiredRootObject(JsonElement root, string endpoint) => root.ValueKind == JsonValueKind.Object
        ? root
        : throw new InvalidDataException($"Open-Meteo {endpoint} response must be a JSON object.");
    private static bool TryGetProperty(JsonElement element, string property, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object) return element.TryGetProperty(property, out value);
        value = default;
        return false;
    }
    private static string RequiredString(JsonElement element, string property) => TryGetProperty(element, property, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new InvalidDataException($"Open-Meteo response is missing string '{property}'.");
    private static string? OptionalString(JsonElement element, string property) => TryGetProperty(element, property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static double RequiredDouble(JsonElement element, string property) => TryGetProperty(element, property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : throw new InvalidDataException($"Open-Meteo response is missing numeric '{property}'.");
    private static int RequiredInt(JsonElement element, string property) => TryGetProperty(element, property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : throw new InvalidDataException($"Open-Meteo response is missing integer '{property}'.");
    private static long RequiredInt64(JsonElement element, string property) => TryGetProperty(element, property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : throw new InvalidDataException($"Open-Meteo response is missing integer '{property}'.");
    private static bool RequiredBool(JsonElement element, string property) => RequiredInt(element, property) switch { 0 => false, 1 => true, _ => throw new InvalidDataException($"Open-Meteo response field '{property}' must be 0 or 1.") };
    private static double[] RequiredDoubleArray(JsonElement element, string property) => GetArray(element, property, static item => item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var value) && double.IsFinite(value) ? value : throw new InvalidDataException("Expected finite numeric array item."));
    private static int[] RequiredIntArray(JsonElement element, string property) => GetArray(element, property, static item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value) ? value : throw new InvalidDataException("Expected integer array item."));
    private static long[] RequiredInt64Array(JsonElement element, string property) => GetArray(element, property, static item => item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var value) ? value : throw new InvalidDataException("Expected integer timestamp array item."));
    private static T[] GetArray<T>(JsonElement element, string property, Func<JsonElement, T> convert)
    {
        if (!TryGetProperty(element, property, out var value) || value.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"Open-Meteo response is missing array '{property}'.");
        return value.EnumerateArray().Select(convert).ToArray();
    }
    private static void RequireEqualLengths(string name, params int[] lengths)
    {
        if (lengths.Any(length => length != lengths[0])) throw new InvalidDataException($"Open-Meteo {name} arrays have different lengths: {string.Join(", ", lengths)}.");
    }
}
