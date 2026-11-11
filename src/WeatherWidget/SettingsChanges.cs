using System.Security;
namespace WeatherWidget;

internal static class SettingsChanges
{
    // Preserve placement and alert history that can change while the editor is open.
    internal static bool Commit(AppSettings live,AppSettings draft,Action<bool> configureStartup,out string error,string? path=null,Action<string>? logger=null)
    {
        error="";
        var candidate=live.CreateDraft();candidate.CopyUserChoices(draft);
        bool startupChanged=candidate.StartWithWindows!=live.StartWithWindows;
        try
        {
            if(startupChanged) configureStartup(candidate.StartWithWindows);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or SecurityException)
        {error=ex.Message;return false;}
        if(!candidate.Save(path,logger))
        {
            error="Could not save settings.";
            if(startupChanged)
            {
                try {configureStartup(live.StartWithWindows);}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or SecurityException)
                {error+=" Startup registration could not be restored: "+ex.Message;}
            }
            return false;
        }
        live.CopyUserChoices(candidate);
        return true;
    }
}
