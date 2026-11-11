using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WeatherWidget.Core;

namespace WeatherWidget;

internal sealed class CityPicker : Window
{
    private readonly WeatherClient client;
    private readonly string language;
    private readonly TextBox query = new() { MinWidth = 330, Margin = new Thickness(0, 10, 8, 8), FontSize = 16 };
    private readonly ListBox results = new() { MinHeight = 200, Margin = new Thickness(0, 4, 0, 8) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private CancellationTokenSource? search;
    private int generation;
    private bool closed;
    public WeatherLocation? SelectedLocation { get; private set; }

    public CityPicker(WeatherClient client, string language, string currentCity)
    {
        this.client = client; this.language = language;
        Title = T("Promijeni grad", "Change city"); Width = 490; Height = 390;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        var panel = new DockPanel { Margin = new Thickness(20) };
        var heading = new TextBlock { Text = T("Unesite grad ili poštanski broj", "Enter a city or postcode"), FontSize = 17 };
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        var input = new DockPanel();
        var searchButton = new Button { Content = T("Traži", "Search"), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 10, 0, 8) };
        searchButton.Click += async (_, _) => await SearchAsync();
        DockPanel.SetDock(searchButton, Dock.Right); input.Children.Add(searchButton); input.Children.Add(query);
        DockPanel.SetDock(input, Dock.Top); panel.Children.Add(input);
        var choose = new Button { Content = T("Odaberi grad", "Use this city"), Padding = new Thickness(12, 7, 12, 7), HorizontalAlignment = HorizontalAlignment.Right };
        choose.Click += (_, _) => Choose(); DockPanel.SetDock(choose, Dock.Bottom); panel.Children.Add(choose);
        DockPanel.SetDock(status, Dock.Bottom); panel.Children.Add(status); panel.Children.Add(results); Content = panel;
        System.Windows.Automation.AutomationProperties.SetName(query, T("Naziv grada", "City name"));
        System.Windows.Automation.AutomationProperties.SetName(results, T("Rezultati pretraživanja", "City search results"));
        results.MouseDoubleClick += (_, _) => Choose();
        query.TextChanged += (_, _) => { debounce.Stop(); search?.Cancel(); generation++; results.Items.Clear(); debounce.Start(); };
        debounce.Tick += async (_, _) => { debounce.Stop(); await SearchAsync(); };
        PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter) { if (results.SelectedItem != null) Choose(); else await SearchAsync(); e.Handled = true; }
        };
        Closed += (_, _) => { closed = true; debounce.Stop(); search?.Cancel(); search?.Dispose(); };
        Loaded += (_, _) => { query.Text = currentCity; query.SelectAll(); query.Focus(); };
    }
    private async Task SearchAsync()
    {
        debounce.Stop(); search?.Cancel(); search?.Dispose(); search = new CancellationTokenSource();
        var own = search; var ownGeneration = ++generation;
        results.Items.Clear();
        if (query.Text.Trim().Length < 2) { status.Text = T("Unesite barem dva znaka.", "Enter at least two characters."); return; }
        status.Text = T("Pretraživanje…", "Searching…");
        try
        {
            var found = await client.SearchAsync(query.Text.Trim(), UiText.Resolve(language), own.Token);
            if (closed || ownGeneration != generation) return;
            foreach (var city in found)
                results.Items.Add(new ListBoxItem { Content = string.Join(", ", new[] { city.Name, city.Region, city.Country }.Where(s => !string.IsNullOrWhiteSpace(s))), Tag = city, Padding = new Thickness(5, 7, 5, 7) });
            status.Text = found.Count == 0 ? T("Nema rezultata. Pokušajte s drugim nazivom.", "No results. Try another name.") : T("Odaberite grad i regiju.", "Choose the city and region.");
            if (found.Count > 0) results.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or InvalidDataException)
        { if (!closed && ownGeneration == generation) status.Text = T("Pretraživanje nije uspjelo: ", "Search failed: ") + ex.Message; }
    }
    private void Choose()
    {
        if (results.SelectedItem is ListBoxItem { Tag: WeatherLocation city }) { SelectedLocation = city; DialogResult = true; }
    }
    private string T(string hr, string en) => UiText.T(language, hr, en);
}
