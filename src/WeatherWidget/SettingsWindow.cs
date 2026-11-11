using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WeatherWidget.Core;

namespace WeatherWidget;

/// <summary>Edits an isolated settings draft. The owner decides when that draft is applied.</summary>
internal sealed partial class SettingsWindow : Window
{
    private readonly AppSettings draft;
    private readonly WeatherClient client;
    private readonly Func<AppSettings, bool> apply;
    private readonly Action showStatus;
    private readonly string language;
    private readonly TextBlock cityValue = new();
    private readonly ComboBox temperature = new(), wind = new(), languageChoice = new(), themeChoice = new();
    private readonly Slider cardScale = new(), detailScale = new();
    private readonly TextBlock cardScaleValue = new(), detailScaleValue = new();
    private readonly CheckBox locked = new(), snap = new(), alwaysOnTop = new(), startup = new();
    private readonly CheckBox notifications = new(), quietHours = new();
    private readonly ComboBox quietStart = new(), quietEnd = new();
    private readonly Image themePreview = new();
    private readonly TextBlock applyStatus = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), TextWrapping = TextWrapping.Wrap };

    public SettingsWindow(AppSettings draft, WeatherClient client, Func<AppSettings, bool> apply, Action showStatus, ILocationDiscovery? discovery=null)
    {
        this.draft = draft; this.discovery=discovery;
        this.client = client;
        this.apply = apply;
        this.showStatus = showStatus;
        language = draft.Language;

        Title = T("Postavke vremenskog widgeta", "Weather widget settings");
        Width = 620; Height = 620; MinWidth = 520; MinHeight = 480;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        FontFamily = new FontFamily("Segoe UI"); FontSize=13; Background=Brushes.White;
        SetWindowIcon();
        Closed+=(_,_)=>{discoveryClosed=true;locationRequest?.Cancel();this.discovery?.Dispose();};

        var root = new DockPanel { Background=Brushes.White, Margin = new Thickness(20, 16, 20, 16), LastChildFill = true };
        var footer = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = true };
        DockPanel.SetDock(footer, Dock.Bottom);
        var statusButton = Button(T("Otvori status", "View status"), (_, _) => showStatus());
        statusButton.HorizontalAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetName(statusButton, T("Otvori status widgeta", "Open widget status"));
        var footerRows = new StackPanel();
        var statusRow = new DockPanel { LastChildFill = true, MinHeight = 28 };
        statusRow.Children.Add(applyStatus);
        var actionsRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(statusButton, Dock.Left); actionsRow.Children.Add(statusButton);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button(T("Odustani", "Cancel"), (_, _) => Close());
        cancel.Margin = new Thickness(8, 0, 0, 0);
        var applyButton = Button(T("Primijeni", "Apply"), (_, _) => ApplyDraft());
        applyButton.Margin = new Thickness(8, 0, 0, 0);
        var saveButton = Button(T("Spremi i zatvori", "Save and close"), (_, _) => { if (ApplyDraft()) Close(); });
        saveButton.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(cancel); actions.Children.Add(applyButton); actions.Children.Add(saveButton);
        DockPanel.SetDock(actions, Dock.Right); actionsRow.Children.Add(actions);
        footerRows.Children.Add(statusRow); footerRows.Children.Add(actionsRow); footer.Children.Add(footerRows); root.Children.Add(footer);

        var heading = new TextBlock { Text = T("Postavke", "Settings"), FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var tabs = new TabControl();
        tabs.Items.Add(Tab(T("Lokacija i jedinice", "Location & units"), LocationPage()));
        tabs.Items.Add(Tab(T("Izgled", "Appearance"), AppearancePage()));
        tabs.Items.Add(Tab(T("Radna površina", "Desktop"), DesktopPage()));
        tabs.Items.Add(Tab(T("Obavijesti", "Notifications"), NotificationsPage()));
        root.Children.Add(tabs);
        Content = root;

        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { Close(); e.Handled = true; } };
        quietHours.Checked += (_, _) => SetQuietEnabled();
        quietHours.Unchecked += (_, _) => SetQuietEnabled();
        themeChoice.SelectionChanged += (_, _) => UpdateThemePreview();
        cardScale.ValueChanged += (_, _) => cardScaleValue.Text = cardScale.Value.ToString("0.00", UiText.Culture(language)) + "×";
        detailScale.ValueChanged += (_, _) => detailScaleValue.Text = detailScale.Value.ToString("0.00", UiText.Culture(language)) + "×";
    }

    private TabItem Tab(string title, UIElement content) => new() { Header = title, Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(8) } };

    private UIElement LocationPage()
    {
        var panel = PageStack();
        AddLocationControls(panel);
        panel.Children.Add(Section(T("Jedinice i jezik", "Units and language")));
        Fill(temperature, ("celsius", T("Celzijevi (°C)", "Celsius (°C)")), ("fahrenheit", T("Fahrenheit (°F)", "Fahrenheit (°F)")));
        temperature.SelectedValue = draft.TemperatureUnit;
        panel.Children.Add(Labeled(T("Temperatura", "Temperature"), temperature));
        Fill(wind, ("kmh", T("Kilometri na sat (km/h)", "Kilometres per hour (km/h)")), ("ms", T("Metri u sekundi (m/s)", "Metres per second (m/s)")), ("mph", T("Milje na sat (mph)", "Miles per hour (mph)")));
        wind.SelectedValue = draft.WindUnit;
        panel.Children.Add(Labeled(T("Vjetar", "Wind"), wind));
        Fill(languageChoice, ("system", T("Jezik sustava", "System language")), ("hr", "Hrvatski"), ("en", "English"));
        languageChoice.SelectedValue = draft.Language;
        panel.Children.Add(Labeled(T("Jezik", "Language"), languageChoice));
        panel.Children.Add(new TextBlock {Text=T("Jezik ovog prozora mijenja se nakon ponovnog otvaranja.","Reopen Settings to change its language."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0),Foreground=Brushes.DimGray});
        return panel;
    }

    private UIElement AppearancePage()
    {
        var panel = PageStack();
        panel.Children.Add(Section(T("Tema", "Theme")));
        Fill(themeChoice, ThemeCatalog.All.Select(theme => (theme.Id, UiText.Resolve(language) == "hr" ? theme.LabelHr : theme.LabelEn)).ToArray());
        themeChoice.SelectedValue = draft.Theme;
        panel.Children.Add(Labeled(T("Odaberite temu", "Choose a theme"), themeChoice));
        themePreview.Width = 194; themePreview.Height = 100; themePreview.Stretch = Stretch.Uniform; themePreview.Margin = new Thickness(0, 5, 0, 12);
        themePreview.HorizontalAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetName(themePreview, T("Pregled odabrane teme", "Selected theme preview"));
        panel.Children.Add(themePreview);
        panel.Children.Add(Section(T("Veličina", "Size")));
        ConfigureSlider(cardScale, .5, 3, draft.Scale);
        panel.Children.Add(SliderRow(T("Widget", "Widget"), cardScale, cardScaleValue));
        ConfigureSlider(detailScale, .8, 2, draft.DetailScale);
        panel.Children.Add(SliderRow(T("Detaljna prognoza", "Forecast details"), detailScale, detailScaleValue));
        UpdateThemePreview();
        return panel;
    }

    private UIElement DesktopPage()
    {
        var panel = PageStack();
        panel.Children.Add(Section(T("Ponašanje na radnoj površini", "Desktop behavior")));
        ConfigureCheck(locked, T("Zaključaj položaj widgeta", "Lock widget position"), draft.Locked); panel.Children.Add(locked);
        ConfigureCheck(snap, T("Poravnaj uz rubove zaslona", "Snap to screen edges"), draft.SnapToEdges); panel.Children.Add(snap);
        ConfigureCheck(alwaysOnTop, T("Uvijek na vrhu", "Always on top"), draft.AlwaysOnTop); panel.Children.Add(alwaysOnTop);
        ConfigureCheck(startup, T("Pokreni pri prijavi u Windows", "Start with Windows"), draft.StartWithWindows); panel.Children.Add(startup);
        return panel;
    }

    private UIElement NotificationsPage()
    {
        var panel = PageStack();
        panel.Children.Add(Section(T("Obavijesti o vremenu", "Weather notifications")));
        ConfigureCheck(notifications, T("Uključite lokalne vremenske obavijesti", "Enable local weather notifications"), draft.WeatherNotifications);
        panel.Children.Add(notifications);
        var hint = new TextBlock { Text = T("Obavijesti se obrađuju na ovom računalu. Za svaku vrstu šalje se najviše jedan podsjetnik svakih 6 sati.", "Notifications are handled on this computer. At most one reminder per type is sent every 6 hours."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, -2, 0, 12), Foreground = SystemColors.GrayTextBrush };
        panel.Children.Add(hint);
        panel.Children.Add(Section(T("Tiho razdoblje", "Quiet hours")));
        ConfigureCheck(quietHours, T("Utišaj obavijesti u odabranom razdoblju", "Mute notifications during selected hours"), draft.QuietHours);
        panel.Children.Add(quietHours);
        FillHours(quietStart, draft.QuietStart); FillHours(quietEnd, draft.QuietEnd);
        var hours = new StackPanel { Orientation = Orientation.Horizontal };
        hours.Children.Add(Labeled(T("Početak", "Start"), quietStart));
        hours.Children.Add(Labeled(T("Završetak", "End"), quietEnd));
        panel.Children.Add(hours);
        panel.Children.Add(new TextBlock {Text=T("Koristi se lokalno vrijeme računala. Jednako vrijeme početka i završetka utišava obavijesti cijeli dan.","Uses computer local time. Equal start and end hours mute notifications all day."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0),Foreground=Brushes.DimGray});
        SetQuietEnabled();
        return panel;
    }

    private void PickCity()
    {
        var picker = new CityPicker(client, language, draft.CityName) { Owner = this };
        if (picker.ShowDialog() == true && picker.SelectedLocation is { } selected)
        {
            currentLocationDetails=selected;
            draft.CityName = selected.Name; draft.Latitude = selected.Latitude; draft.Longitude = selected.Longitude;
            ShowDraftLocation(); locationStatus.Text="";
        }
    }

    private bool ApplyDraft()
    {
        if(locationRequest!=null){locationStatus.Text=T("Pričekajte završetak traženja lokacije.","Wait for location discovery to finish.");return false;}
        UpdateDraftFromControls();
        var succeeded = apply(draft);
        applyStatus.Text = succeeded ? T("Postavke su spremljene.", "Settings applied.") : string.Empty;
        AutomationProperties.SetLiveSetting(applyStatus, System.Windows.Automation.AutomationLiveSetting.Polite);
        return succeeded;
    }

    private void UpdateDraftFromControls()
    {
        draft.TemperatureUnit = temperature.SelectedValue as string ?? "celsius";
        draft.WindUnit = wind.SelectedValue as string ?? "kmh";
        draft.Language = languageChoice.SelectedValue as string ?? "system";
        draft.Theme = themeChoice.SelectedValue as string ?? "modern";
        draft.Scale = cardScale.Value; draft.DetailScale = detailScale.Value;
        draft.Locked = locked.IsChecked == true; draft.SnapToEdges = snap.IsChecked == true;
        draft.AlwaysOnTop = alwaysOnTop.IsChecked == true; draft.StartWithWindows = startup.IsChecked == true;
        draft.WeatherNotifications = notifications.IsChecked == true; draft.QuietHours = quietHours.IsChecked == true;
        draft.QuietStart = quietStart.SelectedIndex; draft.QuietEnd = quietEnd.SelectedIndex;
    }

    private void UpdateThemePreview()
    {
        if (themeChoice.SelectedValue is string id)
            themePreview.Source = WeatherArt.Background(2, true, false, id);
    }

    private void SetQuietEnabled()
    {
        quietStart.IsEnabled = quietHours.IsChecked == true;
        quietEnd.IsEnabled = quietHours.IsChecked == true;
    }

    private void SetWindowIcon()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "weather-widget-512.png");
        try
        {
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute); image.EndInit(); image.Freeze(); Icon = image;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
        { AppSettings.Log("Settings window icon unavailable: " + ex.Message); }
    }

    private string T(string hr, string en) => UiText.T(language, hr, en);
    private static StackPanel PageStack() => new() { Orientation = Orientation.Vertical, Margin = new Thickness(4) };
    private TextBlock Section(string text) => new() { Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 7) };
    private UIElement Labeled(string label, UIElement control)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 3, 12, 9), MinWidth = 165 };
        var heading = new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) };
        AutomationProperties.SetLabeledBy(control, heading); stack.Children.Add(heading); stack.Children.Add(control); return stack;
    }
    private Button Button(string text, RoutedEventHandler onClick)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 6, 12, 6), MinHeight = 34, MinWidth = 78, Margin = new Thickness(0, 2, 0, 2) };
        button.Click += onClick; AutomationProperties.SetName(button, text); return button;
    }
    private static void Fill(ComboBox combo, params (string Value, string Label)[] items)
    {
        combo.MinWidth = 230; combo.MinHeight=30; combo.Padding=new Thickness(6,2,6,2); combo.ItemsSource = items.Select(item => new Choice(item.Value, item.Label)).ToArray();
        combo.DisplayMemberPath = nameof(Choice.Label); combo.SelectedValuePath = nameof(Choice.Value);
    }
    private static void FillHours(ComboBox combo, int selected)
    {
        combo.MinWidth = 100; combo.MinHeight=30; combo.ItemsSource = Enumerable.Range(0, 24).Select(hour => $"{hour:00}:00").ToArray();
        combo.SelectedIndex = Math.Clamp(selected, 0, 23);
    }
    private void ConfigureCheck(CheckBox box, string label, bool value)
    {
        box.Content = label; box.IsChecked = value; box.Margin = new Thickness(0, 5, 0, 7); box.MinHeight = 28;
        AutomationProperties.SetName(box, label);
    }
    private static void ConfigureSlider(Slider slider, double min, double max, double value)
    {
        slider.Minimum = min; slider.Maximum = max; slider.Value = Math.Clamp(value, min, max);
        slider.TickFrequency = .1; slider.IsSnapToTickEnabled = false; slider.MinWidth = 230;
        AutomationProperties.SetName(slider, "Scale");
    }
    private UIElement SliderRow(string label, Slider slider, TextBlock value)
    {
        value.Width = 48; value.VerticalAlignment = VerticalAlignment.Center; value.TextAlignment = TextAlignment.Right;
        value.Text = slider.Value.ToString("0.00", UiText.Culture(language)) + "×";
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(slider, 0); Grid.SetColumn(value, 1); grid.Children.Add(slider); grid.Children.Add(value);
        AutomationProperties.SetName(slider, label);
        return Labeled(label, grid);
    }
    private sealed record Choice(string Value, string Label);
}
