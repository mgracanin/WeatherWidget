using System.Windows.Media;

namespace WeatherWidget;

internal sealed class ThemeDefinition(string id,string labelHr,string labelEn,Color foreground,Color muted,Color accent,bool textShadow)
{
    public string Id { get; }=id;
    public string LabelHr { get; }=labelHr;
    public string LabelEn { get; }=labelEn;
    public Color Foreground { get; }=foreground;
    public bool TextShadow { get; }=textShadow;
    public SolidColorBrush ForegroundBrush { get; }=Frozen(foreground);
    public SolidColorBrush MutedBrush { get; }=Frozen(muted);
    public SolidColorBrush AccentBrush { get; }=Frozen(accent);
    public SolidColorBrush SelectionBrush { get; }=Frozen(Color.FromArgb(28,foreground.R,foreground.G,foreground.B));
    private static SolidColorBrush Frozen(Color color) {var brush=new SolidColorBrush(color);brush.Freeze();return brush;}
}
internal static class ThemeCatalog
{
    private static readonly IReadOnlyList<ThemeDefinition> themes =
    [
        new("modern", "Moderna", "Modern", Colors.White, Color.FromRgb(221, 231, 242), Color.FromRgb(113, 194, 255), true),
        new("classic", "Klasična", "Classic", Colors.White, Color.FromRgb(232, 239, 247), Color.FromRgb(255, 211, 94), true),
        new("aurora", "Polarna svjetlost", "Aurora", Color.FromRgb(245, 253, 252), Color.FromRgb(193, 222, 220), Color.FromRgb(101, 231, 204), false),
        new("paper", "Papir", "Paper", Color.FromRgb(35, 47, 48), Color.FromRgb(81, 96, 93), Color.FromRgb(194, 111, 57), false),
        new("sunset", "Zalazak sunca", "Sunset", Color.FromRgb(255, 248, 235), Color.FromRgb(226, 212, 196), Color.FromRgb(255, 183, 103), false)
    ];

    public static IReadOnlyList<ThemeDefinition> All => themes;

    public static ThemeDefinition Get(string? id) =>
        themes.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? themes[0];

    public static bool IsKnown(string? id) =>
        themes.Any(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase));
}

