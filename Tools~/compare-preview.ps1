# Mean colour of the game's preview.png against the playable area in an
# editor screenshot (top-down, as open-map.ps1 takes them). Prints both and the
# per-channel ratio editor/preview.
#   powershell -File Tools~/compare-preview.ps1 -Shot Build\shot-The_Forge.png -Preview Build\preview-The_Forge.png
param([Parameter(Mandatory)][string]$Shot, [Parameter(Mandatory)][string]$Preview, [int]$X = 0, [int]$Y = 0, [int]$W = 0, [int]$H = 0)
Add-Type -AssemblyName System.Drawing
function Get-MeanRgb($bmp, [int]$left, [int]$top, [int]$wide, [int]$tall) {
	[double]$sumR = 0; [double]$sumG = 0; [double]$sumB = 0; [int]$cnt = 0
	for ($py = $top; $py -lt $top + $tall; $py += 2) { for ($px = $left; $px -lt $left + $wide; $px += 2) {
		$pix = $bmp.GetPixel($px, $py); $sumR += $pix.R; $sumG += $pix.G; $sumB += $pix.B; $cnt++ } }
	return ,@(($sumR / $cnt), ($sumG / $cnt), ($sumB / $cnt))
}
$s = [Drawing.Bitmap]::FromFile((Resolve-Path $Shot))
$p = [Drawing.Bitmap]::FromFile((Resolve-Path $Preview))
if ($W -eq 0) {
	# Find the playable area: the bounding box of non-black pixels right of the side panel.
	$minX = $s.Width; $minY = $s.Height; $maxX = 0; $maxY = 0
	for ($y = 40; $y -lt $s.Height - 120; $y += 4) { for ($x = 320; $x -lt $s.Width; $x += 4) {
		$c = $s.GetPixel($x, $y); if ($c.R + $c.G + $c.B -gt 60) { if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }; if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y } } } }
	$X = $minX + 8; $Y = $minY + 8; $W = $maxX - $minX - 16; $H = $maxY - $minY - 16
}
$ed = Get-MeanRgb $s $X $Y $W $H
$pv = Get-MeanRgb $p 4 4 ($p.Width - 8) ($p.Height - 8)
"editor  rect ($X,$Y,$W,$H) mean RGB {0:0} {1:0} {2:0}" -f $ed[0], $ed[1], $ed[2]
"preview mean RGB {0:0} {1:0} {2:0}" -f $pv[0], $pv[1], $pv[2]
"ratio editor/preview {0:0.00} {1:0.00} {2:0.00}" -f ($ed[0] / $pv[0]), ($ed[1] / $pv[1]), ($ed[2] / $pv[2])
$s.Dispose(); $p.Dispose()
