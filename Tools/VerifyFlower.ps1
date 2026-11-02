param([switch]$ScreensOnly,[int]$Seconds=20)
$ErrorActionPreference='Stop'
if ($Seconds -lt 1) { throw 'Seconds 必须为正数。' }
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskPlayer=Join-Path $taskRoot 'Builds\Validation\MindState.exe'
$taskEvidence=Join-Path $taskRoot 'Evidence\FlowerArt\LegacyWholeBud'
$taskArgs=@('-verifyFlower','-flowerSeconds',$Seconds,'-evidencePath',('"'+$taskEvidence+'"'),'-screen-width','1920','-screen-height','1080','-windowed','-force-d3d11','-logFile',('"'+(Join-Path $taskRoot 'FlowerPlayer.log')+'"'))
if ($ScreensOnly) { $taskArgs+='-flowerScreensOnly' }
$taskProcess=Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle Hidden -Wait -PassThru
if ($taskProcess.ExitCode -ne 0) { throw '整花验证失败，请检查 FlowerPlayer.log 和 Evidence/FlowerArt/LegacyWholeBud/Checks.txt。' }
Get-Content -LiteralPath (Join-Path $taskEvidence 'Validation.json')
