param([switch]$RecreateArt)
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
Invoke-TaskUnity 'MindState.Editor.CreateGallery.Build' 'Build.log'
Write-Output (Join-Path $taskRoot 'Builds\Windows\MindState.exe')
