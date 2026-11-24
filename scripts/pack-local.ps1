$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts/packages'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Get-ChildItem $out -Filter '*.nupkg' -ErrorAction SilentlyContinue | Remove-Item -Force

# Build the generator first: the runtime's Release reference is the NuGet package.
dotnet build (Join-Path $root 'Src/Generators/Easel.Generator/Easel.Generator.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Building Easel.Generator failed.' }

# Local test feed: build only the net8.0-windows runtime asset (compatible with the .NET 10 demo).
# The release pipeline packs every target framework with the older SDKs installed.
# EaselLocalGenerator keeps local packing on the working tree's generator; CI uses the published package.
dotnet build (Join-Path $root 'Src/Easel/Easel.csproj') -c Release -p:EaselLocalTargetFramework=net8.0-windows -p:EaselLocalGenerator=true
if ($LASTEXITCODE -ne 0) { throw 'Building Easel failed.' }

Get-ChildItem (Join-Path $root 'Src/Easel/bin/Release') -Filter 'Easel.*.nupkg' | Copy-Item -Destination $out
Get-ChildItem (Join-Path $root 'Src/Generators/Easel.Generator/bin/Release') -Filter 'WinForms.Easel.Generator.*.nupkg' | Copy-Item -Destination $out

Write-Host "Packed WinForms.Easel and WinForms.Easel.Generator into $out"
Write-Host 'Build the demo against the packages with:'
Write-Host '  dotnet build Examples/XamlGallery/Demo/Demo.csproj -p:UseEaselPackage=true'
