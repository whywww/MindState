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
if($RecreateFlower){Invoke-TaskUnity 'MindState.Editor.CreateFlowerGallery.Create' 'Scene.log'}
if($Validation){Invoke-TaskUnity 'MindState.Editor.PerformanceBuild.Validation' 'ValidationBuild.log'}else{Invoke-TaskUnity 'MindState.Editor.PerformanceBuild.Delivery' 'Build.log'}
Write-Output (Join-Path $taskRoot $(if($Validation){'Builds/Validation/MindState.exe'}else{'Builds/Windows/MindState.exe'}))
