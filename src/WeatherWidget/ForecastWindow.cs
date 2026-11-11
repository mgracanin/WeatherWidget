using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WeatherWidget.Core;
namespace WeatherWidget;

internal sealed class ForecastWindow : Window
{
    private const double SceneWidth=330, SceneHeight=330d*194/264;
    private readonly Canvas scene=new() { Width=SceneWidth,Height=SceneHeight,Background=Brushes.Transparent };
    private readonly Canvas content=new() { Width=SceneWidth,Height=SceneHeight };
    private readonly AppSettings? preferences;
    private ThemeDefinition theme=ThemeCatalog.Get("modern");
    private readonly Thumb resizeGrip;
    private WeatherSnapshot? snapshot;
    private string language="en",unit="C",windUnit="km/h";
    private bool offline,showDays,closing,dismissScheduled,moving,resizing,pendingRender;
    private string? error;
    private double resizeWidth,resizeHeight;
    private Native.Point resizeOrigin;
    private ScrollViewer? hoursViewport;
    private double hourOffset;
    private WeatherSnapshot? renderedSnapshot;
    private ForecastState? renderedState;
    private readonly record struct ForecastState(string Theme,string Language, string Unit, string WindUnit, bool Offline,
        string? Error, bool ShowDays, bool Expired, bool Stale, int AgeMinute, DateOnly Today, DateTimeOffset? FirstHour);

