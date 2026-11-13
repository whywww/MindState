param([string]$Unity='C:\Program Files\Unity 2022.3.62f3c1\Editor\Unity.exe',[switch]$PlaybackOnly)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskRoot 'Outputs'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
if(-not (Test-Path -LiteralPath $Unity)){throw "Unity not found: $Unity"}
$taskArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','MindState.FlowerBakeRunner.Run','-bakeFlower','-logFile',('"'+(Join-Path $taskOutput 'Bake.log')+'"'))
if($PlaybackOnly){$taskArgs+='-bakePlayback'}else{$taskArgs+='-rebuildBottom'}
$taskProcess=Start-Process -FilePath $Unity -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -Wait -PassThru
if($taskProcess.ExitCode -ne 0){throw 'Flower bake failed. See Outputs/Bake.log.'}
if($PlaybackOnly){Write-Output 'GPU playback cache regenerated; existing compensation provenance retained. See Outputs/PlaybackBake.txt.'}else{Write-Output 'Flower compensation and GPU playback caches generated; provenance saved. See Outputs/PlaybackBake.txt.'}
