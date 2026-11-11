# Choosing a location

Open **Settings → Location & units**. Searching for a city, choosing a saved location or detecting a location changes a draft. Click **Apply** or **Save and close** to use it; **Cancel** discards pending changes.

| Method | What to expect |
| --- | --- |
| **Search city** | Choose a city and region from Open-Meteo search results. |
| **Saved locations** | Keep up to ten locations locally. Only the selected location downloads a forecast. |
| **Use Windows location** | Windows asks for permission, then supplies one position. The result is labelled My location; no extra reverse-geocoding service is contacted. |
| **Estimate using IP** | A separate HTTPS request to ipapi.co estimates the city associated with your public IP. A VPN, mobile network or ISP gateway may place it elsewhere. |

Discovery runs only when requested. It does not run on launch, retry through another provider automatically, subscribe to movement updates or track travel in the background. Detected coordinates are rounded to two decimal places, which is sufficient for a local weather forecast.

If Windows access is denied, check **Windows Settings → Privacy & security → Location** and the desktop-app location setting. You can keep using manual city search without enabling location services. An IP lookup is always a separate choice; Windows denial never triggers it.

If detection times out or the IP service is unavailable/rate-limited, the current location stays unchanged. Retry later or search for a city. Review approximate results before applying them.

Save current adds the chosen position to your saved list; Remove saved removes only the saved entry. Preferences and saved locations are stored in `%LOCALAPPDATA%\WeatherWidget\settings.json` and restored on the next launch.

[Windows location and permissions](https://learn.microsoft.com/en-us/windows/apps/develop/maps-and-location/get-location) · [Windows location privacy](https://support.microsoft.com/en-us/windows/privacy/windows-location-service-and-privacy) · [ipapi.co API](https://ipapi.co/api/)
