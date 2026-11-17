# CydLan identity

An original C-shaped conversation bubble with an open connection and a peer dot. The mark represents direct conversation between nearby devices.

- `cydlan-mark.svg` and `cydlan-mark.png`: transparent mark.
- `cydlan-icon.svg` and `cydlan-icon.png`: rounded app tile.
- `cydlan.ico`: Windows executable and tray icon, with 16, 24, 32, 48, 64, 128, and 256px variants.
- `src/CydLan.Desktop/Brand.xaml`: editable WPF vector used by the running interface.

The accent is `#85B1BF`, the peer dot is `#EDF4F6`, and the icon tile is `#202729`. SVG and WPF paths share the same 64×64 coordinate system. Raster exports are 512×512 and preserve transparency. Keep the mark clear of surrounding text and do not stretch it.

To regenerate the PNG and ICO exports after editing `Brand.xaml`, run from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File scripts/Export-BrandAssets.ps1
```

This only sets the execution policy for that script process. The SVG companions are editable source assets and should be updated alongside geometry changes in the WPF source.
