using System.Windows.Media;
using System.Windows.Media.Effects;
namespace WeatherWidget;
internal static class FrozenStyles
{
    internal static readonly DropShadowEffect CompactShadow=Shadow(3,.95);
    internal static readonly DropShadowEffect ForecastShadow=Shadow(2,.8);
    private static DropShadowEffect Shadow(double blur,double opacity)
    {
        var effect=new DropShadowEffect {Color=Colors.Black,BlurRadius=blur,ShadowDepth=1,Opacity=opacity};
        effect.Freeze();return effect;
    }
}
