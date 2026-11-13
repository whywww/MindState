[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
foreach($taskParent in @('Outputs','Builds')){
    $taskParentPath=Join-Path $taskRoot $taskParent
    if((Test-Path -LiteralPath $taskParentPath) -and ((Get-Item -LiteralPath $taskParentPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Refusing linked artifact parent: $taskParentPath"}
}
# Only disposable, project-local artifacts. Keep source, current caches, delivery,
# BrainLinkTools, user settings, and the final ten-second video.
$taskRelativePaths=@('.validation','Evidence','EvidenceShingleFixContacts','Variants','Logs','Obj','.vs','Builds/Validation','Builds/BrainLinkValidation')
$taskOutput=Join-Path $taskRoot 'Outputs'
if(Test-Path -LiteralPath $taskOutput){
    $taskRelativePaths+=@(Get-ChildItem -LiteralPath $taskOutput -Force | Where-Object {$_.Name -ne 'ClosingContinuity_10s.mp4'} | ForEach-Object {'Outputs/'+$_.Name})
}
if(-not (Test-Path -LiteralPath (Join-Path $taskRoot 'Temp/UnityLockfile'))){$taskRelativePaths+='Temp'}
foreach($taskRelative in $taskRelativePaths){
    $taskTarget=[IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
    if(-not $taskTarget.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Outside project: $taskTarget"}
    if(-not (Test-Path -LiteralPath $taskTarget)){continue}
    $taskItem=Get-Item -LiteralPath $taskTarget -Force
    if($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Refusing linked target: $taskTarget"}
    if($taskItem.PSIsContainer -and @(Get-ChildItem -LiteralPath $taskTarget -Recurse -Force -Attributes ReparsePoint).Count -gt 0){throw "Refusing target containing links: $taskTarget"}
    $taskTracked=@(& git -C $taskRoot ls-files -- $taskRelative)
    if($LASTEXITCODE -ne 0){throw 'Cannot verify tracked files before cleanup.'}
    if($taskTracked.Count -gt 0){throw "Refusing to delete tracked content: $taskTarget"}
    if($PSCmdlet.ShouldProcess($taskTarget,'Delete generated/history artifact')){Remove-Item -LiteralPath $taskTarget -Recurse -Force}
}
Get-ChildItem -LiteralPath $taskRoot -File -Filter '*.log' | ForEach-Object {
    $taskTracked=@(& git -C $taskRoot ls-files -- $_.Name)
    if($LASTEXITCODE -ne 0 -or $taskTracked.Count -gt 0){throw "Cannot safely remove root log: $($_.FullName)"}
    if($PSCmdlet.ShouldProcess($_.FullName,'Delete historical root log')){Remove-Item -LiteralPath $_.FullName -Force}
}
