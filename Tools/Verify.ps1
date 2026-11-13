param([switch]$Contacts,[string]$Unity='C:/Program Files/Unity 2022.3.62f3c1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskRoot 'Outputs'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
if(-not (Test-Path -LiteralPath $Unity)){throw "Unity not found: $Unity"}
$taskArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','MindState.FlowerProjectValidation.Run','-verifyProject','-logFile',('"'+(Join-Path $taskOutput 'Verify.log')+'"'))
if($Contacts){$taskArgs+='-scanContacts'}
$taskProcess=Start-Process -FilePath $Unity -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -Wait -PassThru
if($taskProcess.ExitCode -ne 0){throw 'Project validation failed. See Outputs/Verify.log.'}
$taskChecks=Get-Content -LiteralPath (Join-Path $taskOutput 'Validation/Checks.txt')
Write-Output ($taskChecks.Count.ToString()+' checks passed. See Outputs/Validation.')
$taskGpuArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','MindState.FlowerPlaybackValidation.Run','-verifyPlayback','-logFile',('"'+(Join-Path $taskOutput 'PlaybackVerify.log')+'"'))
$taskGpuProcess=Start-Process -FilePath $Unity -ArgumentList $taskGpuArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -Wait -PassThru
if($taskGpuProcess.ExitCode -ne 0){throw 'GPU playback validation failed. See Outputs/PlaybackVerify.log.'}
Get-Content -LiteralPath (Join-Path $taskOutput 'PlaybackValidation/Result.txt')
$taskMotionArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','MindState.FlowerMotionValidation.Run','-verifyMotion','-logFile',('"'+(Join-Path $taskOutput 'MotionVerify.log')+'"'))
$taskMotionProcess=Start-Process -FilePath $Unity -ArgumentList $taskMotionArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -Wait -PassThru
if($taskMotionProcess.ExitCode -ne 0){throw 'Motion continuity validation failed. See Outputs/MotionVerify.log.'}
Get-Content -LiteralPath (Join-Path $taskOutput 'MotionValidation/Result.txt')
