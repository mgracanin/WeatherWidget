using System.Net;
using System.Text;
using System.Text.Json;
using WeatherWidget.Core;

var checks = new (string Name, Func<Task> Run)[]
{
    ("temperature conversion and formatting", TestTemperature),
    ("freshness boundaries", TestFreshness),
    ("condition artwork and bilingual descriptions", TestConditions),
    ("forecast parsing, UTC epochs, and full forecast ranges", TestFetch),
    ("invalid and truncated provider data", TestInvalidData),
    ("timezone local dates across DST in a negative offset", TestNegativeZoneDates),
    ("cancellation and coordinate validation", TestCancellationAndCoordinates),
    ("captured live Open-Meteo forecast contract", TestLiveFixture),
    ("provider HTTP errors", TestHttpError),
    ("accented city search and language", TestSearch)
};

var failed = 0;
foreach (var check in checks)
{
    try { await check.Run(); Console.WriteLine($"PASS {check.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {check.Name}: {ex.Message}"); }
}
Console.WriteLine($"{checks.Length - failed}/{checks.Length} checks passed");
return failed == 0 ? 0 : 1;

static Task TestTemperature()
{
    Equal(32d, TemperatureFormatter.Convert(0, TemperatureUnit.Fahrenheit));
    Equal(-40d, TemperatureFormatter.Convert(-40, TemperatureUnit.Fahrenheit));
    Equal("68°F", TemperatureFormatter.Format(20, TemperatureUnit.Fahrenheit));
    Equal("20°C", TemperatureFormatter.Format(20, TemperatureUnit.Celsius));
    return Task.CompletedTask;
}

static Task TestFreshness()
{
    var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    Equal(WeatherFreshness.Current, WeatherFreshnessPolicy.Get(now.AddMinutes(-59), now));
    Equal(WeatherFreshness.Stale, WeatherFreshnessPolicy.Get(now.AddHours(-1), now));
    Equal(WeatherFreshness.Expired, WeatherFreshnessPolicy.Get(now.AddHours(-6), now));
    Equal(WeatherFreshness.Current, WeatherFreshnessPolicy.Get(now.AddHours(1), now));
    return Task.CompletedTask;
}

static Task TestConditions()
{
    Equal("sun.png", ConditionCatalog.Artwork(0, true));
    Equal("Moon.png", ConditionCatalog.Artwork(0, false));
    Equal("Moon_Partlycomboclouds.png", ConditionCatalog.Artwork(2, false));
    Equal("lightning.png", ConditionCatalog.Artwork(95, true));
    Equal("na.png", ConditionCatalog.Artwork(999, true));
    Equal("Djelomice oblačno", ConditionCatalog.Description(2, true, "hr"));
    Equal("Partly cloudy", ConditionCatalog.Description(2, true, "en-US"));
    return Task.CompletedTask;
}

static async Task TestFetch()
{
    var now = new DateTimeOffset(2026, 3, 29, 0, 0, 0, TimeSpan.Zero);
    var json = ForecastJson(now, 7, 7);
    var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    using var client = new WeatherClient(new HttpClient(handler));
    var place = new WeatherLocation("Zagreb", 45.815, 15.982, "City of Zagreb", "Croatia");
    var result = await client.FetchAsync(place);
    Equal(place, result.Location);
    Equal("Europe/Zagreb", result.TimeZoneId);
    Equal(now.ToUnixTimeSeconds(), result.Current.TimeUtc.ToUnixTimeSeconds());
    Equal(12.5, result.Current.TemperatureC);
    Equal(7, result.Hourly.Count);
    Equal(7, result.Daily.Count);
    Equal(new DateOnly(2026, 3, 29), result.Daily[0].Date);
    Equal(45, result.Hourly[0].PrecipitationProbability);
    True(result.Current.IsDay);
    var request = handler.LastRequest!;
    True(request.Query.Contains("timeformat=unixtime", StringComparison.Ordinal));
    True(request.Query.Contains("timezone=auto", StringComparison.Ordinal));
    True(request.Query.Contains("forecast_days=5", StringComparison.Ordinal));
}

