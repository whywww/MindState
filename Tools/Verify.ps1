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
