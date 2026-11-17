param([string]$Unity='C:\Program Files\Unity 2022.3.62f3c1\Editor\Unity.exe',[switch]$PlaybackOnly,[switch]$RetainFlexCandidate)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskRoot 'Outputs'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
if(-not (Test-Path -LiteralPath $Unity)){throw "Unity not found: $Unity"}
$taskArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','MindState.FlowerBakeRunner.Run','-bakeFlower','-logFile',('"'+(Join-Path $taskOutput 'Bake.log')+'"'))
if($RetainFlexCandidate){$taskArgs+='-retainFlexCandidate'}
if($PlaybackOnly){$taskArgs+='-bakePlayback'}else{$taskArgs+='-rebuildBottom'}
$taskRollback=Join-Path $taskOutput 'BakeRollback'
New-Item -ItemType Directory -Force -Path $taskRollback | Out-Null
$taskCacheFiles=@('Assets/Flower/Resources/BottomOverlap.bytes','Assets/Flower/Resources/BottomOverlapProvenance.txt','Assets/Flower/Resources/FlowerPlaybackProvenance.txt','Assets/StreamingAssets/FlowerPlayback.bin','Assets/StreamingAssets/WanderingFlex.bin','Assets/StreamingAssets/WanderingFlex.bin.meta')
$taskOriginals=@{}
foreach($taskRelative in $taskCacheFiles){
    $taskTarget=[IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
    if(-not $taskTarget.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Cache rollback path escapes project.'}
    $taskOriginals[$taskRelative]=Test-Path -LiteralPath $taskTarget
    if($taskOriginals[$taskRelative]){Copy-Item -LiteralPath $taskTarget -Destination (Join-Path $taskRollback ([IO.Path]::GetFileName($taskRelative))) -Force}
}
$taskProcess=Start-Process -FilePath $Unity -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -Wait -PassThru
if($taskProcess.ExitCode -ne 0){
    foreach($taskRelative in $taskCacheFiles){
        $taskTarget=[IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
        if($taskOriginals[$taskRelative]){Copy-Item -LiteralPath (Join-Path $taskRollback ([IO.Path]::GetFileName($taskRelative))) -Destination $taskTarget -Force}
        elseif(Test-Path -LiteralPath $taskTarget){Remove-Item -LiteralPath $taskTarget}
    }
    throw 'Flower bake failed; previous caches restored. See Outputs/Bake.log.'
}
if($PlaybackOnly){Write-Output 'GPU playback cache regenerated; existing compensation provenance retained. See Outputs/PlaybackBake.txt.'}else{Write-Output 'Flower compensation and GPU playback caches generated; provenance saved. See Outputs/PlaybackBake.txt.'}
