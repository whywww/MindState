param([switch]$RecreateArt,[switch]$RecreateFlower,[switch]$Validation)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskEditor = 'C:\Program Files\Unity 2022.3.62f3c1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $taskEditor)) { throw "需要 Unity 2022.3.62f3c1：$taskEditor" }
function Invoke-TaskUnity([string]$method, [string]$logName) {
    $taskLog = Join-Path $taskRoot $logName
    $taskArgs = @('-batchmode', '-projectPath', ('"' + $taskRoot + '"'), '-executeMethod', $method, '-quit', '-logFile', ('"' + $taskLog + '"'))
    $taskProcess = Start-Process -FilePath $taskEditor -ArgumentList $taskArgs -WindowStyle Hidden -Wait -PassThru
    if ($taskProcess.ExitCode -ne 0) { throw "Unity 失败，参见 $taskLog" }
}
if ($RecreateArt) { Invoke-TaskUnity 'MindState.Editor.CreateGallery.Create' 'Setup.log' }
if ($RecreateArt -or $RecreateFlower) { Invoke-TaskUnity 'MindState.Editor.CreateFlowerGallery.Create' 'FlowerSetup.log' }
if ($Validation) { Invoke-TaskUnity 'MindState.Editor.PerformanceBuild.Validation' 'ValidationBuild.log' } else { Invoke-TaskUnity 'MindState.Editor.PerformanceBuild.Delivery' 'Build.log' }
Write-Output (Join-Path $taskRoot $(if($Validation){'Builds\Validation\MindState.exe'}else{'Builds\Windows\MindState.exe'}))

