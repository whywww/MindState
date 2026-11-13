#if UNITY_EDITOR || FLOWER_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 internal void MotionStep(float dt)=>UpdateArt(dt);
 internal bool VerifyStandardArtPath(FlowerArtState state,float time) {
  suppressArtMotion=true;
  SetArtPreviewImmediate(state);MotionStep(time);
  var petals=GetDiagnosticPetals();var vertices=new Vector3[petals.Length][];var matrices=new Matrix4x4[petals.Length];
  for(int i=0;i<petals.Length;i++){vertices[i]=PetalMesh(petals[i].layer,petals[i].index).vertices;matrices[i]=generated.worldToLocalMatrix*petals[i].body.localToWorldMatrix;}
  float progress=bloomProgress;SetBloomProgress(progress);bool equal=true;
  for(int i=0;i<petals.Length;i++){
   var current=PetalMesh(petals[i].layer,petals[i].index).vertices;
   for(int j=0;j<current.Length;j++)equal&=(current[j]-vertices[i][j]).sqrMagnitude<1e-12f;
   var matrix=generated.worldToLocalMatrix*petals[i].body.localToWorldMatrix;
   for(int j=0;j<16;j++)equal&=Mathf.Abs(matrix[j]-matrices[i][j])<1e-5f;
  }
  suppressArtMotion=false;return equal;
 }
}
public sealed class FlowerProjectValidation : MonoBehaviour {
 const string Output="Outputs/Validation";
 readonly List<string> checks=new List<string>();
#if UNITY_EDITOR
 public static void Run(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");UnityEditor.EditorApplication.EnterPlaymode();}
#endif
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyProject")>=0)Camera.main.gameObject.AddComponent<FlowerProjectValidation>();}
 void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Path.Combine(Output,"Checks.txt"),checks);if(!ok)throw new Exception(message);}
 static void Exit(int code){
#if UNITY_EDITOR
  if(Application.isEditor){UnityEditor.EditorApplication.Exit(code);return;}
#endif
  Application.Quit(code);
 }
 IEnumerator Start(){
  Application.runInBackground=true;Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"Result.txt"),"RUNNING");
  yield return null;
  var run=RunChecks();bool passed=true;
  while(true){object current=null;bool more=false;try{more=run.MoveNext();if(more)current=run.Current;}catch(Exception e){Debug.LogException(e);passed=false;}if(!passed||!more)break;yield return current;}
  File.WriteAllText(Path.Combine(Output,"Result.txt"),passed?"PASS; dynamic contacts accepted by user; hardware not tested":"FAIL");Exit(passed?0:2);
 }
 IEnumerator RunChecks(){
  var f=FindObjectOfType<FlowerAssembler>();Check(f!=null&&f.Ready&&f.PetalCount==18,"Current flower initialized: 18 petals");f.enabled=false;
  var gallery=FindObjectOfType<FlowerGallery>();gallery.showInterface=false;
  for(int i=0;i<=100;i++){f.SetBloomProgress(i/100f);Check(f.RootAnchorError()<1e-5f,"Standard roots at "+i+"%");}
  for(int i=0;i<=64;i++){f.SetBloomProgress(i/64f);Check(f.RootAnchorError()<1e-5f,"Cache node/midpoint roots "+i+"/64");}
  f.SetBloomProgress(.5f);int uploads=f.MeshUpdates;f.SetBloomProgress(.5f);Check(f.MeshUpdates==uploads,"Repeated progress does not upload base meshes");
  Check(f.VerifyPackedInterpolation().StartsWith("PASS"),"Packed interpolation matches reference");
  for(int state=0;state<4;state++)for(int sample=0;sample<3;sample++)Check(f.VerifyStandardArtPath((FlowerArtState)state,sample*1.25f),"Standard geometry and transforms state="+state+" sample="+sample);
  for(int state=0;state<4;state++){
   f.SetArtPreviewImmediate((FlowerArtState)state);f.SyncEchoHistory();f.MotionStep(1.3f);
   Check(f.EchoPetalCount==18&&f.EchoVisible,"Echo flower visible with 18 petals state="+state);
   Check(Mathf.Abs(f.EchoScaleValue-.50f)<.01f,"Echo flower at 50% scale");
   Check(f.EchoClearance()>=1f,"Echo flower clears the bloom state="+state+" clearance="+f.EchoClearance().ToString("F3"));
   Check(f.EchoMeshFinite(),"Echo mesh finite state="+state);
   Check(f.VisibleRibbonCount==1,"One silk thread in every state state="+state);
   Check(f.RibbonThreadVisible,"Silk thread stays visible state="+state);
   Check(f.RibbonVerticesFinite(),"Silk thread vertices finite state="+state);
   Check(f.RibbonClearance()>=1.10f,"Silk thread clears the bloom state="+state+" clearance="+f.RibbonClearance().ToString("F3")+" rb="+f.RibbonBloomRadius.ToString("F3"));
   Check(f.RibbonTrailTurns>=.45f&&f.RibbonTrailTurns<=1.5f,"Silk trail stays within one and a half turns state="+state);
  }
  f.ResetBrainBaseline();f.BrainBaselineFrozen=true;
  Check(FlowerAssembler.BrainArtWeights(1,1)==new Vector4(1,0,0,0),"Raw corner Clarity");
  Check(FlowerAssembler.BrainArtWeights(1,0)==new Vector4(0,1,0,0),"Raw corner Gathering");
  Check(FlowerAssembler.BrainArtWeights(0,1)==new Vector4(0,0,1,0),"Raw corner Wandering");
  Check(FlowerAssembler.BrainArtWeights(0,0)==new Vector4(0,0,0,1),"Raw corner Quiet");
  f.SetBrainMetrics(.55f,.45f);var display55=f.DisplayWeights;
  Check(Mathf.Abs(display55.x+display55.y+display55.z+display55.w-1)<1e-4f,"Display weights normalize");
  Check(display55.y>=.60f,"55/45 commits to Gathering");
  var gatheringAccent=new Color(.98f,.68f,.32f);
  var accent55=f.AccentFor(display55);
  Check(ChannelDistance(accent55,gatheringAccent)<.10f,"55/45 keeps the Gathering accent");
  f.SetBrainMetrics(.65f,.35f);
  Check(f.DisplayWeights.y>=.82f,"65/35 strengthens Gathering");
  Check(ChannelDistance(f.AccentFor(f.DisplayWeights),gatheringAccent)<.06f,"65/35 keeps the Gathering accent");
  f.SetBrainMetrics(.5f,.5f);var mix=f.DisplayWeights;
  Check(ChannelDistance(f.AccentFor(mix),accent55)<.10f,"Near tie keeps the committed accent");
  Check(!float.IsNaN(mix.x)&&!float.IsNaN(mix.y)&&!float.IsNaN(mix.z)&&!float.IsNaN(mix.w),"Tie weights stay finite");
  Check(mix.y>=mix.x&&mix.y>=mix.z&&mix.y>=mix.w,"Near tie keeps the committed state");
  // A violent raw-signal step must still move color and the silk route smoothly.
  f.WaitForArtBrain();f.SetBrainMetrics(0,0);
  for(int i=0;i<150;i++) f.MotionStep(1/60f);
  f.SetBrainMetrics(0,1);
  float accentStep=0,routeStep=0;
  var lastAccent=f.ArtAccentColor;
  for(int frame=0;frame<180;frame++){
   f.MotionStep(1/60f);
   accentStep=Mathf.Max(accentStep,ChannelDistance(f.ArtAccentColor,lastAccent));lastAccent=f.ArtAccentColor;
   routeStep=Mathf.Max(routeStep,f.RibbonMaxVertexStep);
  }
  Check(accentStep<.03f,"Color changes continuously under a violent step "+accentStep.ToString("F3"));
  Check(routeStep<.12f,"Silk route moves continuously under a violent step "+routeStep.ToString("F3"));
  // The bright head must keep travelling forward, never reversing.
  float travelled=0,worstBackstep=0,previousHead=f.RibbonHeadTurn;
  for(int frame=0;frame<1560;frame++){
   f.MotionStep(1/60f);
   float step=f.RibbonHeadTurn-previousHead;previousHead=f.RibbonHeadTurn;
   travelled+=step;worstBackstep=Mathf.Min(worstBackstep,step);
  }
  Check(travelled>=.9f,"Silk head travels a full turn "+travelled.ToString("F2"));
  Check(worstBackstep>=-1e-5f,"Silk head never moves backwards "+worstBackstep.ToString("F5"));
  // Attention/relaxation must reshape the route in the documented directions.
  f.SetBrainMetrics(.8f,.8f);
  for(int i=0;i<420;i++) f.MotionStep(1/60f);
  float clarityRadius=f.RibbonMeanRadius,clarityHeight=f.RibbonMeanHeight,clarityTrail=f.RibbonTrailTurns;
  f.SetBrainMetrics(.2f,.8f);
  for(int i=0;i<420;i++) f.MotionStep(1/60f);
  float wanderingRadius=f.RibbonMeanRadius,wanderingHeight=f.RibbonMeanHeight,wanderingTrail=f.RibbonTrailTurns,wanderingWave=f.RibbonWaveAmplitude;
  f.SetBrainMetrics(.8f,.2f);
  for(int i=0;i<420;i++) f.MotionStep(1/60f);
  float gatheringWave=f.RibbonWaveAmplitude,gatheringTrail=f.RibbonTrailTurns;
  Check(wanderingRadius>clarityRadius+.02f,"Relaxation widens the silk loop "+wanderingRadius.ToString("F2")+" > "+clarityRadius.ToString("F2"));
  Check(clarityHeight>wanderingHeight+.05f,"Attention lifts the silk loop "+clarityHeight.ToString("F2")+" > "+wanderingHeight.ToString("F2"));
  Check(gatheringWave>wanderingWave+.03f,"Tension adds silk jitter "+gatheringWave.ToString("F2")+" > "+wanderingWave.ToString("F2"));
  Check(wanderingTrail>gatheringTrail+.10f,"Relaxation lengthens the silk trail "+wanderingTrail.ToString("F2")+" > "+gatheringTrail.ToString("F2"));
  Check(Mathf.Abs(clarityTrail-wanderingTrail)<.25f,"Equal relaxation keeps the trail length");
  f.BrainBaselineFrozen=false;
  f.SetArtPreviewImmediate(FlowerArtState.Clarity);f.SyncEchoHistory();
  for(int i=0;i<10;i++) f.MotionStep(1f);
  f.SetArtState(FlowerArtState.Gathering);f.MotionStep(1f);
  Check(f.EchoWeights.x>.99f,"Echo ignores the live flower inside the 10 s hold");
  for(int i=0;i<9;i++) f.MotionStep(1f);
  Check(f.EchoWeights.x>.99f,"Echo still sees only the pre-hold history at 20 s");
  for(int i=0;i<16;i++) f.MotionStep(1f);
  Check(Mathf.Abs(f.EchoWeights.y-.5f)<.08f,"Echo reports the 10-45 s average across the transition");
  Check(f.ArtWeights.y>.99f,"Live flower has already settled");
  for(int i=0;i<25;i++) f.MotionStep(1f);
  Check(f.EchoWeights.y>.98f,"Echo forgets Clarity once the old state leaves the window");
  for(int from=0;from<4;from++)for(int to=0;to<4;to++)if(from!=to){
   f.SetArtPreviewImmediate((FlowerArtState)from);f.SetFrozen(false);f.SetArtState((FlowerArtState)to);
   var weights=f.ArtWeights;f.MotionStep(0);Check(f.ArtWeights==weights,"Retarget continuous "+from+" to "+to);
   for(int frame=0;frame<40;frame++){f.MotionStep(.25f);if(frame%10==0)Check(f.RootAnchorError()<1e-5f,"Transition roots "+from+" to "+to+" frame="+frame);}
   Check(Mathf.Abs(f.ArtWeights[to]-1)<1e-5f,"6-second transition endpoint "+from+" to "+to);yield return null;
  }
  f.SetFrozen(true);float time=f.ArtTime;uploads=f.MeshUpdates;var frozenWeights=f.ArtWeights;f.MotionStep(4);
  Check(f.ArtTime==time&&f.MeshUpdates==uploads&&f.ArtWeights==frozenWeights,"Freeze preserves time, weights and mesh uploads");f.SetFrozen(false);
  f.ResetArt();f.MotionStep(0);Check(f.ArtWeights.x==1,"Reset to Clarity");
  f.SetBrainMetrics(float.NaN,1);f.MotionStep(0);Check(f.ArtWeights.x==1,"Invalid sample does not change weights");
  f.SetBrainMetrics(.4f,.6f);f.MotionStep(20);var held=f.ArtWeights;yield return new WaitForSecondsRealtime(5.2f);f.MotionStep(0);Check(f.ArtStatus=="正在重新感知","External timeout");
  Check((f.ArtWeights-held).sqrMagnitude<1e-6f,"Timeout holds the last state");f.SetBrainMetrics(.4f,.6f);Check(f.ArtStatus=="外部脑电","External recovery");
  f.SetBloomProgress(.5f);Check(!f.ArtActive,"Manual preview exits art");f.PlayBloom();Check(f.autoPlay&&f.bloomProgress==0&&!f.frozen,"Replay resets opening");
  for(int state=0;state<4;state++){f.SetArtPreviewImmediate((FlowerArtState)state);f.SyncEchoHistory();f.MotionStep(1.4f);f.ResetRibbonThread();f.MotionStep(0);Shot("State"+state);}
  gallery.SetView(22,55);float closeDistance=gallery.distance*.66f;
  gallery.SetView(22,55,closeDistance);
  for(int state=0;state<4;state++){f.SetArtPreviewImmediate((FlowerArtState)state);f.SyncEchoHistory();f.MotionStep(1.4f);f.ResetRibbonThread();f.MotionStep(0);Shot("State"+state+"_close");}
  gallery.SetView(22,55);
  f.SetArtPreviewImmediate(FlowerArtState.Wandering);
  f.BrainBaselineFrozen=true;f.SetBrainMetrics(.5f,.5f);f.MotionStep(20);Shot("Mix");f.BrainBaselineFrozen=false;
  f.SetArtPreviewImmediate(FlowerArtState.Wandering);f.SyncEchoHistory();f.MotionStep(1.4f);f.ResetRibbonThread();f.MotionStep(0);Shot("Hero",1920,1080);
  f.SetArtPreviewImmediate(FlowerArtState.Clarity);f.SyncEchoHistory();
  for(int i=0;i<10;i++) f.MotionStep(1f);
  f.SetArtState(FlowerArtState.Gathering);for(int i=0;i<32;i++) f.MotionStep(.25f);
  Shot("Echo");
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-scanContacts")>=0){
   var scan=new FlowerSurfaceCollision(f);var hits=new List<FlowerSurfaceCollision.Contact>();var lines=new List<string>{"state,time,a,b,lengthA,lengthB,root"};
   for(int state=0;state<4;state++)for(int sample=0;sample<=12;sample++){
    f.SetArtPreviewImmediate((FlowerArtState)state);f.MotionStep(sample*1.25f);scan.SampleRendered();scan.FindContacts(hits,parallel:true);
    foreach(var h in hits)lines.Add(state+","+(sample*1.25f)+","+scan.Petals[h.petalA].id+","+scan.Petals[h.petalB].id+","+h.lengthA+","+h.lengthB+","+h.rootRegion);
    File.WriteAllLines(Path.Combine(Output,"Contacts.csv"),lines);yield return null;
   }
  }
 }
 static float ChannelDistance(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)));
 static void Shot(string name,int width=960,int height=540){
  var cam=Camera.main;var rt=new RenderTexture(width,height,24);var tex=new Texture2D(width,height,TextureFormat.RGB24,false);var old=RenderTexture.active;var target=cam.targetTexture;
  try{rt.Create();cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),tex.EncodeToPNG());}
  finally{cam.targetTexture=target;RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(tex);}
 }
}
}
#endif
