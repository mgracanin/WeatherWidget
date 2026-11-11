# Optional Windows 7 artwork

WeatherWidget works out of the box with **seven original vector themes**. Modern and Classic are original glass-inspired designs; **Legacy Modern** and **Legacy Classic** are optional appearances using images you supply locally. Nothing is downloaded automatically, and no Microsoft gadget PNGs are included in the current source tree or v0.6 package.

## Before importing

Only import files where your licence or permission allows this use. Access to a Windows 7 installation is not a new licence to copy its artwork into another product. Microsoft's Windows 7 terms, section 3(c), restrict sharing its images; attribution does not override those terms. This feature provides local image loading, not a licensing workaround. Imported files must not be committed or redistributed with WeatherWidget.

## Find the images

On a Windows 7 installation you are authorized to access, look for:

```text
C:\Program Files\Windows Sidebar\Gadgets\Weather.Gadget\images
```

Other installations can use `C:\Program Files (x86)\Windows Sidebar\Gadgets\Weather.Gadget\images`, the `Windows Sidebar\Shared Gadgets` directory, or `%LOCALAPPDATA%\Microsoft\Windows Sidebar\Gadgets\Weather.Gadget\images`. Microsoft's gadget documentation identifies the system, shared and per-user gadget directories; the Weather.Gadget folder may not be present on every installation.

Use an existing image folder or, where permitted, copy its PNG files to a local folder on the Windows 11 PC. You do not need to install or run the old Sidebar/Gadget software. The importer reads only recognized PNG names: it does not run JavaScript, copy gadget executables or change Windows access controls. If you cannot read the source files, use an authorized readable copy; the importer does not bypass permissions.

## Easiest: import from Settings

1. Right-click the widget or tray icon → **Settings… → Appearance**.
2. Expand **Optional legacy artwork** and click **Import legacy images…**.
3. Select the `Weather.Gadget` folder or its `images` folder. A folder containing the supported PNGs directly also works.
4. Read the imported-file count. Choose **Legacy Modern · local images** or **Legacy Classic · local images**, then **Apply**.

Only recognized images are copied to your user profile. Your source folder is not modified. Importing files is independent of Apply/Cancel for preferences: cancelling Settings does not delete an imported local pack. Updates preserve your local images because they are outside the app and repository.

An incomplete pack still works: each missing/unreadable scene falls back to the corresponding original Modern or Classic artwork. If there is no compact sunny/partly-cloudy image, the imported expanded scene is fitted automatically. Optional `sun.png` and `partlycloudy.png` cards from an authorized existing WinPrognoza collection are also recognized. Select their containing folder as an additional import to merge them into the local pack.

## Manual copy

Click **Open local folder** in that Settings section, or open this path with Win+R:

```text
%LOCALAPPDATA%\WeatherWidget\LegacyArtwork
```

Create that folder if necessary. Copy supported files directly into it, preserving filenames—no nested `images` folder at the destination:

```text
LegacyArtwork\
  docked_black_cloudy.png
  undocked_black_cloudy.png
  undocked_blue_sun.png
  ...
```

Then click **Reload local images**, choose a Legacy theme and Apply. Restarting the widget also reloads local files. Remove the local PNGs and reload to return legacy options to their original-art fallback; selecting Modern or Classic always uses original art regardless of imports.

## Recognized filenames

The following sky names work with both `docked_` and `undocked_` prefixes:

```text
black_cloudy
black_few-showers
black_foggy
black_hail
black_moon-full
black_moon-full_partly-cloudy
black_rainy
black_snow
black_thunderstorm
```

Add `.png` to each filename, for example `undocked_black_snow.png`. Loading files are `docked-loading.png` and `undocked-loading.png`. Daytime files are `undocked_blue_sun.png` and `undocked_blue_partly-cloudy.png`, plus optional `docked_blue_sun.png`, `docked_blue_partly-cloudy.png`, `sun.png`, and `partlycloudy.png`.

Other files and moon-phase variants are ignored. A PNG must be at most 1 MiB, no dimension above 1024 pixels and no more than 524,288 pixels in total. These limits cover the standard 130×67 and 264×194 gadget backgrounds. Invalid recognized files cause the import to stop before writing new files; correct the file or remove it from your selected source and retry. Missing/corrupt manually copied files fall back safely. A disk-write failure can leave some successfully imported images; retrying merges valid files.

[Microsoft gadget directories](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/Win7Samples/winui/sidebar/IDesktopGadget/README.txt) · [Windows 7 licence terms](https://www.microsoft.com/content/dam/microsoft/usetm/documents/windows/7-professional/oem-pre-installed/Windows_7%20Professional_English.pdf) · [Microsoft permissions policy](https://www.microsoft.com/en-us/legal/intellectualproperty/copyright/permissions)
