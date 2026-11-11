using System.Text.Json;
using WeatherWidget.Core;

namespace WeatherWidget;

internal sealed record DiscoveredLocation(WeatherLocation Location,string Source,double? AccuracyMetres=null);
internal interface ILocationDiscovery : IDisposable
{
    Task<DiscoveredLocation> FromWindowsAsync(CancellationToken cancellationToken);
    Task<DiscoveredLocation> FromIpAsync(CancellationToken cancellationToken);
}

internal sealed class LocationDiscovery : ILocationDiscovery
{
    private HttpClient? http;
    private readonly bool ownsHttp;
    private readonly Func<CancellationToken,Task<DiscoveredLocation>> windows;
    internal LocationDiscovery(HttpClient? http=null,Func<CancellationToken,Task<DiscoveredLocation>>? windows=null)
    {this.http=http;ownsHttp=http==null;this.windows=windows??WindowsDeviceLocation.ReadAsync;}

    public Task<DiscoveredLocation> FromWindowsAsync(CancellationToken cancellationToken)=>windows(cancellationToken);
    public async Task<DiscoveredLocation> FromIpAsync(CancellationToken cancellationToken)
    {
        http??=new HttpClient {Timeout=TimeSpan.FromSeconds(15)};
        using var request=new HttpRequestMessage(HttpMethod.Get,"https://ipapi.co/json/");
        request.Headers.UserAgent.ParseAdd("WeatherWidget/0.5");
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
        response.EnsureSuccessStatusCode();
        const int limit=64*1024;
        if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Location response is too large.");
        using var stream=await response.Content.ReadAsStreamAsync(cancellationToken);
        var bytes=new byte[limit+1];int length=0;
        while(length<bytes.Length)
        {
            int count=await stream.ReadAsync(bytes.AsMemory(length),cancellationToken);
            if(count==0)break;
            length+=count;
        }
        if(length>limit)throw new InvalidDataException("Location response is too large.");
        using var json=JsonDocument.Parse(bytes.AsMemory(0,length));
        var root=json.RootElement;
        if(root.ValueKind!=JsonValueKind.Object || root.TryGetProperty("error",out var error)&&error.ValueKind!=JsonValueKind.False)
            throw new InvalidDataException("IP location service could not determine a location.");
        double Coordinate(string name,double maximum)
        {
            if(!root.TryGetProperty(name,out var value)||value.ValueKind!=JsonValueKind.Number||!value.TryGetDouble(out var number)||!double.IsFinite(number)||Math.Abs(number)>maximum)
                throw new InvalidDataException("IP location service returned invalid coordinates.");
            return Math.Round(number,2);
        }
        string? Text(string name)
        {
            if(!root.TryGetProperty(name,out var value)||value.ValueKind!=JsonValueKind.String)return null;
            var text=value.GetString()?.Trim();
            if(string.IsNullOrWhiteSpace(text))return null;
            return text[..Math.Min(120,text.Length)];
        }
        var city=Text("city")??throw new InvalidDataException("IP location service returned no city. Choose a city manually.");
        return new(new WeatherLocation(city,Coordinate("latitude",90),Coordinate("longitude",180),Text("region"),Text("country_name")),"IP (ipapi.co)");
    }
    public void Dispose(){if(ownsHttp)http?.Dispose();}
}

// Loaded only when the user explicitly requests Windows location; no PositionChanged subscription.
internal static class WindowsDeviceLocation
{
    internal static async Task<DiscoveredLocation> ReadAsync(CancellationToken cancellationToken)
    {
        var fix=await WindowsLocation.WindowsLocationReader.ReadAsync(cancellationToken);
        return new(new WeatherLocation("My location",Math.Round(fix.Latitude,2),Math.Round(fix.Longitude,2)),"Windows",fix.AccuracyMetres+1000);
    }
}
