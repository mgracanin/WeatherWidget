using WeatherWidget.Core;

namespace WeatherWidget;

internal static class SavedLocations
{
    internal const int MaximumCount = 10;
    private const double CoordinateTolerance = 0.0001;

    internal static List<WeatherLocation> Normalize(IEnumerable<WeatherLocation?>? locations)
    {
        var result = new List<WeatherLocation>(MaximumCount);
        if (locations is null) return result;

        foreach (var location in locations)
        {
            if (location is null || !ValidCoordinates(location.Latitude, location.Longitude)) continue;
            var name = location.Name?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (result.Any(existing => SameCoordinates(existing, location))) continue;
            result.Add(location with { Name = name[..Math.Min(name.Length, 120)] });
            if (result.Count == MaximumCount) break;
        }
        return result;
    }

    internal static bool Add(List<WeatherLocation> locations, WeatherLocation location)
    {
        var normalized = Normalize(new WeatherLocation?[] { location }).FirstOrDefault();
        if (normalized is null || locations.Any(existing => SameCoordinates(existing, normalized)) ||
            locations.Count >= MaximumCount) return false;
        locations.Add(normalized);
        return true;
    }

    internal static bool Same(WeatherLocation location, double latitude, double longitude) =>
        Math.Abs(location.Latitude - latitude) <= CoordinateTolerance &&
        Math.Abs(location.Longitude - longitude) <= CoordinateTolerance;

    internal static bool Remove(AppSettings settings, double latitude, double longitude)
    {
        var index = settings.SavedLocations.FindIndex(location =>
            Same(location, latitude, longitude));
        if (index < 0) return false;
        settings.SavedLocations.RemoveAt(index);
        return true;
    }

    internal static bool Select(AppSettings settings, WeatherLocation location)
    {
        if (!ValidCoordinates(location.Latitude, location.Longitude) || string.IsNullOrWhiteSpace(location.Name)) return false;
        settings.CityName = location.Name.Trim()[..Math.Min(location.Name.Trim().Length, 120)];
        settings.Latitude = location.Latitude;
        settings.Longitude = location.Longitude;
        return true;
    }

    private static bool ValidCoordinates(double latitude, double longitude) =>
        double.IsFinite(latitude) && latitude is >= -90 and <= 90 &&
        double.IsFinite(longitude) && longitude is >= -180 and <= 180;

    private static bool SameCoordinates(WeatherLocation first, WeatherLocation second) =>
        Math.Abs(first.Latitude - second.Latitude) <= CoordinateTolerance &&
        Math.Abs(first.Longitude - second.Longitude) <= CoordinateTolerance;
}

internal enum TemperatureTrendDirection { Unknown, Rising, Falling, Steady }

internal static class TemperatureTrend
{
    internal static TemperatureTrendDirection Get(WeatherSnapshot snapshot, DateTimeOffset now)
    {
        if (!WeatherExtras.IsFresh(snapshot, now) || !double.IsFinite(snapshot.Current.TemperatureC))
            return TemperatureTrendDirection.Unknown;

        var target = now.AddHours(3);
        HourlyForecast? nearest = null;
        var nearestDistance = TimeSpan.MaxValue;
        for (int index=0;index<snapshot.Hourly.Count;index++)
        {
            var hour=snapshot.Hourly[index];
            var distance = (hour.TimeUtc - target).Duration();
            if (distance < nearestDistance)
            {
                nearest = hour;
                nearestDistance = distance;
            }
        }

        if (nearest is null || nearestDistance > TimeSpan.FromMinutes(30) || !double.IsFinite(nearest.TemperatureC))
            return TemperatureTrendDirection.Unknown;
        var change = nearest.TemperatureC - snapshot.Current.TemperatureC;
        if (change >= 1) return TemperatureTrendDirection.Rising;
        if (change <= -1) return TemperatureTrendDirection.Falling;
        return TemperatureTrendDirection.Steady;
    }
}
