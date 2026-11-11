# Windows 7 gadget images

Modern and Classic come with new artwork and work straight away. **Legacy Modern** and **Legacy Classic** let you use your own copies of the old gadget images.

The images are not included with WeatherWidget. Import only where you have the necessary rights. A Windows 7 installation or a copyright credit does not, by itself, give permission to reuse or share Microsoft's artwork. [NOTICE.md](../NOTICE.md) has the relevant links.

## 1. Find the image folder

On the Windows 7 PC, press **Win+R**, paste this path and press Enter:

```text
C:\Program Files\Windows Sidebar\Gadgets\Weather.Gadget\images
```

If it is not there, check these locations:

```text
C:\Program Files (x86)\Windows Sidebar\Gadgets\Weather.Gadget\images
C:\Program Files\Windows Sidebar\Shared Gadgets\Weather.Gadget\images
%LOCALAPPDATA%\Microsoft\Windows Sidebar\Gadgets\Weather.Gadget\images
```

If you are reading a Windows 7 drive from another PC, replace `C:` with that drive's letter. Some installations no longer have the Weather gadget files; the included themes work without them.

Where permitted, copy the `images` folder to a local folder on your Windows 11 PC, for example `Documents\Weather gadget images`. You only need the PNGs. The old Sidebar/Gadget software does not need to be installed or running.

## 2. Import the folder

1. Right-click the widget or tray icon and choose **Settings…**.
2. Open **Appearance** and expand **Optional legacy artwork**.
3. Click **Import legacy images…**.
4. Select your copied `images` folder. Selecting the parent `Weather.Gadget` folder also works.
5. Check the imported-file count, then select **Legacy Modern · local images** or **Legacy Classic · local images** from the theme list.
6. Click **Apply** or **Save and close**.

**Classic · original** is the new built-in design. Choose **Legacy Classic · local images** to see your imported files. The same distinction applies to Modern.

The import leaves your source files alone and keeps a copy in your user profile. Importing files is a separate action from saving preferences: Cancel closes Settings without changing the theme, but it does not remove files you have already imported.

You can import another folder later to add to the same collection. Existing WinPrognoza cards named `sun.png` and `partlycloudy.png` are supported too, where you have the right to use them.

## Manual copying

In **Appearance → Optional legacy artwork**, click **Open local folder**. You can also open this path with Win+R:

```text
%LOCALAPPDATA%\WeatherWidget\LegacyArtwork
```

Create the folder if necessary. Copy supported PNGs directly into it, keeping their names:

```text
LegacyArtwork\
  docked_black_cloudy.png
  undocked_black_cloudy.png
  undocked_blue_sun.png
  ...
```

Do not put another `images` folder inside `LegacyArtwork`. After copying, click **Reload local images**, choose a Legacy theme and Apply. Restarting the widget reloads them too.

Your local collection is outside the app folder, so updates keep it. Selecting a built-in theme uses the included artwork. To stop using a local image, remove that PNG from `LegacyArtwork` and reload.

## If something looks wrong

| What happens | What to check |
| --- | --- |
| No supported images found | Select `Weather.Gadget`, its `images` folder, or a folder containing the PNGs directly. |
| Some weather uses the new artwork | The matching old image is missing or unreadable. The app fills the gap with the built-in design. |
| Sunny cards look different from the old small card | If a compact image is missing, the larger sunny scene is fitted to the card. Optional compact images can be added later. |
| Manual copies do not appear | Click **Reload local images** and make sure a **Legacy** theme is selected. |
| A file is rejected | Copy it again from your source. Keep the original filename and PNG format. See the size limits below. |

A failed disk write can leave some successfully copied images. Importing the folder again will merge the valid files.

## Supported filenames

These names work with both `docked_` and `undocked_` prefixes, followed by `.png`:

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

For example: `docked_black_cloudy.png` and `undocked_black_cloudy.png`.

Loading images are `docked-loading.png` and `undocked-loading.png`. Daytime images are `undocked_blue_sun.png` and `undocked_blue_partly-cloudy.png`. The importer also accepts `docked_blue_sun.png`, `docked_blue_partly-cloudy.png`, `sun.png` and `partlycloudy.png` if available. Other filenames are ignored.

Each PNG must be at most **1 MiB**, with neither dimension above **1024 pixels** and no more than **524,288 pixels** in total. The usual 130×67 and 264×194 backgrounds fit within those limits. All recognized files are checked before copying begins.

[Microsoft's gadget folder documentation](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/Win7Samples/winui/sidebar/IDesktopGadget/README.txt) · [Windows 7 licence terms](https://www.microsoft.com/content/dam/microsoft/usetm/documents/windows/7-professional/oem-pre-installed/Windows_7%20Professional_English.pdf) · [Microsoft permissions policy](https://www.microsoft.com/en-us/legal/intellectualproperty/copyright/permissions)
