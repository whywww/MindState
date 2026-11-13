param([switch]$RecreateFlower,[switch]$Validation,[string]$Unity='C:/Program Files/Unity 2022.3.62f3c1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskRoot 'Outputs'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
if(-not (Test-Path -LiteralPath $Unity)){throw "Unity not found: $Unity"}
function Invoke-TaskUnity([string]$method,[string]$logName){
    $taskArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod',$method,'-quit','-logFile',('"'+(Join-Path $taskOutput $logName)+'"'))
    $taskProcess=Start-Process -FilePath $Unity -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -Wait -PassThru
    if($taskProcess.ExitCode -ne 0){throw "Unity failed: Outputs/$logName"}
}
function Invoke-TaskUnityCode([string]$method,[string]$logName){
    $taskArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod',$method,'-quit','-logFile',('"'+(Join-Path $taskOutput $logName)+'"'))
    $taskProcess=Start-Process -FilePath $Unity -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -Wait -PassThru
    return $taskProcess.ExitCode
}
$taskCache=Invoke-TaskUnityCode 'MindState.Editor.PerformanceBuild.CheckCaches' 'CacheCheck.log'
if($taskCache -eq 4){throw 'Geometry cache is stale. Run Tools/Bake.ps1 before building.'}
if($taskCache -eq 3){
    Write-Output 'Playback cache is stale. Baking it first; this takes several minutes.'
    & (Join-Path $PSScriptRoot 'Bake.ps1') -PlaybackOnly
    $taskCache=Invoke-TaskUnityCode 'MindState.Editor.PerformanceBuild.CheckCaches' 'CacheCheck.log'
}
if($taskCache -ne 0){throw "Cache check failed with exit code $taskCache. See Outputs/CacheCheck.log"}
if($RecreateFlower){Invoke-TaskUnity 'MindState.Editor.CreateFlowerGallery.Create' 'Scene.log'}
if($Validation){Invoke-TaskUnity 'MindState.Editor.PerformanceBuild.Validation' 'ValidationBuild.log'}else{Invoke-TaskUnity 'MindState.Editor.PerformanceBuild.Delivery' 'Build.log'}
Write-Output (Join-Path $taskRoot $(if($Validation){'Builds/Validation/MindState.exe'}else{'Builds/Windows/MindState.exe'}))
