using System.Windows;
using System.Windows.Media;

namespace WeatherWidget;

internal static class NewThemeArtwork
{
    public static DrawingImage Create(string themeId, int weatherCode, bool isDay, bool expanded)
    {
        var theme = ThemeCatalog.Get(themeId);
        var width = expanded ? 264d : 130d;
        var height = expanded ? 194d : 67d;
        var group = new DrawingGroup
        {
            ClipGeometry = RoundedRect(width, height, expanded ? 18 : 12)
        };

        using (var dc = group.Open())
        {
            var palette = Palette.For(theme.Id, isDay);
            DrawBackdrop(dc, palette, theme.Id, width, height, expanded);
            DrawWeather(dc, palette, theme.Id, FamilyFor(weatherCode), isDay, expanded);
        }

        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static void DrawBackdrop(DrawingContext dc, Palette p, string theme, double w, double h, bool expanded)
    {
        System.Windows.Media.Brush background = theme == "paper"
            ? Brush(p.Base)
            : Gradient(p.Base, p.Deep, new Point(0, 0), new Point(1, 1));
        dc.DrawRoundedRectangle(background, null, new Rect(0, 0, w, h), expanded ? 18 : 12, expanded ? 18 : 12);

        if (theme == "aurora")
        {
            dc.DrawEllipse(Brush(p.Glow, .30), null, new Point(w * .36, h * .22), w * .44, h * .52);
            dc.DrawRoundedRectangle(Gradient(p.Glow, p.Glow2, new Point(0, 0), new Point(1, 1), .28), null,
                new Rect(w * .18, h * .08, w * .78, h * .68), h * .25, h * .25);
            if (expanded)
                dc.DrawRectangle(Gradient(Color.FromArgb(12, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
                    new Point(0, 0), new Point(0, 1)), null, new Rect(0, 0, w, h));
        }
        else if (theme == "sunset")
        {
            dc.DrawEllipse(Brush(p.Glow, .24), null, new Point(w * .29, h * .36), h * .43, h * .43);
            var ridge = Path("M0,64 L0,52 C22,45 39,52 58,41 C78,30 99,43 119,32 C138,22 154,33 175,20 L175,64 Z");
            dc.PushTransform(new ScaleTransform(w / 175, h / 64));
            dc.DrawGeometry(Brush(p.Land, .52), null, ridge);
            var near = Path("M0,64 L0,57 C25,48 42,61 69,49 C93,39 112,53 139,40 C154,34 164,39 175,34 L175,64 Z");
            dc.DrawGeometry(Brush(p.Land2, .67), null, near);
            dc.Pop();
        }
        else if (expanded)
        {
            dc.DrawRoundedRectangle(Brush(Color.FromArgb(20, 29, 51, 50)), null,
                new Rect(1, 1, w - 2, h - 2), 18, 18);
        }

        // A light edge keeps the rounded card distinct from the desktop without effects.
        dc.DrawRoundedRectangle(null, Pen(p.Edge, .48, 1), new Rect(.5, .5, w - 1, h - 1),
            expanded ? 18 : 12, expanded ? 18 : 12);
    }

    private static void DrawWeather(DrawingContext dc, Palette p, string theme, int code, bool day, bool expanded)
    {
        var (sunny, rain, snow, storm, fog) = WeatherKind(code);
        var size = expanded ? 72d : 44d;
        var x = expanded ? 14d : -1d;
        var y = expanded ? 12d : 1d;

        // Weather illustration sits in the upper-left background; the card's labels remain an overlay.
        dc.PushOpacity(expanded ? .18 : 1);
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(size / 64, size / 64));

        if (sunny && day)
        {
            var sun = theme == "paper" ? p.Accent : p.Glow;
            dc.DrawEllipse(Brush(sun, theme == "aurora" ? .75 : .92), null, new Point(33, 26), 16, 16);
            DrawSunRays(dc, p, theme);
        }
        else if (sunny)
        {
            dc.DrawEllipse(Brush(p.Moon, .92), null, new Point(34, 25), 15, 15);
            dc.DrawEllipse(Brush(p.Deep, .88), null, new Point(42, 18), 14, 14);
            DrawStars(dc, p);
        }
        else
        {
            if (day && code is 1 or 2)
            {
                dc.DrawEllipse(Brush(p.Glow, theme == "paper" ? .78 : .85), null, new Point(28, 20), 13, 13);
                DrawSunRays(dc, p, theme, 28, 20, 18);
            }
            else if (!day && code is 1 or 2)
            {
                dc.DrawEllipse(Brush(p.Moon, .8), null, new Point(28, 20), 12, 12);
                dc.DrawEllipse(Brush(p.Deep, .82), null, new Point(34, 14), 11, 11);
                DrawStars(dc, p);
            }

            DrawCloud(dc, p, theme, storm);
            if (rain || snow || storm)
                DrawPrecipitation(dc, p, snow, storm);
            if (fog)
                DrawFog(dc, p);
        }

        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private static void DrawSunRays(DrawingContext dc, Palette p, string theme, double cx = 33, double cy = 26, double radius = 23)
    {
        var rayColor = theme == "paper" ? p.Accent : p.Glow;
        var pen = Pen(rayColor, theme == "aurora" ? .64 : .82, theme == "paper" ? 2 : 1.6);
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4;
            var inner = radius * .75;
            var outer = radius;
            dc.DrawLine(pen,
                new Point(cx + Math.Cos(angle) * inner, cy + Math.Sin(angle) * inner),
                new Point(cx + Math.Cos(angle) * outer, cy + Math.Sin(angle) * outer));
        }
    }

    private static void DrawCloud(DrawingContext dc, Palette p, string theme, bool storm)
    {
        var geometry = Path("M11,45 C7,45 4,42 4,38 C4,34 7,31 12,31 C13,23 19,18 27,18 C34,18 39,22 41,29 C48,27 55,32 55,39 C55,43 52,46 47,46 L11,46 Z");
        var fill = p.Cloud;
        dc.DrawGeometry(Brush(fill, theme == "aurora" ? .94 : 1), Pen(p.CloudEdge, theme == "paper" ? .9 : .58, 1.25), geometry);
        if (storm)
        {
            var bolt = Path("M30,40 L24,51 L30,51 L26,61 L38,47 L32,47 L36,40 Z");
            dc.DrawGeometry(Brush(p.Accent), null, bolt);
        }
    }

    private static void DrawPrecipitation(DrawingContext dc, Palette p, bool snow, bool storm)
    {
        if (snow)
        {
            foreach (var point in new[] { new Point(17, 53), new Point(31, 56), new Point(45, 53) })
                DrawSnowflake(dc, p, point);
        }
        else if (!storm)
        {
            var pen = Pen(p.Rain, .9, 2.2);
            dc.DrawLine(pen, new Point(17, 49), new Point(13, 57));
            dc.DrawLine(pen, new Point(31, 49), new Point(27, 57));
            dc.DrawLine(pen, new Point(45, 49), new Point(41, 57));
        }
    }

    private static void DrawSnowflake(DrawingContext dc, Palette p, Point center)
    {
        var pen = Pen(p.Snow, .92, 1.5);
        foreach (var angle in new[] { 0d, Math.PI / 3, 2 * Math.PI / 3 })
        {
            var dx = Math.Cos(angle) * 4;
            var dy = Math.Sin(angle) * 4;
            dc.DrawLine(pen, new Point(center.X - dx, center.Y - dy), new Point(center.X + dx, center.Y + dy));
        }
        dc.DrawEllipse(Brush(p.Snow), null, center, 1.2, 1.2);
    }

    private static void DrawFog(DrawingContext dc, Palette p)
    {
        var pen = Pen(p.CloudEdge, .88, 2);
        dc.DrawLine(pen, new Point(8, 50), new Point(53, 50));
        dc.DrawLine(pen, new Point(13, 56), new Point(47, 56));
    }

    private static void DrawStars(DrawingContext dc, Palette p)
    {
        foreach (var point in new[] { new Point(10, 15), new Point(51, 12), new Point(55, 23) })
            dc.DrawEllipse(Brush(p.Star, .9), null, point, 1.2, 1.2);
    }

    internal static int FamilyFor(int code)=>code switch
    {
        0=>0,1 or 2=>2,3=>3,45 or 48=>45,
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82=>61,
        71 or 73 or 75 or 77 or 85 or 86=>71,95 or 96 or 99=>95,_=>3
    };
    private static (bool Sunny,bool Rain,bool Snow,bool Storm,bool Fog) WeatherKind(int family)=>family switch
    {
        0=>(true,false,false,false,false),45=>(false,false,false,false,true),
        61=>(false,true,false,false,false),71=>(false,false,true,false,false),
        95=>(false,true,false,true,false),_=>(false,false,false,false,false)
    };
    private static Geometry RoundedRect(double w, double h, double radius)
    {
        var geometry = new RectangleGeometry(new Rect(0, 0, w, h), radius, radius);
        geometry.Freeze();
        return geometry;
    }

    private static Geometry Path(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }

    private static SolidColorBrush Brush(Color color, double opacity = 1)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    private static Pen Pen(Color color, double opacity, double thickness)
    {
        var pen = new Pen(Brush(color, opacity), thickness);
        pen.Freeze();
        return pen;
    }

    private static LinearGradientBrush Gradient(Color start, Color end, Point startPoint, Point endPoint, double opacity = 1)
    {
        var brush = new LinearGradientBrush(start, end, startPoint, endPoint) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    private sealed record Palette(Color Base, Color Deep, Color Glow, Color Glow2, Color Accent, Color Cloud, Color CloudEdge, Color Rain, Color Snow, Color Moon, Color Star, Color Land, Color Land2, Color Edge)
    {
        public static Palette For(string theme, bool day) => theme switch
        {
            "paper" => day
                ? new(Color.FromRgb(246, 241, 229), Color.FromRgb(234, 226, 210), Color.FromRgb(255, 207, 121), Color.FromRgb(251, 224, 172), Color.FromRgb(187, 91, 49), Color.FromRgb(255, 253, 247), Color.FromRgb(92, 103, 96), Color.FromRgb(61, 133, 166), Color.FromRgb(82, 137, 171), Color.FromRgb(253, 233, 188), Color.FromRgb(83, 91, 86), Color.FromRgb(189, 205, 183), Color.FromRgb(153, 179, 161), Color.FromArgb(58, 53, 64, 58))
                : new(Color.FromRgb(234, 239, 237), Color.FromRgb(218, 227, 225), Color.FromRgb(225, 160, 96), Color.FromRgb(233, 190, 134), Color.FromRgb(239, 171, 109), Color.FromRgb(222, 226, 216), Color.FromRgb(117, 133, 127), Color.FromRgb(134, 193, 210), Color.FromRgb(218, 232, 225), Color.FromRgb(242, 218, 162), Color.FromRgb(228, 222, 203), Color.FromRgb(86, 111, 103), Color.FromRgb(66, 91, 86), Color.FromArgb(70, 255, 255, 255)),
            "sunset" => day
                ? new(Color.FromRgb(105, 75, 68), Color.FromRgb(51, 58, 69), Color.FromRgb(255, 179, 103), Color.FromRgb(250, 206, 151), Color.FromRgb(255, 198, 119), Color.FromRgb(239, 204, 178), Color.FromRgb(119, 93, 88), Color.FromRgb(173, 210, 220), Color.FromRgb(241, 235, 220), Color.FromRgb(255, 225, 177), Color.FromRgb(255, 236, 206), Color.FromRgb(94, 91, 93), Color.FromRgb(66, 70, 82), Color.FromArgb(64, 255, 255, 255))
                : new(Color.FromRgb(43, 49, 63), Color.FromRgb(22, 28, 43), Color.FromRgb(228, 139, 103), Color.FromRgb(157, 105, 117), Color.FromRgb(255, 184, 125), Color.FromRgb(139, 146, 164), Color.FromRgb(192, 161, 154), Color.FromRgb(148, 190, 210), Color.FromRgb(223, 231, 237), Color.FromRgb(239, 208, 158), Color.FromRgb(250, 224, 196), Color.FromRgb(68, 76, 93), Color.FromRgb(48, 56, 73), Color.FromArgb(66, 255, 255, 255)),
            _ => day
                ? new(Color.FromRgb(10, 65, 71), Color.FromRgb(7, 35, 50), Color.FromRgb(59, 205, 174), Color.FromRgb(81, 150, 180), Color.FromRgb(115, 241, 206), Color.FromRgb(213, 246, 239), Color.FromRgb(164, 219, 216), Color.FromRgb(142, 220, 225), Color.FromRgb(224, 249, 245), Color.FromRgb(223, 242, 216), Color.FromRgb(222, 255, 241), Color.FromRgb(25, 100, 105), Color.FromRgb(11, 69, 83), Color.FromArgb(58, 196, 255, 238))
                : new(Color.FromRgb(7, 37, 51), Color.FromRgb(4, 17, 34), Color.FromRgb(42, 134, 155), Color.FromRgb(78, 86, 153), Color.FromRgb(111, 208, 202), Color.FromRgb(187, 216, 223), Color.FromRgb(127, 177, 189), Color.FromRgb(125, 193, 214), Color.FromRgb(217, 238, 241), Color.FromRgb(215, 232, 214), Color.FromRgb(211, 240, 232), Color.FromRgb(16, 60, 83), Color.FromRgb(9, 39, 62), Color.FromArgb(48, 177, 220, 220))
        };
    }
}

