# Artwork and asset inventory

The current application bundles **seven original vector themes**: Modern, Classic, Aurora, Paper, Sunset, Nordic and Forest. Modern and Classic use new glass-inspired sky compositions and weather symbols; they do not load or trace the removed Microsoft bitmaps. The vector renderer creates compact/expanded and day/night/weather scenes on demand, freezes them and caches a finite set.

`Assets/Brand` retains the editable SVG, generated window-icon PNG and application/embedded ICO. Different formats serve different purposes. The current tree and v0.6 package contain no legacy Weather.Gadget or WinPrognoza weather PNGs. README previews show the original themes with sample data.

Two additional choices—Legacy Modern and Legacy Classic—load optional user-provided PNGs from `%LOCALAPPDATA%\WeatherWidget\LegacyArtwork`. That folder is outside the repository/package and is preserved by upgrades. Unavailable or invalid images fall back per scene to original artwork. Importing/reloading invalidates presentation caches without background polling. See the [local import guide](legacy-artwork.md).

## Provenance and rights

The old Weather.Gadget manifest identifies Microsoft Corporation as author and includes © 2009. Any locally imported original artwork retains its original rights. Credits record provenance and do not grant permission, imply endorsement or authorize redistribution. Access to Windows 7 alone is not an import licence. Users must establish the appropriate rights for their use and must not distribute their local legacy pack with this application.

Older private tags/releases may retain earlier bundled legacy files; the removal described here applies to the current source tree and v0.6 release, without rewriting repository history.

[Notices](../NOTICE.md) · [Microsoft permissions policy](https://www.microsoft.com/en-us/legal/intellectualproperty/copyright/permissions)
