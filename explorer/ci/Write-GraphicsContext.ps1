param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$result = [ordered]@{ utc = [DateTime]::UtcNow.ToString('o'); errors = @() }
function Read-Context([string]$Name, [scriptblock]$Read) {
    try { $result[$Name] = & $Read } catch { $result.errors += "$Name`: $($_.Exception.Message)" }
}
Read-Context 'system' { Get-CimInstance Win32_ComputerSystem -OperationTimeoutSec 5 | Select-Object Manufacturer,Model,NumberOfLogicalProcessors,TotalPhysicalMemory }
Read-Context 'cpu' { @(Get-CimInstance Win32_Processor -OperationTimeoutSec 5 | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors) }
Read-Context 'os' { Get-CimInstance Win32_OperatingSystem -OperationTimeoutSec 5 | Select-Object Caption,Version,BuildNumber }
Read-Context 'display_drivers' { @(Get-CimInstance Win32_PnPSignedDriver -Filter "DeviceClass='DISPLAY'" -OperationTimeoutSec 5 | Select-Object DeviceName,DeviceID,DriverProviderName,DriverVersion,InfName,IsSigned) }
Read-Context 'driver_packages' {
    @(foreach ($driver in $result.display_drivers) {
        Get-WindowsDriver -Online -Driver $driver.InfName | Select-Object Driver,OriginalFileName,ProviderName,ClassName,Version,Date
    })
}
Read-Context 'video_controllers' { @(Get-CimInstance Win32_VideoController -OperationTimeoutSec 5 | Select-Object Name,PNPDeviceID,DriverVersion,VideoProcessor,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate,Status) }
Read-Context 'session' {
    Add-Type -AssemblyName System.Windows.Forms
    @{ id = [Diagnostics.Process]::GetCurrentProcess().SessionId; remote = [Windows.Forms.SystemInformation]::TerminalServerSession; screens = @([Windows.Forms.Screen]::AllScreens | Select-Object DeviceName,Primary,Bounds) }
}
Read-Context 'active_display_paths' {
    if (-not ('ExplorerGraphicsPaths' -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class ExplorerGraphicsPaths {
    [StructLayout(LayoutKind.Sequential)] public struct Luid { public uint Low; public int High; public override string ToString() { return ((uint)High).ToString("X8") + Low.ToString("X8"); } }
    [StructLayout(LayoutKind.Sequential)] public struct Source { public Luid Adapter; public uint Id, Mode, Status; }
    [StructLayout(LayoutKind.Sequential)] public struct Target { public Luid Adapter; public uint Id, Mode, Technology, Rotation, Scaling, Numerator, Denominator, Scanline; public int Available; public uint Status; }
    [StructLayout(LayoutKind.Sequential)] public struct Path { public Source Source; public Target Target; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] public struct Header { public uint Type, Size; public Luid Adapter; public uint Id; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct AdapterName { public Header Header; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Name; }
    [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")] static extern int GetAdapterName(ref AdapterName name);
    static string AdapterPath(Luid id) {
        var name=new AdapterName {Header=new Header {Type=4,Size=(uint)Marshal.SizeOf(typeof(AdapterName)),Adapter=id}};
        int error=GetAdapterName(ref name);
        return error==0 ? name.Name : "Unavailable: " + error;
    }
    public class Entry { public string SourceAdapter, TargetAdapter, SourceDevicePath, TargetDevicePath; public uint SourceId, TargetId, OutputTechnology, RefreshNumerator, RefreshDenominator; }
    [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint paths, [In,Out] Path[] data, ref uint modes, IntPtr modeData, IntPtr topology);
    public static Entry[] Read() {
        for (int attempt=0; attempt<3; attempt++) {
            uint paths, modes;
            int error=GetDisplayConfigBufferSizes(2,out paths,out modes);
            if (error!=0) throw new InvalidOperationException("Display buffer query failed: " + error);
            if (paths>256 || modes>1024) throw new InvalidOperationException("Unexpected display count");
            var data=new Path[paths]; var memory=Marshal.AllocHGlobal(checked((int)modes*64));
            try {
                error=QueryDisplayConfig(2,ref paths,data,ref modes,memory,IntPtr.Zero);
                if (error==122) continue;
                if (error!=0) throw new InvalidOperationException("Display path query failed: " + error);
                var entries=new Entry[paths];
                for (int i=0;i<paths;i++) entries[i]=new Entry {SourceAdapter=data[i].Source.Adapter.ToString(),TargetAdapter=data[i].Target.Adapter.ToString(),SourceDevicePath=AdapterPath(data[i].Source.Adapter),TargetDevicePath=AdapterPath(data[i].Target.Adapter),SourceId=data[i].Source.Id,TargetId=data[i].Target.Id,OutputTechnology=data[i].Target.Technology,RefreshNumerator=data[i].Target.Numerator,RefreshDenominator=data[i].Target.Denominator};
                return entries;
            } finally { Marshal.FreeHGlobal(memory); }
        }
        throw new InvalidOperationException("Display topology kept changing");
    }
}
"@
    }
    @([ExplorerGraphicsPaths]::Read())
}
Read-Context 'nvidia' {
    $smi = (Get-Command nvidia-smi.exe -ErrorAction Stop).Source
    $xmlPath = Join-Path $OutputDirectory 'nvidia-context.xml'
    $errorPath = Join-Path $OutputDirectory 'nvidia-context-error.log'
    $command = Start-Process $smi -ArgumentList @('-q','-x') -WindowStyle Hidden -PassThru -RedirectStandardOutput $xmlPath -RedirectStandardError $errorPath
    try {
        if (-not $command.WaitForExit(10000)) { $command.Kill(); [void]$command.WaitForExit(5000); throw 'NVIDIA context timed out' }
        [xml]$xml = [IO.File]::ReadAllText($xmlPath)
        if (-not $xml.nvidia_smi_log) { throw 'NVIDIA context did not return a GPU report' }
        @{ driver_version = [string]$xml.nvidia_smi_log.driver_version; gpus = @(foreach ($gpu in $xml.nvidia_smi_log.gpu) {
            $item = [ordered]@{ name = [string]$gpu.product_name }
            foreach ($field in @('display_mode','display_active','display_attached','gpu_virtualization_mode','gpu_vgpu_licensed_product','performance_state','clocks','clocks_throttle_reasons','clocks_event_reasons','gpu_power_readings','power_readings')) {
                $node = $gpu.SelectSingleNode($field)
                if ($node) { $item[$field] = $node.InnerXml }
            }
            $item
        }) }
    } finally {
        $command.Dispose()
        if (Test-Path -LiteralPath $xmlPath) { Remove-Item -LiteralPath $xmlPath -Force }
    }
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'graphics-context.json'), ($result | ConvertTo-Json -Depth 8))