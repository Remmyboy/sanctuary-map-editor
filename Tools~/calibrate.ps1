# Render the developer maps' previews with given display settings and print
# the mean-colour ratio editor/game for each (Tools~/compare-preview.ps1 on
# the preview images themselves, so no window chrome is involved).
#   powershell -File Tools~/calibrate.ps1 -Extra @('-remapgain','2.5')
param([string[]]$Extra = @(), [string[]]$Maps = @('The_Forge', 'Two_Step_Shuffle', 'There_Is_Time', 'White_Desert'), [string]$Tag = 'cal')
$root = Split-Path $PSScriptRoot -Parent
$game = 'C:\Program Files (x86)\Steam\steamapps\common\Sanctuary Shattered Sun Playtest\engine\Sanctuary_Data\Maps'
Add-Type -AssemblyName System.Drawing
function Get-Mean($path) {
	$bmp = [Drawing.Bitmap]::FromFile($path)
	[double]$r = 0; [double]$g = 0; [double]$b = 0; [int]$n = 0
	for ($y = 2; $y -lt $bmp.Height - 2; $y += 3) { for ($x = 2; $x -lt $bmp.Width - 2; $x += 3) {
		$p = $bmp.GetPixel($x, $y); $r += $p.R; $g += $p.G; $b += $p.B; $n++ } }
	$bmp.Dispose()
	return ,@(($r / $n), ($g / $n), ($b / $n))
}
$sum = @(0.0, 0.0, 0.0); $err = 0.0
foreach ($m in $Maps) {
	$out = Join-Path $root "Build\$Tag-$m"
	$null = & (Join-Path $PSScriptRoot 'open-map.ps1') -Map $m -Extra (@('-previewdebug', "`"$out`"") + $Extra)
	$ed = Get-Mean "$out-ortho.png"
	$gm = Get-Mean (Join-Path $game "$m\preview.png")
	$ratio = @(($ed[0] / $gm[0]), ($ed[1] / $gm[1]), ($ed[2] / $gm[2]))
	"{0,-18} editor {1,3:0} {2,3:0} {3,3:0}  game {4,3:0} {5,3:0} {6,3:0}  ratio {7:0.00} {8:0.00} {9:0.00}" -f $m, $ed[0], $ed[1], $ed[2], $gm[0], $gm[1], $gm[2], $ratio[0], $ratio[1], $ratio[2]
	foreach ($c in 0..2) { $err += [Math]::Abs([Math]::Log($ratio[$c])) }
}
"mean |log ratio| = {0:0.000}" -f ($err / ($Maps.Count * 3))
