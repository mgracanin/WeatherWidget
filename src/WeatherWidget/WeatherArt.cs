using System.Windows.Media.Imaging;
using System.Windows.Media;
using WeatherWidget.Core;
namespace WeatherWidget;
internal static class WeatherArt
{
    private static readonly Dictionary<string, BitmapImage> Images = new();
    private static readonly Dictionary<(string Theme,int Family,bool Day,bool Expanded),DrawingImage> VectorImages=new();
    public static ImageSource Background(int code,bool day,bool expanded,string themeId)
    {
        var theme=ThemeCatalog.Get(themeId);
        if(theme.Id is "modern" or "classic") return Background(code,day,expanded);
        int family=NewThemeArtwork.FamilyFor(code);
        var key=(theme.Id,family,day,expanded);
        if(!VectorImages.TryGetValue(key,out var image)) {image=NewThemeArtwork.Create(theme.Id,family,day,expanded);VectorImages.Add(key,image);}
        return image;
    }    public static BitmapImage Background(int code, bool day, bool expanded)
    {
        string path;
        var sky = code switch
        {
            0 => day ? "blue_sun" : "black_moon-full",
            1 or 2 => day ? "blue_partly-cloudy" : "black_moon-full_partly-cloudy",
            3 => "black_cloudy", 45 or 48 => "black_foggy",
            56 or 57 or 66 or 67 or 96 or 99 => "black_hail",
            51 or 53 or 55 or 61 or 63 or 65 => "black_rainy",
            80 or 81 or 82 => "black_few-showers",
            71 or 73 or 75 or 77 or 85 or 86 => "black_snow",
            95 => "black_thunderstorm", _ => "loading"
        };
        if (!expanded && day && code is 0 or 1 or 2)
            path = System.IO.Path.Combine("WinPrognoza", "slike", ConditionCatalog.Artwork(code, day));
        else
        {
            var prefix = expanded ? "undocked" : "docked";
            path = System.IO.Path.Combine("WeatherGadget", sky == "loading" ? prefix + "-loading.png" : prefix + "_" + sky + ".png");
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
