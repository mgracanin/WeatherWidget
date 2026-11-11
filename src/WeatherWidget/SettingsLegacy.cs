using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WeatherWidget;
internal sealed partial class SettingsWindow
{
    private readonly TextBlock legacyStatus=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(0,6,0,8)};
    private void AddLegacyControls(StackPanel panel)
    {
        var details=new StackPanel();
        var expander=new Expander{Header=T("Opcionalne stare slike","Optional legacy artwork"),Content=details,IsExpanded=draft.Theme.StartsWith("legacy-",StringComparison.Ordinal),Margin=new Thickness(0,4,0,8)};panel.Children.Add(expander);
        details.Children.Add(new TextBlock{Text=T("Modern i Classic koriste izvorne crteže. Stari izgled dostupan je uz lokalno uvezene slike i odgovarajuća prava korištenja.","Modern and Classic use original artwork. Legacy appearances use optional local images where you have the necessary rights."),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray});
        var actions=new WrapPanel();
        actions.Children.Add(Button(T("Uvezi stare slike…","Import legacy images…"),(_,_)=>ImportLegacyImages()));
        var reload=Button(T("Ponovno učitaj","Reload local images"),(_,_)=>{WeatherArt.ReloadLegacy();UpdateThemePreview();UpdateLegacyStatus();});reload.Margin=new Thickness(8,2,0,2);actions.Children.Add(reload);
        var open=Button(T("Otvori mapu","Open local folder"),(_,_)=>
        {
            try{Directory.CreateDirectory(LegacyArtwork.Root);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LegacyArtwork.Root){UseShellExecute=true});}
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){legacyStatus.Text=ex.Message;}
        });open.Margin=new Thickness(8,2,0,2);actions.Children.Add(open);
        details.Children.Add(actions);details.Children.Add(legacyStatus);UpdateLegacyStatus();
        themeChoice.SelectionChanged+=(_,_)=>{if(themeChoice.SelectedValue is string selected&&selected.StartsWith("legacy-",StringComparison.Ordinal))expander.IsExpanded=true;};
    }
    private void UpdateLegacyStatus()=>legacyStatus.Text=T($"Prepoznate datoteke: {LegacyArtwork.AvailableCount}. Nedostajuće ili neispravne slike zamjenjuju se izvornim crtežima.",$"Recognized local files: {LegacyArtwork.AvailableCount}. Missing or invalid images fall back to original artwork.");
    private void ImportLegacyImages()
    {
        var picker=new Microsoft.Win32.OpenFolderDialog{Title=T("Odaberite Weather.Gadget ili mapu images","Select Weather.Gadget or its images folder")};
        if(picker.ShowDialog(this)!=true)return;
        try
        {
            int count=LegacyArtwork.Import(picker.FolderName);
            UpdateThemePreview();
            legacyStatus.Text=T($"Uvezeno slika: {count}. Odaberite stari izgled i primijenite postavke.",$"Imported {count} images. Select a Legacy theme and Apply settings.");
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or FormatException or InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException)
        {legacyStatus.Text=T("Uvoz nije dovršen: ","Import was not completed: ")+ex.Message;}
    }
}
