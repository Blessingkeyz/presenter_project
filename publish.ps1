# Builds the Beacon Worship installer: one Setup.exe that installs the app into Program Files.
#
# From the repo folder, run:
#   powershell -ExecutionPolicy Bypass -File publish.ps1
#
# Output: dist\BeaconWorship-Setup.exe (the file to share).
# Needs Inno Setup 6 (free): winget install -e --id JRSoftware.InnoSetup

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "BiblePresenter.App\BiblePresenter.App.csproj"
$script = Join-Path $PSScriptRoot "installer\BeaconWorship.iss"
$dist = Join-Path $PSScriptRoot "dist"
$folder = Join-Path $dist "BeaconWorship"
$setup = Join-Path $dist "BeaconWorship-Setup.exe"
$zip = Join-Path $dist "BeaconWorship-win-x64.zip"

Remove-Item -Path $folder, $setup, $zip -Recurse -Force -ErrorAction SilentlyContinue

# Self-contained: the people you share it with don't need .NET installed. Single file: one exe to run.
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $folder
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

# The app reads these from the Resources folder beside the exe; a package without them is incomplete.
foreach ($required in @("BiblePresenter.App.exe", "Resources\KJV.xml", "Resources\Scripture.jpg")) {
    if (-not (Test-Path (Join-Path $folder $required))) { throw "Missing from the package: $required" }
}

Remove-Item -Path (Join-Path $folder "*.pdb") -Force -ErrorAction SilentlyContinue

# Inno Setup packs the published folder into the single installer.
$iscc = @("C:\Program Files (x86)\Inno Setup 6\ISCC.exe", "C:\Program Files\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 is not installed. Install it with: winget install -e --id JRSoftware.InnoSetup" }

& $iscc $script
if ($LASTEXITCODE -ne 0) { throw "Building the installer failed." }

$size = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ""
Write-Host "Done. Share this file: $setup ($size MB)"
Write-Host "Running it installs Beacon Worship into Program Files, with Start menu and uninstall entries."
