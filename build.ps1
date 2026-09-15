param([switch]$Publish, [switch]$WindowsChecks, [string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $PSScriptRoot
try {
    & $DotNet restore MiniTask.slnx --configfile NuGet.Config
    if ($LASTEXITCODE) { throw 'Restore failed.' }
    & $DotNet build MiniTask.slnx -c Release --no-restore
    if ($LASTEXITCODE) { throw 'Build failed.' }
    New-Item -ItemType Directory -Force -Path artifacts | Out-Null
    & $DotNet run --project tests/MiniTask.Core.Tests -c Release --no-build | Tee-Object -FilePath artifacts/core-tests.txt
    if ($LASTEXITCODE) { throw 'Engine tests failed.' }
    if ($WindowsChecks) {
        & $DotNet run --project tests/MiniTask.Ui.Tests -c Release --no-build -- $PSScriptRoot | Tee-Object -FilePath artifacts/ui-tests.txt
        if ($LASTEXITCODE) { throw 'UI checks failed.' }
        & $DotNet run --project tests/MiniTask.Windows.Tests -c Release --no-build | Tee-Object -FilePath artifacts/windows-tests.txt
        if ($LASTEXITCODE) { throw 'Windows checks failed or the desktop was unavailable. See artifacts/windows-tests.txt.' }
    }
    if ($Publish) {
        & $DotNet publish src/MiniTask.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true --configfile NuGet.Config -o artifacts/portable
        if ($LASTEXITCODE) { throw 'Publish failed.' }
        Copy-Item -LiteralPath README.md -Destination artifacts/portable/README.md -Force
        Copy-Item -Path docs -Destination artifacts/portable -Recurse -Force
        Compress-Archive -Path artifacts/portable/MiniTask.exe, artifacts/portable/README.md, artifacts/portable/docs -DestinationPath artifacts/MiniTask-win-x64.zip -Force
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $sourceZipPath = Join-Path $PSScriptRoot 'artifacts/MiniTask-source.zip'
        $sourceStream = [System.IO.File]::Open($sourceZipPath, [System.IO.FileMode]::Create)
        $sourceArchive = [System.IO.Compression.ZipArchive]::new($sourceStream, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            $sourceFiles = Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj|artifacts|\.git|\.vs)\\' }
            foreach ($sourceFile in $sourceFiles) {
                $entryName = 'MiniTask/' + $sourceFile.FullName.Substring($PSScriptRoot.Length + 1).Replace('\', '/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceArchive, $sourceFile.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        }
        finally { $sourceArchive.Dispose(); $sourceStream.Dispose() }
        Get-FileHash -Algorithm SHA256 artifacts/portable/MiniTask.exe, artifacts/MiniTask-win-x64.zip | Format-Table
    }
}
finally { Pop-Location }
