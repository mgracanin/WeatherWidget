using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WeatherWidget.Core;
namespace WeatherWidget;
public sealed partial class WidgetWindow
{
    private void InitializeCard() => UpdateWeatherPresentation();
    public void UpdateWeatherPresentation()
    {
        var settings = app.Settings; var weather = app.Weather; var data = weather.Snapshot;
        bool classic = settings.Theme == "classic";
        double width = classic ? 180 : 220, height = width * 67 / 130;
        Width = width * settings.Scale; Height = height * settings.Scale;
        var canvas = new Canvas { Width = width, Height = height };
        void Put(FrameworkElement element, double x, double y) { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); canvas.Children.Add(element); }
        TextBlock Label(string value, double size, double maxWidth) => new() {
            Text = value, FontSize = size, Foreground = Brushes.White, Width = maxWidth,
            TextTrimming = TextTrimming.CharacterEllipsis, FontFamily = new FontFamily("Segoe UI"),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = .95 } };
        var age = data == null ? TimeSpan.Zero : DateTimeOffset.UtcNow - (data.Current.TimeUtc < data.FetchedAtUtc ? data.Current.TimeUtc : data.FetchedAtUtc);
        bool expired = data != null && age >= TimeSpan.FromHours(6);
        string temperature = data == null || expired ? "—°" : Temp(data.Current.TemperatureC);
        string condition = data == null ? weather.IsLoading ? app.Text("Dohvaćanje…", "Loading…") : app.Text("Nema podataka", "No weather") : expired ? app.Text("Podaci su prestari", "Weather too old") :
            ConditionCatalog.Description(data.Current.WeatherCode, data.Current.IsDay, UiText.Resolve(settings.Language));
        string freshness = weather.Offline ? app.Text("Izvan mreže", "Offline") : age >= TimeSpan.FromHours(1) ? app.Text("Zastarjelo", "Stale") : weather.IsLoading ? app.Text("Osvježavanje…", "Updating…") : "";
        Put(new Image { Source = WeatherArt.Background(data?.Current.WeatherCode ?? -1, data?.Current.IsDay ?? true, false), Width = width, Height = height, Stretch = Stretch.Uniform }, 0, 0);
        if (classic)
        {
            var city = Label(settings.CityName, 12, 84); city.TextAlignment = TextAlignment.Right; city.FontWeight = FontWeights.SemiBold; Put(city, 83, 10);
            var temp = Label(temperature, 29, 94); temp.TextAlignment = TextAlignment.Right; Put(temp, 73, 27);
            Put(Label(freshness.Length == 0 ? condition : freshness + " · " + condition, 10, 157), 11, 65);
            status = new TextBlock();
        }
        else
        {
            var city = Label(settings.CityName, 14, 113); city.TextAlignment = TextAlignment.Right; city.FontWeight = FontWeights.SemiBold; Put(city, 91, 13);
            var temp = Label(temperature, 36, 117); temp.TextAlignment = TextAlignment.Right; Put(temp, 87, 31);
            Put(Label(condition, 11, 190), 14, 76);
            var day = data?.Daily.FirstOrDefault();
            var summary = freshness.Length > 0 ? freshness : day == null ? "" : "↑ " + Temp(day.HighC) + "   ↓ " + Temp(day.LowC);
            status = Label(summary, 10, 190); Put(status, 14, 91);
        }
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        string sourceTime = data == null ? "" : "\n" + app.Text("Vrijeme podataka: ", "Weather time: ") +
            TimeZoneInfo.ConvertTime(data.Current.TimeUtc, TimeZoneInfo.FindSystemTimeZoneById(data.TimeZoneId)).ToString("g", UiText.Culture(settings.Language));
        ToolTip = settings.CityName + " · " + temperature + " · " + condition + sourceTime +
            (freshness.Length == 0 ? "" : "\n" + freshness + " · " + Math.Max(0, (int)age.TotalMinutes) + " min") +
            (weather.Error == null ? "" : "\n" + weather.Error) + "\nOpen-Meteo · CC BY 4.0\n" + app.Text("Klik: prognoza · Desni klik: postavke", "Click: forecast · Right click: settings");
        System.Windows.Automation.AutomationProperties.SetName(this, settings.CityName + " " + temperature + " " + condition + " " + freshness);
        ContextMenu = app.BuildWidgetMenu();
    }
    private string Temp(double c) => Math.Round(app.Settings.TemperatureUnit == "fahrenheit" ? c * 9 / 5 + 32 : c).ToString(UiText.Culture(app.Settings.Language)) + "°" + (app.Settings.TemperatureUnit == "fahrenheit" ? "F" : "C");
}