static async Task TestInvalidData()
{
    foreach (var invalidRoot in new[] { "[]", "null", "\"not an object\"" })
    {
        using var client = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok(invalidRoot))));
        await Throws<InvalidDataException>(() => client.FetchAsync(new WeatherLocation("Zagreb", 45, 16)));
    }

    var missing = "{\"timezone\":\"Europe/Zagreb\"}";
    using (var client = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok(missing)))))
        await Throws<InvalidDataException>(() => client.FetchAsync(new WeatherLocation("Zagreb", 45, 16)));

    var badProbability = ForecastJson(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), 3, 7).Replace("45,45,45", "145,45,45", StringComparison.Ordinal);
    using (var client = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok(badProbability)))))
        await Throws<InvalidDataException>(() => client.FetchAsync(new WeatherLocation("Zagreb", 45, 16)));

    var truncated = ForecastJson(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), 3, 7).Replace("[45,45,45]", "[45,45]", StringComparison.Ordinal);
    using (var client = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok(truncated)))))
        await Throws<InvalidDataException>(() => client.FetchAsync(new WeatherLocation("Zagreb", 45, 16)));

    var valid = ForecastJson(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), 3, 7);
    foreach (var invalid in new[]
    {
        valid.Replace("\"temperature_2m\":12.5", "\"temperature_2m\":null", StringComparison.Ordinal),
        valid.Replace("\"temperature_2m\":12.5", "\"temperature_2m\":\"warm\"", StringComparison.Ordinal),
        valid.Replace("\"temperature_2m\":[12.5,12.5", "\"temperature_2m\":[\"warm\",12.5", StringComparison.Ordinal)
    })
    {
        using var client = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok(invalid))));
        await Throws<InvalidDataException>(() => client.FetchAsync(new WeatherLocation("Zagreb", 45, 16)));
    }
}

static async Task TestNegativeZoneDates()
{
    var start = new DateOnly(2026, 10, 31);
    var json = ForecastJson(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), 3, 7, "America/Los_Angeles", start);
    using var client = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok(json))));
    var result = await client.FetchAsync(new WeatherLocation("Los Angeles", 34.05, -118.25));
    Equal("America/Los_Angeles", result.TimeZoneId);
    for (var i = 0; i < result.Daily.Count; i++) Equal(start.AddDays(i), result.Daily[i].Date);
    Equal(7, result.Daily.Select(item => item.Date).Distinct().Count());
}

static async Task TestCancellationAndCoordinates()
{
    using (var client = new WeatherClient(new HttpClient(new BlockingHandler())))
    using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20)))
        await Throws<OperationCanceledException>(() => client.FetchAsync(new WeatherLocation("Zagreb", 45, 16), cancellation.Token));

    var handler = new FixtureHandler(_ => Ok("{}"));
    using (var client = new WeatherClient(new HttpClient(handler)))
        await Throws<ArgumentOutOfRangeException>(() => client.FetchAsync(new WeatherLocation("invalid", 90.1, 16)));
    Equal(0, handler.RequestCount);

    using (var client = new WeatherClient(new HttpClient(new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) }))))
    using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
        await Throws<OperationCanceledException>(() => client.SearchAsync("Zagreb", "en", cancellation.Token));
}

static async Task TestLiveFixture()
{
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "Zagreb-live.json");
    var json = await File.ReadAllTextAsync(path);
    using var document = JsonDocument.Parse(json);
    var root = document.RootElement;
    var zoneId = root.GetProperty("timezone").GetString()!;
    var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
    var currentEpoch = root.GetProperty("current").GetProperty("time").GetInt64();
    var currentLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(currentEpoch), zone);
    var dailyEpochs = root.GetProperty("daily").GetProperty("time").EnumerateArray().Select(value => value.GetInt64()).ToArray();
    var hourlyEpochs = root.GetProperty("hourly").GetProperty("time").EnumerateArray().Select(value => value.GetInt64()).ToArray();
    var handler = new FixtureHandler(_ => Ok(json));
    using var client = new WeatherClient(new HttpClient(handler));
    var result = await client.FetchAsync(new WeatherLocation("Zagreb", 45.815, 15.982, null, "Croatia"));

    Equal(DateTimeOffset.FromUnixTimeSeconds(currentEpoch), result.Current.TimeUtc);
    Equal(currentLocal.Date, TimeZoneInfo.ConvertTime(result.Current.TimeUtc, TimeZoneInfo.FindSystemTimeZoneById(result.TimeZoneId)).Date);
    Equal(hourlyEpochs.Length, result.Hourly.Count);
    Equal(dailyEpochs.Length, result.Daily.Count);
    Equal(DateOnly.FromDateTime(currentLocal.DateTime), result.Daily[0].Date);
    var hourFloorUtc = new DateTimeOffset(result.Current.TimeUtc.UtcDateTime.Date.AddHours(result.Current.TimeUtc.UtcDateTime.Hour), TimeSpan.Zero);
    True(result.Hourly.Any(hour => hour.TimeUtc == hourFloorUtc));
    for (var i = 0; i < dailyEpochs.Length; i++)
    {
        var expectedDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(dailyEpochs[i]), zone).DateTime);
        Equal(expectedDate, result.Daily[i].Date);
    }
}

static async Task TestHttpError()
{
    using var client = new WeatherClient(new HttpClient(new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("maintenance") })));
    var error = await Throws<HttpRequestException>(() => client.FetchAsync(new WeatherLocation("Zagreb", 45, 16)));
    Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
    True(error.Message.Contains("maintenance", StringComparison.Ordinal));
}

