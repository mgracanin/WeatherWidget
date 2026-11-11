namespace WeatherWidget.Core;

/// <summary>Maps Open-Meteo WMO codes to legacy filename identifiers and localized labels; legacy image files are not bundled.</summary>
public static class ConditionCatalog
{
    public static string Artwork(int code, bool day) => code switch
    {
        0 => day ? "sun.png" : "Moon.png",
        1 or 2 => day ? "partlycloudy.png" : "Moon_Partlycomboclouds.png",
        3 => "cloudy.png",
        45 or 48 => "fog.png",
        51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 => "rain.png",
        56 or 57 or 66 or 67 => "ice.png",
        71 or 73 or 75 or 77 or 85 or 86 => "flurries.png",
        95 => "lightning.png",
        96 or 99 => "hail.png",
        _ => "na.png"
    };

    public static string Description(int code, bool day, string language)
    {
        var hr = language.StartsWith("hr", StringComparison.OrdinalIgnoreCase);
        return code switch
        {
            0 => hr ? (day ? "Vedro" : "Vedra noć") : (day ? "Clear" : "Clear night"),
            1 => hr ? "Pretežno vedro" : "Mainly clear",
            2 => hr ? "Djelomice oblačno" : "Partly cloudy",
            3 => hr ? "Oblačno" : "Overcast",
            45 or 48 => hr ? "Magla" : "Fog",
            51 or 53 or 55 => hr ? "Rosulja" : "Drizzle",
            56 or 57 => hr ? "Ledena rosulja" : "Freezing drizzle",
            61 or 63 or 65 => hr ? "Kiša" : "Rain",
            66 or 67 => hr ? "Ledena kiša" : "Freezing rain",
            71 or 73 or 75 or 77 => hr ? "Snijeg" : "Snow",
            80 or 81 or 82 => hr ? "Pljuskovi" : "Rain showers",
            85 or 86 => hr ? "Snježni pljuskovi" : "Snow showers",
            95 => hr ? "Grmljavina" : "Thunderstorm",
            96 or 99 => hr ? "Grmljavina s tučom" : "Thunderstorm with hail",
            _ => hr ? "Nepoznati uvjeti" : "Unknown conditions"
        };
    }
}
