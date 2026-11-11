# WeatherWidget

Standalone Windows 11 desktop weather widget, replacing the original XWidget-based WinPrognoza.

## Planned implementation

- Native WPF application on .NET 10, independent of XWidget.
- Compact weather card using the original artwork.
- Current weather from Open-Meteo, with Zagreb as the default city.
- City search, Celsius/Fahrenheit, refresh, dragging and saved preferences.
- Optional startup with Windows.

Application implementation has not started yet. This repository currently contains the image resources and project setup.

## Resources

- `Assets/WeatherGadget`: images copied from the supplied Weather.Gadget/images folder, including DPI variants.
- `Assets/WinPrognoza`: PNG images from the original WinPrognoza widget, preserving their relative folders.
- `Assets/OriginalCards`: the original WinPrognoza card image set, copied during setup.

Original files remain unchanged in the portable XWidget installation. Artwork retains its original ownership; this private repository does not grant redistribution rights.

## Development

The local .NET SDK is in `tools/dotnet`, excluded from Git. Use an installed .NET 10 SDK or run `tools/dotnet/dotnet.exe` locally. No credentials are stored in this repository.

Weather provider documentation: https://open-meteo.com/en/docs
