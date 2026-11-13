#if UNITY_EDITOR || FLOWER_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MindState {
public sealed partial class FlowerAssembler {
 struct MotionSnapshot {
  public float radius,bloom,head;public Vector4 weights;public Vector3 echoPosition,headPosition;public Quaternion echoRotation;
 }
 MotionSnapshot MotionSample(){
  AverageEchoHistory(out var weights,out _,out float bloom);
  return new MotionSnapshot{radius=CurrentBloomRadius(),bloom=bloom,head=ribbonHeadTurn,weights=weights,echoPosition=echoRoot.position,echoRotation=echoRoot.rotation,headPosition=ribbonThread.root.TransformPoint(ribbonThread.positions[RibbonSegments])};
 }
 void SeedMotionHistory(){
  echoHead=echoCount=0;
  for(int i=0;i<=90;i++){
   float time=-45+i*.5f,q=.5f+.5f*Mathf.Sin(time*.13f);
   var weights=new Vector4(1-q,q,0,0);
   echoSamples[echoHead]=new EchoSample{clock=time,weights=weights,light=weights,bloom=Mathf.Lerp(.9f,.1f,q)};
   echoHead=(echoHead+1)%EchoCapacity;echoCount++;
  }
  lastEchoSample=0;
 }
 internal void VerifyMotionContinuity(List<string> report){
  if(!gpuPlayback)throw new Exception("Motion verification requires GPU playback");
  ResetArt();SetArtPreviewImmediate(FlowerArtState.Clarity);
  // Analytic linear history tests the integral independently of its implementation.
  echoHead=echoCount=0;
  for(int i=0;i<EchoCapacity;i++){float t=i*.5f,q=t/64;echoSamples[i]=new EchoSample{clock=t,bloom=q,weights=new Vector4(1-q,q,0,0),light=new Vector4(1-q,q,0,0)};echoCount++;}
  float historyError=0;
  foreach(float time in new[]{0f,9.999f,10f,10.001f,44.999f,45f,45.001f,59.999f,60f,60.001f}){
   artTime=time;AverageEchoHistory(out var weights,out var light,out float bloom);
   float expected=time<=10?0:(Mathf.Max(0,time-45)+time-10)/128;
   historyError=Mathf.Max(historyError,Mathf.Abs(expected-bloom));
   if(Mathf.Abs(weights.y-expected)>3e-6f||Mathf.Abs(light.y-expected)>3e-6f||Mathf.Abs(expected-bloom)>3e-6f)throw new Exception("Continuous echo integral mismatch at "+time);
  }
  // Same signal after the ring buffer wraps, with a non-zero history origin.
  echoHead=17;echoCount=EchoCapacity;
  for(int i=0;i<EchoCapacity;i++){float t=64+i*.5f,q=i/128f;echoSamples[(17+i)%EchoCapacity]=new EchoSample{clock=t,bloom=q,weights=new Vector4(1-q,q,0,0),light=new Vector4(1-q,q,0,0)};}
  artTime=124;AverageEchoHistory(out _,out _,out float wrappedBloom);
  if(Mathf.Abs(wrappedBloom-.5078125f)>3e-6f)throw new Exception("Wrapped echo integral mismatch");
  report.Add("PASS continuous 10-45 s echo integral, startup hold, window edges and ring wrap; max error="+historyError);

  var knots=new SortedSet<float>();foreach(var row in playback)foreach(var clip in row)foreach(float t in clip.times)knots.Add(t);
  float maxKnotStep=0;
  foreach(float t in knots){
   if(t<=0||t>=1)continue;
   SetBloomProgress(t-.000001f);float before=CurrentBloomRadius();SetBloomProgress(t+.000001f);float after=CurrentBloomRadius();
   maxKnotStep=Mathf.Max(maxKnotStep,Mathf.Abs(after-before));
  }
  if(maxKnotStep>.001f)throw new Exception("Ribbon envelope jumps at a cache knot: "+maxKnotStep);
  report.Add("PASS all "+knots.Count+" cache knots; radius change across progress +/- 1e-6="+maxKnotStep);

  MotionSnapshot[] baseline=null;
  foreach(int rate in new[]{30,60,120}){
   ResetArt();SetArtPreviewImmediate(FlowerArtState.Clarity);SeedMotionHistory();MotionStep(0);SetArtState(FlowerArtState.Gathering);
   var samples=new MotionSnapshot[17];samples[0]=MotionSample();var previous=samples[0];float maxWeightSpeed=0,maxBloomSpeed=0,maxRadiusSpeed=0,maxEchoSpeed=0,maxHeadSpeed=0;
   int initialUploads=MeshUpdates;long startBytes=GC.GetAllocatedBytesForCurrentThread();
   for(int frame=1;frame<=rate*16;frame++){
    if(frame==rate*10+1)SetArtState(FlowerArtState.Quiet);
    MotionStep(1f/rate);var current=MotionSample();
    maxWeightSpeed=Mathf.Max(maxWeightSpeed,(current.weights-previous.weights).magnitude*rate);
    maxBloomSpeed=Mathf.Max(maxBloomSpeed,Mathf.Abs(current.bloom-previous.bloom)*rate);
    maxRadiusSpeed=Mathf.Max(maxRadiusSpeed,Mathf.Abs(current.radius-previous.radius)*rate);
    maxEchoSpeed=Mathf.Max(maxEchoSpeed,Vector3.Distance(current.echoPosition,previous.echoPosition)*rate);
    maxHeadSpeed=Mathf.Max(maxHeadSpeed,Vector3.Distance(current.headPosition,previous.headPosition)*rate);
    if(current.head<previous.head-1e-6f||RibbonClearance()<RibbonMinRadius-.0001f||RootAnchorError()>1e-5f)throw new Exception("Motion head/clearance/root invariant failed");
    if(frame%rate==0)samples[frame/rate]=current;previous=current;
   }
   long allocated=GC.GetAllocatedBytesForCurrentThread()-startBytes;
   if(maxWeightSpeed>.05f||maxBloomSpeed>.03f||maxRadiusSpeed>4||maxEchoSpeed>2||maxHeadSpeed>8)throw new Exception("Motion step spike at "+rate+" Hz: weights="+maxWeightSpeed+" bloom="+maxBloomSpeed+" radius="+maxRadiusSpeed+" echo="+maxEchoSpeed+" head="+maxHeadSpeed);
   if(MeshUpdates!=initialUploads||allocated!=0)throw new Exception("Motion playback allocated or uploaded flower vertices: "+allocated);
   if(baseline==null)baseline=samples;
   else for(int i=0;i<samples.Length;i++){
    if(Vector3.Distance(samples[i].echoPosition,baseline[i].echoPosition)>.02f||Quaternion.Angle(samples[i].echoRotation,baseline[i].echoRotation)>.5f||Mathf.Abs(samples[i].radius-baseline[i].radius)>.02f||Vector3.Distance(samples[i].headPosition,baseline[i].headPosition)>.03f||(samples[i].weights-baseline[i].weights).magnitude>.002f)throw new Exception("Motion depends on update rate at "+rate+" Hz, second "+i);
   }
   var held=MotionSample();float clock=artTime;SetFrozen(true);for(int i=0;i<rate;i++)MotionStep(1f/rate);var still=MotionSample();
   if(artTime!=clock||held.head!=still.head||held.bloom!=still.bloom||held.echoPosition!=still.echoPosition||held.headPosition!=still.headPosition)throw new Exception("Frozen extras moved");
   SetFrozen(false);MotionStep(1f/rate);var resumed=MotionSample();if((resumed.head-held.head)*rate>.09f)throw new Exception("Resume caught up frozen time");
   report.Add("PASS "+rate+" Hz close/breath/window/freeze/resume; max speeds weights="+maxWeightSpeed+" bloom="+maxBloomSpeed+" radius="+maxRadiusSpeed+" echo="+maxEchoSpeed+" head="+maxHeadSpeed+"; GC="+allocated+" uploads=0");
  }
 }
 internal void PrepareClosingCapture(){
  ResetArt();SetArtPreviewImmediate(FlowerArtState.Clarity);
  for(int i=0;i<60*60;i++){
   if(i==30*60)SetArtState(FlowerArtState.Wandering);
   if(i==40*60)SetArtState(FlowerArtState.Quiet);
   if(i==50*60)SetArtState(FlowerArtState.Clarity);
   MotionStep(1f/60);
  }
  SetArtState(FlowerArtState.Gathering);
 }
 internal string ClosingCaptureRow(int frame){var s=MotionSample();return frame+","+s.radius.ToString("R",CultureInfo.InvariantCulture)+","+s.bloom.ToString("R",CultureInfo.InvariantCulture)+","+s.head.ToString("R",CultureInfo.InvariantCulture);}
}
public sealed class FlowerMotionValidation:MonoBehaviour {
#if UNITY_EDITOR
 public static void Run(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");UnityEditor.EditorApplication.EnterPlaymode();}
#endif
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyMotion")>=0)new GameObject("Motion continuity validation").AddComponent<FlowerMotionValidation>();}
 IEnumerator Start(){
  yield return null;int exit=0;var report=new List<string>();var flower=FindObjectOfType<FlowerAssembler>();
  try{if(flower==null||!flower.Ready)throw new Exception("Flower not ready");flower.enabled=false;flower.VerifyMotionContinuity(report);}
  catch(Exception e){Debug.LogException(e);report.Add("FAIL "+e);exit=2;}
  Directory.CreateDirectory("Outputs/MotionValidation");File.WriteAllLines("Outputs/MotionValidation/Result.txt",report);
  if(exit==0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-captureClosing")>=0){
   var capture=CaptureClosing(flower);bool more=true;
   while(more){object current=null;try{more=capture.MoveNext();if(more)current=capture.Current;}catch(Exception e){Debug.LogException(e);report.Add("FAIL capture "+e);exit=2;more=false;}if(more)yield return current;}
   File.WriteAllLines("Outputs/MotionValidation/Result.txt",report);
  }
#if UNITY_EDITOR
  if(Application.isEditor){UnityEditor.EditorApplication.Exit(exit);yield break;}
#endif
  Application.Quit(exit);
 }
 IEnumerator CaptureClosing(FlowerAssembler flower){
  var gallery=FindObjectOfType<FlowerGallery>();gallery.enabled=false;gallery.showInterface=false;flower.PrepareClosingCapture();
  var camera=Camera.main;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;camera.enabled=false;
  var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf){antiAliasing=Mathf.Max(1,QualitySettings.antiAliasing)};target.Create();
  var image=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=target;
  var rows=new List<string>{"frame,ribbon_radius,echo_bloom,head_turns"};Directory.CreateDirectory("Outputs/ClosingFrames");
  try{
   for(int frame=0;frame<600;frame++){
    flower.MotionStep(1f/60);camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=oldActive;
    File.WriteAllBytes("Outputs/ClosingFrames/frame_"+frame.ToString("0000")+".png",image.EncodeToPNG());rows.Add(flower.ClosingCaptureRow(frame));
    if(frame%60==0)Debug.Log("CLOSING_CAPTURE "+frame+"/600");yield return null;
   }
   File.WriteAllLines("Outputs/MotionValidation/Closing.csv",rows);
  }finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Destroy(image);target.Release();Destroy(target);}
 }
}
}
#endif
