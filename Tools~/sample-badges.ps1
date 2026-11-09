# Read the slot-badge colours off a stock map's preview.png at the spawn
# positions its .sanmap records (preview covers PlayableArea, row 0 = north).
#   powershell -File Tools~/sample-badges.ps1 -Map The_Forge
param([string]$Map = "The_Forge", [string]$Maps = "C:\Program Files (x86)\Steam\steamapps\common\Sanctuary Shattered Sun Playtest\engine\Sanctuary_Data\Maps")
Add-Type -AssemblyName System.Drawing
$j = Get-Content (Join-Path $Maps "$Map\$Map.sanmap") -Raw | ConvertFrom-Json
$area = $j.areas.PlayableArea
$bmp = [Drawing.Bitmap]::FromFile((Join-Path $Maps "$Map\preview.png"))
$res = $bmp.Width
$radius = [int][Math]::Round($res * 0.035)
$i = 0
foreach ($army in $j.armies.PSObject.Properties.Name) {
	$i++
	$spawn = $j.markers.Spawn.transforms.PSObject.Properties | Where-Object { $_.Name -ieq $army } | Select-Object -First 1
	if (-not $spawn) { continue }
	$p = $spawn.Value.position
	$cx = [int](($p.x - $area.x) / $area.width * $res)
	$cy = [int](($area.y + $area.height - $p.z) / $area.height * $res)
	# Sample a ring at 70% of the badge radius, away from the digit.
	$cols = @()
	foreach ($ang in 0, 90, 180, 270) {
		$sx = $cx + [int]([Math]::Cos($ang * [Math]::PI / 180) * $radius * 0.7)
		$sy = $cy + [int]([Math]::Sin($ang * [Math]::PI / 180) * $radius * 0.7)
		if ($sx -ge 0 -and $sy -ge 0 -and $sx -lt $res -and $sy -lt $res) { $cols += $bmp.GetPixel($sx, $sy) }
	}
	$r = [int](($cols | Measure-Object -Property R -Average).Average)
	$g = [int](($cols | Measure-Object -Property G -Average).Average)
	$b = [int](($cols | Measure-Object -Property B -Average).Average)
	"slot {0} {1,-8} pixel ({2},{3}) colour {4} {5} {6}" -f $i, $army, $cx, $cy, $r, $g, $b
}
$bmp.Dispose()
