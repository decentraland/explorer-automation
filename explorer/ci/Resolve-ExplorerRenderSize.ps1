<#
.SYNOPSIS
Picks the size to render the Explorer at on a Windows CI host, from the display it actually has.

.DESCRIPTION
There is no runner setting for the virtual display, and nothing guarantees the driver exposes
1920x1080, so a fixed render size is a wish rather than a configuration. This asks for the target
mode when the driver lists it, then derives the size from whatever mode ends up active.

The UI scales on width against a 1920x1080 canvas, so what has to hold is the aspect ratio, not the
pixel count: a viewport wider than 16:9 costs the layout vertical room and pushes panel headers off
screen where a press can no longer reach them. Clamping width to height * 16/9 leaves the canvas at
least 1080 units tall on any host.

Writes EXPLORER_RENDER_WIDTH and EXPLORER_RENDER_HEIGHT to GITHUB_ENV when running under Actions,
and prints them either way, so it can be run by hand on a candidate runner image.
#>
[CmdletBinding()]
param(
    [int]$TargetWidth = 1920,
    [int]$TargetHeight = 1080,

    # Room for the title bar, borders and taskbar, so the window is not resized to fit after we
    # ask for it.
    [int]$Chrome = 96,

    # Below this the host cannot drive the UI at all and the run is not worth starting.
    [int]$MinWidth = 1024,
    [int]$MinHeight = 600
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class DisplayMode
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion; public short dmDriverVersion; public short dmSize; public short dmDriverExtra;
        public int dmFields; public int dmPositionX; public int dmPositionY;
        public int dmDisplayOrientation; public int dmDisplayFixedOutput;
        public short dmColor; public short dmDuplex; public short dmYResolution;
        public short dmTTOption; public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels; public int dmBitsPerPel; public int dmPelsWidth; public int dmPelsHeight;
        public int dmDisplayFlags; public int dmDisplayFrequency;
        public int dmICMMethod; public int dmICMIntent; public int dmMediaType; public int dmDitherType;
        public int dmReserved1; public int dmReserved2; public int dmPanningWidth; public int dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    public static extern bool EnumDisplaySettingsA(string deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    public static extern int ChangeDisplaySettingsExA(string deviceName, ref DEVMODE devMode, IntPtr hwnd, int flags, IntPtr param);
}
'@

$primary = [System.Windows.Forms.Screen]::PrimaryScreen
if (-not $primary) { throw "Windows did not expose a primary display" }
$device = $primary.DeviceName

# EnumDisplaySettings, not Screen.Bounds: PowerShell is not DPI-aware, so Bounds reports scaled
# pixels and under-reports the real mode.
function Get-ActiveMode {
    $mode = New-Object DisplayMode+DEVMODE
    $mode.dmSize = [int16][Runtime.InteropServices.Marshal]::SizeOf($mode)
    if (-not [DisplayMode]::EnumDisplaySettingsA($device, -1, [ref]$mode)) {
        throw "EnumDisplaySettings could not read the active mode of $device"
    }
    return $mode
}

$active = Get-ActiveMode
Write-Host "Active mode: $($active.dmPelsWidth)x$($active.dmPelsHeight) @ $($active.dmDisplayFrequency)Hz on $device"

$modes = @()
$i = 0
while ($true) {
    $m = New-Object DisplayMode+DEVMODE
    $m.dmSize = [int16][Runtime.InteropServices.Marshal]::SizeOf($m)
    if (-not [DisplayMode]::EnumDisplaySettingsA($device, $i, [ref]$m)) { break }
    $modes += [pscustomobject]@{ Width = $m.dmPelsWidth; Height = $m.dmPelsHeight; Hz = $m.dmDisplayFrequency }
    $i++
}
Write-Host "=== Modes the driver exposes ==="
$modes | Sort-Object Width, Height -Unique | ForEach-Object { "  $($_.Width)x$($_.Height)" } | Sort-Object -Unique

# Asking is free; a virtual display often has no mode this large, so treat refusal as information
# rather than a failure.
$wanted = $modes | Where-Object { $_.Width -eq $TargetWidth -and $_.Height -eq $TargetHeight } | Select-Object -First 1
if (-not $wanted) {
    Write-Host "Driver does not expose ${TargetWidth}x${TargetHeight}; keeping the active mode."
} elseif ($active.dmPelsWidth -eq $TargetWidth -and $active.dmPelsHeight -eq $TargetHeight) {
    Write-Host "Display is already at ${TargetWidth}x${TargetHeight}."
} else {
    $request = Get-ActiveMode
    $request.dmPelsWidth = $TargetWidth
    $request.dmPelsHeight = $TargetHeight
    # DM_PELSWIDTH | DM_PELSHEIGHT, applied with CDS_UPDATEREGISTRY so the mode survives.
    $request.dmFields = 0x80000 -bor 0x100000
    $result = [DisplayMode]::ChangeDisplaySettingsExA($device, [ref]$request, [IntPtr]::Zero, 0x00000001, [IntPtr]::Zero)
    if ($result -eq 0) {
        Write-Host "Switched the display to ${TargetWidth}x${TargetHeight}."
        $active = Get-ActiveMode
    } else {
        Write-Warning "ChangeDisplaySettingsEx returned $result; keeping the active mode."
    }
}

$height = $active.dmPelsHeight - $Chrome
$width = [Math]::Min($active.dmPelsWidth, [int][Math]::Round($height * 16 / 9))
if ($height -lt $MinHeight -or $width -lt $MinWidth) {
    throw "Active mode $($active.dmPelsWidth)x$($active.dmPelsHeight) leaves only ${width}x${height} for the Explorer, which is too small to drive the UI."
}

$canvas = [int](1920 * $height / $width)
Write-Host "Render size ${width}x${height} from a $($active.dmPelsWidth)x$($active.dmPelsHeight) display (UI canvas height $canvas)"

if ($env:GITHUB_ENV) {
    "EXPLORER_RENDER_WIDTH=$width" | Out-File $env:GITHUB_ENV -Append -Encoding utf8
    "EXPLORER_RENDER_HEIGHT=$height" | Out-File $env:GITHUB_ENV -Append -Encoding utf8
}
if ($env:GITHUB_STEP_SUMMARY) {
    "- Display: ``$($active.dmPelsWidth)x$($active.dmPelsHeight)``, Explorer render size: ``${width}x${height}`` (UI canvas height ``$canvas``)" |
        Out-File $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}
