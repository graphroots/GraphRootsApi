# Obtain `GH_IO.dll` from Rhino

GrasshopperLib uses McNeel’s `GH_IO` to turn binary Grasshopper archives (`.gh`, and nested cluster byte arrays inside `.ghx`) into XML that the parser already understands. There is no NuGet package. The assembly is covered by the Rhino EULA and **must not be committed**; copy it from a local Rhino 8 install to `lib/GH_IO.dll`.

Use the plugin DLL, not `…/ref/net48/GH_IO.dll` (that one is a reference assembly).

| Platform | Plugin path |
| --- | --- |
| macOS | `/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Versions/A/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll` |
| Windows | `C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll` |

After a Rhino update, copy the plugin DLL over `lib/GH_IO.dll` and rebuild. `file GH_IO.dll` will still say “PE32 … for MS Windows”; that is normal for IL-only .NET assemblies and does not mean the binary is Windows-only.

The copy used while developing this tree was Grasshopper / Rhino 8, assembly version `8.33.26188.13002`.

## Why import of `.gh` files failed on macOS

XML `.ghx` files go through `XmlSerializer` and do not load this DLL for the outer archive. Binary `.gh` files (and cluster documents stored as `gh_bytearray`) call `GH_Archive.Deserialize_Binary`. That path constructs `System.Drawing.Bitmap` for thumbnail items (`gh_drawing_bitmap`).

Two things then go wrong if you run the CLI as a standalone `net10.0` process (not inside Rhino):

1. **`System.Drawing.Common` 7+ is Windows-only.** Version 10 looks up native `gdiplus.dll` via the Windows GDI+ stack (`System.Private.Windows.GdiPlus`). On macOS that fails with `DllNotFoundException`. Rhino itself does not hit this because it ships a Core Graphics `System.Drawing` implementation.
2. **The last Unix-capable package is `System.Drawing.Common` 6.0.0**, behind `System.Drawing.EnableUnixSupport`. Its loader looks for `libgdiplus` at `/usr/local/lib` (Intel Homebrew) and `/opt/local/lib` (MacPorts), **not** Apple Silicon Homebrew (`/opt/homebrew/lib`).

The importer therefore:

- Pins `System.Drawing.Common` **6.0.0** (`Directory.Packages.props`) and sets `System.Drawing.EnableUnixSupport` (`Directory.Build.props`).
- Stages `libgdiplus` next to the drawing assembly at startup (`GrasshopperLib/Parser/GdiPlusNative.cs`) from well-known install paths, including `/opt/homebrew/lib`.

Windows still uses OS GDI+; the Unix staging is a no-op there.

## Native dependency

| Platform | Package |
| --- | --- |
| macOS | `brew install mono-libgdiplus` |
| Debian / Ubuntu | `apt install libgdiplus` |

If `libgdiplus` is missing, `GhxArchiveConverter` throws a clear error instead of a raw `DllNotFoundException`.
