param(
    [Parameter(Mandatory=$true)][int]$ExplorerProcessId,
    [Parameter(Mandatory=$true)][string]$DisplayDevice,
    [Parameter(Mandatory=$true)][int]$ExpectedWidth,
    [Parameter(Mandatory=$true)][int]$ExpectedHeight,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class ExplorerTestDisplay {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Info { public uint Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device; }
    public class Placement { public string Device; public int ClientWidth, ClientHeight, Left, Top, Right, Bottom; }
    delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref Info info);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    static Info ReadMonitor(IntPtr handle) {
        var info=new Info {Size=(uint)Marshal.SizeOf(typeof(Info))};
        if (!GetMonitorInfo(handle,ref info)) throw new InvalidOperationException("Cannot read window monitor");
        return info;
    }
    public static Placement Read(IntPtr window) {
        var previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        try {
            Rect bounds, client;
            var actual=ReadMonitor(MonitorFromWindow(window,2));
            if (!GetWindowRect(window,out bounds) || !GetClientRect(window,out client)) throw new InvalidOperationException("Cannot inspect Explorer viewport");
            return new Placement {Device=actual.Device,ClientWidth=client.Right,ClientHeight=client.Bottom,Left=bounds.Left,Top=bounds.Top,Right=bounds.Right,Bottom=bounds.Bottom};
        } finally { if (previous!=IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
    }
    public static Placement Place(IntPtr window, string device) {
        var previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        try {
            Info target=new Info(); bool found=false;
            MonitorCallback callback=delegate(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr data) {
                var info=ReadMonitor(monitor);
                if (String.Equals(info.Device,device,StringComparison.OrdinalIgnoreCase)) {target=info;found=true;}
                return true;
            };
            if (!EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero) || !found) throw new InvalidOperationException("Requested display unavailable");
            Rect bounds, client, originalClient;
            if (!GetClientRect(window,out originalClient)) throw new InvalidOperationException("Cannot read initial viewport");
            if (!GetWindowRect(window,out bounds)) throw new InvalidOperationException("Cannot read Explorer window");
            int width=bounds.Right-bounds.Left, height=bounds.Bottom-bounds.Top;
            if (width>target.Work.Right-target.Work.Left || height>target.Work.Bottom-target.Work.Top) throw new InvalidOperationException("Explorer window does not fit target work area");
            int x=target.Work.Left+(target.Work.Right-target.Work.Left-width)/2, y=target.Work.Top+(target.Work.Bottom-target.Work.Top-height)/2;
            if (!SetWindowPos(window,IntPtr.Zero,x,y,0,0,0x0015)) throw new InvalidOperationException("Cannot move Explorer window");
            var actual=ReadMonitor(MonitorFromWindow(window,2));
            if (!GetWindowRect(window,out bounds) || !GetClientRect(window,out client)) throw new InvalidOperationException("Cannot verify Explorer viewport");
            if (client.Right!=originalClient.Right || client.Bottom!=originalClient.Bottom) {
                // A monitor DPI change can alter borders while retaining the outer size.
                width=bounds.Right-bounds.Left+originalClient.Right-client.Right;
                height=bounds.Bottom-bounds.Top+originalClient.Bottom-client.Bottom;
                if (width>target.Work.Right-target.Work.Left || height>target.Work.Bottom-target.Work.Top) throw new InvalidOperationException("Adjusted viewport does not fit target work area");
                x=target.Work.Left+(target.Work.Right-target.Work.Left-width)/2;
                y=target.Work.Top+(target.Work.Bottom-target.Work.Top-height)/2;
                if (!SetWindowPos(window,IntPtr.Zero,x,y,width,height,0x0014) || !GetWindowRect(window,out bounds) || !GetClientRect(window,out client)) throw new InvalidOperationException("Cannot preserve viewport after display change");
                actual=ReadMonitor(MonitorFromWindow(window,2));
            }
            return new Placement {Device=actual.Device,ClientWidth=client.Right,ClientHeight=client.Bottom,Left=bounds.Left,Top=bounds.Top,Right=bounds.Right,Bottom=bounds.Bottom};
        } finally { if (previous!=IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
    }
}
"@
$process = Get-Process -Id $ExplorerProcessId
try {
    if ($process.ProcessName -ne 'Decentraland' -or $process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Expected an owned Explorer window.' }
    $placement = [ExplorerTestDisplay]::Place($process.MainWindowHandle,$DisplayDevice)
    [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    $record = @{ utc = [DateTime]::UtcNow.ToString('o'); process_id = $ExplorerProcessId; requested_device = $DisplayDevice; expected_width = $ExpectedWidth; expected_height = $ExpectedHeight; actual = $placement }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'window-placement.json'), ($record | ConvertTo-Json -Depth 4))
    if ($placement.Device -ne $DisplayDevice -or $placement.ClientWidth -ne $ExpectedWidth -or $placement.ClientHeight -ne $ExpectedHeight) {
        throw "Explorer did not retain the requested display and viewport; see window-placement.json."
    }
    Write-Host "Explorer verified on $($placement.Device) at $($placement.ClientWidth)x$($placement.ClientHeight)."
} finally { $process.Dispose() }