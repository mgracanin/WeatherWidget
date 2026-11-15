using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WeatherWidget;

internal sealed partial class SettingsWindow
{
    private UIElement AboutPage()
    {
        var panel=PageStack();
        var heading=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,12)};
        if (Icon is { } icon)
            heading.Children.Add(new Image {Source=icon,Width=56,Height=56,Margin=new Thickness(0,0,14,0)});
        var title=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
        title.Children.Add(new TextBlock {Text="WeatherWidget",FontSize=23,FontWeight=FontWeights.SemiBold});
        var version=typeof(App).Assembly.GetName().Version?.ToString(3) ?? "";
        title.Children.Add(new TextBlock {Text=T("Verzija ","Version ")+version,Foreground=Brushes.DimGray,Margin=new Thickness(0,3,0,0)});
        heading.Children.Add(title);panel.Children.Add(heading);
        void Paragraph(string text) => panel.Children.Add(new TextBlock {Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});
        Paragraph(T("Mali vremenski widget za Windows 11, nadahnut gadgetima iz Windowsa 7.",
            "A small weather widget for Windows 11, inspired by the Windows 7 desktop gadgets."));
        Paragraph("Matija Gračanin");
        panel.Children.Add(AboutLink(T("Projekt na GitHubu","Project on GitHub"),"https://github.com/mgracanin/WeatherWidget"));
        panel.Children.Add(AboutLink(T("Prijavite problem ili predložite značajku","Report a problem or suggest a feature"),"https://github.com/mgracanin/WeatherWidget/issues"));
        panel.Children.Add(Section(T("Podaci o vremenu i lokaciji","Weather and location data")));
        Paragraph(T("Prognoza: Open-Meteo · CC BY 4.0. Pretraživanje gradova temelji se na podacima GeoNamesa. Neobavezno određivanje lokacije prema IP adresi koristi ipapi.co.",
            "Forecasts: Open-Meteo · CC BY 4.0. City search is based on GeoNames data. Optional IP location lookup uses ipapi.co."));
        var providers=new WrapPanel();
        providers.Children.Add(AboutLink("Open-Meteo","https://open-meteo.com/"));
        providers.Children.Add(AboutLink("CC BY 4.0","https://creativecommons.org/licenses/by/4.0/"));
        providers.Children.Add(AboutLink("GeoNames","https://www.geonames.org/"));
        providers.Children.Add(AboutLink("ipapi.co","https://ipapi.co/"));
        panel.Children.Add(providers);
        panel.Children.Add(Section(T("Izgled i zasluge","Artwork and credits")));
        Paragraph(T("Sedam uključenih tema koristi crteže izrađene za WeatherWidget. Stare teme koriste vaše lokalno uvezene slike; slike iz Windowsa 7 nisu uključene.",
            "The seven included themes use artwork made for WeatherWidget. Legacy themes use images you import locally; Windows 7 images are not included."));
        panel.Children.Add(AboutLink(T("Zasluge, licence i napomene","Credits, licences and notices"),"https://github.com/mgracanin/WeatherWidget/blob/main/NOTICE.md"));
        panel.Children.Add(AboutLink(T("Kako dodati stare slike","How to add legacy artwork"),"https://github.com/mgracanin/WeatherWidget/blob/main/docs/legacy-artwork.md"));
        return panel;
    }

    private TextBlock AboutLink(string label,string url)
    {
        var text=new TextBlock {Margin=new Thickness(0,0,16,8),TextWrapping=TextWrapping.Wrap};
        var link=new Hyperlink(new Run(label)) {NavigateUri=new Uri(url),ToolTip=url};
        AutomationProperties.SetName(link,label);
        link.RequestNavigate+=(_,e)=>
        {
            e.Handled=true;
            try {Process.Start(new ProcessStartInfo(url) {UseShellExecute=true});}
            catch (Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {applyStatus.Text=T("Poveznicu nije moguće otvoriti: ","Unable to open the link: ")+url;}
        };
        text.Inlines.Add(link);
        return text;
    }
}
