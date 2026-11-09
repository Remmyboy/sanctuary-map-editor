# Open a Sanctuary map in the built editor, screenshot it, quit, and show the
# loader's log lines.
#   powershell -File Tools~/open-map.ps1 -Map Two_Step_Shuffle [-Shot out.png] [-Keep]
param(
	[Parameter(Mandatory)][string]$Map,
	[string]$Shot,
	[switch]$Keep,
	[int]$TimeoutSec = 180,
	[string[]]$Extra = @()
)
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "Build\SanctuaryMapEditor.exe"
if (-not (Test-Path $Map)) {
	$maps = "C:\Program Files (x86)\Steam\steamapps\common\Sanctuary Shattered Sun Playtest\engine\Sanctuary_Data\Maps"
	$Map = Join-Path $maps $Map
}
if (-not $Shot) { $Shot = Join-Path $root ("Build\shot-" + (Split-Path $Map -Leaf) + ".png") }
elseif (-not [IO.Path]::IsPathRooted($Shot)) { $Shot = Join-Path $root $Shot }
Remove-Item $Shot -ErrorAction SilentlyContinue
$args = @('-sanmap', "`"$Map`"", '-screenshot', "`"$Shot`"", '-screen-width', '1600', '-screen-height', '900', '-screen-fullscreen', '0')
if (-not $Keep) { $args += '-quit' }
$args += $Extra
$p = Start-Process -FilePath $exe -ArgumentList $args -PassThru
if (-not $p.WaitForExit($TimeoutSec * 1000) -and -not $Keep) { Stop-Process -Id $p.Id; Write-Output "timed out; killed pid $($p.Id)" }
$log = Join-Path $env:USERPROFILE "AppData\LocalLow\Sanctuary Mods\Sanctuary Map Editor\Player.log"
Select-String -Path $log -Pattern 'Sanctuary|Exception|error|Screenshot' | ForEach-Object { $_.Line } | Select-Object -First 40
if (Test-Path $Shot) { Write-Output "screenshot: $Shot" } else { Write-Output "no screenshot" }
