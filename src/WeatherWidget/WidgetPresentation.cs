using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WeatherWidget.Core;
namespace WeatherWidget;
public sealed partial class WidgetWindow
{
    private bool resizingCard;
    private WeatherSnapshot? renderedSnapshot;
    private CompactState? renderedState;
    private readonly record struct CompactState(string City, string Theme, string Language, string Unit,
        double Scale, bool Offline, bool Loading, string? Error, bool Expired, string Freshness, int AgeMinute, string Rain, TemperatureTrendDirection Trend, int ArtGeneration);
    private void InitializeCard()
    {
        PreviewMouseWheel += (_, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            app.Settings.Scale = Math.Clamp(app.Settings.Scale + (e.Delta > 0 ? .1 : -.1), .75, 3);
            FinishCardResize(); e.Handled = true;
        };
        UpdateWeatherPresentation();
    }
    private void FinishCardResize()
    {
        resizingCard = false; UpdateWeatherPresentation(); ContextMenu=app.BuildWidgetMenu();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => { RecoverOnScreen(); }));
    }
    public void UpdateWeatherPresentation(DateTimeOffset? nowUtc = null)
    {
        if (resizingCard) return;
        var settings=app.Settings; var weather=app.Weather; var data=weather.Snapshot;
        var now=nowUtc ?? DateTimeOffset.UtcNow;
        var age=data==null ? TimeSpan.Zero : now-(data.Current.TimeUtc<data.FetchedAtUtc ? data.Current.TimeUtc:data.FetchedAtUtc);
        bool expired=data!=null && age>=TimeSpan.FromHours(6);
        string freshness=weather.Offline ? app.Text("Izvan mreže","Offline") : age>=TimeSpan.FromHours(1) ? app.Text("Zastarjelo","Stale") : weather.IsLoading ? app.Text("Osvježavanje…","Updating…") : "";
        string rain=data==null || weather.Offline ? "" : WeatherExtras.RainSoon(data,now,settings.Language);
        var trend=data==null || weather.Offline ? TemperatureTrendDirection.Unknown : TemperatureTrend.Get(data,now);
        string trendText=trend switch {TemperatureTrendDirection.Rising=>app.Text("Oko 3 h: temperatura raste","About 3 h: warming"),TemperatureTrendDirection.Falling=>app.Text("Oko 3 h: temperatura pada","About 3 h: cooling"),TemperatureTrendDirection.Steady=>app.Text("Oko 3 h: bez veće promjene temperature","About 3 h: temperature steady"),_=>""};
        var state=new CompactState(settings.CityName,settings.Theme,UiText.Resolve(settings.Language),settings.TemperatureUnit,
            settings.Scale,weather.Offline,weather.IsLoading,weather.Error,expired,freshness,
            freshness.Length>0 ? Math.Max(0,(int)age.TotalMinutes) : 0,rain,trend,WeatherArt.Generation);
        if (ReferenceEquals(renderedSnapshot,data) && renderedState==state) return;
        const double width=130, height=67;
        Width=width*settings.Scale; Height=height*settings.Scale;
        var theme=ThemeCatalog.Get(settings.Theme);
        var canvas=new Canvas { Width=width, Height=height };
        void Put(FrameworkElement element,double x,double y) { Canvas.SetLeft(element,x);Canvas.SetTop(element,y);canvas.Children.Add(element); }
        TextBlock Label(string value,double size,double maxWidth) => new() {
            Text=value,FontSize=size,Foreground=theme.ForegroundBrush,Width=maxWidth,TextTrimming=TextTrimming.CharacterEllipsis,
            FontFamily=new FontFamily("Segoe UI"),Effect=theme.TextShadow?FrozenStyles.CompactShadow:null };
        string temperature=data==null || expired ? "—°" : Temp(data.Current.TemperatureC);
        string condition=data==null ? weather.IsLoading ? app.Text("Dohvaćanje…","Loading…") : app.Text("Nema podataka","No weather") : expired ? app.Text("Podaci su prestari","Weather too old") :
            ConditionCatalog.Description(data.Current.WeatherCode,data.Current.IsDay,UiText.Resolve(settings.Language));
        Put(new Image { Source=WeatherArt.Background(data?.Current.WeatherCode??-1,data?.Current.IsDay??true,false,theme.Id), Width=width,Height=height,Stretch=Stretch.Uniform },0,0);
        var city=Label(settings.CityName,10,80); city.TextAlignment=TextAlignment.Right; city.FontWeight=FontWeights.SemiBold; Put(city,40,7);
        var temp=Label(temperature,23,78); temp.TextAlignment=TextAlignment.Right; temp.FontWeight=settings.Theme is "classic" or "legacy-classic" ? FontWeights.SemiBold:FontWeights.Light; Put(temp,42,19);
        if(trend!=TemperatureTrendDirection.Unknown){var arrow=Label(trend switch {TemperatureTrendDirection.Rising=>"↑",TemperatureTrendDirection.Falling=>"↓",_=>"→"},12,15);arrow.ToolTip=trendText;Put(arrow,27,29);}
        status=Label(freshness.Length>0 ? freshness:rain.Length>0 ? app.Text("Kiša uskoro","Rain soon"):condition,8.5,108); Put(status,8,49);
        var grip=UiChrome.ResizeGrip(app.Text("Povucite za veličinu · Ctrl+kotačić","Drag to resize · Ctrl+wheel"),theme.ForegroundBrush);
        grip.Width=10; grip.Height=10;
        grip.DragStarted+=(_,_)=>resizingCard=true;
        grip.DragDelta+=(_,e)=>
        {
            var delta=Math.Abs(e.HorizontalChange)>=Math.Abs(e.VerticalChange) ? e.HorizontalChange/width:e.VerticalChange/height;
            settings.Scale=Math.Clamp(settings.Scale+delta,.75,3);
            Width=width*settings.Scale;Height=height*settings.Scale;
        };
        grip.DragCompleted+=(_,_)=>FinishCardResize(); Put(grip,117,52);
        Content=new Viewbox {Child=canvas,Stretch=Stretch.Uniform};
        string sourceTime=data==null ? "" : "\n"+app.Text("Vrijeme podataka: ","Weather time: ")+
            TimeZoneInfo.ConvertTime(data.Current.TimeUtc,TimeZoneInfo.FindSystemTimeZoneById(data.TimeZoneId)).ToString("g",UiText.Culture(settings.Language));
        var day=data?.Daily.FirstOrDefault();
        ToolTip=settings.CityName+" · "+temperature+" · "+condition+(day==null ? "":"\n↑ "+Temp(day.HighC)+"   ↓ "+Temp(day.LowC))+sourceTime+
            (trendText.Length==0 ? "":"\n"+trendText)+
            (rain.Length==0 ? "":"\n"+rain+app.Text(" · satna prognoza ≥60%"," · hourly forecast ≥60%"))+
            (freshness.Length==0 ? "":"\n"+freshness+" · "+Math.Max(0,(int)age.TotalMinutes)+" min")+
            (weather.Error==null ? "":"\n"+weather.Error)+"\nOpen-Meteo · CC BY 4.0\n"+
            app.Text("Klik: prognoza · Desni klik: postavke · Kut: veličina","Click: forecast · Right click: settings · Corner: resize");
        System.Windows.Automation.AutomationProperties.SetName(this,settings.CityName+" "+temperature+" "+condition+" "+freshness);
        renderedSnapshot=data; renderedState=state;
    }
    private string Temp(double c)=>Math.Round(app.Settings.TemperatureUnit=="fahrenheit" ? c*9/5+32:c).ToString(UiText.Culture(app.Settings.Language))+"°"+(app.Settings.TemperatureUnit=="fahrenheit" ? "F":"C");
}
