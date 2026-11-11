using System.Windows.Media.Imaging;
using WeatherWidget.Core;
namespace WeatherWidget;
internal static class WeatherArt
{
    private static readonly Dictionary<string, BitmapImage> Images = new();
    public static BitmapImage Background(int code, bool day, bool expanded)
    {
        string path;
        if (!expanded) path = System.IO.Path.Combine("WinPrognoza", "slike", ConditionCatalog.Artwork(code, day));
        else
        {
            var sky = code switch
            {
                0 => day ? "blue_sun" : "black_moon-full",
                1 or 2 => day ? "blue_partly-cloudy" : "black_moon-full_partly-cloudy",
                3 => day ? "gray_cloudy" : "black_cloudy",
                45 or 48 => day ? "gray_foggy" : "black_foggy",
                56 or 57 or 66 or 67 or 96 or 99 => day ? "gray_hail" : "black_hail",
                51 or 53 or 55 or 61 or 63 or 65 => day ? "gray_rainy" : "black_rainy",
                80 or 81 or 82 => day ? "gray_few-showers" : "black_few-showers",
                71 or 73 or 75 or 77 or 85 or 86 => day ? "blue_snow" : "black_snow",
                95 => day ? "gray_thunderstorm" : "black_thunderstorm",
                _ => "loading"
            };
            path = System.IO.Path.Combine("WeatherGadget", sky == "loading" ? "undocked-loading.png" : "undocked_" + sky + ".png");
        }
        if (!Images.TryGetValue(path, out var image))
        {
            image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", path));
            image.EndInit(); image.Freeze(); Images[path] = image;
        }
        return image;
    }
}
