using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using WeatherWidget;
using WeatherWidget.Core;

internal static class LocationDiscoveryChecks
{
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private sealed class Handler(string json,HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Requests++;Check(request.RequestUri?.ToString()=="https://ipapi.co/json/","Unexpected or insecure IP endpoint.");
            return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(json,Encoding.UTF8,"application/json")});
        }
    }
    public static async Task Provider()
    {
        var handler=new Handler("{\"city\":\" Zagreb \",\"latitude\":45.81444,\"longitude\":15.97798,\"region\":\"Grad Zagreb\",\"country_name\":\"Croatia\"}");
        using var http=new HttpClient(handler);using var provider=new LocationDiscovery(http,_=>throw new UnauthorizedAccessException("test denial"));
        var result=await provider.FromIpAsync(CancellationToken.None);
        Check(result.Location.Name=="Zagreb"&&result.Location.Latitude==45.81&&result.Location.Longitude==15.98,"IP result was not trimmed and rounded.");
        try{await provider.FromWindowsAsync(CancellationToken.None);throw new Exception("Expected Windows denial.");}catch(UnauthorizedAccessException){}
        Check(handler.Requests==1,"Windows failure contacted IP without an explicit request.");
        foreach(var json in new[]{"[]","null","{}","{\"error\":true}","{\"city\":\"Test\",\"latitude\":91,\"longitude\":0}","{\"city\":\"Test\",\"latitude\":\"45\",\"longitude\":0}",new string('x',65537)})
        {
            using var badHttp=new HttpClient(new Handler(json));using var bad=new LocationDiscovery(badHttp);
            try{await bad.FromIpAsync(CancellationToken.None);throw new Exception("Invalid IP payload was accepted.");}
            catch(Exception ex) when(ex is InvalidDataException or JsonException){}
        }
        using var busyHttp=new HttpClient(new Handler("{}",HttpStatusCode.TooManyRequests));using var busy=new LocationDiscovery(busyHttp);
        try{await busy.FromIpAsync(CancellationToken.None);throw new Exception("Provider rate limit was ignored.");}catch(HttpRequestException){}
    }
    private sealed class Fake : ILocationDiscovery
    {
        internal bool Denied,Pending,Disposed;
        internal int Windows,Ip;
        internal CancellationToken Token;
        public Task<DiscoveredLocation> FromWindowsAsync(CancellationToken token)
        {
            Windows++;Token=token;
            if(Denied)return Task.FromException<DiscoveredLocation>(new UnauthorizedAccessException());
            if(Pending)
            {
                var done=new TaskCompletionSource<DiscoveredLocation>(TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(()=>done.TrySetCanceled(token));return done.Task;
            }
            return Task.FromResult(new DiscoveredLocation(new("Windows",45.81,15.98),"Windows",1000));
        }
        public Task<DiscoveredLocation> FromIpAsync(CancellationToken token){Ip++;return Task.FromResult(new DiscoveredLocation(new("Split",43.51,16.44),"IP (ipapi.co)"));}
        public void Dispose(){Disposed=true;}
    }
    private static T Field<T>(SettingsWindow window,string name)=>(T)typeof(SettingsWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
    private static Task Detect(SettingsWindow window,bool ip)=>(Task)typeof(SettingsWindow).GetMethod("DetectLocationAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object[]{ip})!;
    public static async Task Editor()
    {
        using var weather=new WeatherClient();var live=new AppSettings {Language="en"};var draft=live.CreateDraft();var fake=new Fake {Denied=true};
        var window=new SettingsWindow(draft,weather,_=>true,()=>{},fake);
        try
        {
            Check(fake.Windows==0&&fake.Ip==0,"Opening settings triggered discovery.");
            await Detect(window,false);
            Check(fake.Windows==1&&fake.Ip==0&&draft.CityName=="Zagreb","Denied discovery changed selection or silently contacted IP.");
            Check(Field<TextBlock>(window,"locationStatus").Text.Contains("denied"),"Permission guidance missing.");
            await Detect(window,true);
            Check(draft.CityName=="Split"&&live.CityName=="Zagreb"&&fake.Ip==1,"Discovery did not remain a draft.");
            Field<Button>(window,"saveLocation").RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Check(draft.SavedLocations.Count==1&&live.SavedLocations.Count==0,"Saving location mutated live settings before Apply.");
        }
        finally{window.Close();}
        Check(fake.Disposed,"Closed editor retained provider.");
        var pending=new Fake {Pending=true};var pendingDraft=live.CreateDraft();var closing=new SettingsWindow(pendingDraft,weather,_=>true,()=>{},pending);
        var request=Detect(closing,false);closing.Close();await request;
        Check(pending.Token.IsCancellationRequested&&pending.Disposed&&pendingDraft.CityName=="Zagreb","Closing did not cancel/dispose discovery safely.");
    }
}
