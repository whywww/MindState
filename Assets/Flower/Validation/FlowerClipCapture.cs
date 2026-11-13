#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 internal bool clipReverseEcho;
 internal float clipEchoBloom;
 internal void ClipStep(float dt)=>UpdateArt(dt);
 // A single Bezier path through the four looks has no internal zero-speed joins.
 internal void ClipBlend(Vector4 weights){
  ArtWeights=ArtLightWeights=artTarget=artFrom=artDestination=weights;
  artTransitionTime=6;ArtInput=FlowerArtInput.Preview;
  Color accent=Color.clear;Vector2 shape=Vector2.zero;
  for(int i=0;i<4;i++){accent+=artLooks[i].accent*weights[i];shape+=ShapePresets[i]*weights[i];}
  previewAccentFrom=previewAccentTo=accent;
  shapeAttentionTarget=shape.x;shapeRelaxationTarget=shape.y;
 }
}
// Offline 10-second montage: each state is premultiplied to a settled pose, then captured as a PNG
// sequence. The silk thread needs up to 12.5 s to lay down a full trail after a reset, hence the
// 13 s pre-roll; the echo flower records the same state during that warm-up.
public sealed class FlowerClipCapture : MonoBehaviour {
 string Output=>continuous?"Outputs/ArtContinuousFrames":showcase?(preview?"Outputs/ArtPreview":"Outputs/ArtShowcaseFrames"):"Outputs/ClipFrames";
 bool showcase,preview,continuous;
 readonly int[] shotFrames={162,132,132,210};
 readonly List<string> report=new List<string>();
 readonly List<string> motion=new List<string>{"frame,bloom,clarity,gathering,wandering,quiet,echoBloom"};
 int width,height,segFrames,fps,exitCode;
 float preRoll;
 FlowerArtState[] states;
 string failure;
 public static void Run(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");EditorApplication.EnterPlaymode();}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
 {
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-recordClip")>=0&&Camera.main!=null)
   Camera.main.gameObject.AddComponent<FlowerClipCapture>();
 }
 IEnumerator Start()
 {
  continuous=Arg("-clipContinuous","0")=="1";
  showcase=Arg("-artShowcase","0")=="1";preview=Arg("-clipPreview","0")=="1";
  Application.runInBackground=true;Application.targetFrameRate=-1;
  width=Mathf.Max(64,ArgInt("-clipWidth",1920));height=Mathf.Max(64,ArgInt("-clipHeight",1080));
  segFrames=Mathf.Max(2,ArgInt("-clipSegFrames",180));fps=Mathf.Max(1,ArgInt("-clipFps",60));
  preRoll=Mathf.Max(0f,ArgFloat("-clipPreroll",13f));
  states=ParseStates(Arg("-clipStates","Clarity,Gathering,Wandering,Quiet"));
  if(continuous){fps=60;preRoll=Mathf.Max(13,preRoll);states=ParseStates("Gathering");}
  if(showcase){fps=60;preRoll=Mathf.Max(13,preRoll);states=ParseStates("Clarity,Gathering,Quiet,Wandering");}
  Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"Result.txt"),"RUNNING");
  var run=Capture();bool more=true;
  while(more){
   object current=null;
   try{more=run.MoveNext();if(more)current=run.Current;}
   catch(Exception e){Debug.LogException(e);failure=failure??e.Message;exitCode=exitCode!=0?exitCode:5;more=false;}
   if(more)yield return current;
  }
  File.WriteAllLines(Path.Combine(Output,"ClipReport.txt"),report);
  if(continuous)File.WriteAllLines(Path.Combine(Output,"Motion.csv"),motion);
  File.WriteAllText(Path.Combine(Output,"Result.txt"),failure??"PASS");
  if(failure!=null)Debug.LogError(failure);
  EditorApplication.Exit(exitCode);
 }
 IEnumerator Capture()
 {
  var f=FindObjectOfType<FlowerAssembler>();var gallery=FindObjectOfType<FlowerGallery>();var cam=Camera.main;
  Require(f!=null&&gallery!=null&&cam!=null,3,"FlowerGallery scene is not ready.");
  float readyDeadline=Time.realtimeSinceStartup+120f;
  while(!(f.Ready&&f.PetalCount==18)){
   Require(Time.realtimeSinceStartup<readyDeadline,3,"Flower did not become ready within 120 s.");
   yield return null;
  }
  var post=cam.GetComponent<GalleryPost>();
  Require(post!=null&&post.enabled&&post.shader!=null,3,"GalleryPost is missing or disabled.");
  gallery.showInterface=false;gallery.enabled=false;cam.enabled=false;
  var brain=cam.GetComponent<MindState.BCI.BrainLink.BrainLinkFlowerInput>();if(brain!=null)brain.enabled=false;
  f.enabled=false;f.SetFrozen(false);f.BrainBaselineFrozen=false;
  var hi=new RenderTexture(width*2,height*2,24,RenderTextureFormat.ARGBHalf){antiAliasing=Mathf.Max(1,QualitySettings.antiAliasing)};var lo=new RenderTexture(width,height,0,RenderTextureFormat.ARGBHalf);
  using var readback=new FlowerCaptureReadback(width,height);
  report.Add(FlowerCaptureReadback.VerifyTransfer());
  var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
  var oldTarget=cam.targetTexture;var oldActive=RenderTexture.active;
  try{
   hi.Create();lo.Create();cam.targetTexture=hi;
   yield return null;
   gallery.SetView(22,55);
   yield return null;
   for(int s=0;s<states.Length;s++){
    var state=states[s];string folder=Path.Combine(Output,"state"+s);Directory.CreateDirectory(folder);
    f.clipReverseEcho=continuous;
    if(showcase)f.ResetArt();f.SetArtPreviewImmediate(state);
    Require(f.EchoVisible,3,"Echo flower is missing for "+state+".");
    Require(f.VisibleRibbonCount==1,3,"Silk thread is missing for "+state+".");
    int steps=Mathf.CeilToInt(preRoll*fps);
    for(int i=0;i<steps;i++){f.ClipStep(1f/fps);if(i%300==299)yield return null;}
    float mean=0,std=0;
    int count=continuous?600:showcase?shotFrames[s]:segFrames;
    float el=showcase&&s==1?45:55;if(showcase)gallery.SetView(18,el);
    Vector3 pivot=gallery.focus;float distance=gallery.distance;
    if(continuous)distance*=.94f;
    if(showcase&&s==1){pivot=Vector3.Lerp(pivot,f.FullBloomBounds.center,.6f);distance*=.72f;}
    for(int frame=0;frame<(preview?1:count);frame++){
     if(continuous){
      float clock=frame/60f;
      // Ease only the start/end of the whole take; never stop at an intermediate look.
      float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((clock-.4f)/9f)),u=1-t;
      f.ClipBlend(new Vector4(3*u*t*t,u*u*u,t*t*t,3*u*u*t));
     }
     if(showcase||continuous){
      float az=Mathf.Lerp(18,26,preview?.5f:(float)frame/(count-1));
      float a=az*Mathf.Deg2Rad,e=el*Mathf.Deg2Rad;
      Vector3 dir=new Vector3(Mathf.Sin(a)*Mathf.Cos(e),Mathf.Sin(e),-Mathf.Cos(a)*Mathf.Cos(e));
      Quaternion rot=Quaternion.LookRotation(-dir,Vector3.up);
      cam.transform.SetPositionAndRotation(pivot+dir*distance,rot);
      f.SetViewFrame(dir,rot*Vector3.up,rot*Vector3.right,distance);
     }
     f.ClipStep(1f/fps);
     if(continuous){var w=f.ArtWeights;motion.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R}",frame,f.bloomProgress,w.x,w.y,w.z,w.w,f.clipEchoBloom));}
     cam.Render();Graphics.Blit(hi,lo);
     readback.Read(lo,tex);tex.Apply();
     byte[] png=tex.EncodeToPNG();
     File.WriteAllBytes(Path.Combine(folder,"frame_"+frame.ToString("0000")+".png"),png);
     if(frame%30==0||frame==count-1){
      Measure(tex,out mean,out std);
      Require(mean>.003f&&mean<.9f&&std>.006f,4,"Blank frame "+frame+" for "+state+" (mean "+mean.ToString("F4",CultureInfo.InvariantCulture)+", std "+std.ToString("F4",CultureInfo.InvariantCulture)+").");
     }
     if(frame%60==0||frame==count-1)
      report.Add(state+","+frame+",mean="+mean.ToString("F4",CultureInfo.InvariantCulture)+",std="+std.ToString("F4",CultureInfo.InvariantCulture)+",bytes="+png.Length);
     if(frame%60==0)Debug.Log("[Clip] "+state+" "+frame+"/"+count);
     yield return null;
    }
    var weights=f.ArtWeights;
    Require(continuous||weights[(int)state]>.999f,4,"Art weights not settled for "+state+": "+weights.ToString("F4"));
    var echo=f.EchoWeights;
    Require(continuous||echo[(int)state]>.90f,4,"Echo weights do not match "+state+": "+echo.ToString("F4"));
    report.Add(state+",settled,art="+weights.ToString("F4")+",echo="+echo.ToString("F4"));
    Debug.Log("[Clip] "+state+" complete. art="+weights.ToString("F4")+" echo="+echo.ToString("F4"));
   }
  }
  finally{
   cam.targetTexture=oldTarget;RenderTexture.active=oldActive;
   if(hi!=null){hi.Release();Destroy(hi);}
   if(lo!=null){lo.Release();Destroy(lo);}
   if(tex!=null)Destroy(tex);
  }
 }
 void Require(bool ok,int code,string message)
 {
  if(ok)return;
  exitCode=code;failure=message;throw new InvalidOperationException(message);
 }
 static void Measure(Texture2D tex,out float mean,out float std)
 {
  var pixels=tex.GetPixels32();double sum=0,sum2=0;int count=0;
  for(int i=0;i<pixels.Length;i+=16){
   var p=pixels[i];double l=(.2126*p.r+.7152*p.g+.0722*p.b)/255.0;
   sum+=l;sum2+=l*l;count++;
  }
  mean=(float)(sum/count);std=(float)Math.Sqrt(Math.Max(0,sum2/count-(sum/count)*(sum/count)));
 }
 static string Arg(string name,string fallback)
 {
  var args=Environment.GetCommandLineArgs();
  for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];
  return fallback;
 }
 static int ArgInt(string name,int fallback)=>int.TryParse(Arg(name,""),NumberStyles.Integer,CultureInfo.InvariantCulture,out int value)?value:fallback;
 static float ArgFloat(string name,float fallback)=>float.TryParse(Arg(name,""),NumberStyles.Float,CultureInfo.InvariantCulture,out float value)?value:fallback;
 static FlowerArtState[] ParseStates(string csv)
 {
  var list=new List<FlowerArtState>();
  foreach(var part in csv.Split(new[]{','},StringSplitOptions.RemoveEmptyEntries))
   if(Enum.TryParse(part.Trim(),true,out FlowerArtState state))list.Add(state);
  if(list.Count==0)list.Add(FlowerArtState.Clarity);
  return list.ToArray();
 }
}
}
#endif
