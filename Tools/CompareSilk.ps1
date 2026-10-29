param([switch]$ScreensOnly,[int]$Seconds=60,[int]$Rounds=3)
$ErrorActionPreference='Stop'
if ($Seconds -lt 1 -or $Rounds -lt 1) { throw 'Seconds 和 Rounds 必须为正数。' }
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskPlayer=Join-Path $taskRoot 'Builds\Windows\MindState.exe'
$taskEvidence=Join-Path $taskRoot 'Evidence\SilkMicroDetail'
$taskLog=Join-Path $taskRoot 'SilkComparisonPlayer.log'
$taskArgs=@('-silkAB','-abSeconds',$Seconds,'-abRounds',$Rounds,'-evidencePath',('"'+$taskEvidence+'"'),'-screen-width','1920','-screen-height','1080','-windowed','-force-d3d11','-logFile',('"'+$taskLog+'"'))
if ($ScreensOnly) { $taskArgs+='-abScreensOnly' }
$taskProcess=Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle Hidden -Wait -PassThru
if ($taskProcess.ExitCode -ne 0) { throw "A/B 测试失败，参见 $taskLog" }
Get-Content -LiteralPath (Join-Path $taskEvidence 'ProfilerComparison.json')
