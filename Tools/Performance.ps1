param([switch]$Reference,[string]$Executable='Builds/Validation/MindState.exe')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskExecutable=Join-Path $taskRoot $Executable
if(-not(Test-Path -LiteralPath $taskExecutable)){throw 'Build the validation player first: Tools/Build.ps1 -Validation'}
$taskName=if($Reference){'PerformanceBefore'}else{'PerformanceAfter'}
$taskArgs=@('-flowerBenchmark','-benchmarkOutput',('Outputs/'+$taskName),'-logFile',('Outputs/'+$taskName+'.log'))
if($Reference){$taskArgs+='-flowerCpuReference'}
$taskProcess=Start-Process -FilePath $taskExecutable -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -Wait -PassThru
if($taskProcess.ExitCode -ne 0){throw 'Performance capture failed. See Outputs.'}
Get-Content -LiteralPath (Join-Path $taskRoot ('Outputs/'+$taskName+'/Summary.txt'))
