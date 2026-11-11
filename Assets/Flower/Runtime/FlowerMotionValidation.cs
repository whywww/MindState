#if UNITY_EDITOR || FLOWER_VALIDATION
using System;
using System.IO;
using System.Collections;
using System.Text;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 internal void MotionAssertions(string path) {
  var report=new StringBuilder();
  Action<bool,string> check=(ok,message)=>{report.AppendLine((ok?"PASS ":"FAIL ")+message);File.WriteAllText(Path.Combine(path,"Checks.txt"),report.ToString());if(!ok)throw new Exception(message);};
  SetArtPreviewImmediate(FlowerArtState.Clarity);SetFrozen(false);SetArtState(FlowerArtState.Gathering);
  UpdateArt(9);check(Mathf.Abs(ArtWeights.y-.5f)<.0001f,"18-second transition midpoint");
  var before=ArtWeights;SetArtState(FlowerArtState.Quiet);UpdateArt(0);check(ArtWeights==before,"Retarget preserves current weights");
  SetFrozen(true);float clock=ArtTime;UpdateArt(4);check(ArtWeights==before&&ArtTime==clock,"Freeze retains transition time");
  SetFrozen(false);UpdateArt(18);check(ArtWeights.w==1,"Resume reaches exact target");
  ResetArt();UpdateArt(0);check(ArtWeights.x==1,"Reset clears pending transition");
  check(VerifyGatheringPath(0)&&VerifyGatheringPath(1.25f)&&VerifyGatheringPath(2.5f),"Gathering endpoints retain original path");
  SetArtPreviewImmediate(FlowerArtState.Clarity);SetBrainMetrics(float.NaN,1);UpdateArt(0);check(ArtWeights.x==1,"Invalid brain input ignored");
  SimulateArtBrain();for(int i=0;i<100;i++)UpdateArt(.05f);check(Mathf.Abs(ArtWeights.x+ArtWeights.y+ArtWeights.z+ArtWeights.w-1)<.0001f,"Simulation normalized");
  SetBrainMetrics(.4f,.6f);UpdateArt(.05f);check(ArtInput==FlowerArtInput.External,"External streaming input");
  var scan=new FlowerSurfaceCollision(this);var hits=new System.Collections.Generic.List<FlowerSurfaceCollision.Contact>();var contacts=new StringBuilder("clip,frame,same,cross,self,root\n");
  for(int clip=0;clip<6;clip++){
   int other=clip/2==0?0:clip/2==1?2:3;bool closing=clip%2==0;SetArtPreviewImmediate((FlowerArtState)(closing?other:1));SetArtState((FlowerArtState)(closing?1:other));
   for(int frame=0;frame<=72;frame++){UpdateArt(frame==0?0:.25f);check(RootAnchorError()<1e-5f,"Attached roots "+clip+"/"+frame);if(frame%18!=0)continue;
    scan.SampleRendered();scan.FindContacts(hits,parallel:true);int same=0,cross=0,self=0,root=0;foreach(var h in hits){if(h.rootRegion)root++;else if(h.petalA==h.petalB)self++;else if(scan.Petals[h.petalA].layer==scan.Petals[h.petalB].layer)same++;else cross++;}contacts.AppendLine(clip+","+frame+","+same+","+cross+","+self+","+root);
   }
  }
  File.WriteAllText(Path.Combine(path,"ArtContacts.csv"),contacts.ToString());
  SetBloomProgress(.5f);check(!ArtActive,"Legacy API exits art");PlayBloom();check(autoPlay&&bloomProgress==0,"Replay starts at original bud");
 }
 internal void MotionStep(float dt) { UpdateArt(dt); }
 internal Vector3 MotionTip(int layer) {
  var l=layers[layer];var p=l.petals[0];var mesh=p.body.GetComponent<MeshFilter>().sharedMesh;
  return generated.InverseTransformPoint(p.body.TransformPoint(mesh.vertices[80*129+64]));
 }
}
public sealed class FlowerMotionValidation:MonoBehaviour {
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyArtMotion")>=0)Camera.main.gameObject.AddComponent<FlowerMotionValidation>();}
 IEnumerator Start(){
  yield return null;
  var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-motionOutput");
  string path=Path.GetFullPath(arg>=0?args[arg+1]:"Evidence/ArtMotion/After");Directory.CreateDirectory(path);
  var f=FindObjectOfType<FlowerAssembler>();var g=FindObjectOfType<FlowerGallery>();f.enabled=false;g.showInterface=false;g.SetView(22,55,9);
  var cam=GetComponent<Camera>();cam.enabled=false;var rt=new RenderTexture(640,360,24);rt.Create();cam.targetTexture=rt;var tex=new Texture2D(640,360,TextureFormat.RGB24,false);
  if(Array.IndexOf(args,"-motionChecksOnly")>=0){f.MotionAssertions(path);File.WriteAllText(Path.Combine(path,"Complete.txt"),"Complete");Application.Quit();yield break;}
  var csv=new StringBuilder("clip,frame,layer,progress,tipStep,updateMs,gc\n");
  for(int clip=0;clip<6;clip++){
   int other=clip/2==0?0:clip/2==1?2:3;bool closing=clip%2==0;
   f.SetArtPreviewImmediate((FlowerArtState)(closing?other:1));f.SetFrozen(false);f.SetArtState((FlowerArtState)(closing?1:other));
   string folder=Path.Combine(path,"Clip"+clip);Directory.CreateDirectory(folder);
   var previous=new Vector3[f.LayerCount];for(int l=0;l<previous.Length;l++)previous[l]=f.MotionTip(l);
   for(int frame=0;frame<=400;frame++){
    long bytes=GC.GetAllocatedBytesForCurrentThread(),start=System.Diagnostics.Stopwatch.GetTimestamp();f.MotionStep(.05f);double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;long gc=GC.GetAllocatedBytesForCurrentThread()-bytes;
    for(int l=0;l<previous.Length;l++){var tip=f.MotionTip(l);csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6}\n",clip,frame,l,f.bloomProgress,(tip-previous[l]).magnitude,ms,gc);previous[l]=tip;}
    if(frame%2==0){cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,640,360),0,0);tex.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(folder,frame.ToString("0000")+".jpg"),tex.EncodeToJPG(85));}
    if(frame%20==0)yield return null;
   }
  }
  File.WriteAllText(Path.Combine(path,"Motion.csv"),csv.ToString());
  if(Array.IndexOf(args,"-motionChecks")>=0)f.MotionAssertions(path);
  cam.targetTexture=null;rt.Release();Destroy(rt);Destroy(tex);File.WriteAllText(Path.Combine(path,"Complete.txt"),"Complete");Application.Quit();
 }
}
}
#endif
