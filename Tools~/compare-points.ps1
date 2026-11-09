# Side-by-side colours of two same-size images at a grid of points (mean of a
# 7x7 patch at each), for comparing an editor preview with the game's.
#   powershell -File Tools~/compare-points.ps1 -A game.png -B editor.png [-Grid 5]
param([Parameter(Mandatory)][string]$A, [Parameter(Mandatory)][string]$B, [int]$Grid = 5)
Add-Type -AssemblyName System.Drawing
$ia = [Drawing.Bitmap]::FromFile((Resolve-Path $A))
$ib = [Drawing.Bitmap]::FromFile((Resolve-Path $B))
function Get-Patch($bmp, [int]$cx, [int]$cy) {
	[double]$sr = 0; [double]$sg = 0; [double]$sb = 0; [int]$cnt = 0
	for ($yy = $cy - 3; $yy -le $cy + 3; $yy++) { for ($xx = $cx - 3; $xx -le $cx + 3; $xx++) {
		if ($xx -lt 0 -or $yy -lt 0 -or $xx -ge $bmp.Width -or $yy -ge $bmp.Height) { continue }
		$pix = $bmp.GetPixel($xx, $yy); $sr += $pix.R; $sg += $pix.G; $sb += $pix.B; $cnt++ } }
	return ,@([int]($sr / $cnt), [int]($sg / $cnt), [int]($sb / $cnt))
}
"   x   y   game(A) RGB      editor(B) RGB    B-A"
for ($gy = 0; $gy -lt $Grid; $gy++) { for ($gx = 0; $gx -lt $Grid; $gx++) {
	$x = [int](($gx + 0.5) / $Grid * $ia.Width); $y = [int](($gy + 0.5) / $Grid * $ia.Height)
	$pa = Get-Patch $ia $x $y
	$pb = Get-Patch $ib ([int]($x * $ib.Width / $ia.Width)) ([int]($y * $ib.Height / $ia.Height))
	"{0,4} {1,4}   {2,3} {3,3} {4,3}      {5,3} {6,3} {7,3}      {8,4} {9,4} {10,4}" -f $x, $y, $pa[0], $pa[1], $pa[2], $pb[0], $pb[1], $pb[2], ($pb[0]-$pa[0]), ($pb[1]-$pa[1]), ($pb[2]-$pa[2])
} }
$ia.Dispose(); $ib.Dispose()
