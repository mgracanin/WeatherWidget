# Choosing a location

Open **Settings → Location & units**. Pick or find a location, then click **Apply** or **Save and close** to use it. Cancel leaves the current forecast location unchanged.

## Search and saved locations

Use **Search city…** and choose the right city and region from the results.

To keep it for later, click **Save current**, then Apply. You can save up to ten locations. Choose one from **Saved locations** and Apply to switch forecasts. **Remove saved** removes the list entry; it does not change the current location.

Only the selected location downloads weather. Your list and last selection are remembered after you close the app.

## Find your location

You have two choices:

- **Use Windows location** asks Windows for a one-time position, using its location permissions. The result is called **My location**.
- **Estimate using IP** contacts ipapi.co for an approximate city. A VPN, mobile network or ISP gateway may place it somewhere else.

Check the result before clicking Apply. Neither option runs when the app starts or follows you in the background. If Windows location fails, the app leaves it to you to choose another method.

If access is denied, check **Windows Settings → Privacy & security → Location**, including the desktop-app location setting. You can still search for a city manually. If a request times out or the IP service is busy, retry later or use city search.

Detected coordinates are rounded to two decimal places for the forecast. No extra service is contacted to turn a Windows position into a street address or city name.

Preferences are saved in `%LOCALAPPDATA%\WeatherWidget\settings.json`.

[Windows location permissions](https://learn.microsoft.com/en-us/windows/apps/develop/maps-and-location/get-location) · [Windows location privacy](https://support.microsoft.com/en-us/windows/privacy/windows-location-service-and-privacy) · [ipapi.co API](https://ipapi.co/api/)