    public ForecastWindow(AppSettings? preferences=null)
    {
        this.preferences=preferences;
        theme=ThemeCatalog.Get(preferences?.Theme);
        language=preferences?.Language ?? "system";
        var scale=Math.Clamp(preferences?.DetailScale ?? 1,.8,2);
        Title="WeatherWidget — Forecast";Width=SceneWidth*scale;Height=SceneHeight*scale;
        SetWindowIcon();
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
        ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;FontFamily=new FontFamily("Segoe UI");
        scene.Children.Add(content);
        var thumb=UiChrome.ResizeGrip(T("Povucite za promjenu veličine","Drag to resize"),theme.ForegroundBrush);
        resizeGrip=thumb;
        Canvas.SetLeft(thumb,291);Canvas.SetTop(thumb,201);scene.Children.Add(thumb);
        System.Windows.Automation.AutomationProperties.SetName(thumb,"Resize forecast");
        thumb.DragStarted += (_,_) =>
        {
            resizing=true;resizeWidth=Width;resizeHeight=Height;Native.GetCursorPos(out resizeOrigin);
        };
        thumb.DragDelta += (_,_) =>
        {
            Native.GetCursorPos(out var cursor);
            var dpi=VisualTreeHelper.GetDpi(this);
            double dx=(cursor.X-resizeOrigin.X)/dpi.DpiScaleX;
            double dy=(cursor.Y-resizeOrigin.Y)/dpi.DpiScaleY*SceneWidth/SceneHeight;
            double delta=Math.Abs(dx)>=Math.Abs(dy)?dx:dy;
            double next=Math.Clamp(resizeWidth+delta,SceneWidth*.8,SceneWidth*2);
            Width=next;Height=resizeHeight*(next/resizeWidth);
        };
        thumb.DragCompleted += (_,_) =>
        {
            resizing=false;
            if (preferences != null) { preferences.DetailScale=Width/SceneWidth;preferences.Save(); }
            FinishManipulation();
        };
        Content=new Viewbox { Child=scene,Stretch=Stretch.Uniform };
        PreviewMouseLeftButtonDown += BeginMove;
        Deactivated += (_,_)=>DismissWhenInactive();
        PreviewKeyDown += (_,e)=> { if(e.Key==Key.Escape) {RequestClose();e.Handled=true;} };
    }
    private void BeginMove(object sender,MouseButtonEventArgs e)
    {
        if (UiChrome.IsInteractiveSource(e.OriginalSource as DependencyObject) || Mouse.LeftButton != MouseButtonState.Pressed) return;
        var point=e.GetPosition(scene);
        if(point.Y>113) return;
        moving=true;
        try { DragMove();e.Handled=true; }
        finally { moving=false;FinishManipulation(); }
    }
    private void FinishManipulation()
    {
        if(pendingRender) {pendingRender=false;Render();}
        if(!IsActive) DismissWhenInactive();
    }
    private void SetWindowIcon()
    {
        var path=Path.Combine(AppContext.BaseDirectory,"Assets","Brand","weather-widget-512.png");
        try
        {
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
            image.UriSource=new Uri(path,UriKind.Absolute);image.EndInit();image.Freeze();Icon=image;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException)
        {AppSettings.Log("Forecast window icon unavailable: "+ex.Message);}
    }
    internal void RequestClose()
    {
        if(closing) return;
        closing=true;
        try {Close();} catch {closing=false;throw;}
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        closing=true;base.OnClosing(e);if(e.Cancel) closing=false;
    }
    internal void DismissWhenInactive()
    {
        if(closing || dismissScheduled || moving || resizing) return;
        dismissScheduled=true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>
        {
            dismissScheduled=false;
            if(!closing && !moving && !resizing && IsVisible && !IsActive) RequestClose();
        }));
    }
    public void Update(WeatherSnapshot data,string lang,string temperatureUnit,string speedUnit,bool isOffline,string? lastError,DateTimeOffset? nowUtc=null)
    {
        if(closing) return;
        snapshot=data;language=lang;unit=temperatureUnit;windUnit=speedUnit;offline=isOffline;error=lastError;
        if(moving || resizing) {pendingRender=true;return;} Render(nowUtc);
    }
    private void Render(DateTimeOffset? nowUtc=null)
    {
        if(closing || snapshot is not {} data) return;
        var now=nowUtc ?? DateTimeOffset.UtcNow;
        var zone=TimeZoneInfo.FindSystemTimeZoneById(data.TimeZoneId);
        var age=now-(data.Current.TimeUtc<data.FetchedAtUtc?data.Current.TimeUtc:data.FetchedAtUtc);
        bool expired=age>=TimeSpan.FromHours(6);
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,zone).DateTime);
        var firstHour=showDays ? null : data.Hourly.FirstOrDefault(h=>h.TimeUtc>=now.AddHours(-1))?.TimeUtc;
        var nextTheme=ThemeCatalog.Get(preferences?.Theme);
        var state=new ForecastState(nextTheme.Id,UiText.Resolve(language),unit,windUnit,offline,error,showDays,expired,
            age>=TimeSpan.FromHours(1),offline || age>=TimeSpan.FromHours(1) ? Math.Max(0,(int)age.TotalMinutes):0,today,firstHour);
        if(ReferenceEquals(renderedSnapshot,data) && renderedState==state) return;
        theme=nextTheme;resizeGrip.Foreground=theme.ForegroundBrush;
        content.Children.Clear();
        Put(new Image {Source=WeatherArt.Background(data.Current.WeatherCode,data.Current.IsDay,true,theme.Id),
            Width=SceneWidth,Height=SceneHeight,Stretch=Stretch.Uniform},0,0);
        Title=data.Location.Name+" — "+T("Prognoza","Forecast");
        Put(Label(data.Location.Name,14,230,true),25,22);
        var close=FlatButton("×",T("Zatvori prognozu (Esc)","Close forecast (Esc)"),(_,_)=>RequestClose());
        close.Width=20;close.Height=20;close.FontSize=17;Put(close,281,20);
        Put(Label(expired?T("Podaci su prestari","Weather too old"):ConditionCatalog.Description(
            data.Current.WeatherCode,data.Current.IsDay,UiText.Resolve(language)),10,155),25,47);
        var currentDay=data.Daily.FirstOrDefault(d=>d.Date==today);
        Put(Label(currentDay==null?"":"↑ "+Temp(currentDay.HighC)+"   ↓ "+Temp(currentDay.LowC),9,173),25,63);
        var temperature=Label(expired?"—":Temp(data.Current.TemperatureC),30,108);
        temperature.TextAlignment=TextAlignment.Right;Put(temperature,192,44);
        var metrics=new UniformGrid {Columns=3,Width=272,Height=29};
        metrics.Children.Add(Metric(T("Osjet","Feels like"),expired?"—":Temp(data.Current.FeelsLikeC)));
        metrics.Children.Add(Metric(T("Vlažnost","Humidity"),expired?"—":data.Current.Humidity+"%"));
        metrics.Children.Add(Metric(T("Vjetar","Wind"),expired?"—":Wind(data.Current.WindKmh)));
        Put(metrics,25,82);
        var hourly=FlatButton(T("Sljedećih 12 sati","Next 12 hours"),T("Satna prognoza","Hourly forecast"),(_,_)=>{showDays=false;Render();},!showDays);
        hourly.Width=112;Put(hourly,25,116);
        var daily=FlatButton(T("Za 5 dana","5-day forecast"),T("Dnevna prognoza","Daily forecast"),(_,_)=>{showDays=true;Render();},showDays);
        daily.Width=104;Put(daily,139,116);
        var columns=new StackPanel {Orientation=Orientation.Horizontal,Height=45};
        if(showDays)
        {
            foreach(var day in data.Daily.Where(d=>d.Date>=today).Take(5))
            {
                var stack=new StackPanel {Width=54.4};
                var heading=Label((day.Date==today?T("Danas","Today"):day.Date.ToString("ddd",UiText.Culture(language)))+" "+Symbol(day.WeatherCode,true),8.5,54);
                heading.ToolTip=ConditionCatalog.Description(day.WeatherCode,true,UiText.Resolve(language));
                stack.Children.Add(heading);
                stack.Children.Add(Label(Temp(day.HighC).Replace(unit,"")+" / "+Temp(day.LowC).Replace(unit,""),9,54,true));
                stack.Children.Add(Label(day.PrecipitationProbability+"%",8.5,54));
                columns.Children.Add(stack);
            }
            Put(columns,25,143);hoursViewport=null;
        }
        else
        {
            foreach(var hour in data.Hourly.Where(h=>h.TimeUtc>=now.AddHours(-1)).Take(12))
            {
                var stack=new StackPanel {Width=44};
                var heading=Label(TimeZoneInfo.ConvertTime(hour.TimeUtc,zone).ToString("HH:mm"),8.5,43);
                heading.ToolTip=ConditionCatalog.Description(hour.WeatherCode,hour.IsDay,UiText.Resolve(language));
                stack.Children.Add(heading);stack.Children.Add(Label(Temp(hour.TemperatureC),10.5,43,true));
                stack.Children.Add(Label(hour.PrecipitationProbability+"%",8.5,43));columns.Children.Add(stack);
            }
            var viewer=new ScrollViewer {Content=columns,Width=264,Height=45,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,PanningMode=PanningMode.HorizontalOnly};
            hoursViewport=viewer;viewer.ScrollChanged+=(_,_)=>hourOffset=viewer.HorizontalOffset;
            viewer.Loaded+=(_,_)=>viewer.ScrollToHorizontalOffset(hourOffset);
            viewer.PreviewMouseWheel+=(_,e)=>{viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset-(e.Delta>0?132:-132));e.Handled=true;};
            Put(viewer,25,143);
            var previous=FlatButton("‹",T("Raniji sati","Earlier hours"),(_,_)=>hoursViewport?.ScrollToHorizontalOffset((hoursViewport?.HorizontalOffset??0)-176));
            var next=FlatButton("›",T("Kasniji sati","Later hours"),(_,_)=>hoursViewport?.ScrollToHorizontalOffset((hoursViewport?.HorizontalOffset??0)+176));
            previous.Width=23;next.Width=23;previous.FontSize=16;next.FontSize=16;Put(previous,250,116);Put(next,274,116);
        }
        var time=TimeZoneInfo.ConvertTime(data.Current.TimeUtc,zone).ToString("d MMM HH:mm",UiText.Culture(language));
        var fetched=TimeZoneInfo.ConvertTime(data.FetchedAtUtc,zone).ToString("HH:mm",UiText.Culture(language));
        string fresh=offline?T("Izvan mreže","Offline"):age>=TimeSpan.FromHours(1)?T("Zastarjelo","Stale"):"";
        Put(Label((fresh.Length>0?fresh+" · "+Math.Max(0,(int)age.TotalMinutes)+" min · ":"")+T("Podaci: ","Weather: ")+time,7.5,272),25,191);
        var source=FlatButton("Open-Meteo · CC BY 4.0 ↗","Open-Meteo.com",(_,_)=>OpenProvider());
        source.Height=10;source.Width=121;source.FontSize=7.5;Put(source,23,203);
        var fetchedLabel=Label(T("Dohvat ","Fetched ")+fetched+" · "+data.TimeZoneId,7.5,136);fetchedLabel.TextAlignment=TextAlignment.Right;Put(fetchedLabel,148,203);
        ToolTip=(error==null?"":error+"\n")+T("Postotci: vjerojatnost oborina. Lokalno vrijeme odabranog grada. Povucite zaglavlje za pomicanje.","Percentages: precipitation probability. Times are local to the selected city. Drag the header to move.");
        renderedSnapshot=data;renderedState=state;
    }
    private void OpenProvider()
    {
        try {System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://open-meteo.com/"){UseShellExecute=true});}
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {AppSettings.Log("Cannot open provider: "+ex.Message);}
    }
    private StackPanel Metric(string title,string value)
    {
        var stack=new StackPanel();stack.Children.Add(Label(title,9,89));stack.Children.Add(Label(value,12,89,true));return stack;
    }
    private Button FlatButton(string text,string name,RoutedEventHandler action,bool selected=false)
    {
        var button=UiChrome.FlatButton(text,name,action);
        button.FontFamily=new FontFamily("Segoe UI");button.FontSize=9;button.Foreground=theme.ForegroundBrush;
        button.Background=selected?theme.SelectionBrush:Brushes.Transparent;
        button.BorderBrush=selected?theme.AccentBrush:Brushes.Transparent;
        button.BorderThickness=new Thickness(0,0,0,selected?1:0);
        button.Padding=new Thickness(0);button.Height=20;button.Cursor=Cursors.Hand;
        return button;
    }    private static string Symbol(int code,bool day)=>code switch {0=>day?"☀":"☾",1 or 2 or 3=>"☁",45 or 48=>"≋",>=71 and <=77 or 85 or 86=>"❄",95 or 96 or 99=>"ϟ",>=51 and <=67 or >=80 and <=82=>"☂",_=>"—"};
    private void Put(FrameworkElement item,double x,double y){Canvas.SetLeft(item,x);Canvas.SetTop(item,y);content.Children.Add(item);}
    private TextBlock Label(string value,double size,double width,bool bold=false)=>new(){Text=value,Width=width,FontSize=size,
        Foreground=theme.ForegroundBrush,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextTrimming=TextTrimming.CharacterEllipsis,
        Effect=theme.TextShadow?FrozenStyles.ForecastShadow:null};
    private string T(string hr,string en)=>UiText.T(language,hr,en);
    private string Temp(double c)=>Math.Round(unit=="F"?c*9/5+32:c).ToString(UiText.Culture(language))+"°"+unit;
    private string Wind(double kmh)=>(windUnit=="m/s"?kmh/3.6:windUnit=="mph"?kmh/1.609344:kmh).ToString("0",UiText.Culture(language))+" "+windUnit;
}


