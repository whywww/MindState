param(
    [switch]$Quick,
    [switch]$Screens,
    [switch]$ContactsOnly,
    [string]$Output='Evidence/ShingleOverlap/ShapeSafeEdgeFinal',
    [string]$Baseline=''
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','MindState.FlowerShingleValidation.Run','-shingleRegression','-shingleOutput',('"'+$taskOutput+'"'),'-logFile',('"'+(Join-Path $taskOutput 'Editor.log')+'"'))
if($Quick) { $taskArgs+=@('-shingleQuick','1') }
if($Screens) { $taskArgs+=@('-shingleScreens','1') }
if($ContactsOnly) { $taskArgs+=@('-shingleScanOnly','1') }
if($Baseline) {
    $taskBaseline=[IO.Path]::GetFullPath((Join-Path $taskRoot $Baseline))
    foreach($taskFile in @('FullBloom.bin','Roots.bin','FixedSurface.bin','Transforms.bin')) {
        if(-not (Test-Path -LiteralPath (Join-Path $taskBaseline $taskFile))) { throw "Missing baseline $taskFile" }
    }
    if($taskBaseline -eq $taskOutput) { throw 'Baseline and output must differ.' }
    $taskArgs+=@('-shingleBaseline',('"'+$taskBaseline+'"'))
}
$taskProcess=Start-Process 'C:/Program Files/Unity 2022.3.62f3c1/Editor/Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -Wait -PassThru
if($taskProcess.ExitCode -ne 0) { throw "Shingle regression failed: $taskOutput" }
Get-Content -LiteralPath (Join-Path $taskOutput 'Checks.txt')
