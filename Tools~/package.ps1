# Build the editor and zip what a user needs to run it.
#   powershell -File Tools~/package.ps1 [-Version v0.1] [-NoBuild] [-From Build]
# -From packages another build folder (e.g. while Build\ is open).
# Writes release\SanctuaryMapEditor-<version>-win64.zip: the exe, Unity's
# runtime files, the data folder, LICENSE.md and a short README.txt. Test
# screenshots and other files left in Build\ are not included. Publishing
# (a GitHub release) is a separate, deliberate step.
param([string]$Version = "v0.3.1", [switch]$NoBuild, [string]$From = "Build")
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = if ([IO.Path]::IsPathRooted($From)) { $From } else { Join-Path $root $From }
if (-not $NoBuild) {
	& (Join-Path $PSScriptRoot 'build.ps1') -Out $build | Write-Output
	if ($LASTEXITCODE -ne 0) { throw "build failed" }
}
$keep = @('SanctuaryMapEditor.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', 'SanctuaryMapEditor_Data', 'MonoBleedingEdge', 'D3D12')
foreach ($k in $keep[0..3]) { if (-not (Test-Path (Join-Path $build $k))) { throw "missing from Build: $k" } }

$out = Join-Path $root 'release'
New-Item -ItemType Directory -Force $out | Out-Null
$zip = Join-Path $out "SanctuaryMapEditor-$Version-win64.zip"
if (Test-Path $zip) { Remove-Item $zip }

$stage = Join-Path ([IO.Path]::GetTempPath()) ("sme-package-" + [Guid]::NewGuid().ToString('N'))
$dir = Join-Path $stage "SanctuaryMapEditor-$Version"
New-Item -ItemType Directory -Force $dir | Out-Null
foreach ($k in $keep) {
	$src = Join-Path $build $k
	if (Test-Path $src) { Copy-Item $src -Destination $dir -Recurse }
}
Copy-Item (Join-Path $root 'LICENSE.md') $dir
@"
Sanctuary Map Editor $Version
=============================

A map editor for Sanctuary: Shattered Sun. Run SanctuaryMapEditor.exe.

- It finds the game through Steam. If it doesn't, set the install folder
  in Edit > Preferences.
- File > Open takes a map's .sanmap (the game's Maps folder is
  Sanctuary_Data\Maps). File > Save writes it back; before overwriting, the
  files it rewrites are copied to %LOCALAPPDATA%\SanctuaryMapEditor\Backups.
- File > New Map makes a blank map; File > Save As copies the map to a new
  folder in Maps.
- Restart the game after saving a map: it reads maps at start.

Edits terrain, texture layers (click a layer's texture to pick another),
water, spawns and alloy spots, the playable area, and props (the Props
button). Lighting, sky and decals pass through unchanged.

Based on the FA Forever Map Editor by ozonex and contributors (MIT);
see LICENSE.md. Source and notes:
https://github.com/Remmyboy/sanctuary-map-editor
"@ | Set-Content -Encoding utf8 (Join-Path $dir 'README.txt')

# Entries written one by one with forward slashes: Windows PowerShell's
# ZipFile.CreateFromDirectory writes backslashes, which other unzippers
# turn into odd file names.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$fs = [IO.File]::Open($zip, 'CreateNew')
$archive = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
foreach ($f in Get-ChildItem $stage -Recurse -File) {
	$name = $f.FullName.Substring($stage.Length + 1).Replace('\', '/')
	[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $f.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
}
$archive.Dispose()
$fs.Dispose()
Remove-Item $stage -Recurse -Force
$mb = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Output "packaged $zip ($mb MB)"
