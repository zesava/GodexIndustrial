# Godex Industrial

Windows Forms application for sending GoDEX label commands through LAN (port 9100), a COM port, or a Windows printer queue.

## Build

Requirements: Windows, Visual Studio with the .NET Framework 4.7.2 targeting pack and MSBuild, and access to the FontAwesome.Sharp 6.6.0 NuGet package.

From a Visual Studio Developer PowerShell in the solution directory:

    msbuild WindowsFormsApp1.sln /restore /p:Configuration=Release

The application is in WindowsFormsApp1/bin/Release/GodexIndustrial.exe.

Run the lightweight checks with:

    msbuild Tests/Tests.csproj /p:Configuration=Debug
    Tests/bin/Debug/GodexIndustrial.Tests.exe

## Use

1. Select or create a label template. Width, length, gap, darkness, speed, text size, and X/Y offsets control the printer commands. Text rotation can be selected as 0°, 90°, 180°, or 270°; saved templates keep their numeric rotation value.
2. Open **Print data** and paste tab-separated cells from Excel. Choose whether to replace or append the rows. The template's column count determines how many cells may be imported.
3. Select LAN, COM, or USB in **Printer connection**. For LAN, enter an IPv4 address and click **Apply**. For COM and USB, choose the port or printer in the list.
4. Use **Preview** to inspect up to ten labels per page, each with a frame based on its width and length in millimetres. The gray strip represents the configured gap; dashed outlines mark text fields, and red marks text that extends beyond a label. Coordinates are converted for a 203 dpi printer (8 dots/mm). The preview rotates text according to the selected angle and remains an approximation; verify placement with a test print.
5. Click **Print**. A success message means the job was handed to the printer connection or Windows spooler; it does not confirm that paper was produced.

The current printer profile supports ASCII text. Non-ASCII characters cause a visible error instead of being silently replaced. Confirm the printer's character set before adding other encodings.

Templates, settings, and daily logs are stored in %LocalAppData%/GodexIndustrial. On first run, templates from a Templates folder beside the executable are copied there. Existing files in the new location take priority. Back up the application data directory to preserve custom templates.

## Troubleshooting

If a job fails, check the message and **Log** tab. LAN connections and status reads time out after three seconds. COM and USB require an available device selected in the corresponding list. The USB path uses the Windows RAW print API; the Windows printer queue must be installed for the device.

The automated checks cover TSV import, label command generation, template validation, and atomic file replacement. Printer output and physical label layout still require testing on the target GoDEX model.




