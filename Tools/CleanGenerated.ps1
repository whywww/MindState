[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskOutput=Join-Path $taskRoot 'Outputs'
function Assert-TaskPath([string]$path){
    $resolved=[IO.Path]::GetFullPath($path)
    if(-not $resolved.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Outside project: $resolved"}
    $ancestor=$resolved
    while($ancestor -ne $taskRoot){
        if((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Refusing linked path: $ancestor"}
        $ancestor=Split-Path -Parent $ancestor
    }
    return $resolved
}
# Verify final delivery before computing any deletion targets.
$taskManifestPath=Assert-TaskPath (Join-Path $taskOutput 'Pair24/FinalEvidenceManifest.json')
$taskManifest=Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json
$taskKeep=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($entry in $taskManifest.files.PSObject.Properties){
    $path=Assert-TaskPath (Join-Path $taskOutput ('Pair24/'+$entry.Name))
    if(-not $path.StartsWith((Join-Path $taskOutput 'Pair24')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest entry escapes final delivery.'}
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value.sha256){throw "Final artifact hash mismatch: $path"}
    $null=$taskKeep.Add($path)
}
$taskExtra=@(
 'Pair24/FinalEvidenceManifest.json','Pair24/FixedSizeCandidateSource.txt','Pair24/ExistingValidationFailure.txt','Pair24/ShrinkFixStatus.txt',
 'Pair24/FinalFlexCpuChecks.txt','Pair24/FinalFlexGpuChecks.txt','Pair24/FinalFlexShapeRegularity.txt',
 'Pair24/CleanupProtectedFiles.json','Pair24/CleanupSummary.json',
 'Bake.log','Build.log','CacheCheck.log','Verify.log','PlaybackVerify.log','MotionVerify.log','PlaybackBake.txt',
 'Validation/Checks.txt','Validation/Result.txt',
 'Pair24/FixedSizeFinalCombinedTransitions.log','Pair24/FixedSizeFinalFlexCpu.log','Pair24/FixedSizeFinalFlexGpu.log',
 'Pair24/FixedSizeFinalGpuProof.log','Pair24/FixedSizeFinalClarityCapture.log','Pair24/FixedSizeFinalGatheringCapture.log','Pair24/FixedSizeFinalQuietCapture.log'
)
foreach($name in @('Candidate.bin','CpuResult.txt','GpuResult.txt','CpuValidatedSource.txt','GpuValidatedSource.txt','CpuValidatedCandidate.sha256','GpuValidatedCandidate.sha256','CpuStandardHashes.txt','GpuStandardHashes.txt','GpuBase.sha256','CaptureView.json')){$taskExtra+='WanderFlex/'+$name}
foreach($name in @('FinalSuite.txt','FinalSuiteSource.txt','GentleOtherStates.txt','GentleSteady.txt','GentleTransitions.txt','GentleValidatedSource.txt','GeometryEquivalence.json','GpuCurl.txt','GpuValidatedSource.txt','ScaleBounds.txt','Pair10/Result.txt','Pair12/Result.txt','Pair12/Selected.txt')){$taskExtra+='Pair24/GeometryCertificate/'+$name}
foreach($name in $taskExtra){$null=$taskKeep.Add((Assert-TaskPath (Join-Path $taskOutput $name)))}
$taskTrees=@($taskOutput,(Join-Path $taskRoot '.validation'),(Join-Path $taskRoot 'Docs/Checkpoints'),(Join-Path $taskRoot 'Logs'))
foreach($tree in $taskTrees){
    $null=Assert-TaskPath $tree
    if((Test-Path -LiteralPath $tree) -and @(Get-ChildItem -LiteralPath $tree -Recurse -Force -Attributes ReparsePoint).Count -gt 0){throw "Refusing tree containing links: $tree"}
}
if(Test-Path -LiteralPath (Join-Path $taskRoot 'Temp/UnityLockfile')){throw 'Close Unity before cleanup.'}
$taskDirty=@(& git -C $taskRoot status --porcelain -- Docs/Checkpoints)
if($LASTEXITCODE -ne 0){throw 'Git status could not be checked.'}
$taskDirty=@($taskDirty | Where-Object {$_.Substring(0,2).Trim() -ne 'D'})
if($taskDirty.Count -gt 0){throw 'Checkpoints contain local changes.'}
$taskFiles=@()
foreach($tree in $taskTrees){if(Test-Path -LiteralPath $tree){$taskFiles+=@(Get-ChildItem -LiteralPath $tree -File -Recurse -Force | Where-Object {-not $taskKeep.Contains($_.FullName)})}}
$taskTracked=@(& git -C $taskRoot ls-files)
if($LASTEXITCODE -ne 0){throw 'Cannot inspect tracked files.'}
$taskTrackedSet=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($name in $taskTracked){$null=$taskTrackedSet.Add($name)}
foreach($file in $taskFiles){
    # The complete candidate trees were already checked for links above.
    $target=[IO.Path]::GetFullPath($file.FullName)
    if(-not $target.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Outside project: $target"}
    $relative=$target.Substring($taskRoot.Length+1).Replace('\','/')
    if($taskTrackedSet.Contains($relative) -and -not ($relative.StartsWith('Docs/Checkpoints/') -or $relative.StartsWith('Outputs/'))){throw "Refusing tracked non-history file: $relative"}
}
$taskBytes=[long]0;$taskCount=0
# Use literal file paths, then remove empty directories. No recursive parent deletion.
foreach($file in $taskFiles){
    if($PSCmdlet.ShouldProcess($file.FullName,'Delete historical artifact')){
        $length=$file.Length
        Remove-Item -LiteralPath $file.FullName -Force
        $taskBytes+=$length;$taskCount++
        if($taskCount%5000 -eq 0){Write-Output "Deleted $taskCount historical files..."}
    }
}
foreach($tree in $taskTrees){
    if(-not (Test-Path -LiteralPath $tree)){continue}
    $dirs=@(Get-ChildItem -LiteralPath $tree -Directory -Recurse -Force | Sort-Object {$_.FullName.Length} -Descending)
    if($tree -ne $taskOutput){$dirs+=Get-Item -LiteralPath $tree -Force}
    foreach($dir in $dirs){
        $target=[IO.Path]::GetFullPath($dir.FullName)
        if(-not $target.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Outside project: $target"}
        if(@(Get-ChildItem -LiteralPath $target -Force).Count -eq 0 -and $PSCmdlet.ShouldProcess($target,'Delete empty history directory')){Remove-Item -LiteralPath $target -Force}
    }
}
if(-not $WhatIfPreference){
    [ordered]@{deleted_files=$taskCount;deleted_bytes=$taskBytes;deleted_gib=[Math]::Round($taskBytes/1GB,3);source_digest=$taskManifest.source_digest;completed_utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'Pair24/CleanupSummary.json') -Encoding utf8
}
Write-Output ("Deleted $taskCount files; {0:N3} GiB logical file bytes removed." -f ($taskBytes/1GB))
