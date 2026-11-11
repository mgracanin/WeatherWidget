using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using WeatherWidget.Core;
namespace WeatherWidget;

internal sealed class ForecastWindow : Window
{
    private readonly Canvas scene = new() { Width = 528, Height = 388 };
    private WeatherSnapshot? snapshot;
    private string language = "en", unit = "C", windUnit = "km/h";
    private bool offline, showDays;
    private string? error;
    public ForecastWindow()
    {
        Title = "WeatherWidget — Forecast"; Width = 528; Height = 388;
        SetWindowIcon();
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        FontFamily = new FontFamily("Segoe UI");
        Content = new Viewbox { Child = scene, Stretch = Stretch.Uniform };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    private void SetWindowIcon()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "weather-widget-512.png");
        try
        {
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute); image.EndInit(); image.Freeze(); Icon = image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { AppSettings.Log("Forecast window icon unavailable: " + ex.Message); }
    }
    public void Update(WeatherSnapshot data, string lang, string temperatureUnit, string speedUnit, bool isOffline, string? lastError)
    {
        snapshot = data; language = lang; unit = temperatureUnit; windUnit = speedUnit; offline = isOffline; error = lastError;
        Render();
    }
    private void Render()
    {
        if (snapshot is not { } data) return;
        scene.Children.Clear();
        Put(new Image { Source = WeatherArt.Background(data.Current.WeatherCode, data.Current.IsDay, true), Width = 528, Height = 388, Stretch = Stretch.Uniform }, 0, 0);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(data.TimeZoneId);
        var age = DateTimeOffset.UtcNow - (data.Current.TimeUtc < data.FetchedAtUtc ? data.Current.TimeUtc : data.FetchedAtUtc);
        bool expired = age >= TimeSpan.FromHours(6);
        Title = data.Location.Name + " — " + T("Prognoza", "Forecast");
        Put(Label(data.Location.Name, 22, 390, true), 40, 29);
        var close = Button("×", T("Zatvori prognozu (Esc)", "Close forecast (Esc)"), (_, _) => Close());
        close.Width = 28; close.Height = 28; Put(close, 460, 30);
        Put(Label(expired ? T("Podaci su prestari", "Weather too old") : ConditionCatalog.Description(data.Current.WeatherCode, data.Current.IsDay, UiText.Resolve(language)), 15, 280), 40, 65);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        var currentDay = data.Daily.FirstOrDefault(d => d.Date == today);
        Put(Label(currentDay == null ? "" : "↑ " + Temp(currentDay.HighC) + "   ↓ " + Temp(currentDay.LowC), 13, 280), 40, 88);
        var temp = Label(expired ? "—" : Temp(data.Current.TemperatureC), 40, 160); temp.TextAlignment = TextAlignment.Right; Put(temp, 324, 58);
        var metrics = new UniformGrid { Columns = 3, Width = 448, Height = 46 };
        metrics.Children.Add(Metric(T("Osjet", "Feels like"), expired ? "—" : Temp(data.Current.FeelsLikeC)));
        metrics.Children.Add(Metric(T("Vlažnost", "Humidity"), expired ? "—" : data.Current.Humidity + "%"));
        metrics.Children.Add(Metric(T("Vjetar", "Wind"), expired ? "—" : Wind(data.Current.WindKmh)));
        Put(metrics, 40, 117);
        var hourly = Button(T("Sljedećih 12 sati", "Next 12 hours"), T("Prikaži satnu prognozu", "Show hourly forecast"), (_, _) => { showDays = false; Render(); });
        var daily = Button(T("Prognoza za 5 dana", "5-day forecast"), T("Prikaži dnevnu prognozu", "Show daily forecast"), (_, _) => { showDays = true; Render(); });
        hourly.Width = 205; daily.Width = 231; hourly.Opacity = showDays ? .65 : 1; daily.Opacity = showDays ? 1 : .65;
        Put(hourly, 40, 174); Put(daily, 253, 174);
        var columns = new StackPanel { Orientation = Orientation.Horizontal };
        if (showDays)
        {
            foreach (var day in data.Daily.Where(d => d.Date >= today).Take(5))
            {
                var col = Column(day.Date == today ? T("Danas", "Today") : day.Date.ToString("ddd d.", UiText.Culture(language)), day.WeatherCode, true, 88);
                col.Children.Add(Label(Temp(day.HighC) + " / " + Temp(day.LowC), 12, 84, true));
                col.Children.Add(Label(day.PrecipitationProbability + "% " + T("oborina", "rain"), 10, 84));
                columns.Children.Add(col);
            }
        }
        else
        {
            // Unix epochs preserve the selected city's actual hourly slots, including DST and half-hour offsets.
            var currentSlot = DateTimeOffset.UtcNow.AddHours(-1);
            foreach (var hour in data.Hourly.Where(h => h.TimeUtc >= currentSlot).Take(12))
            {
                var col = Column(TimeZoneInfo.ConvertTime(hour.TimeUtc, zone).ToString("HH:mm"), hour.WeatherCode, hour.IsDay, 64);
                col.Children.Add(Label(Temp(hour.TemperatureC), 15, 60, true));
                col.Children.Add(Label(hour.PrecipitationProbability + "%", 11, 60)); columns.Children.Add(col);
            }
        }
        if (columns.Children.Count == 0) columns.Children.Add(Label(T("Za ovo razdoblje nema spremljene prognoze.", "No cached forecast for this period."), 13, 420));
        Put(new ScrollViewer { Content = columns, Width = 448, Height = 103, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }, 40, 209);
        var time = TimeZoneInfo.ConvertTime(data.Current.TimeUtc, zone).ToString("d MMM HH:mm", UiText.Culture(language));
        var fetched = TimeZoneInfo.ConvertTime(data.FetchedAtUtc, zone).ToString("HH:mm", UiText.Culture(language));
        string freshness = offline ? T("IZVAN MREŽE", "OFFLINE") : age >= TimeSpan.FromHours(1) ? T("ZASTARJELO", "STALE") : "";
        Put(Label((freshness.Length > 0 ? freshness + " · " + Math.Max(0, (int)age.TotalMinutes) + " min · " : "") +
            T("Podaci: ", "Weather: ") + time + " · " + T("dohvaćeno ", "fetched ") + fetched, 10, 448), 40, 314);
        var source = Button("Open-Meteo · CC BY 4.0 ↗", "Open-Meteo.com", (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://open-meteo.com/") { UseShellExecute = true }));
        source.FontSize = 10; source.Height = 20; source.Width = 190; Put(source, 40, 326);
        var zoneLabel = Label(data.TimeZoneId, 10, 250); zoneLabel.TextAlignment = TextAlignment.Right; Put(zoneLabel, 234, 330);
        ToolTip = (error == null ? "" : error + "\n") + T("Postotci: vjerojatnost oborina. Vrijeme je lokalno za odabrani grad.", "Percentages: precipitation probability. Times are local to the selected city.");
    }
    private StackPanel Column(string heading, int code, bool day, double width)
    {
        var stack = new StackPanel { Width = width };
        stack.Children.Add(Label(heading, 12, width - 4));
        var symbol = Label(Symbol(code, day), 25, width - 4); symbol.FontFamily = new FontFamily("Segoe UI Symbol");
        symbol.ToolTip = ConditionCatalog.Description(code, day, UiText.Resolve(language)); stack.Children.Add(symbol);
        return stack;
    }
    private StackPanel Metric(string title, string value)
    {
        var stack = new StackPanel(); stack.Children.Add(Label(title, 11, 140)); stack.Children.Add(Label(value, 17, 140, true)); return stack;
    }
    private static string Symbol(int code, bool day) => code switch { 0 => day ? "☀" : "☾", 1 or 2 or 3 => "☁", 45 or 48 => "≋", >= 71 and <= 77 or 85 or 86 => "❄", 95 or 96 or 99 => "ϟ", >= 51 and <= 67 or >= 80 and <= 82 => "☂", _ => "—" };
    private void Put(FrameworkElement item, double x, double y) { Canvas.SetLeft(item, x); Canvas.SetTop(item, y); scene.Children.Add(item); }
    private static TextBlock Label(string value, double size, double width, bool bold = false) => new() { Text = value, Width = width,
        FontSize = size, Foreground = Brushes.White, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = .95 } };
    private static Button Button(string text, string name, RoutedEventHandler action)
    {
        var button = new Button { Content = text, ToolTip = name, Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(100, 8, 23, 36)), BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
            BorderThickness = new Thickness(1), Padding = new Thickness(6, 3, 6, 3), Height = 28 };
        System.Windows.Automation.AutomationProperties.SetName(button, name); button.Click += action; return button;
    }
    private string T(string hr, string en) => UiText.T(language, hr, en);
    private string Temp(double c) => Math.Round(unit == "F" ? c * 9 / 5 + 32 : c).ToString(UiText.Culture(language)) + "°" + unit;
    private string Wind(double kmh) => (windUnit == "m/s" ? kmh / 3.6 : windUnit == "mph" ? kmh / 1.609344 : kmh).ToString("0", UiText.Culture(language)) + " " + windUnit;
}
