# Packaging

MiniTask is distributed as a portable Windows x64 executable with the .NET runtime included. Users do not need to install .NET separately.

## Create a download

With the .NET 10 SDK installed, run this from the repository root:

```powershell
./build.ps1 -Publish
```

This builds the app, runs the engine tests, and creates:

- `artifacts/portable/MiniTask.exe`
- `artifacts/MiniTask-win-x64.zip` — the app, README, guides, and runtime license notices.
- `artifacts/MiniTask-source.zip` — source code, build files, tests, and documentation.

The source archive excludes build outputs and local artifacts. Debug symbols are not included in the portable ZIP.

## Runtime and size

Most of the executable's size comes from the bundled .NET and Windows Desktop runtimes. Keep WPF trimming disabled. Native runtime files may be extracted into the user's temporary `.net` cache when the app starts.

A smaller build can use an installed runtime:

```powershell
dotnet publish src/MiniTask.Desktop -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o artifacts/framework-dependent
```

That build requires the matching .NET 10 Desktop Runtime on the user's computer.

The app is unsigned. Settings and diagnostics are stored in `%LOCALAPPDATA%\MiniTask`; recordings are saved only when the user chooses Save.
