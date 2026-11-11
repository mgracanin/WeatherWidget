using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using WeatherWidget;
using WeatherWidget.Core;

internal static class SettingsChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static Task Persistence()
    {
        var directory=Path.Combine(Path.GetTempPath(),"WidgetSettings-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"settings.json");
        try
        {
            var live=new AppSettings {CityName="Zagreb",Left=-100,Top=20,AlertLocation="original"};
            var draft=live.CreateDraft();
            draft.Theme="forest";draft.CityName="Split";draft.Latitude=43.5;draft.Longitude=16.4;
            draft.TemperatureUnit="fahrenheit";draft.WindUnit="ms";draft.Language="hr";
            draft.Scale=1.7;draft.DetailScale=1.4;draft.Locked=true;draft.SnapToEdges=false;
            draft.AlwaysOnTop=true;draft.StartWithWindows=true;draft.WeatherNotifications=true;
            draft.QuietHours=true;draft.QuietStart=23;draft.QuietEnd=8;
            Check(live.Theme=="modern"&&!live.StartWithWindows,"Draft mutated live preferences.");
            // Simulate card movement and alert delivery while settings were open.
            live.Left=-200;live.Top=70;live.AlertHistory["rain"]=DateTimeOffset.UtcNow;
            var registrations=new List<bool>();
            Check(SettingsChanges.Commit(live,draft,value=>registrations.Add(value),out var error,path,_=>{}),error);
            Check(registrations.SequenceEqual(new[]{true}),"Startup was not configured once.");
            var loaded=AppSettings.Load(path,_=>{});
            Check(loaded.CityName=="Split"&&loaded.Latitude==43.5&&loaded.Longitude==16.4&&loaded.Theme=="forest","Location/theme did not survive restart.");
            Check(loaded.TemperatureUnit=="fahrenheit"&&loaded.WindUnit=="ms"&&loaded.Language=="hr"&&loaded.Scale==1.7&&loaded.DetailScale==1.4,"Units/appearance did not persist.");
            Check(loaded.Locked&&!loaded.SnapToEdges&&loaded.AlwaysOnTop&&loaded.StartWithWindows&&loaded.WeatherNotifications&&loaded.QuietHours&&loaded.QuietStart==23&&loaded.QuietEnd==8,"Desktop/notification choices did not persist.");
            Check(loaded.Left==-200&&loaded.Top==70&&loaded.AlertHistory.ContainsKey("rain")&&loaded.AlertLocation=="original","Apply overwrote live placement/history with stale draft state.");
            Check(live.Theme==loaded.Theme&&live.Scale==loaded.Scale,"Apply did not update running preferences.");
            registrations.Clear();SettingsChanges.Commit(live,draft,value=>registrations.Add(value),out _,path,_=>{});
            Check(registrations.Count==0,"Unchanged startup preference rewrote the registry.");
        }
        finally{Directory.Delete(directory,true);}
        return Task.CompletedTask;
    }
    public static Task SaveFailures()
    {
        var directory=Path.Combine(Path.GetTempPath(),"WidgetSettingsFailure-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var live=new AppSettings();var draft=live.CreateDraft();draft.Theme="nordic";draft.StartWithWindows=true;
            var calls=new List<bool>();
            Check(!SettingsChanges.Commit(live,draft,value=>calls.Add(value),out var error,directory,_=>{}),"Blocked settings path was reported saved.");
            Check(error.Length>0&&calls.SequenceEqual(new[]{true,false})&&!live.StartWithWindows&&live.Theme=="modern","Save failure did not roll back startup/live preferences.");
            var path=Path.Combine(directory,"never-written.json");
            Check(!SettingsChanges.Commit(live,draft,_=>throw new UnauthorizedAccessException("test refusal"),out error,path,_=>{}),"Startup failure was ignored.");
            Check(!File.Exists(path)&&error.Contains("test refusal")&&live.Theme=="modern","Startup failure wrote preferences.");
            Check(!live.Save(directory,_=>{}),"Save must report failure to the editor.");
        }
        finally{Directory.Delete(directory,true);}
        return Task.CompletedTask;
    }
    private static T Field<T>(SettingsWindow window,string name)=>(T)typeof(SettingsWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T match)yield return match;
        foreach(var item in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var child in Descendants<T>(item))yield return child;
    }
    public static Task Editor()
    {
        using var client=new WeatherClient();
        var live=new AppSettings {Language="en",Scale=1.37};
        int applied=0;AppSettings? captured=null;
        var window=new SettingsWindow(live.CreateDraft(),client,draft=>{applied++;captured=draft.CreateDraft();return true;},()=>{});
        try
        {
            Check(Descendants<TabItem>(window).Count()==4,"Expected four settings sections.");
            Check(Field<Slider>(window,"cardScale").Value==1.37,"Editor rounded an existing custom size.");
            Field<ComboBox>(window,"themeChoice").SelectedValue="forest";
            Check(Field<ComboBox>(window,"themeChoice").Text=="Forest","Selected theme label is blank.");
            Field<ComboBox>(window,"temperature").SelectedValue="fahrenheit";
            Field<CheckBox>(window,"notifications").IsChecked=true;
            Field<CheckBox>(window,"quietHours").IsChecked=false;
            Check(!Field<ComboBox>(window,"quietStart").IsEnabled,"Quiet time controls remained enabled.");
            var apply=Descendants<Button>(window).Single(button=>Equals(button.Content,"Apply"));
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(applied==1&&captured?.Theme=="forest"&&captured.TemperatureUnit=="fahrenheit"&&captured.WeatherNotifications&&!captured.QuietHours,"Apply ignored edited controls.");
            Check(live.Theme=="modern","UI changed live preferences outside apply callback.");
            Field<ComboBox>(window,"themeChoice").SelectedValue="nordic";
            Descendants<Button>(window).Single(button=>Equals(button.Content,"Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(applied==1&&captured?.Theme=="forest","Cancel applied pending changes.");
        }
        finally{window.Close();}
        var refused=new SettingsWindow(live.CreateDraft(),client,_=>false,()=>{});
        try
        {
            int closing=0;refused.Closing+=(_,_)=>closing++;
            Descendants<Button>(refused).Single(b=>Equals(b.Content,"Save and close")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(closing==0,"Failed Save closed the editor.");
        }
        finally{refused.Close();}
        return Task.CompletedTask;
    }
    public static Task<int> Render()
    {
        var output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/previews"));Directory.CreateDirectory(output);
        using var client=new WeatherClient();
        foreach(var language in new[]{"en","hr"}) foreach(bool small in new[]{false,true})
        {
            var window=new SettingsWindow(new AppSettings {Language=language,Theme="forest"},client,_=>true,()=>{});
            try
            {
                var tabs=Descendants<TabControl>(window).Single();
                for(int i=0;i<4;i++)
                {
                    tabs.SelectedIndex=i;
                    var root=(FrameworkElement)window.Content;
                    int width=small?520:620,height=small?440:580;
                    root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
                    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(root);
                    var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    var path=Path.Combine(output,$"settings-{language}-{(small?"small":"normal")}-{i}.png");using var stream=File.Create(path);encoder.Save(stream);
                }
            }
            finally{window.Close();}
        }
        Console.WriteLine("Settings fixture previews: "+output);return Task.FromResult(0);
    }}
