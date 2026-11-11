using System.Windows;
using System.Windows.Media;
namespace WeatherWidget;
internal static class WeatherArt
{
    private static readonly Dictionary<(string Theme,int Family,bool Day,bool Expanded),DrawingImage> Vectors=new();
    private static readonly Dictionary<(string Root,string Name,bool Expanded),ImageSource?> Legacy=new();
    internal static int Generation {get;private set;}
    internal static void ReloadLegacy(){Legacy.Clear();Generation++;}
    public static ImageSource Background(int code,bool day,bool expanded)=>Background(code,day,expanded,"modern");
    public static ImageSource Background(int code,bool day,bool expanded,string themeId)
    {
        var theme=ThemeCatalog.Get(themeId);
        if(theme.Id.StartsWith("legacy-",StringComparison.Ordinal))
        {
            var imported=Imported(code,day,expanded);
            if(imported!=null)return imported;
            theme=ThemeCatalog.Get(theme.Id=="legacy-classic"?"classic":"modern");
        }
        int family=NewThemeArtwork.FamilyFor(code);var key=(theme.Id,family,day,expanded);
        if(!Vectors.TryGetValue(key,out var image)){image=NewThemeArtwork.Create(theme.Id,family,day,expanded);Vectors.Add(key,image);}
        return image;
    }
    private static ImageSource? Imported(int code,bool day,bool expanded)
    {
        string sky=code switch {0=>day?"blue_sun":"black_moon-full",1 or 2=>day?"blue_partly-cloudy":"black_moon-full_partly-cloudy",3=>"black_cloudy",45 or 48=>"black_foggy",56 or 57 or 66 or 67 or 96 or 99=>"black_hail",51 or 53 or 55 or 61 or 63 or 65=>"black_rainy",80 or 81 or 82=>"black_few-showers",71 or 73 or 75 or 77 or 85 or 86=>"black_snow",95=>"black_thunderstorm",_=>"loading"};
        var prefix=expanded?"undocked":"docked";
        var name=sky=="loading"?prefix+"-loading.png":prefix+"_"+sky+".png";
        var candidates=!expanded&&day&&code is 0 or 1 or 2 ? new[]{name,code==0?"sun.png":"partlycloudy.png","undocked_"+sky+".png"}:new[]{name};
        foreach(var candidate in candidates)
        {
            var key=(LegacyArtwork.Root,candidate,expanded);
            if(!Legacy.TryGetValue(key,out var image))
            {
                var bitmap=LegacyArtwork.Load(candidate);
                if(bitmap==null)image=null;
                else
                {
                    double w=expanded?264:130,h=expanded?194:67;
                    var drawing=new DrawingGroup{ClipGeometry=new RectangleGeometry(new Rect(0,0,w,h),expanded?12:8,expanded?12:8)};
                    using(var dc=drawing.Open()){double scale=Math.Max(w/bitmap.PixelWidth,h/bitmap.PixelHeight);dc.DrawImage(bitmap,new Rect((w-bitmap.PixelWidth*scale)/2,(h-bitmap.PixelHeight*scale)/2,bitmap.PixelWidth*scale,bitmap.PixelHeight*scale));}
                    drawing.Freeze();var result=new DrawingImage(drawing);result.Freeze();image=result;
                }
                Legacy[key]=image;
            }
            if(image!=null)return image;
        }
        return null;
    }
}
