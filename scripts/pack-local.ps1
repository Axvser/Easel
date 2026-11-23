$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts/packages'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Get-ChildItem $out -Filter '*.nupkg' -ErrorAction SilentlyContinue | Remove-Item -Force

# Local test feed: build only the net8.0-windows runtime asset (compatible with the .NET 10 demo).
# The release pipeline packs every target framework with the older SDKs installed.
dotnet build (Join-Path $root 'Src/Easel/Easel.csproj') -c Release -p:EaselLocalTargetFramework=net8.0-windows
if ($LASTEXITCODE -ne 0) { throw 'Building Easel failed.' }

dotnet build (Join-Path $root 'Src/Generators/Easel.Generator/Easel.Generator.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Building Easel.Generator failed.' }

Get-ChildItem (Join-Path $root 'Src/Easel/bin/Release') -Filter 'Easel.*.nupkg' | Copy-Item -Destination $out
Get-ChildItem (Join-Path $root 'Src/Generators/Easel.Generator/bin/Release') -Filter 'Easel.Generator.*.nupkg' | Copy-Item -Destination $out

Write-Host "Packed Easel and Easel.Generator into $out"
Write-Host 'Build the demo against the packages with:'
Write-Host '  dotnet build Examples/XamlGallery/Demo/Demo.csproj -p:UseEaselPackage=true'
