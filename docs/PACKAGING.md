# Packaging

MiniTask is distributed as a portable Windows x64 executable with the .NET runtime included. Users do not need to install .NET separately.

## Create a download

With the .NET 10 SDK installed, run this from the repository root:

```powershell
./build.ps1 -Publish
```

This builds the app, runs the engine tests, and creates:

- `artifacts/MiniTask.exe` — the standalone download, ready to distribute.
- `artifacts/portable/` — intermediate publish output, including developer debug symbols.

Users only need the executable. Runtime license notices are embedded and available under **Prefs → Tools → Licenses**. Guides remain in the repository.

Add `-SourceArchive` to also create `artifacts/MiniTask-source.zip`. This optional archive includes source code, build files, tests, and documentation; it excludes build outputs and local artifacts.

## Runtime and size

Most of the executable's size comes from the bundled .NET and WPF runtimes. Single-file compression is enabled, and runtime language resources are limited to English, matching the app's interface. The tray uses Windows APIs directly, so Windows Forms is not bundled. Keep WPF trimming disabled.

Native runtime files may be extracted into the user's temporary `.net` cache when the app starts. First launches can therefore take longer than subsequent launches. Compression also adds some startup work.

A smaller build can use an installed runtime:

```powershell
dotnet publish src/MiniTask.Desktop -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o artifacts/framework-dependent
```

That build requires the matching .NET 10 Desktop Runtime on the user's computer.

The app is unsigned. Settings and diagnostics are stored in `%LOCALAPPDATA%\MiniTask`; recordings are saved only when the user chooses Save.
