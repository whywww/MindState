[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
# Only disposable, project-local artifacts. Keep Windows delivery and BrainLinkTools.
$taskRelativePaths=@('.validation','Evidence','EvidenceShingleFixContacts','Variants','Logs','Builds/Validation','Builds/BrainLinkValidation','Outputs/AcceptedBottomOverlap.bytes')
foreach($taskRelative in $taskRelativePaths){
    $taskTarget=[IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
    if(-not $taskTarget.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Outside project: $taskTarget"}
    if(-not (Test-Path -LiteralPath $taskTarget)){continue}
    $taskItem=Get-Item -LiteralPath $taskTarget -Force
    if($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Refusing linked target: $taskTarget"}
    if($PSCmdlet.ShouldProcess($taskTarget,'Delete generated/history artifact')){Remove-Item -LiteralPath $taskTarget -Recurse -Force}
}
Get-ChildItem -LiteralPath $taskRoot -File -Filter '*.log' | ForEach-Object {
    if($PSCmdlet.ShouldProcess($_.FullName,'Delete historical root log')){Remove-Item -LiteralPath $_.FullName -Force}
}
