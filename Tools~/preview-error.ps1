# How close an editor render of a whole map (the -previewdebug <prefix>
# "-ortho.png") is to the game's own preview.png. Both are shrunk to -Size
# square and compared pixel by pixel:
#   rmse   RGB error, 0-255 (lower is better)
#   mean   mean colour of each, and the ratio editor/game
#   corr   correlation of the brightness pattern (1 = same light and dark
#          places, whatever the overall level)
# The game's preview has spawn icons on it; they add the same error to every
# variant, so compare variants of one map, not maps with each other.
#   powershell -File Tools~/preview-error.ps1 -Render x-ortho.png -Preview <map>\preview.png
param([Parameter(Mandatory)][string]$Render, [Parameter(Mandatory)][string]$Preview, [int]$Size = 96)
Add-Type -AssemblyName System.Drawing
function Shrink([string]$path) {
	$src = [Drawing.Image]::FromFile((Resolve-Path $path))
	$dst = New-Object Drawing.Bitmap $Size, $Size
	$g = [Drawing.Graphics]::FromImage($dst)
	$g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
	$g.DrawImage($src, 0, 0, $Size, $Size)
	$g.Dispose(); $src.Dispose()
	$px = New-Object 'double[]' ($Size * $Size * 3)
	for ($y = 0; $y -lt $Size; $y++) { for ($x = 0; $x -lt $Size; $x++) {
		$c = $dst.GetPixel($x, $y); $i = ($y * $Size + $x) * 3
		$px[$i] = $c.R; $px[$i + 1] = $c.G; $px[$i + 2] = $c.B } }
	$dst.Dispose()
	return ,$px
}
$a = Shrink $Render
$b = Shrink $Preview
$n = $Size * $Size
[double]$se = 0; $ma = @(0.0, 0.0, 0.0); $mb = @(0.0, 0.0, 0.0)
$la = New-Object 'double[]' $n; $lb = New-Object 'double[]' $n
for ($p = 0; $p -lt $n; $p++) {
	for ($c = 0; $c -lt 3; $c++) { $d = $a[$p * 3 + $c] - $b[$p * 3 + $c]; $se += $d * $d; $ma[$c] += $a[$p * 3 + $c]; $mb[$c] += $b[$p * 3 + $c] }
	$la[$p] = 0.2126 * $a[$p * 3] + 0.7152 * $a[$p * 3 + 1] + 0.0722 * $a[$p * 3 + 2]
	$lb[$p] = 0.2126 * $b[$p * 3] + 0.7152 * $b[$p * 3 + 1] + 0.0722 * $b[$p * 3 + 2]
}
$rmse = [Math]::Sqrt($se / ($n * 3))
for ($c = 0; $c -lt 3; $c++) { $ma[$c] /= $n; $mb[$c] /= $n }
$xa = ($la | Measure-Object -Average).Average; $xb = ($lb | Measure-Object -Average).Average
[double]$sab = 0; [double]$saa = 0; [double]$sbb = 0
for ($p = 0; $p -lt $n; $p++) { $u = $la[$p] - $xa; $v = $lb[$p] - $xb; $sab += $u * $v; $saa += $u * $u; $sbb += $v * $v }
$corr = $sab / [Math]::Sqrt($saa * $sbb)
"rmse {0:0.0}  corr {1:0.000}  mean editor {2:0} {3:0} {4:0}  game {5:0} {6:0} {7:0}  ratio {8:0.00} {9:0.00} {10:0.00}" -f $rmse, $corr, $ma[0], $ma[1], $ma[2], $mb[0], $mb[1], $mb[2], ($ma[0] / $mb[0]), ($ma[1] / $mb[1]), ($ma[2] / $mb[2])
