param([string]$Executable = "$PSScriptRoot\..\artifacts\portable\MiniTask.exe", [switch]$InputTest)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class MiniTaskSmokeNative {
    public delegate bool EnumProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", SetLastError=true)] public static extern bool PostMessageW(IntPtr window, uint message, UIntPtr wp, IntPtr lp);
    public static bool CloseToolbar(int processId, string expectedTitle) {
        bool posted = false;
        EnumWindows((window, parameter) => {
            uint pid; GetWindowThreadProcessId(window, out pid);
            if (pid != processId) return true;
            var title = new StringBuilder(256); GetWindowTextW(window, title, 256);
            if (title.ToString() == expectedTitle) { posted = PostMessageW(window, 0x10, UIntPtr.Zero, IntPtr.Zero); return false; }
            return true;
        }, IntPtr.Zero);
        return posted;
    }
}
'@
$resolvedExe = (Resolve-Path -LiteralPath $Executable).Path
if ($InputTest) { $process = Start-Process -FilePath $resolvedExe -ArgumentList '--input-test' -PassThru -WindowStyle Hidden }
else { $process = Start-Process -FilePath $resolvedExe -PassThru -WindowStyle Hidden }
Start-Sleep -Seconds 3
$process.Refresh()
if ($process.HasExited) { throw "Portable app exited prematurely: $($process.ExitCode)" }
$cpuStart = $process.TotalProcessorTime.TotalMilliseconds
$watch = [System.Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Seconds 3
$watch.Stop()
$process.Refresh()
$cpuMilliseconds = $process.TotalProcessorTime.TotalMilliseconds - $cpuStart
$workingSet = $process.WorkingSet64
$privateBytes = $process.PrivateMemorySize64
$expectedTitle = if ($InputTest) { 'MiniTask · Input test' } else { 'MiniTask' }
if (-not [MiniTaskSmokeNative]::CloseToolbar($process.Id, $expectedTitle)) { throw 'Could not find the portable window for graceful shutdown.' }
if (-not $process.WaitForExit(10000)) { throw 'Portable app did not exit after its normal Close request.' }
if ($process.ExitCode -ne 0) { throw "Portable app failed: $($process.ExitCode)" }
"PASS self-contained MiniTask.exe starts and closes through normal WPF shutdown without a separately installed .NET 10 runtime. Mode: $expectedTitle"
"Idle sample: {0:N0} working-set bytes; {1:N0} private bytes; {2:F3} ms CPU during {3:F3} ms wall time ({4:F3}% of one logical CPU)." -f $workingSet, $privateBytes, $cpuMilliseconds, $watch.Elapsed.TotalMilliseconds, (100 * $cpuMilliseconds / $watch.Elapsed.TotalMilliseconds)
