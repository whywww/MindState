param(
  [string]$States='Clarity,Gathering,Wandering,Quiet',
  [int]$SegmentFrames=180,
  [int]$OverlapFrames=40,
  [int]$Preroll=13,
  [int]$Fps=60,
  [int]$Width=1920,
  [int]$Height=1080,
  [string]$Unity='C:/Program Files/Unity 2022.3.62f3c1/Editor/Unity.exe',
  [string]$FFmpeg='',
  [string]$FFprobe='',
  [switch]$ArtShowcase,
  [switch]$Continuous,
  [switch]$PreviewOnly,
  [switch]$EncodeOnly
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$out=Join-Path $taskRoot 'Outputs'
$frames=Join-Path $out 'ClipFrames'
$video=Join-Path $out 'MindState_10s.mp4'
if($PreviewOnly -and -not $ArtShowcase){throw 'PreviewOnly requires ArtShowcase'}
if($ArtShowcase){
  $Fps=60;$States='Clarity,Gathering,Quiet,Wandering';$OverlapFrames=12
  $frames=Join-Path $out $(if($PreviewOnly){'ArtPreview'}else{'ArtShowcaseFrames'})
  $video=Join-Path $out 'MindState_ArtShowcase_10s.mp4'
}
if($Continuous){
  if($ArtShowcase -or $PreviewOnly){throw 'Continuous is a separate recording mode'}
  $Fps=60;$States='Gathering';$OverlapFrames=0
  $frames=Join-Path $out 'ArtContinuousFrames';$video=Join-Path $out 'MindState_OpposedBloom_10s.mp4'
}
$stateNames=$States.Split(',')|ForEach-Object{$_.Trim()}|Where-Object{$_}
$stateCount=$stateNames.Count
$lengths=if($Continuous){@(600)}elseif($ArtShowcase){@(162,132,132,210)}else{@($stateNames|ForEach-Object{$SegmentFrames})}
$totalFrames=($lengths|Measure-Object -Sum).Sum-($stateCount-1)*$OverlapFrames
if($totalFrames -ne 10*$Fps){throw "Montage math gives $totalFrames frames, expected $(10*$Fps). Adjust SegmentFrames/OverlapFrames."}
function Resolve-Tool([string]$given,[string]$name){
  if($given){if(Test-Path -LiteralPath $given){return $given};throw "$name not found: $given"}
  $cmd=Get-Command $name -ErrorAction SilentlyContinue
  if($cmd){return $cmd.Source}
  $pkg=Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
  if(Test-Path -LiteralPath $pkg){
    $hit=Get-ChildItem -LiteralPath $pkg -Filter "$name.exe" -Recurse -ErrorAction SilentlyContinue|Select-Object -First 1
    if($hit){return $hit.FullName}
  }
  throw "$name not found. Install with: winget install --id Gyan.FFmpeg -e"
}
$ffmpeg=Resolve-Tool $FFmpeg 'ffmpeg'
$ffprobe=Resolve-Tool $FFprobe 'ffprobe'
New-Item -ItemType Directory -Force -Path $out|Out-Null
if(-not $EncodeOnly){
  if(-not (Test-Path -LiteralPath $Unity)){throw "Unity not found: $Unity"}
  if(Test-Path -LiteralPath (Join-Path $taskRoot 'Temp/UnityLockfile')){throw 'Close the Unity editor that has this project open, then rerun.'}
  if(Test-Path -LiteralPath $frames){
    $resolved=(Resolve-Path -LiteralPath $frames).Path
    if((Split-Path -Parent $resolved) -ne $out){throw 'Frame cleanup must stay directly inside Outputs'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
  }
  $unityArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','MindState.FlowerClipCapture.Run','-recordClip',
    '-clipStates',$States,'-clipSegFrames',"$SegmentFrames",'-clipPreroll',"$Preroll",'-clipFps',"$Fps",
    '-clipWidth',"$Width",'-clipHeight',"$Height",'-force-d3d11',
    '-logFile',('"'+(Join-Path $out 'Record.log')+'"'))
  if($Continuous){$unityArgs+=@('-clipContinuous','1')}
  if($ArtShowcase){$unityArgs+=@('-artShowcase','1')}
  if($PreviewOnly){$unityArgs+=@('-clipPreview','1')}
  $taskProcess=Start-Process -FilePath $Unity -ArgumentList $unityArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -Wait -PassThru
  if($taskProcess.ExitCode -ne 0){throw "Clip capture failed with exit code $($taskProcess.ExitCode). See Outputs/Record.log"}
  $result=Get-Content -LiteralPath (Join-Path $frames 'Result.txt') -Raw
  if($result.Trim() -ne 'PASS'){throw "Capture reported: $result"}
}
if($PreviewOnly){Write-Output "Previews ready: $frames";return}
for($i=0;$i -lt $stateCount;$i++){
  $dir=Join-Path $frames "state$i"
  $files=@(Get-ChildItem -LiteralPath $dir -Filter 'frame_*.png' -ErrorAction SilentlyContinue)
  if($files.Count -ne $lengths[$i]){throw "state$i has $($files.Count) frames, expected $($lengths[$i])"}
  if(@($files|Where-Object{$_.Length -eq 0}).Count -gt 0){throw "state$i has empty frames"}
}
$chain=@();$prev='[0:v]'
for($i=1;$i -lt $stateCount;$i++){
  $label=if($i -eq $stateCount-1){'[x]'}else{"[x$i]"}
  $offset=[double](($lengths[0..($i-1)]|Measure-Object -Sum).Sum-$i*$OverlapFrames)/$Fps
  $offsetText=$offset.ToString('0.######',[System.Globalization.CultureInfo]::InvariantCulture)
  $durationText=([double]$OverlapFrames/$Fps).ToString('0.######',[System.Globalization.CultureInfo]::InvariantCulture)
  $chain+="${prev}[${i}:v]xfade=transition=fade:duration=${durationText}:offset=${offsetText}${label}"
  $prev=$label
}
$fade=if($ArtShowcase -or $Continuous){''}else{'fade=t=in:st=0:d=0.4,fade=t=out:st=9.5:d=0.5,'}
$chain+="${prev}${fade}scale=out_color_matrix=bt709:out_range=tv,format=yuv420p[v]"
$ffArgs=@('-y')
for($i=0;$i -lt $stateCount;$i++){$ffArgs+=@('-framerate',"$Fps",'-start_number','0','-i',(Join-Path $frames "state$i\frame_%04d.png"))}
if($ArtShowcase -or $Continuous){$chain[-1]=$chain[-1].Replace('format=yuv420p[v]','format=yuv420p,setparams=color_primaries=bt709:color_trc=iec61966-2-1:colorspace=bt709:range=limited[v]')}
$ffArgs+=@('-filter_complex',($chain -join ';'),'-map','[v]','-c:v','libx264','-preset','slow','-crf','16','-pix_fmt','yuv420p',
  '-colorspace','bt709','-color_primaries','bt709','-color_trc',$(if($ArtShowcase -or $Continuous){'iec61966-2-1'}else{'bt709'}),'-movflags','+faststart','-an','-t','10',$video)
& $ffmpeg @ffArgs
if($LASTEXITCODE -ne 0){throw "ffmpeg failed with exit code $LASTEXITCODE"}
$probe=(& $ffprobe -v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate,nb_frames,duration -of json $video)|ConvertFrom-Json
$stream=$probe.streams[0]
if([int]$stream.width -ne $Width){throw "Unexpected width $($stream.width)"}
if([int]$stream.height -ne $Height){throw "Unexpected height $($stream.height)"}
if($stream.r_frame_rate -ne "$Fps/1"){throw "Unexpected frame rate $($stream.r_frame_rate)"}
if([int]$stream.nb_frames -ne 10*$Fps){throw "Unexpected frame count $($stream.nb_frames)"}
if([math]::Abs([double]$stream.duration-10) -gt 0.02){throw "Unexpected duration $($stream.duration)"}
$times=@(0.5,2.5,4.0,6.0,8.5,9.8)
foreach($t in $times){
  $stamp=$t.ToString('0.0',[System.Globalization.CultureInfo]::InvariantCulture)
  & $ffmpeg -y -v error -ss $stamp -i $video -frames:v 1 (Join-Path $out "ClipCheck_$stamp.png")
  if($LASTEXITCODE -ne 0){throw "ffmpeg frame extraction failed at $stamp s"}
}
Write-Output ("Video ready: "+$video+" ("+$Width+"x"+$Height+", "+$Fps+"fps, "+(10*$Fps)+" frames). Checks in Outputs/ClipCheck_*.png")
