using System.Globalization;
using System.IO;
using System.Text.Json;
namespace WeatherWidget;
public sealed class AppSettings
{
    public string CityName { get; set; } = "Zagreb";
    public double Latitude { get; set; } = 45.81444;
    public double Longitude { get; set; } = 15.97798;
    public string TemperatureUnit { get; set; } = "celsius";
    public string WindUnit { get; set; } = "kmh";
    public string Theme { get; set; } = "modern";
    public double Scale { get; set; } = 1;
    public double DetailScale { get; set; } = 1;
    public string Language { get; set; } = "system";
    public bool Locked { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool StartWithWindows { get; set; }
    // Physical screen pixels; negative origins are valid.
    public double? Left { get; set; }
    public double? Top { get; set; }
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeatherWidget");
    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static AppSettings Load(string? path = null, Action<string>? logger = null)
    {
        var settingsPath = path ?? SettingsPath;
        try
        {
            if (!File.Exists(settingsPath)) return new();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath)) ?? new();
            if (settings.Left is { } x && (!double.IsFinite(x) || Math.Abs(x) > 100000)) settings.Left = null;
            if (settings.Top is { } y && (!double.IsFinite(y) || Math.Abs(y) > 100000)) settings.Top = null;
            if (!double.IsFinite(settings.Scale) || settings.Scale < .5 || settings.Scale > 3) settings.Scale = 1;
            if (!double.IsFinite(settings.DetailScale) || settings.DetailScale < .8 || settings.DetailScale > 2) settings.DetailScale = 1;
            settings.Language = settings.Language is "hr" or "en" or "system" ? settings.Language : "system";
            settings.TemperatureUnit = settings.TemperatureUnit is "fahrenheit" or "F" ? "fahrenheit" : "celsius";
            settings.WindUnit = settings.WindUnit switch { "ms" or "m/s" => "ms", "mph" => "mph", _ => "kmh" };
            settings.Theme = settings.Theme == "classic" ? "classic" : "modern";
            settings.CityName = string.IsNullOrWhiteSpace(settings.CityName) ? "Zagreb" : settings.CityName.Trim()[..Math.Min(120, settings.CityName.Trim().Length)];
            if (!double.IsFinite(settings.Latitude) || settings.Latitude is < -90 or > 90 ||
                !double.IsFinite(settings.Longitude) || settings.Longitude is < -180 or > 180)
            { settings.Latitude=45.81444; settings.Longitude=15.97798; settings.CityName="Zagreb"; }
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { (logger ?? Log)("Settings load failed; using defaults: " + ex.Message); return new(); }
    }
    public void Save(string? path = null, Action<string>? logger = null)
    {
        var settingsPath = path ?? SettingsPath;
        var temporary = settingsPath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, settingsPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { (logger ?? Log)("Settings save failed: " + ex.Message); }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { (logger ?? Log)("Settings temporary-file cleanup failed: " + ex.Message); }
        }
    }
    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var path = Path.Combine(DataDirectory, "shell.log");
            if (File.Exists(path) && new FileInfo(path).Length > 512000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}

