# Portable packaging

The release executable is **MiniTask.exe**, Windows x64, self-contained .NET 10.0.12 with WPF. No installer or separately installed runtime is required.

Version 1.1.1 measured executable size: **173,168,698 bytes (165.1 MiB)**. This is much larger than TinyTask because it includes the managed runtime and Windows desktop framework. ZIP compression reduces download size; the precise final archive size and SHA-256 values are recorded in the accompanying `release-metrics-v1.1.1.json` and `SHA256SUMS-v1.1.1.txt`.

The portable ZIP contains the executable, README, guides, format/compatibility/validation documents, and .NET license notices. Debug symbol files remain in the build directory but are not required or included in the portable ZIP. The separate source ZIP contains the complete source, icon, solution, build script, tests and documentation, excluding generated binaries, object files, runtime downloads and artifacts.

The executable is unsigned; code signing and reputation establishment were not available for this delivery. There is no installer, automatic update mechanism or network access in the application. Runtime native libraries may be extracted into the user's temporary `.net` cache by the .NET single-file host. Preferences and diagnostic codes are stored in `%LOCALAPPDATA%\MiniTask`; portable distribution does not mean zero local files.

## Resource observation

The original 1.0 toolbar's short startup-idle sample measured the values below. These are historical measurements, not a benchmark of the redesigned 1.1 toolbar:

- Working set: **121,049,088 bytes (115.4 MiB)**.
- Private bytes: **65,662,976 bytes (62.6 MiB)**.
- CPU: **78.125 ms during 3,009.417 ms**, approximately **2.596% of one logical processor**.

This was a three-second sample after three seconds of startup settling, not a long-running benchmark. Idle usage varies with WPF rendering, host desktop activity and Windows scheduling. No recording was active in that sample. Large recordings use additional memory up to the bounded capture/file limits.

## Rebuild tradeoffs

The default packaging preserves an untrimmed WPF runtime for reliability and uses single-file bundling with native-library extraction. A framework-dependent publish is smaller but requires the matching Windows Desktop runtime:

```powershell
dotnet publish src/MiniTask.Desktop -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o artifacts/framework-dependent
```

That alternative was not measured or delivered as the primary package. Do not enable trimming/AOT or omit framework files merely to reduce the size without revalidating WPF and interop behavior. Microsoft runtime license notices are included under `runtime-licenses/`.
