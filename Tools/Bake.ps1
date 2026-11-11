param([string]$Unity='C:\Program Files\Unity 2022.3.62f3c1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskRoot 'Outputs'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
if(-not (Test-Path -LiteralPath $Unity)){throw "Unity not found: $Unity"}
$taskArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','MindState.FlowerBakeRunner.Run','-rebuildBottom','-bakeFlower','-logFile',('"'+(Join-Path $taskOutput 'Bake.log')+'"'))
$taskProcess=Start-Process -FilePath $Unity -ArgumentList $taskArgs -WindowStyle Hidden -Wait -PassThru
if($taskProcess.ExitCode -ne 0){throw 'Flower bake failed. See Outputs/Bake.log.'}
Write-Output 'Flower cache generated and provenance saved.'
