using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using WeatherWidget.Core;

namespace WeatherWidget;
internal sealed partial class SettingsWindow
{
    private readonly ComboBox savedLocations=new();
    private readonly TextBlock locationStatus=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,8),Foreground=Brushes.DimGray};
    private Button? windowsLocation,ipLocation,searchCity,saveLocation,removeLocation;
    private WeatherLocation? currentLocationDetails;
    private ILocationDiscovery? discovery;
    private CancellationTokenSource? locationRequest;
    private bool discoveryClosed,rebuildingLocations;

    private void AddLocationControls(StackPanel panel)
    {
        currentLocationDetails=draft.SavedLocations.FirstOrDefault(location=>SavedLocations.Same(location,draft.Latitude,draft.Longitude));
        panel.Children.Add(Section(T("Lokacija","Location")));
        var row=new DockPanel();
        searchCity=Button(T("Traži grad…","Search city…"),(_,_)=>PickCity());
        DockPanel.SetDock(searchCity,Dock.Right);row.Children.Add(searchCity);
        cityValue.VerticalAlignment=VerticalAlignment.Center;cityValue.TextWrapping=TextWrapping.Wrap;
        cityValue.Margin=new Thickness(0,0,12,0);row.Children.Add(cityValue);ShowDraftLocation();
        panel.Children.Add(Labeled(T("Trenutačna lokacija","Current location"),row));
        savedLocations.MinHeight=30;savedLocations.DisplayMemberPath=nameof(LocationChoice.Label);
        AutomationProperties.SetName(savedLocations,T("Spremljene lokacije","Saved locations"));
        savedLocations.SelectionChanged+=(_,_)=>
        {
            if(rebuildingLocations || savedLocations.SelectedItem is not LocationChoice selected)return;
            currentLocationDetails=selected.Location;SavedLocations.Select(draft,selected.Location);ShowDraftLocation();locationStatus.Text="";
        };
        panel.Children.Add(Labeled(T("Spremljene lokacije (do 10)","Saved locations (up to 10)"),savedLocations));
        var savedActions=new WrapPanel();
        saveLocation=Button(T("Spremi trenutačnu","Save current"),(_,_)=>
        {
            var current=currentLocationDetails is { } details&&SavedLocations.Same(details,draft.Latitude,draft.Longitude) ? details with {Name=draft.CityName} : new WeatherLocation(draft.CityName,draft.Latitude,draft.Longitude);
            bool added=SavedLocations.Add(draft.SavedLocations,current);
            locationStatus.Text=added ? T("Lokacija je dodana. Primijenite promjene za spremanje.","Location added. Apply changes to save it.")
                : T("Lokacija je već spremljena ili je dosegnuto ograničenje od 10.","Location is already saved or the limit of 10 has been reached.");
            RebuildSavedLocations();
        });
        removeLocation=Button(T("Ukloni spremljenu","Remove saved"),(_,_)=>
        {
            if(savedLocations.SelectedItem is not LocationChoice selected)return;
            SavedLocations.Remove(draft,selected.Location.Latitude,selected.Location.Longitude);RebuildSavedLocations();
            locationStatus.Text=T("Uklonjeno s popisa. Trenutačna prognoza ostaje odabrana do promjene lokacije.","Removed from the list. The active forecast stays selected until you choose another location.");
        });
        removeLocation.Margin=new Thickness(8,2,0,2);savedActions.Children.Add(saveLocation);savedActions.Children.Add(removeLocation);panel.Children.Add(savedActions);
        var detection=new WrapPanel{Margin=new Thickness(0,10,0,0)};
        windowsLocation=Button(T("Otkrij putem Windowsa","Use Windows location"),async(_,_)=>await DetectLocationAsync(false));
        ipLocation=Button(T("Procijeni putem IP-a","Estimate using IP"),async(_,_)=>await DetectLocationAsync(true));
        ipLocation.Margin=new Thickness(8,2,0,2);detection.Children.Add(windowsLocation);detection.Children.Add(ipLocation);panel.Children.Add(detection);
        panel.Children.Add(new TextBlock{Text=T("Samo na zahtjev. Windows traži dopuštenje. IP procjena šalje zahtjev servisu ipapi.co i može biti netočna zbog VPN-a ili pružatelja interneta. Lokacija se ne prati u pozadini.","On demand only. Windows requests permission. IP estimation contacts ipapi.co and may be inaccurate with a VPN or ISP routing. No background location tracking."),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(0,6,0,0)});
        panel.Children.Add(locationStatus);
        RebuildSavedLocations();
    }
    private void ShowDraftLocation()
    {
        cityValue.Text=draft.CityName;
        cityValue.ToolTip=FormattableString.Invariant($"{draft.CityName}\n{draft.Latitude:F2}, {draft.Longitude:F2}");
        if(!rebuildingLocations && savedLocations.Items.Count>0)
        {
            rebuildingLocations=true;
            savedLocations.SelectedItem=savedLocations.Items.OfType<LocationChoice>().FirstOrDefault(c=>SavedLocations.Same(c.Location,draft.Latitude,draft.Longitude));
            rebuildingLocations=false;
        }
        if(removeLocation!=null)removeLocation.IsEnabled=savedLocations.SelectedItem!=null;
    }
    private void RebuildSavedLocations()
    {
        rebuildingLocations=true;
        savedLocations.ItemsSource=draft.SavedLocations.Select(location=>new LocationChoice(location,string.Join(" · ",new[]{location.Name,location.Region,location.Country}.Where(text=>!string.IsNullOrWhiteSpace(text))))).ToArray();
        savedLocations.SelectedItem=savedLocations.Items.OfType<LocationChoice>().FirstOrDefault(c=>SavedLocations.Same(c.Location,draft.Latitude,draft.Longitude));
        savedLocations.IsEnabled=savedLocations.Items.Count>0;
        if(removeLocation!=null)removeLocation.IsEnabled=savedLocations.SelectedItem!=null;
        rebuildingLocations=false;
    }
    private async Task DetectLocationAsync(bool byIp)
    {
        if(locationRequest!=null)return;
        var own=new CancellationTokenSource(TimeSpan.FromSeconds(25));locationRequest=own;
        SetLocationBusy(true);
        locationStatus.Text=T("Traženje lokacije…","Finding location…");
        try
        {
            discovery??=new LocationDiscovery();
            var result=byIp?await discovery.FromIpAsync(own.Token):await discovery.FromWindowsAsync(own.Token);
            if(discoveryClosed || own.IsCancellationRequested)return;
            var found=result.Location;
            if(!byIp)found=found with {Name=T("Moja lokacija","My location")};
            if(!SavedLocations.Select(draft,found))throw new InvalidDataException("Location service returned invalid data.");
            currentLocationDetails=found;ShowDraftLocation();
            var coordinates=FormattableString.Invariant($"{found.Latitude:F2}, {found.Longitude:F2}");
            string accuracy=result.AccuracyMetres is { } metres&&double.IsFinite(metres)&&metres>=0 ? T($" · točnost oko {metres:0} m",$" · accuracy about {metres:0} m"):"";
            locationStatus.Text=(byIp?T("Približna IP lokacija: ","Approximate IP location: "):T("Windows lokacija: ","Windows location: "))+found.Name+" · "+coordinates+accuracy+
                T(". Provjerite i primijenite promjene za korištenje.",". Review and Apply changes to use it.");
        }
        catch(OperationCanceledException){if(!discoveryClosed)locationStatus.Text=T("Traženje je isteklo. Pokušajte ponovno ili odaberite grad.","Discovery timed out. Retry or choose a city.");}
        catch(UnauthorizedAccessException){if(!discoveryClosed)locationStatus.Text=T("Lokacija nije dostupna ili je pristup odbijen. Provjerite Windows Postavke → Privatnost i sigurnost → Lokacija. IP procjena ostaje zaseban izbor.","Location is unavailable or access was denied. Check Windows Settings → Privacy & security → Location. IP estimation remains a separate choice.");}
        catch(Exception ex) when(ex is HttpRequestException or IOException or JsonException or COMException or ArgumentException or InvalidOperationException or NotSupportedException)
        {if(!discoveryClosed)locationStatus.Text=T("Lokacija nije pronađena. Pokušajte drugu metodu ili tražite grad. ","Location was not found. Try another method or search for a city. ")+ex.Message;}
        finally
        {
            if(ReferenceEquals(locationRequest,own))locationRequest=null;
            own.Dispose();
            if(!discoveryClosed)SetLocationBusy(false);
        }
    }
    private void SetLocationBusy(bool busy)
    {
        foreach(var button in new[]{windowsLocation,ipLocation,searchCity,saveLocation})if(button!=null)button.IsEnabled=!busy;
        savedLocations.IsEnabled=!busy&&savedLocations.Items.Count>0;
        if(removeLocation!=null)removeLocation.IsEnabled=!busy&&savedLocations.SelectedItem!=null;
    }
    private sealed record LocationChoice(WeatherLocation Location,string Label);
}
