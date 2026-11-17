#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace MindState {
public sealed class FlowerOpenClearancePreview : MonoBehaviour {
 public const string Key="MindState.OpenClearancePreview";
 public const string BaselineKey="MindState.OpenClearanceBaseline";
 string previewLabel;
 public static bool Requested=>SessionState.GetBool(Key,false)||Array.IndexOf(Environment.GetCommandLineArgs(),"-previewOpenClearance")>=0;
 [MenuItem("MindState/Preview Open Clearance (CPU, no cache writes)")]
 public static void Run() {
  SessionState.SetBool(Key,true);
  SessionState.SetBool(BaselineKey,false);
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");
  EditorApplication.EnterPlaymode();
 }
 [MenuItem("MindState/Preview Existing Geometry (CPU, no cache writes)")]
 public static void RunBaseline() {
  SessionState.SetBool(Key,true);SessionState.SetBool(BaselineKey,true);
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");
  EditorApplication.EnterPlaymode();
 }
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Boot(){if(Requested&&!FullBloomLocalPreview.Requested)Camera.main.gameObject.AddComponent<FlowerOpenClearancePreview>();}
 IEnumerator Start() {
  yield return null;
  var flower=FindObjectOfType<FlowerAssembler>();
  if(flower==null||!flower.Ready||flower.UsesGpuPlayback)throw new Exception("CPU clearance preview failed to initialize.");
  if(SessionState.GetBool(BaselineKey,false)) {
   previewLabel="CPU baseline preview (existing geometry). The intersection fix is NOT approved.";
   flower.SetArtPreviewImmediate(FlowerArtState.Wandering);
   FindObjectOfType<FlowerGallery>().showInterface=true;
   Debug.Log("CPU_BASELINE_PREVIEW_READY no cache writes");
   yield break;
  }
  flower.enabled=false;
  FindObjectOfType<FlowerGallery>().showInterface=false;
  Directory.CreateDirectory("Outputs/OpenClearance");
  File.WriteAllText("Outputs/OpenClearance/Result.txt","RUNNING; no approval to bake.");
  var gallery=FindObjectOfType<FlowerGallery>();gallery.SetView(22,25);gallery.SetView(22,25,gallery.distance*.72f);
  flower.SetArtPreviewImmediate(FlowerArtState.Wandering);flower.MotionStep(0);
  Shot("Before");
  var markers=flower.OpenSeamMarkers();Shot("SeamMarkers");Destroy(markers);yield return null;
  var dynamicBaseline=new System.Collections.Generic.HashSet<string>();
  foreach(float sample in new[]{0f,5f,10f,15f,FlowerAssembler.OpenCrossProbeTime}) {
   flower.SetArtPreviewImmediate(FlowerArtState.Wandering);flower.MotionStep(sample);
   dynamicBaseline.UnionWith(CrossContacts(flower));
  }
  flower.SetArtPreviewImmediate(FlowerArtState.Wandering);flower.MotionStep(0);
  flower.BuildOpenClearancePatches();
  if(!flower.OpenOrderPassed) {
   File.WriteAllText("Outputs/OpenClearance/Result.txt","Geometry FAIL: "+flower.OpenOrderResult+"\nNot ready for manual approval or baking. Earlier After/MinimumGap images are prior candidates.");
   flower.enabled=true;FindObjectOfType<FlowerGallery>().showInterface=true;
   Shot("FailedBaseline");
   Debug.LogWarning(flower.OpenOrderResult);
   if(Application.isBatchMode)EditorApplication.Exit(2);
   yield break;
  }
  flower.SetArtPreviewImmediate(FlowerArtState.Wandering);flower.MotionStep(0);
  Shot("After");
  var order=new System.Collections.Generic.List<string>{"time,progress,minDirectedGap,reversedSamples"};
  float worst=float.PositiveInfinity,worstTime=0;int reversed=0;
  for(int frame=0;frame<900;frame++) {
   if(frame%60==0) {
    int bad=flower.InspectOpenOrder(out float gap);reversed+=bad;
    order.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3}",frame/60f,flower.bloomProgress,gap,bad));
    if(gap<worst){worst=gap;worstTime=frame/60f;}
   }
   flower.MotionStep(1f/60);
   if(flower.RootAnchorError()>1e-5f)throw new Exception("Preview root detached.");
   if(frame==299||frame==599||frame==899)Shot("Wandering"+((frame+1)/60));
   if(frame%30==0)yield return null;
  }
  flower.SetArtPreviewImmediate(FlowerArtState.Wandering);flower.MotionStep(worstTime);Shot("MinimumGap");
  var scanner=new FlowerSurfaceCollision(flower);scanner.SampleRendered();
  var hits=new System.Collections.Generic.List<FlowerSurfaceCollision.Contact>();scanner.FindContacts(hits,parallel:true);
  var dynamicAdded=CrossContacts(flower);dynamicAdded.ExceptWith(dynamicBaseline);
  int self=hits.FindAll(h=>h.petalA==h.petalB&&!h.rootRegion).Count;
  int sameLayer=hits.FindAll(h=>h.petalA!=h.petalB&&!h.rootRegion&&h.lengthA>=.2f&&h.lengthB>=.2f&&scanner.Petals[h.petalA].layer==scanner.Petals[h.petalB].layer).Count;
  File.WriteAllLines("Outputs/OpenClearance/Contacts.csv",hits.ConvertAll(h=>h.petalA+","+h.petalB+","+h.lengthA+","+h.lengthB+","+h.rootRegion));
  flower.SetBloomProgress(1);Shot("StandardOpen");
  flower.SetArtPreviewImmediate(FlowerArtState.Clarity);flower.MotionStep(0);Shot("Clarity");
  int transitionReversed=0;
  foreach(float phase in new[]{.78f,.8125f,.84375f,.875f,.890625f,.90625f,.921875f,.9375f,.953125f,.96875f,.984375f,1f}) {
   flower.SetArtPreviewImmediate(FlowerArtState.Wandering);flower.SetBloomProgress(phase);
   int bad=flower.InspectOpenOrder(out float gap);transitionReversed+=bad;
   order.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"standard,{0},{1},{2}",phase,gap,bad));
  }
  flower.SetArtPreviewImmediate(FlowerArtState.Clarity);
  flower.SetArtState(FlowerArtState.Gathering);
  for(int frame=0;frame<420;frame++){flower.MotionStep(1f/60);if(flower.RootAnchorError()>1e-5f)throw new Exception("Transition root detached.");if(frame%30==0)yield return null;}
  Shot("Gathering");
  File.WriteAllLines("Outputs/OpenClearance/DirectedOrder.csv",order);
  bool passed=flower.OpenOrderPassed&&reversed==0&&transitionReversed==0&&self==0&&sameLayer==0&&dynamicAdded.Count==0;
  File.WriteAllText("Outputs/OpenClearance/Result.txt","Runtime PASS: initialized; 15 simulated seconds Wandering; standard open, Clarity/Gathering; roots attached.\nOrder "+(passed?"PASS":"FAIL")+": reversedSamples="+reversed+" transitionReversed="+transitionReversed+" selfPairs="+self+" sameLayerPairs="+sameLayer+" dynamicCrossAdded="+dynamicAdded.Count+" minGap="+worst+"\n"+flower.OpenOrderResult+"\nNo cache writes; manual visual approval required.");
  Debug.Log("OPEN_CLEARANCE_RUNTIME_PASS ORDER_"+(passed?"PASS":"FAIL"));
  flower.SetArtPreviewImmediate(FlowerArtState.Wandering);flower.enabled=true;
  if(Application.isBatchMode)EditorApplication.Exit(passed?0:2);
 }
 void OnGUI() {
  if(previewLabel!=null) {GUI.color=Color.yellow;GUI.Label(new Rect(12,12,Screen.width-24,40),previewLabel);GUI.color=Color.white;return;}
  if(!File.Exists("Outputs/OpenClearance/Result.txt"))return;
  GUI.color=Color.yellow;
  GUI.Label(new Rect(12,12,Screen.width-24,100),File.ReadAllText("Outputs/OpenClearance/Result.txt"));
  GUI.color=Color.white;
 }
 static System.Collections.Generic.HashSet<string> CrossContacts(FlowerAssembler flower) {
  var scan=new FlowerSurfaceCollision(flower);scan.SampleRendered();
  var hits=new System.Collections.Generic.List<FlowerSurfaceCollision.Contact>();scan.FindContacts(hits,parallel:true);
  var result=new System.Collections.Generic.HashSet<string>();
  foreach(var h in hits)if(!h.rootRegion&&h.lengthA>=.2f&&h.lengthB>=.2f&&scan.Petals[h.petalA].layer!=scan.Petals[h.petalB].layer)
   result.Add(h.petalA+":"+h.petalB);
  return result;
 }
 static void Shot(string name) {
  var camera=Camera.main;var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf){antiAliasing=4};
  var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
  var previous=camera.targetTexture;
  try{target.Create();camera.targetTexture=target;camera.Render();using(var readback=new FlowerCaptureReadback(1280,720)) {readback.Read(target,image);}File.WriteAllBytes("Outputs/OpenClearance/"+name+".png",image.EncodeToPNG());}
  finally{camera.targetTexture=previous;target.Release();Destroy(target);Destroy(image);}
 }
}
}
#endif


