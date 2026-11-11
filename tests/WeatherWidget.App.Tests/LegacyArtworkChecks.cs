using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WeatherWidget;
internal static class LegacyArtworkChecks
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Png(string path,int width,int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(Brushes.CornflowerBlue,null,new Rect(0,0,width,height));
        var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(path);encoder.Save(stream);
    }
    internal static Task Run()
    {
        var directory=Path.Combine(Path.GetTempPath(),"WeatherWidgetLegacyTest-"+Guid.NewGuid().ToString("N"));var source=Path.Combine(directory,"Weather.Gadget");var root=Path.Combine(directory,"local");var before=LegacyArtwork.RootOverride;
        Directory.CreateDirectory(source);LegacyArtwork.RootOverride=root;WeatherArt.ReloadLegacy();
        try
        {
            var original=WeatherArt.Background(3,true,false,"modern");
            Check(ReferenceEquals(original,WeatherArt.Background(3,true,false,"legacy-modern")),"Absent images did not use original art.");
            Png(Path.Combine(source,"images","docked_black_cloudy.png"),130,67);
            Png(Path.Combine(source,"images","undocked_black_cloudy.png"),264,194);
            Png(Path.Combine(source,"images","undocked_blue_sun.png"),264,194);
            File.WriteAllText(Path.Combine(source,"images","gadget.js"),"ignored test data");
            int generation=WeatherArt.Generation;
            Check(LegacyArtwork.Import(source)==3&&WeatherArt.Generation>generation,"Import/reload count failed.");
            Check(!File.Exists(Path.Combine(root,"gadget.js")),"Non-image content was imported.");
            var imported=WeatherArt.Background(3,true,false,"legacy-modern");
            Check(!ReferenceEquals(original,imported)&&ReferenceEquals(imported,WeatherArt.Background(3,true,false,"legacy-modern")),"Imported image not loaded/reused.");
            Check(imported.IsFrozen&&imported.Width==130&&imported.Height==67,"Imported compact size/freeze failed.");
            var compactSun=WeatherArt.Background(0,true,false,"legacy-modern");Check(compactSun.Width==130&&compactSun.Height==67,"Expanded-to-compact fit failed.");
            Check(ReferenceEquals(WeatherArt.Background(95,true,false,"modern"),WeatherArt.Background(95,true,false,"legacy-modern")),"Partial import did not fall back per state.");
            File.WriteAllText(Path.Combine(root,"docked_black_cloudy.png"),"not PNG");WeatherArt.ReloadLegacy();
            Check(ReferenceEquals(original,WeatherArt.Background(3,true,false,"legacy-modern")),"Corrupt local image did not fall back.");
            var previous=File.ReadAllBytes(Path.Combine(root,"undocked_black_cloudy.png"));
            File.WriteAllText(Path.Combine(source,"images","sun.png"),"corrupt recognized PNG");
            try{LegacyArtwork.Import(source);throw new Exception("Bad image was accepted.");}catch(Exception ex) when(ex is InvalidDataException or FormatException){}
            Check(previous.SequenceEqual(File.ReadAllBytes(Path.Combine(root,"undocked_black_cloudy.png"))),"Invalid import altered previous files.");
            File.WriteAllBytes(Path.Combine(source,"images","sun.png"),new byte[1024*1024+1]);
            try{LegacyArtwork.Import(source);throw new Exception("Oversize image was accepted.");}catch(InvalidDataException){}
        }
        finally{LegacyArtwork.RootOverride=before;WeatherArt.ReloadLegacy();Directory.Delete(directory,true);}
        return Task.CompletedTask;
    }
}
