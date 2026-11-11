using System.IO;
using System.Text.Json;
using WeatherWidget;
using WeatherWidget.Core;

internal static class LocationPreferenceChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static Task Run()
    {
        PersistenceAndDraftIsolation();
        InvalidPersistedItemsNormalize();
        TrendClassification();
        return Task.CompletedTask;
    }

    private static void PersistenceAndDraftIsolation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WeatherWidget-Locations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var live = new AppSettings { Left = -120, Top = 80, AlertLocation = "live-location" };
            live.AlertHistory["rain"] = DateTimeOffset.UtcNow;
            var draft = live.CreateDraft();
            Check(SavedLocations.Add(draft.SavedLocations, new WeatherLocation("  Split  ", 43.508, 16.44, "Dalmatia", "Croatia")), "Could not add location to draft.");
            Check(live.SavedLocations.Count == 0, "Draft shares its saved-location list with live settings.");

            live.Left = -240;
            live.Top = 160;
            Check(SettingsChanges.Commit(live, draft, _ => { }, out var error, path, _ => { }), error);
            var loaded = AppSettings.Load(path, _ => { });
            Check(loaded.SavedLocations.Count == 1 && loaded.SavedLocations[0] == new WeatherLocation("Split", 43.508, 16.44, "Dalmatia", "Croatia"), "Saved location did not round-trip.");
            Check(loaded.Left == -240 && loaded.Top == 160 && loaded.AlertLocation == "live-location" && loaded.AlertHistory.ContainsKey("rain"), "Applying saved locations overwrote live placement or alert history.");

            var nextDraft = live.CreateDraft();
            Check(SavedLocations.Remove(nextDraft, 43.508, 16.44), "Could not remove saved location from draft.");
            Check(live.SavedLocations.Count == 1, "Removing from draft mutated live saved locations.");
            Check(SavedLocations.Select(nextDraft, loaded.SavedLocations[0]) && nextDraft.CityName == "Split" && nextDraft.Latitude == 43.508, "Selecting a saved location did not update the draft's current city.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void InvalidPersistedItemsNormalize()
    {
        var longName = new string('x', 140);
        var malformed = new List<WeatherLocation?>
        {
            null,
            new("Bad latitude", double.NaN, 10),
            new("Bad longitude", 10, double.PositiveInfinity),
            new("Out of range", 91, 10),
            new(" ", 10, 10),
            new(" First ", 10, 10),
            new("Duplicate", 10.00005, 10.00005),
            new(longName, 20, 20)
        };
        var normalizedDirectly = SavedLocations.Normalize(malformed);
        Check(normalizedDirectly.Count == 2 && normalizedDirectly[0].Name == "First" && normalizedDirectly[1].Name.Length == 120,
            "Non-finite, out-of-range, blank, or duplicate locations were not filtered by normalization.");

        var persisted = new List<WeatherLocation?>
        {
            null,
            new("Out of range", 91, 10),
            new(" ", 10, 10),
            new(" First ", 10, 10),
            new("Duplicate", 10.00005, 10.00005),
            new(longName, 20, 20)
        };
        for (var i = 0; i < 12; i++) persisted.Add(new($"City {i}", 30 + i, 30 + i));

        var json = JsonSerializer.Serialize(new { SavedLocations = persisted });
        var directory = Path.Combine(Path.GetTempPath(), "WeatherWidget-Locations-Normalize-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        try
        {
            File.WriteAllText(path, json);
            var loaded = AppSettings.Load(path, _ => { });
            Check(loaded.SavedLocations.Count == 10, "Invalid items or the ten-location cap were not normalized.");
            Check(loaded.SavedLocations[0].Name == "First" && loaded.SavedLocations.Count(location => Math.Abs(location.Latitude - 10) < .001) == 1, "Locations were not trimmed or coordinate duplicates were retained.");
            Check(loaded.SavedLocations[1].Name.Length == 120, "Persisted location name was not capped at 120 characters.");
            Check(!SavedLocations.Add(loaded.SavedLocations, new WeatherLocation("Overflow", 80, 80)), "Saved-location helper exceeded the cap.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void TrendClassification()
    {
        var now = DateTimeOffset.UtcNow;
        Check(Trend(now, 5, now.AddHours(3).AddMinutes(10)) == TemperatureTrendDirection.Rising, "Three-hour warming was not classified as rising.");
        Check(Trend(now, -3, now.AddHours(3)) == TemperatureTrendDirection.Falling, "Three-hour cooling was not classified as falling.");
        Check(Trend(now, 0.5, now.AddHours(3)) == TemperatureTrendDirection.Steady, "Sub-threshold change was not classified as steady.");
        Check(Trend(now, 5, now.AddHours(3).AddMinutes(31)) == TemperatureTrendDirection.Unknown, "Forecast outside the target window was used.");
        var stale = Snapshot(now.AddHours(-2), now.AddHours(3), 5);
        Check(TemperatureTrend.Get(stale, now) == TemperatureTrendDirection.Unknown, "Stale snapshot produced a trend.");
        var missing = Snapshot(now, now.AddHours(4), 5);
        Check(TemperatureTrend.Get(missing, now) == TemperatureTrendDirection.Unknown, "Missing future hour produced a trend.");
    }

    private static TemperatureTrendDirection Trend(DateTimeOffset now, double change, DateTimeOffset hourlyTime) =>
        TemperatureTrend.Get(Snapshot(now, hourlyTime, change), now);

    private static WeatherSnapshot Snapshot(DateTimeOffset now, DateTimeOffset hourlyTime, double change) => new(
        new WeatherLocation("Test", 45, 16), now, "UTC",
        new CurrentConditions(now, 10, 10, 50, 10, 0, true),
        new[] { new HourlyForecast(hourlyTime, 10 + change, 0, 0, true) }, Array.Empty<DailyForecast>());
}
