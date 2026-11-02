param(
    [switch]$Baseline,[switch]$Tune,[switch]$Refine,[switch]$Geometry,[switch]$Stagger,[switch]$Fold,[switch]$Natural,[switch]$Stress,[switch]$Coarse,[switch]$NoScreens,
    [string]$Seeds='731,17,42,137,2026',[int]$Steps=201,[int]$Budget=120,
    [string]$LayoutJson='', [string]$Output='Evidence/FlowerPerformance/Contacts',[string]$ProjectPath='',
    [string]$NaturalBaselineJson=''
)
$ErrorActionPreference='Stop'
if ($Steps -lt 2 -or $Budget -lt 1) { throw 'Steps >= 2 and Budget >= 1 are required.' }
if ($Natural -and (-not $NaturalBaselineJson -or -not (Test-Path -LiteralPath $NaturalBaselineJson))) { throw 'Natural search requires -NaturalBaselineJson with an existing JSON file.' }
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskProject=if ($ProjectPath) { [IO.Path]::GetFullPath($ProjectPath) } else { $taskRoot }
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskArgs=@('-batchmode','-projectPath',('"'+$taskProject+'"'),'-executeMethod','MindState.Editor.FlowerAvoidanceEditor.Run','-flowerAvoidance','-avoidanceSeeds',$Seeds,'-avoidanceSteps',$Steps,'-avoidanceBudget',$Budget,'-evidencePath',('"'+$taskOutput+'"'),'-logFile',('"'+(Join-Path $taskOutput 'Editor.log')+'"'))
if ($Baseline) { $taskArgs+='-avoidanceBaseline' }
if ($Tune) { $taskArgs+='-avoidanceTune' }
if ($Geometry) { $taskArgs+='-avoidanceGeometry' }
if ($Refine) { $taskArgs+='-avoidanceRefine' }
if ($Stagger) { $taskArgs+='-avoidanceStagger' }
if ($Fold) { $taskArgs+='-avoidanceFold' }
if ($Natural) { $taskArgs+='-avoidanceNatural' }
if ($NaturalBaselineJson) { $taskArgs+=@('-naturalBaselineJson',('"'+[IO.Path]::GetFullPath($NaturalBaselineJson)+'"')) }
if ($Stress) { $taskArgs+='-avoidanceStress' }
if ($Coarse) { $taskArgs+='-avoidanceCoarse' }
if ($NoScreens) { $taskArgs+='-avoidanceNoScreens' }
if ($LayoutJson) { $taskArgs+=@('-layoutJson',('"'+[IO.Path]::GetFullPath($LayoutJson)+'"')) }
$taskProcess=Start-Process -FilePath 'C:/Program Files/Unity 2022.3.62f3c1/Editor/Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -Wait -PassThru
if ($taskProcess.ExitCode -ne 0) { throw "Validation not passed (exit $($taskProcess.ExitCode)); inspect $taskOutput. Coarse scans and root hits never certify a layout." }
Get-ChildItem -LiteralPath $taskOutput -Filter '*.json' | Select-Object FullName
