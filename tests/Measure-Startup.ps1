param([Parameter(Mandatory)][string[]]$Executable, [int]$Runs = 4, [string]$OutputDirectory = "$PSScriptRoot/../artifacts/startup-checks")
$ErrorActionPreference = 'Stop'
if (Get-Process -Name MiniTask -ErrorAction SilentlyContinue) { throw 'Close MiniTask before measuring startup; no existing instance will be stopped.' }
if ($Runs -lt 1 -or $Runs -gt 10) { throw 'Choose between 1 and 10 runs.' }
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class MiniTaskStartupNative {
    public delegate bool EnumProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowTextW(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeoutW(IntPtr window, uint message, IntPtr wp, IntPtr lp, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr window, uint message, IntPtr wp, IntPtr lp);
    public static IntPtr Toolbar(int id) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, data) => {
            uint process; GetWindowThreadProcessId(window, out process);
            if (process != id) return true;
            var title = new StringBuilder(256); GetWindowTextW(window, title, 256);
            if (title.ToString() == "MiniTask") { found = window; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static bool Responsive(IntPtr window) {
        IntPtr result;
        return SendMessageTimeoutW(window, 0, IntPtr.Zero, IntPtr.Zero, 2, 250, out result) != IntPtr.Zero;
    }
}
'@
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$results = [Collections.Generic.List[object]]::new()
foreach ($candidate in $Executable) {
    $path = (Resolve-Path -LiteralPath $candidate).Path
    $label = Split-Path (Split-Path $path -Parent) -Leaf
    # A fresh extraction directory approximates a first launch; OS caches are not flushed.
    $cache = Join-Path $outputRoot ($label + '-' + [Guid]::NewGuid().ToString('N'))
    for ($run = 1; $run -le $Runs; $run++) {
        $info = [Diagnostics.ProcessStartInfo]::new($path)
        $info.UseShellExecute = $false
        $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $info.Environment['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = $cache
        $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
        $started = $false
        $watch = [Diagnostics.Stopwatch]::StartNew()
        try {
            if (-not $process.Start()) { throw 'Could not start benchmark process.' }
            $started = $true
            if (-not $process.WaitForInputIdle(15000)) { throw 'Application did not reach an idle message loop.' }
            $window = [IntPtr]::Zero
            while ($watch.ElapsedMilliseconds -lt 15000) {
                if ($process.HasExited) { throw "Application exited early: $($process.ExitCode)" }
                $window = [MiniTaskStartupNative]::Toolbar($process.Id)
                if ($window -ne [IntPtr]::Zero -and [MiniTaskStartupNative]::Responsive($window)) { break }
                Start-Sleep -Milliseconds 10
            }
            if ($window -eq [IntPtr]::Zero -or -not [MiniTaskStartupNative]::Responsive($window)) { throw 'No responsive MiniTask toolbar was found.' }
            $watch.Stop()
            $row = [pscustomobject]@{ Candidate=$label; Run=$run; Cache= $(if ($run -eq 1) { 'Fresh extraction' } else { 'Warm extraction' }); Milliseconds=[math]::Round($watch.Elapsed.TotalMilliseconds, 1); ExeBytes=(Get-Item -LiteralPath $path).Length }
            $results.Add($row); Write-Output ($row | ConvertTo-Json -Compress)
            if (-not [MiniTaskStartupNative]::PostMessageW($window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero)) { throw 'Normal close request failed.' }
            if (-not $process.WaitForExit(5000)) { throw 'Application did not close normally.' }
            if ($process.ExitCode -ne 0) { throw "Application failed on exit: $($process.ExitCode)" }
        }
        finally {
            # Only the process created for this measurement can be stopped here.
            if ($started -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
            $process.Dispose()
        }
    }
}
$results | ConvertTo-Json | Set-Content (Join-Path $outputRoot 'results.json')
