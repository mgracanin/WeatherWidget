namespace WeatherWidget.WindowsLocation;

// Keep SDK-specific types in a separate assembly so weather-only operation need not load them.
public static class WindowsLocationReader
{
    public static async Task<(double Latitude,double Longitude,double AccuracyMetres)> ReadAsync(CancellationToken cancellationToken)
    {
        var access=await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync().AsTask(cancellationToken);
        if(access!=Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed)
            throw new UnauthorizedAccessException("Windows location is disabled or permission was denied.");
        var locator=new Windows.Devices.Geolocation.Geolocator {DesiredAccuracyInMeters=1000};
        var position=await locator.GetGeopositionAsync(TimeSpan.FromMinutes(5),TimeSpan.FromSeconds(15)).AsTask(cancellationToken);
        var point=position.Coordinate.Point.Position;
        if(!double.IsFinite(point.Latitude)||!double.IsFinite(point.Longitude)||Math.Abs(point.Latitude)>90||Math.Abs(point.Longitude)>180)
            throw new IOException("Windows returned invalid coordinates.");
        return (point.Latitude,point.Longitude,position.Coordinate.Accuracy);
    }
}
