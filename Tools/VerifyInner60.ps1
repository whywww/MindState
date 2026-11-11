param([switch]$Quick,[switch]$Rebuild,[switch]$Save,[switch]$Screens,[string]$Output='Evidence/Inner60/Regression')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','MindState.FlowerShingleValidation.Run','-shingleRegression','-inner60','1','-shingleOutput',('"'+$taskOutput+'"'),'-logFile',('"'+(Join-Path $taskOutput 'Editor.log')+'"'))
if($Quick){$taskArgs+=@('-innerQuick','1')}
if($Rebuild){$taskArgs+='-rebuildBottom'}
if($Save){$taskArgs+=@('-innerSave','1')}
if($Screens){$taskArgs+=@('-innerScreens','1')}
$taskProcess=Start-Process 'C:/Program Files/Unity 2022.3.62f3c1/Editor/Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -Wait -PassThru
if($taskProcess.ExitCode -ne 0){throw "Inner clearance regression failed: $taskOutput"}
Get-Content -LiteralPath (Join-Path $taskOutput 'Checks.txt')
