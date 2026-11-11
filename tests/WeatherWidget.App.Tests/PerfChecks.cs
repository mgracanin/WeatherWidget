using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using WeatherWidget;
using WeatherWidget.Core;

internal static class PerfChecks
{
    public static async Task PresentationReuse(string fixture)
    {
        using var http=new HttpClient(new FixtureHandler(fixture));
        using var client=new WeatherClient(http);
        var seed=await client.FetchAsync(new WeatherLocation("Kathmandu",27.7,85.3));
        var now=new DateTimeOffset(2026,11,11,10,10,0,TimeSpan.Zero);
        var today=new DateOnly(2026,11,11);
        var snapshot=seed with {
            TimeZoneId="Asia/Kathmandu",FetchedAtUtc=now,Current=seed.Current with {TimeUtc=now},
            Hourly=Enumerable.Range(0,24).Select(i=>seed.Hourly[0] with {TimeUtc=new DateTimeOffset(2026,11,11,9,15,0,TimeSpan.Zero).AddHours(i)}).ToArray(),
            Daily=Enumerable.Range(0,7).Select(i=>seed.Daily[0] with {Date=today.AddDays(i)}).ToArray()
        };
        var panel=new ForecastWindow();
        try
        {
            void Update(DateTimeOffset at,string unit="C",bool offline=false,WeatherSnapshot? data=null)=>
                panel.Update(data??snapshot,"en",unit,"km/h",offline,null,at);
            Canvas Content()=>((Canvas)((Viewbox)panel.Content).Child).Children.OfType<Canvas>().Single();
            Update(now);
            var priorArt=Content().Children[0];WeatherArt.ReloadLegacy();Update(now);
            Require(!ReferenceEquals(priorArt,Content().Children[0]),"Artwork reload was skipped by forecast memoization.");
            var first=Content().Children[0];Update(now.AddMinutes(1));
            Require(ReferenceEquals(first,Content().Children[0]),"Healthy minute tick rebuilt forecast children.");
            Update(now.AddMinutes(6));
            Require(!ReferenceEquals(first,Content().Children[0]),"Quarter-hour forecast boundary did not update.");
            var scroll=Content().Children.OfType<ScrollViewer>().Single();
            var heading=((StackPanel)((StackPanel)scroll.Content).Children[0]).Children.OfType<TextBlock>().First();
            Require(heading.Text=="16:00","Kathmandu forecast did not advance its actual first hour.");
            first=Content().Children[0];Update(now.AddMinutes(6),"F");
            Require(!ReferenceEquals(first,Content().Children[0]),"Unit change was skipped.");
            first=Content().Children[0];Update(now.AddMinutes(6),"F",data:snapshot with {Current=snapshot.Current with {TemperatureC=42}});
            Require(!ReferenceEquals(first,Content().Children[0]),"New snapshot was skipped.");
            Update(now,"C",true);first=Content().Children[0];Update(now.AddMinutes(1),"C",true);
            Require(!ReferenceEquals(first,Content().Children[0]),"Visible offline age was not updated.");
            var daily=Content().Children.OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Daily forecast");
            daily.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,daily));
            Require(!Content().Children.OfType<ScrollViewer>().Any(),"Tab selection was skipped.");
        }
        finally {panel.RequestClose();}

        var settings=new AppSettings {Language="en"};
        var app=PreviewRenderer.FixtureApp;
        using var controller=new WeatherController(settings,Dispatcher.CurrentDispatcher,client,
            Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json"),_=>{});
        Set(controller,"Snapshot",snapshot);Set(controller,"Offline",false);
        Set(app,"Settings",settings);Set(app,"Weather",controller);
        var card=new WidgetWindow(app);
        try
        {
            card.UpdateWeatherPresentation(now);var content=card.Content;var menu=card.ContextMenu;
            card.UpdateWeatherPresentation(now.AddMinutes(1));
            Require(ReferenceEquals(content,card.Content),"Healthy minute tick rebuilt compact content.");
            Require(ReferenceEquals(menu,card.ContextMenu),"Presentation update rebuilt the context menu.");
            settings.TemperatureUnit="fahrenheit";card.UpdateWeatherPresentation(now);
            Require(!ReferenceEquals(content,card.Content),"Compact unit change was skipped.");
            content=card.Content;card.UpdateWeatherPresentation(now.AddHours(6));
            Require(!ReferenceEquals(content,card.Content),"Compact expiry boundary was skipped.");
            Require(((string)card.ToolTip).Contains("Weather too old"),"Expired weather remained current.");
            Set(controller,"Offline",true);card.UpdateWeatherPresentation(now);content=card.Content;
            card.UpdateWeatherPresentation(now.AddMinutes(1));
            Require(!ReferenceEquals(content,card.Content),"Compact offline age was not updated.");
        }
        finally {card.Close();}
    }
    private static void Set(object target,string name,object value)=>target.GetType().GetProperty(name,
        BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void Require(bool success,string message) {if(!success)throw new InvalidOperationException(message);}
    private sealed class FixtureHandler(string fixture):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(fixture,Encoding.UTF8,"application/json")});
    }
}