static async Task TestSearch()
{
    var handler = new FixtureHandler(request =>
    {
        if (!request.Query.Contains("name=Zagreb", StringComparison.Ordinal) || !request.Query.Contains("language=hr", StringComparison.Ordinal)) throw new Exception("Search query or language was not encoded into the request.");
        return Ok("{\"results\":[{\"name\":\"Čakovec\",\"latitude\":46.384,\"longitude\":16.433,\"admin1\":\"Međimurje\",\"country\":\"Hrvatska\"}]}");
    });
    using var client = new WeatherClient(new HttpClient(handler));
    var places = await client.SearchAsync("Zagreb", "hr");
    Equal(1, places.Count);
    Equal("Čakovec", places[0].Name);
    Equal("Međimurje", places[0].Region);
    Equal("Hrvatska", places[0].Country);

    const string validResult = "{\"name\":\"Zagreb\",\"latitude\":45.815,\"longitude\":15.982}";
    using (var mixedClient = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok("{\"results\":[null,1,\"bad\",true," + validResult + "]}")))))
    {
        var mixed = await mixedClient.SearchAsync("Zagreb", "en");
        Equal(1, mixed.Count);
        Equal("Zagreb", mixed[0].Name);
    }

    foreach (var invalidRoot in new[] { "[]", "null", "\"not an object\"" })
    {
        using var invalidClient = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok(invalidRoot))));
        await Throws<InvalidDataException>(() => invalidClient.SearchAsync("Zagreb", "en"));
    }

    using (var nullResultsClient = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok("{\"results\":null}")))))
        Equal(0, (await nullResultsClient.SearchAsync("Zagreb", "en")).Count);

    using (var invalidCoordinatesClient = new WeatherClient(new HttpClient(new FixtureHandler(_ => Ok("{\"results\":[{\"name\":\"Bad\",\"latitude\":91,\"longitude\":16}]}")))))
        await Throws<InvalidDataException>(() => invalidCoordinatesClient.SearchAsync("Zagreb", "en"));
}

static string ForecastJson(DateTimeOffset epoch, int hourlyCount, int dailyCount, string timeZone = "Europe/Zagreb", DateOnly? firstDay = null)
{
    var times = Enumerable.Range(0, hourlyCount).Select(i => epoch.AddHours(i).ToUnixTimeSeconds()).ToArray();
    var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
    var firstDate = firstDay ?? new DateOnly(2026, 3, 29);
    var dailyTimes = Enumerable.Range(0, dailyCount).Select(i =>
    {
        var localMidnight = DateTime.SpecifyKind(firstDate.AddDays(i).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, zone), TimeSpan.Zero).ToUnixTimeSeconds();
    }).ToArray();
    static string Arr<T>(IEnumerable<T> values) => JsonSerializer.Serialize(values);
    return "{" +
        $"\"timezone\":\"{timeZone}\",\"current\":{{" +
        $"\"time\":{epoch.ToUnixTimeSeconds()},\"temperature_2m\":12.5,\"apparent_temperature\":11.1,\"relative_humidity_2m\":55,\"wind_speed_10m\":8.3,\"weather_code\":2,\"is_day\":1" + "}," +
        "\"hourly\":{" +
        $"\"time\":{Arr(times)},\"temperature_2m\":{Arr(Enumerable.Repeat(12.5, hourlyCount))},\"precipitation_probability\":{Arr(Enumerable.Repeat(45, hourlyCount))},\"weather_code\":{Arr(Enumerable.Repeat(2, hourlyCount))},\"is_day\":{Arr(Enumerable.Repeat(1, hourlyCount))}" + "}," +
        "\"daily\":{" +
        $"\"time\":{Arr(dailyTimes)},\"temperature_2m_max\":{Arr(Enumerable.Repeat(16.0, dailyCount))},\"temperature_2m_min\":{Arr(Enumerable.Repeat(8.0, dailyCount))},\"weather_code\":{Arr(Enumerable.Repeat(3, dailyCount))},\"precipitation_probability_max\":{Arr(Enumerable.Repeat(20, dailyCount))}" + "}}";
}

static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
static void True(bool condition) { if (!condition) throw new Exception("Assertion failed: expected true."); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected '{expected}', got '{actual}'."); }
static async Task<TException> Throws<TException>(Func<Task> action) where TException : Exception
{
    try { await action(); } catch (TException ex) { return ex; }
    throw new Exception($"Expected {typeof(TException).Name}.");
}

sealed class FixtureHandler(Func<Uri, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    public Uri? LastRequest { get; private set; }
    public int RequestCount { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequest = request.RequestUri;
        return Task.FromResult(responseFactory(request.RequestUri!));
    }
}

sealed class BlockingHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Unreachable");
    }
}

sealed class StallingStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return 0;
    }
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return 0;
    }
}

static class UriExtensions
{
    public static string Query(this Uri uri) => uri.Query.TrimStart('?');
}
