using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WeatherWidget;
using WeatherWidget.Core;
internal static class ThemeChecks
{
    public static async Task Run(string fixture)
    {
        if(ThemeCatalog.All.Count!=9) throw new Exception("Expected nine selectable themes.");
        var directory=Path.Combine(Path.GetTempPath(),"WeatherWidgetThemes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"settings.json");
        try
        {
            foreach(var theme in ThemeCatalog.All)
            {
                var settings=new AppSettings {Theme=theme.Id};settings.Save(path,_=>{});
                if(AppSettings.Load(path,_=>{}).Theme!=theme.Id) throw new Exception("Theme preference did not persist.");
                if(!theme.ForegroundBrush.IsFrozen || !theme.AccentBrush.IsFrozen) throw new Exception("Theme brushes must be frozen.");

                foreach(var code in new[]{0,2,3,45,61,71,95,999}) foreach(bool day in new[]{true,false}) foreach(bool expanded in new[]{true,false})
                {
                    var art=WeatherArt.Background(code,day,expanded,theme.Id);
                    if(art is not DrawingImage {IsFrozen:true} || !ReferenceEquals(art,WeatherArt.Background(code,day,expanded,theme.Id)))
                        throw new Exception("Vector artwork is not reused and frozen.");
                }
                if(!ReferenceEquals(WeatherArt.Background(61,true,false,theme.Id),WeatherArt.Background(63,true,false,theme.Id)))
                    throw new Exception("Equivalent weather families should share artwork.");
            }
        }
        finally {File.Delete(path);Directory.Delete(directory);}
        using var http=new HttpClient(new Handler(fixture));using var client=new WeatherClient(http);
        var snapshot=await client.FetchAsync(new WeatherLocation("Zagreb",45.8,16));
        var preferences=new AppSettings {Theme="aurora"};var panel=new ForecastWindow(preferences);
        try
        {
            panel.Update(snapshot,"en","C","km/h",false,null);
            var canvas=((Canvas)((Viewbox)panel.Content).Child).Children.OfType<Canvas>().Single();var first=canvas.Children[0];
            preferences.Theme="paper";panel.Update(snapshot,"en","C","km/h",false,null);
            if(ReferenceEquals(first,canvas.Children[0])) throw new Exception("Theme switch did not repaint detail view.");
            var title=canvas.Children.OfType<TextBlock>().First();
            if(title.Foreground!=ThemeCatalog.Get("paper").ForegroundBrush || title.Effect!=null) throw new Exception("Paper text must use dark ink without shadows.");
        }
        finally {panel.RequestClose();}
    }
    private sealed class Handler(string json):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")});}
}
