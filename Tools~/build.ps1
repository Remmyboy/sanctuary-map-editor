# Build SanctuaryMapEditor.exe headlessly and print compile errors.
#   powershell -File Tools~/build.ps1 [-Out Build]
param([string]$Out = "Build", [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe")
$root = Split-Path $PSScriptRoot -Parent
$log = Join-Path $env:TEMP "sanctuary-map-editor-build.log"
$outPath = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out }
$p = Start-Process -FilePath $Unity -ArgumentList '-batchmode','-quit','-projectPath',"`"$root`"",'-executeMethod','SanctuaryBuild.Build','-buildPath',"`"$outPath`"",'-logFile',"`"$log`"" -Wait -PassThru
$errors = Select-String -Path $log -Pattern 'error CS\d+' | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique
$errors | ForEach-Object { Write-Output $_ }
Select-String -Path $log -Pattern 'SanctuaryBuild: ' | Select-Object -Last 1 | ForEach-Object { Write-Output $_.Line.Trim() }
Write-Output "unity exit $($p.ExitCode); $($errors.Count) compile error(s); log $log"
exit $p.ExitCode
