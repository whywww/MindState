#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
namespace MindState {
public sealed class WanderFlexPreview:MonoBehaviour {
 const string Key="MindState.WanderFlexPreview",GpuKey="MindState.WanderFlexGpuPreview",BestKey="MindState.WanderFlexBestPreview";
 public static bool GpuRequested=>SessionState.GetBool(GpuKey,false)||Array.IndexOf(Environment.GetCommandLineArgs(),"-flexGpuPreview")>=0;
 internal static bool Requested=>SessionState.GetBool(Key,false)||Array.IndexOf(Environment.GetCommandLineArgs(),"-wanderFlex")>=0||GpuRequested;
 static bool BestRequested=>SessionState.GetBool(BestKey,false)||Array.IndexOf(Environment.GetCommandLineArgs(),"-flexBest")>=0;
 bool bestBusy,bestPlaying;float bestClock;FlowerAssembler bestFlower;FlowerGallery bestGallery;
 const float BestClipStart=2.86f,BestClipEnd=5f;
 static bool FinalRequested=>Array.IndexOf(Environment.GetCommandLineArgs(),"-flexFinal")>=0;
 string status="Local flexible deformation candidate; no formal cache writes";
 [Serializable]sealed class CaptureView {public Vector3 position;public Quaternion rotation;public float fieldOfView;}
 static void MatchCaptureView(bool record){var camera=Camera.main;const string path="Outputs/WanderFlex/CaptureView.json";if(record||!File.Exists(path))File.WriteAllText(path,JsonUtility.ToJson(new CaptureView{position=camera.transform.position,rotation=camera.transform.rotation,fieldOfView=camera.fieldOfView}));else{var view=JsonUtility.FromJson<CaptureView>(File.ReadAllText(path));camera.transform.SetPositionAndRotation(view.position,view.rotation);camera.fieldOfView=view.fieldOfView;}}

