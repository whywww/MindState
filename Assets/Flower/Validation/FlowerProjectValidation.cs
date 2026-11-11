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
  for(int from=0;from<4;from++)for(int to=0;to<4;to++)if(from!=to){
   f.SetArtPreviewImmediate((FlowerArtState)from);f.SetFrozen(false);f.SetArtState((FlowerArtState)to);
   var weights=f.ArtWeights;f.MotionStep(0);Check(f.ArtWeights==weights,"Retarget continuous "+from+" to "+to);
   for(int frame=0;frame<72;frame++){f.MotionStep(.25f);if(frame%18==0)Check(f.RootAnchorError()<1e-5f,"Transition roots "+from+" to "+to+" frame="+frame);}
   Check(Mathf.Abs(f.ArtWeights[to]-1)<1e-5f,"18-second transition endpoint "+from+" to "+to);yield return null;
  }
  f.SetFrozen(true);float time=f.ArtTime;uploads=f.MeshUpdates;var frozenWeights=f.ArtWeights;f.MotionStep(4);
  Check(f.ArtTime==time&&f.MeshUpdates==uploads&&f.ArtWeights==frozenWeights,"Freeze preserves time, weights and mesh uploads");f.SetFrozen(false);
  f.ResetArt();f.MotionStep(0);Check(f.ArtWeights.x==1,"Reset to Clarity");
  f.SetBrainMetrics(float.NaN,1);f.MotionStep(0);Check(f.ArtWeights.x==1,"Invalid sample does not change weights");
  f.SimulateArtBrain();for(int i=0;i<100;i++)f.MotionStep(.05f);Check(Mathf.Abs(f.ArtWeights.x+f.ArtWeights.y+f.ArtWeights.z+f.ArtWeights.w-1)<1e-5f,"Simulation weights normalized");
  f.SetBrainMetrics(.4f,.6f);yield return new WaitForSecondsRealtime(5.2f);f.MotionStep(0);Check(f.ArtStatus=="正在重新感知","External timeout");f.SetBrainMetrics(.4f,.6f);Check(f.ArtStatus=="外部脑电","External recovery");
  f.SetBloomProgress(.5f);Check(!f.ArtActive,"Manual preview exits art");f.PlayBloom();Check(f.autoPlay&&f.bloomProgress==0&&!f.frozen,"Replay resets opening");
  for(int state=0;state<4;state++){f.SetArtPreviewImmediate((FlowerArtState)state);Shot("State"+state);}
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-scanContacts")>=0){
   var scan=new FlowerSurfaceCollision(f);var hits=new List<FlowerSurfaceCollision.Contact>();var lines=new List<string>{"state,time,a,b,lengthA,lengthB,root"};
   for(int state=0;state<4;state++)for(int sample=0;sample<=12;sample++){
    f.SetArtPreviewImmediate((FlowerArtState)state);f.MotionStep(sample*1.25f);scan.SampleRendered();scan.FindContacts(hits,parallel:true);
    foreach(var h in hits)lines.Add(state+","+(sample*1.25f)+","+scan.Petals[h.petalA].id+","+scan.Petals[h.petalB].id+","+h.lengthA+","+h.lengthB+","+h.rootRegion);
    File.WriteAllLines(Path.Combine(Output,"Contacts.csv"),lines);yield return null;
   }
  }
 }
 static void Shot(string name){
  var cam=Camera.main;var rt=new RenderTexture(960,540,24);var tex=new Texture2D(960,540,TextureFormat.RGB24,false);var old=RenderTexture.active;var target=cam.targetTexture;
  try{rt.Create();cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),tex.EncodeToPNG());}
  finally{cam.targetTexture=target;RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(tex);}
 }
}
}
#endif
