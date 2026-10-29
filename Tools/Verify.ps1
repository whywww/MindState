param([int]$Seconds = 900)
$ErrorActionPreference = 'Stop'
if ($Seconds -lt 1) { throw 'Seconds 必须大于零。' }
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskPlayer = Join-Path $taskRoot 'Builds\Windows\MindState.exe'
$taskEvidence = Join-Path $taskRoot 'Evidence\FanPetal'
$taskLog = Join-Path $taskRoot 'FanVerificationPlayer.log'
if (-not (Test-Path -LiteralPath $taskPlayer)) { throw '请先运行 Tools\Build.ps1。' }
$taskArgs = @('-verifyPetal', '-soakSeconds', $Seconds, '-evidencePath', ('"' + $taskEvidence + '"'), '-screen-width', '1920', '-screen-height', '1080', '-windowed', '-force-d3d11', '-logFile', ('"' + $taskLog + '"'))
$taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle Hidden -Wait -PassThru
if ($taskProcess.ExitCode -ne 0) { throw "验证失败，参见 $taskLog 和 $taskEvidence" }
Get-Content -LiteralPath (Join-Path $taskEvidence 'Validation.json')