 [MenuItem("MindState/Preview Wandering Flex (CPU, no cache writes)")]
 public static void Run(){Enter(false,false);}
 [MenuItem("MindState/Preview Current Flex Animation (CPU, no cache writes)")]
 public static void RunBest(){Enter(false,false,true);}
 [MenuItem("MindState/Preview Wandering Flex (GPU, staged data)")]
 public static bool StagedPlaybackAvailable()=>FlowerAssembler.FlexStagedPlaybackAvailable();
 public static void RunGpu(){if(!FlowerAssembler.FlexStagedPlaybackAvailable())throw new InvalidOperationException("Compatible staged flex candidate missing");Enter(true,false);}
 public static void ShowFinal(){Enter(false,true);}
 static void Enter(bool gpu,bool final,bool best=false){Directory.CreateDirectory("Outputs/WanderFlex");SessionState.SetBool(Key,!final);SessionState.SetBool(GpuKey,gpu);SessionState.SetBool(BestKey,best);SessionState.SetBool(FlowerOpenClearancePreview.Key,!gpu&&!final);SessionState.SetBool(FlowerOpenClearancePreview.BaselineKey,!gpu&&!final);UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Reset(){EditorApplication.playModeStateChanged+=c=>{if(c==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);SessionState.SetBool(GpuKey,false);SessionState.SetBool(BestKey,false);}};}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Requested||FinalRequested){Application.logMessageReceived+=Fail;Camera.main.gameObject.AddComponent<WanderFlexPreview>();}}
 static void Fail(string message,string stack,LogType type){if(type!=LogType.Exception&&type!=LogType.Error)return;File.WriteAllText("Outputs/WanderFlex/Failure.txt",message+"\n"+stack);if(Application.isBatchMode)EditorApplication.Exit(2);}
 void OnDestroy(){Application.logMessageReceived-=Fail;}
 static string Hash(byte[] bytes){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}
 internal static string[] StandardHashes(FlowerAssembler f){var result=new List<string>();for(int i=0;i<=22;i++){f.SetBloomProgress(.78f+i*.01f);foreach(var p in f.GetDiagnosticPetals()){var v=f.PetalMesh(p.layer,p.index).vertices;var bytes=new byte[v.Length*12];var pin=GCHandle.Alloc(v,GCHandleType.Pinned);try{Marshal.Copy(pin.AddrOfPinnedObject(),bytes,0,bytes.Length);}finally{pin.Free();}result.Add(Hash(bytes));}}return result.ToArray();}
 IEnumerator Start(){
  yield return null;yield return null;
  var f=FindObjectOfType<FlowerAssembler>();f.enabled=false;var gallery=FindObjectOfType<FlowerGallery>();gallery.showInterface=false;gallery.SetView(22,25);gallery.SetView(22,25,gallery.distance*.45f);
  string[] paths={"Assets/Flower/Resources/BottomOverlap.bytes","Assets/Flower/Resources/BottomOverlapProvenance.txt","Assets/StreamingAssets/FlowerPlayback.bin","Assets/Flower/Resources/FlowerPlaybackProvenance.txt"};var cacheHashes=Array.ConvertAll(paths,p=>FlowerAssembler.FlexFileHash(p));
  string mode=GpuRequested?"Gpu":FinalRequested?"FormalGpu":"Cpu";File.WriteAllText("Outputs/WanderFlex/"+mode+"Result.txt","RUNNING; not approved");
  if(BestRequested){bestFlower=f;bestGallery=gallery;yield return BuildBestClip();yield break;}
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-flexBuild")>=0){f.BuildFlexCandidate();File.WriteAllText("Outputs/WanderFlex/BuildResult.txt","CPU parameter interpolation passed; no formal cache published");if(Application.isBatchMode)EditorApplication.Exit(0);yield break;}
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-flexDiagnose")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-flexCorners")>=0){
   bool all=true;bool corners=Array.IndexOf(Environment.GetCommandLineArgs(),"-flexCorners")>=0;
   if(corners){foreach(var p in new[]{new Vector3(-4,-4,4),new Vector3(-4,4,4),new Vector3(4,4,-4),new Vector3(4,-4,-4)}){f.SetArtPreviewImmediate(FlowerArtState.Wandering);f.MotionStep(5);bool ok=false;foreach(float fraction in new[]{1f,.75f,.5f,.25f,0f}){ok=f.SolveFlexPose(100+p.x+p.y*.1f+p.z*.01f,fraction,p);if(ok)break;}Debug.Log("FLEX_CORNER "+p+" passed="+ok);all&=ok;yield return null;}}
   else foreach(float time in new[]{5f,0f,1f,2f,2.86f,3f,4f,6f,7f,8f,9f,10f,11f,12f,13f,14f,15f}){f.SetArtPreviewImmediate(FlowerArtState.Wandering);f.MotionStep(time);if(time==5)Shot("Before");bool ok=f.SolveFlexPose(time);if(!ok&&f.flexReachedLimit)foreach(float fraction in new[]{.75f,.5f,.25f,0f}){ok=f.SolveFlexPose(time,fraction);if(ok)break;}if(time==5)Shot("Candidate");all&=ok;yield return null;if(!ok)break;}
   status=all?"CPU diagnostic poses clear; NOT a published correction":"FAIL: residual contacts; no cache published";File.WriteAllText("Outputs/WanderFlex/Result.txt",status);gallery.showInterface=true;if(Application.isBatchMode)EditorApplication.Exit(all?0:2);yield break;
  }
  MatchCaptureView(!GpuRequested&&!FinalRequested);var standards=StandardHashes(f);if(!FinalRequested)f.LoadFlexCandidate();else {f.SetArtPreviewImmediate(FlowerArtState.Wandering);}
  f.ValidateFlexShape();var after=StandardHashes(f);for(int i=0;i<standards.Length;i++)if(standards[i]!=after[i])throw new Exception("Standard vertices changed at sample "+i);File.WriteAllLines("Outputs/WanderFlex/"+mode+"StandardHashes.txt",after);
  var report=new List<string>{"Standard positions: all 18 petals, 23 samples from 78% to 100%, exact bytes unchanged"};
  f.flexBypass=true;f.SetArtPreviewImmediate(FlowerArtState.Wandering);f.MotionStep(5);MatchCaptureView(false);Shot(mode+"Before");f.flexBypass=false;
  float minimum=float.PositiveInfinity,worstTime=0;
  for(int step=0;step<=30;step++){float time=step*.5f;if(!f.ValidateFlexAt(time,out string message))throw new Exception(mode+" "+message);float gap=f.FlexMinimumGap();if(gap<minimum){minimum=gap;worstTime=time;MatchCaptureView(false);Shot(mode+"MinimumGap");}report.Add(message+" gap="+gap);if(step==10){MatchCaptureView(false);Shot(mode+"After");}yield return null;}
  report.Add("minimum rendered-sheet gap="+minimum+" at time="+worstTime);
  foreach(var target in new[]{FlowerArtState.Clarity,FlowerArtState.Wandering,FlowerArtState.Gathering,FlowerArtState.Wandering}){f.SetArtState(target);for(int step=0;step<360;step++){f.MotionStep(1f/60);if(step%60==59){if(!f.ValidateFlexTransition(out string message))throw new Exception(mode+" "+message);report.Add(target+" "+message);}if(step%30==0)yield return null;}}
  for(int i=0;i<paths.Length;i++)if(cacheHashes[i]!=FlowerAssembler.FlexFileHash(paths[i]))throw new Exception("Formal cache changed during preview: "+paths[i]);
  File.WriteAllLines("Outputs/WanderFlex/"+mode+"Checks.txt",report);status=mode+" local flex active: L1-P3; target, neighbours, roots and transitions passed";File.WriteAllText("Outputs/WanderFlex/"+mode+"Result.txt","PASS: "+status+"\nFormal caches unchanged during this preview.");File.WriteAllText("Outputs/WanderFlex/"+mode+"ValidatedSource.txt",FlowerCacheProvenance.Signature());File.WriteAllText("Outputs/WanderFlex/"+mode+"ValidatedCandidate.sha256",FlowerAssembler.FlexFileHash("Outputs/WanderFlex/Candidate.bin"));Debug.Log("FLEX_PREVIEW_PASS "+mode+" GPU="+f.UsesGpuPlayback);
  f.SetArtPreviewImmediate(FlowerArtState.Wandering);f.MotionStep(5);f.enabled=true;gallery.showInterface=true;
  if(Application.isBatchMode)EditorApplication.Exit(0);
 }
 IEnumerator ShowBestPose(float time){
  if(bestBusy)yield break;bestPlaying=false;bestFlower.ClearBestFlex();bestBusy=true;status="Preparing CPU flexible preview...";yield return null;
  var f=bestFlower;f.enabled=false;f.SetBloomProgress(1);f.SetArtPreviewImmediate(FlowerArtState.Wandering);f.MotionStep(time);MatchCaptureView(true);Shot("BestBefore"+time);
  bool passed=false;float selected=1;string check="";
  foreach(float fraction in new[]{1f,.75f,.5f,.25f,0f}){status="Solving local bend at "+time+" s, relative motion="+fraction;yield return null;passed=f.SolveFlexPose(time,fraction);if(passed)passed=f.ValidateFlexSheets(out check);if(passed){selected=fraction;break;}}
  MatchCaptureView(false);Shot("BestAfter"+time);bestGallery.showInterface=true;
  status=passed?"CPU FLEX APPLIED: L1-P3, pose "+time+"s, relative motion "+selected+"; frozen for inspection. Final playback/cache NOT approved.":"CPU candidate FAILED at this pose; no cache published";
  File.WriteAllText("Outputs/WanderFlex/BestPreview.txt",status+"\n"+check+"\nCamera orbit remains interactive. All formal caches unchanged. The saved pose is not a continuous-playback acceptance.");Debug.Log("FLEX_BEST_READY passed="+passed+" time="+time+" fraction="+selected);bestBusy=false;
 }
 IEnumerator BuildBestClip(){
  if(bestBusy)yield break;bestBusy=true;bestPlaying=false;var f=bestFlower;f.enabled=false;f.ClearBestFlex();var poses=new List<FlowerAssembler.BestFlexPose>();
  for(int i=0;i<=8;i++){float time=Mathf.Lerp(BestClipStart,BestClipEnd,i/8f);yield return SolveBestClipPose(time,poses);if(poses.Count!=i+1){bestBusy=false;yield break;}}
  // Refine failed interpolated poses instead of assuming clear endpoints imply a clear animation.
  for(int pass=0;pass<4;pass++){
   f.AttachBestFlex(poses);var failed=new List<float>();int checks=0;
   for(int i=0;i<poses.Count-1;i++)for(int j=1;j<=3;j++){float time=Mathf.Lerp(poses[i].time,poses[i+1].time,j*.25f);status="Checking animation between poses: "+time.ToString("F2")+"s";yield return null;if(!f.ValidateFlexAt(time,out string message)){failed.Add(time);Debug.LogWarning("FLEX_CLIP_REFINE "+time+" "+message);}checks++;}
   if(failed.Count==0){bestBusy=false;bestPlaying=true;bestClock=0;status="CPU FLEX ANIMATION: 2.86-5.00s forward/reverse; checked samples clear. Full Wandering/cache validation pending.";File.WriteAllText("Outputs/WanderFlex/BestClip.txt",status+"\nControl poses="+poses.Count+"; final interior checks="+checks+"; formal caches unchanged.");Debug.Log("FLEX_CLIP_READY poses="+poses.Count+" checks="+checks);yield break;}
   f.ClearBestFlex();foreach(float time in failed){int count=poses.Count;yield return SolveBestClipPose(time,poses);if(poses.Count!=count+1){bestBusy=false;yield break;}}poses.Sort((a,b)=>a.time.CompareTo(b.time));
  }
  f.AttachBestFlex(poses);bestBusy=false;bestPlaying=true;bestClock=0;status="CPU ANIMATED CANDIDATE: residual interpolation contact; NOT approved. Formal caches unchanged.";File.WriteAllText("Outputs/WanderFlex/BestClip.txt",status);Debug.LogWarning("FLEX_CLIP_FAILED interpolation; showing candidate for manual inspection");
 }
 IEnumerator SolveBestClipPose(float time,List<FlowerAssembler.BestFlexPose> poses){
  var f=bestFlower;f.ClearBestFlex();f.SetBloomProgress(1);f.SetArtPreviewImmediate(FlowerArtState.Wandering);f.MotionStep(time);bool passed=false;
  // Use one conservative relative-motion range over this inspection clip;
  // switching between individually clear branches can cross a neighbour.
  foreach(float fraction in new[]{0f}){status="Preparing CPU animation: "+time.ToString("F2")+"s, target follows lower petal";yield return null;passed=f.SolveFlexPose(time,fraction,renderedContacts:true);if(passed)passed=f.ValidateFlexSheets(out _);if(passed){poses.Add(new FlowerAssembler.BestFlexPose{time=time,tilt=f.flexSolvedTilt,controls=(float[])f.flexSolvedControls.Clone()});break;}}
  if(!passed){status="CPU animation failed at "+time+"s; no cache published";Debug.LogWarning("FLEX_CLIP_FAILED time="+time);}
 }
 void Update(){if(!bestPlaying||bestBusy||bestFlower==null)return;bestClock+=Time.unscaledDeltaTime;float radius=(BestClipEnd-BestClipStart)*.5f;float time=(BestClipStart+BestClipEnd)*.5f-radius*Mathf.Cos(bestClock/radius);bestFlower.SetBestFlexTime(time);}
 void OnGUI(){GUI.Label(new Rect(12,45,1500,30),status);if(!BestRequested||bestFlower==null)return;GUI.enabled=!bestBusy;if(GUI.Button(new Rect(12,80,135,28),"Crossing pose: 5s"))StartCoroutine(ShowBestPose(5));if(GUI.Button(new Rect(155,80,145,28),"Opposing pose: 2.86s"))StartCoroutine(ShowBestPose(2.86f));if(GUI.Button(new Rect(308,80,130,28),"Local bend: 12s"))StartCoroutine(ShowBestPose(12));if(GUI.Button(new Rect(446,80,140,28),"Standard bloom 100%")){bestPlaying=false;bestFlower.ClearBestFlex();bestFlower.SetBloomProgress(1);status="Accepted standard bloom, 100%; no Wandering flex applied";}if(GUI.Button(new Rect(594,80,135,28),bestPlaying?"Pause animation":"Play animation")){if(bestFlower.HasBestFlex)bestPlaying=!bestPlaying;else StartCoroutine(BuildBestClip());}GUI.enabled=true;}

 internal static void Shot(string name){var c=Camera.main;var r=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf){antiAliasing=4};var t=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=c.targetTexture;try{r.Create();c.targetTexture=r;c.Render();using(var read=new FlowerCaptureReadback(1280,720))read.Read(r,t);File.WriteAllBytes("Outputs/WanderFlex/"+name+".png",t.EncodeToPNG());}finally{c.targetTexture=old;r.Release();Destroy(r);Destroy(t);}}
}
public sealed partial class FlowerAssembler {
 internal sealed class BestFlexPose {internal float time,tilt;internal float[] controls;}
 List<BestFlexPose> flexBestFrames;
 internal bool HasBestFlex=>flexBestFrames!=null;
 internal void ClearBestFlex(){flexBestFrames=null;ReleaseFlex();}
 internal void AttachBestFlex(List<BestFlexPose> poses){SetBloomProgress(1);var patch=bottomPatches.Find(p=>p.layer==1&&p.index==3);ApplyBottomPatch(patch);AttachFlex(new FlexData{vertices=patch.work.Length,normals=SmoothFlexNormals(patch.work),wall=(int[])patch.wall.Clone()});flexBestFrames=poses;SetArtPreviewImmediate(FlowerArtState.Wandering);SetBestFlexTime(poses[0].time);}
 void QueryBestFlex(float time,float[] controls,out float tilt){int i=0;while(i<flexBestFrames.Count-2&&time>flexBestFrames[i+1].time)i++;var a=flexBestFrames[i];var b=flexBestFrames[i+1];float w=Mathf.InverseLerp(a.time,b.time,time);tilt=Mathf.Lerp(a.tilt,b.tilt,w);for(int j=0;j<controls.Length;j++)controls[j]=Mathf.Lerp(a.controls[j],b.controls[j],w);}
 internal void SetBestFlexTime(float time){artTime=time;UpdateArt(0);}
 // Cubic B-spline coefficients allow adjacent parts of the same petal to bend
 // in opposite directions. The root fade is applied to the basis, not clamped
 // into the rendered vertices after solving.
 internal bool flexReachedLimit;
 internal float[] flexSolvedControls;
 internal bool[][] flexSolvedMasks;
 internal float flexSolvedTilt;
 internal bool SolveFlexPose(float time,float tiltScale=1,Vector3? poseOverride=null,bool renderedContacts=false){
  var patch=bottomPatches.Find(p=>p.layer==1&&p.index==3);ApplyBottomPatch(patch);var rest=(Vertex[])patch.work.Clone();var macroNormals=SmoothFlexNormals(rest);for(int id=0;id<BottomSurface;id++)rest[id].n=macroNormals[id];
  var layer=layers[1];var target=layer.petals[3];PetalArtMotion(ArtWeights,artTime,3*Mathf.PI*2/layer.petals.Length,1,3,out float requestedTilt,out float requestedRoll);PathPetalPose(layer,target,3,StandardLayerProgress(layer.config,bloomProgress),out var pos,out var rot,out var scale);PetalArtMotion(ArtWeights,artTime,3*Mathf.PI*2/layers[0].petals.Length,0,3,out float lowerTilt,out _);if(poseOverride.HasValue){lowerTilt=poseOverride.Value.x;requestedTilt=poseOverride.Value.y;foreach(int id in new[]{3,9}){int l=id==3?0:1,i=id==3?3:2;var layerItem=layers[l];var item=layerItem.petals[i];PetalArtMotion(ArtWeights,artTime,i*Mathf.PI*2/layerItem.petals.Length,l,i,out _,out float roll);PathPetalPose(layerItem,item,i,1,out var pp,out var rr,out var ss);ApplyPetalPose(layerItem,item,i,pp,rr*Quaternion.Euler(id==3?lowerTilt:poseOverride.Value.z,0,roll),ss);}}ApplyPetalPose(layer,target,3,pos,rot*Quaternion.Euler(Mathf.Lerp(lowerTilt,requestedTilt,tiltScale),0,requestedRoll),scale);flexReachedLimit=false;
  var scan=new FlowerSurfaceCollision(this);var petals=scan.Petals;var world=new Vector3[petals.Length][];var halfWorld=new Vector3[petals.Length][];var matrices=new Matrix4x4[petals.Length];
  for(int p=0;p<petals.Length;p++){var item=petals[p];matrices[p]=generated.worldToLocalMatrix*item.body.localToWorldMatrix;var vertices=PetalMesh(item.layer,item.index).vertices;world[p]=new Vector3[BottomSurface];halfWorld[p]=new Vector3[BottomSurface];for(int id=0;id<BottomSurface;id++){world[p][id]=matrices[p].MultiplyPoint3x4((vertices[id]+vertices[id+BottomSurface])*.5f);halfWorld[p][id]=matrices[p].MultiplyVector((vertices[id]-vertices[id+BottomSurface])*.5f);}}
  var baseWorld=(Vector3[])world[10].Clone();var directions=new Vector3[BottomSurface];for(int id=0;id<BottomSurface;id++)directions[id]=matrices[10].MultiplyVector(rest[id].n);
  var neighbours=new[]{3,9,2,16};var signs=new[]{1f,-1f,1f,-1f};var masks=new bool[4][];var trees=new FlexRayTree[4];
  for(int n=0;n<4;n++){masks[n]=new bool[BottomSurface];var p=petals[neighbours[n]];trees[n]=new FlexRayTree(world[neighbours[n]]);}
  var existingPairs=new HashSet<string>();
  var field=new FlexField();var scalar=new float[BottomSurface];var hits=new List<FlowerSurfaceCollision.Contact>();var report=new List<string>{"iteration,targetHits,middleP2Hits,outerP2Hits,innerP3Hits,maxPositive,maxNegative,maxViolation"};bool passed=false;
  var sheetTarget=new Vector3[BottomSurface];var sheetOther=new Vector3[BottomSurface];float clearance=renderedContacts?.005f:FlexNormalClearance;
  void RecordHits(int n,int[] bad){int other=neighbours[n];foreach(var hit in hits){float t=other<10?hit.lengthB:hit.lengthA,s=other<10?hit.widthB:hit.widthA,ot=other<10?hit.lengthA:hit.lengthB;if(t<.25f||(n!=0&&ot<.25f))continue;bad[n]++;int sx=Mathf.RoundToInt((s+1)*64),sy=Mathf.RoundToInt(t*80);for(int y=Mathf.Max(20,sy-8);y<=Mathf.Min(80,sy+8);y++)for(int x=Mathf.Max(0,sx-12);x<=Mathf.Min(128,sx+12);x++)masks[n][y*129+x]=true;}}
  bool outsideLimit=false;
  for(int iteration=0;iteration<=100;iteration++){
   float positive=0,negative=0;for(int id=0;id<BottomSurface;id++){float h=field.Height(id);scalar[id]=h;positive=Mathf.Max(positive,h);negative=Mathf.Min(negative,h);world[10][id]=baseWorld[id]+directions[id]*h;}
   scan.SamplePositions(world);int total=0;var bad=new int[4];
   for(int n=0;n<4;n++){int other=neighbours[n];scan.FindPairContacts(Mathf.Min(other,10),Mathf.Max(other,10),hits);RecordHits(n,bad);if(renderedContacts){for(int sa=0;sa<2;sa++){for(int id=0;id<BottomSurface;id++)sheetTarget[id]=world[10][id]+halfWorld[10][id]*(sa==0?1:-1);scan.SamplePetalPositions(10,sheetTarget);for(int sb=0;sb<2;sb++){for(int id=0;id<BottomSurface;id++)sheetOther[id]=world[other][id]+halfWorld[other][id]*(sb==0?1:-1);scan.SamplePetalPositions(other,sheetOther);scan.FindPairContacts(Mathf.Min(other,10),Mathf.Max(other,10),hits);RecordHits(n,bad);}}scan.SamplePetalPositions(10,world[10]);scan.SamplePetalPositions(other,world[other]);}total+=bad[n];}
   // Keep the nearby outer neighbour in the active band before a sheet
   // actually touches it. Contact-only activation made the minimal bend
   // disappear abruptly as a contact moved across a triangle boundary.
   if(renderedContacts)for(int id=20*129;id<BottomSurface;id++)if(Mathf.Abs(scalar[id])>1e-6f){float d=trees[2].OutsideDistance(world[10][id],directions[id].normalized,out float alignment);if(alignment>.2f&&Mathf.Abs(d)<.08f&&d*alignment>-.012f)masks[2][id]=true;}
   var sum=new float[FlexField.Count];var counts=new int[FlexField.Count];float violation=0;
   for(int n=0;n<4;n++)for(int id=20*129;id<BottomSurface;id++)if(masks[n][id]){var dir=directions[id];float length=dir.magnitude;float d=trees[n].OutsideDistance(world[10][id],dir*(signs[n]/length),out float alignment);if(d>-clearance/Mathf.Max(.02f,alignment)&&d<2f){float correction=(d+clearance/Mathf.Max(.02f,alignment))/length;violation=Mathf.Max(violation,correction);if(Mathf.Abs(field.Height(id)+signs[n]*correction)>.1001f)outsideLimit=true;field.Propose(id,signs[n]*correction,sum,counts);}}
   // Soft membrane strain penalty; ordinary bending below 25% edge strain
   // is unconstrained. This acts on scalar normal offsets through the same
   // control basis and never moves the root or a neighbouring petal.
   for(int y=9;y<=80;y++)for(int x=0;x<129;x++){int a=y*129+x;for(int axis=0;axis<2;axis++){int b=axis==0?(x<128?a+1:a):(y<80?a+129:a);if(a==b||(scalar[a]==0&&scalar[b]==0))continue;var oldEdge=rest[b].p-rest[a].p;float length=oldEdge.magnitude;if(length<1e-6f)continue;var edge=oldEdge+rest[b].n*scalar[b]-rest[a].n*scalar[a];float change=edge.magnitude-length,excess=Mathf.Abs(change)-length*.25f;if(excess<=0)continue;var unit=edge.normalized;float ga=-Vector3.Dot(unit,rest[a].n),gb=Vector3.Dot(unit,rest[b].n),den=ga*ga+gb*gb;if(den<1e-6f)continue;float correction=Mathf.Sign(change)*excess*.03f/den;field.Propose(a,-ga*correction,sum,counts);field.Propose(b,-gb*correction,sum,counts);}}
   report.Add(iteration+","+string.Join(",",bad)+","+positive+","+negative+","+violation);
   flexReachedLimit|=positive>=.0999f||negative<=-.0999f;
   if(total==0&&violation<(renderedContacts?.0004f:.001f)){passed=true;break;}
   if(outsideLimit){flexReachedLimit=true;report.Add("REQUIRED_DISPLACEMENT_EXCEEDS_0.10");break;}
   if(iteration==100)break;field.Step(sum,counts);
  }
  flexSolvedControls=(float[])field.controls.Clone();flexSolvedMasks=masks;flexSolvedTilt=Mathf.Lerp(lowerTilt,requestedTilt,tiltScale);
  File.WriteAllLines("Outputs/WanderFlex/Pose"+time+"_"+tiltScale+".csv",report);Debug.Log("WANDER_FLEX_POSE time="+time+" tiltScale="+tiltScale+" passed="+passed+" "+report[report.Count-1]);
  // Materialize this exact candidate for the render and collision check, including
  // both sheets and the perimeter wall. No standard overlap data is modified.
  for(int id=0;id<BottomSurface;id++){var d=rest[id].n*field.Height(id);patch.work[id].p=rest[id].p+d;patch.work[id+BottomSurface].p=rest[id+BottomSurface].p+d;}
  for(int y=8;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x;if(field.Height(id)==0)continue;var across=patch.work[y*129+Mathf.Min(x+1,128)].p-patch.work[y*129+Mathf.Max(x-1,0)].p;var along=patch.work[Mathf.Min(y+1,80)*129+x].p-patch.work[Mathf.Max(y-1,0)*129+x].p;var tangent=across.normalized;var normal=Vector3.Cross(along,tangent).normalized;patch.work[id].n=normal;patch.work[id+BottomSurface].n=-normal;patch.work[id].t=new Vector4(tangent.x,tangent.y,tangent.z,-1);patch.work[id+BottomSurface].t=new Vector4(tangent.x,tangent.y,tangent.z,1);}
  for(int k=BottomSurface*2;k<patch.work.Length;k++)patch.work[k].p=patch.work[patch.wall[k-BottomSurface*2]].p;
  for(int k=BottomSurface*2;k<patch.work.Length;k+=2){int id=patch.wall[k-BottomSurface*2];int previous=k==BottomSurface*2?patch.work.Length-2:k-2,next=k+2>=patch.work.Length?BottomSurface*2:k+2;var edge=(patch.work[next].p-patch.work[previous].p).normalized;var normal=Vector3.Cross(edge,-patch.work[id].n).normalized;patch.work[k].n=patch.work[k+1].n=normal;patch.work[k].t=patch.work[k+1].t=new Vector4(edge.x,edge.y,edge.z,1);}
  patch.mesh.SetVertexBufferData(patch.work,0,0,patch.work.Length);var bounds=patch.mesh.bounds;bounds.Expand(.2f);patch.mesh.bounds=bounds;
  if(passed){
   var corrected=world[10];var original=new Vector3[BottomSurface];var rawMatrix=OpenMatrix(1,3,1,requestedTilt,requestedRoll);for(int id=0;id<BottomSurface;id++)original[id]=rawMatrix.MultiplyPoint3x4((rest[id].p+rest[id+BottomSurface].p)*.5f);world[10]=original;scan.SamplePositions(world);var beforeHits=new List<FlowerSurfaceCollision.Contact>();scan.FindContacts(beforeHits,parallel:true);foreach(var h in beforeHits)if((h.petalA==10||h.petalB==10)&&h.lengthA>=.25f&&h.lengthB>=.25f)existingPairs.Add(h.petalA+":"+h.petalB);world[10]=corrected;
   scan.SampleRendered();var after=new List<FlowerSurfaceCollision.Contact>();scan.FindContacts(after,parallel:true);
   foreach(var hit in after)if((hit.petalA==10||hit.petalB==10)&&hit.lengthA>=.25f&&hit.lengthB>=.25f&&!existingPairs.Contains(hit.petalA+":"+hit.petalB)){passed=false;Debug.LogWarning("FLEX_NEW_CONTACT time="+time+" pair="+hit.petalA+":"+hit.petalB);}
   for(int id=0;id<9*129;id++)if(!patch.work[id].p.Equals(rest[id].p)||!patch.work[id+BottomSurface].p.Equals(rest[id+BottomSurface].p))throw new InvalidOperationException("Flex moved fixed root");
   File.WriteAllLines("Outputs/WanderFlex/Controls"+time+".csv",Array.ConvertAll(field.controls,c=>c.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
  }
  return passed;
 }
}
}
#endif
