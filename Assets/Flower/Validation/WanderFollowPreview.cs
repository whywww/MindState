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
// Feasibility must pass before any production motion or cache is changed.
public sealed class WanderFollowPreview:MonoBehaviour {
 const string FollowKey="MindState.WanderFollowPreview";
 internal static bool Requested=>SessionState.GetBool(FollowKey,false)||Array.IndexOf(Environment.GetCommandLineArgs(),"-wanderFollow")>=0;
 string status="CPU feasibility check; no cache writes";
 static bool BaselineRequested=>Array.IndexOf(Environment.GetCommandLineArgs(),"-wanderFollowBaseline")>=0;
 [MenuItem("MindState/Preview Wandering Follow (CPU, no cache writes)")]
 public static void Run(){Directory.CreateDirectory("Outputs/WanderFollow");SessionState.SetBool(FollowKey,true);SessionState.SetBool(FlowerOpenClearancePreview.Key,true);SessionState.SetBool(FlowerOpenClearancePreview.BaselineKey,true);UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");Application.logMessageReceived+=Fail;EditorApplication.EnterPlaymode();}
 public static void ShowBaseline(){SessionState.SetBool(FollowKey,false);SessionState.SetBool(FlowerOpenClearancePreview.Key,false);SessionState.SetBool(FlowerOpenClearancePreview.BaselineKey,false);UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void ResetFlag(){EditorApplication.playModeStateChanged+=c=>{if(c==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(FollowKey,false);};}
 static void Fail(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error){File.WriteAllText("Outputs/WanderFollow/Failure.txt",message+"\n"+stack);if(Application.isBatchMode)EditorApplication.Exit(2);}}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Requested||BaselineRequested){Application.logMessageReceived+=Fail;Camera.main.gameObject.AddComponent<WanderFollowPreview>();}}
 void OnDestroy(){Application.logMessageReceived-=Fail;}
 static string Hash(byte[] data){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(data));}
 static string VertexHash(Mesh mesh){var vertices=mesh.vertices;var data=new byte[vertices.Length*12];var pin=GCHandle.Alloc(vertices,GCHandleType.Pinned);try{Marshal.Copy(pin.AddrOfPinnedObject(),data,0,data.Length);}finally{pin.Free();}return Hash(data);}
 static string[] StandardHashes(FlowerAssembler f){var result=new List<string>();for(int i=0;i<=22;i++){f.SetBloomProgress(.78f+i*.01f);foreach(var p in f.GetDiagnosticPetals())result.Add(VertexHash(f.PetalMesh(p.layer,p.index)));}return result.ToArray();}
 IEnumerator Start(){
  yield return null;yield return null;
  if(BaselineRequested){var baseline=FindObjectOfType<FlowerAssembler>();baseline.SetArtPreviewImmediate(FlowerArtState.Wandering);FindObjectOfType<FlowerGallery>().showInterface=true;status="UNFIXED WANDERING BASELINE - candidate rejected; accepted caches unchanged";Debug.Log("WANDER_BASELINE_READY GPU="+baseline.UsesGpuPlayback);yield break;}
  var f=FindObjectOfType<FlowerAssembler>();f.enabled=false;
  Directory.CreateDirectory("Outputs/WanderFollow");
  var g=FindObjectOfType<FlowerGallery>();g.showInterface=false;g.SetView(22,25);g.SetView(22,25,g.distance*.45f);
  string[] cachePaths={"Assets/Flower/Resources/BottomOverlap.bytes","Assets/Flower/Resources/BottomOverlapProvenance.txt","Assets/StreamingAssets/FlowerPlayback.bin","Assets/Flower/Resources/FlowerPlaybackProvenance.txt"};
  var cacheBefore=Array.ConvertAll(cachePaths,p=>Hash(File.ReadAllBytes(p)));var standardBefore=StandardHashes(f);
  bool passed=f.ProbeWanderFollow();
  var standardAfter=StandardHashes(f);for(int i=0;i<standardBefore.Length;i++)if(standardBefore[i]!=standardAfter[i])throw new Exception("Accepted standard vertices changed at sample "+i);
  for(int i=0;i<cachePaths.Length;i++)if(cacheBefore[i]!=Hash(File.ReadAllBytes(cachePaths[i])))throw new Exception("Cache changed: "+cachePaths[i]);
  File.WriteAllLines("Outputs/WanderFollow/CacheHashes.txt",Array.ConvertAll(cachePaths,p=>p+" "+Hash(File.ReadAllBytes(p))));
  status=passed?"Feasibility only: further interpolation/GPU validation required":"FAIL: empty safe domain. Showing accepted baseline, NOT a repaired preview.";
  File.WriteAllText("Outputs/WanderFollow/Result.txt",status+"\nStandard 78%-100%: all 18 petals, 23 samples, exact position byte hashes unchanged.\nAll four cache/signature hashes unchanged. No production motion changed; no new cache published.\nDomain.csv records candidate results; 0 neighbour hits is required, displacement must be <=0.10.\n");
  f.SetArtPreviewImmediate(FlowerArtState.Wandering);f.enabled=true;g.showInterface=true;
  if(Application.isBatchMode)EditorApplication.Exit(passed?0:2);
 }
 void OnGUI(){GUI.Label(new Rect(12,45,1100,30),status);}
 internal static void Shot(string name){var c=Camera.main;var r=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf){antiAliasing=4};var t=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=c.targetTexture;try{r.Create();c.targetTexture=r;c.Render();using(var read=new FlowerCaptureReadback(1280,720))read.Read(r,t);File.WriteAllBytes("Outputs/WanderFollow/"+name+".png",t.EncodeToPNG());}finally{c.targetTexture=old;r.Release();Destroy(r);Destroy(t);}}
}
public sealed partial class FlowerAssembler {
 internal bool ProbeWanderFollow(){
  var scan=new FlowerSurfaceCollision(this);var petals=scan.Petals;var contacts=new List<FlowerSurfaceCollision.Contact>();
  var rows=new List<string>{"time,a,b,lengthA,widthA,lengthB,widthB"};
  foreach(float time in new[]{0f,5f,10f,15f}){
   SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(time);scan.SampleRendered();scan.FindContacts(contacts,parallel:true);
   foreach(var h in contacts)if(h.lengthA>=.25f&&h.lengthB>=.25f)rows.Add(time+","+petals[h.petalA].id+","+petals[h.petalB].id+","+h.lengthA+","+h.widthA+","+h.lengthB+","+h.widthB);
   if(time==5){WanderFollowPreview.Shot("Before");var markers=OpenSeamMarkers();
    foreach(var p in petals){var text=new GameObject(p.id);text.transform.SetParent(markers.transform);var v=PetalMesh(p.layer,p.index).vertices;text.transform.position=p.body.TransformPoint(v[48*129+64]);text.transform.rotation=Camera.main.transform.rotation;var label=text.AddComponent<TextMesh>();label.text=p.id;label.characterSize=.035f;label.fontSize=48;label.anchor=TextAnchor.MiddleCenter;label.color=Color.yellow;}
    WanderFollowPreview.Shot("Markers");DestroyImmediate(markers);
    foreach(var pair in new[]{new[]{3,10},new[]{9,10},new[]{10,16}}){SoloPetal(petals[pair[0]].layer,petals[pair[0]].index);SetPetalVisible(petals[pair[1]].layer,petals[pair[1]].index,true);WanderFollowPreview.Shot("Pair"+pair[0]+"_"+pair[1]);}ShowAllPetals();
   }
  }
  File.WriteAllLines("Outputs/WanderFollow/Contacts.csv",rows);
  SetBloomProgress(1);var local=new Vector3[petals.Length][];var world=new Vector3[petals.Length][];
  for(int p=0;p<petals.Length;p++){local[p]=new Vector3[BottomSurface];world[p]=new Vector3[BottomSurface];SampleDiagnosticSurface(petals[p].layer,petals[p].index,1,local[p]);PetalArtMotion(new Vector4(0,0,1,0),5,petals[p].index*Mathf.PI*2/layers[petals[p].layer].petals.Length,petals[p].layer,petals[p].index,out float tilt,out float roll);var m=OpenMatrix(petals[p].layer,petals[p].index,1,tilt,roll);for(int k=0;k<BottomSurface;k++)world[p][k]=m.MultiplyPoint3x4(local[p][k]);}
  var table=new List<string>{"lowerTilt,upperTilt,rollScale,lift,targetHits,neighbourHits,outerP2Hits,middleP2Hits,innerP3Hits"};
  var rawRoll=artLooks[2].asym*Mathf.Sin(4*1.7f+.9f);
  bool feasibleDomain=true;float amplitude=Mathf.Abs(artProfiles[2].amplitude);
  var failures=new List<string>();
  for(int lowerStep=-2;lowerStep<=2;lowerStep++){
   float lower=lowerStep*amplitude*.5f;
   var low=OpenMatrix(0,3,1,lower,artLooks[2].asym*Mathf.Sin(4*1.7f));for(int k=0;k<BottomSurface;k++)world[3][k]=low.MultiplyPoint3x4(local[3][k]);
   int feasible=0;
   for(int step=-16;step<=16;step++)foreach(float rollScale in new[]{1f,.75f,.5f,.25f,0f}){
    float angle=step*amplitude/16;
    var m=OpenMatrix(1,3,1,angle,rawRoll*rollScale);var up=m.inverse.MultiplyVector(Vector3.up);
    for(int k=0;k<BottomSurface;k++)world[10][k]=m.MultiplyPoint3x4(local[10][k]);
    var mask=new bool[BottomSurface];var required=new float[BottomSurface];var height=new float[BottomSurface];
    var tree=new BottomTree(this,0,3,1,low,true,true);
    for(int iteration=0;iteration<3;iteration++){
     scan.SamplePositions(world);scan.FindPairContacts(3,10,contacts);
     foreach(var h in contacts)if(h.lengthB>=.25f){int sx=Mathf.RoundToInt((h.widthB+1)*64),sy=Mathf.RoundToInt(h.lengthB*80);for(int y=Mathf.Max(20,sy-8);y<=Mathf.Min(80,sy+8);y++)for(int x=Mathf.Max(0,sx-12);x<=Mathf.Min(128,sx+12);x++)mask[y*129+x]=true;}
     for(int k=0;k<BottomSurface;k++)if(mask[k]){float d=tree.OutsideDistance(m.MultiplyPoint3x4(local[10][k]),Vector3.up);if(d>-.008f&&d<.18f)required[k]=Mathf.Max(0,d+.008f);}
     height=(float[])required.Clone();
     for(int pass=0;pass<60;pass++){var next=(float[])height.Clone();for(int y=9;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x;float sum=height[id]*2+height[id-129];int n=3;if(y<80){sum+=height[id+129];n++;}if(x>0){sum+=height[id-1];n++;}if(x<128){sum+=height[id+1];n++;}next[id]=Mathf.Max(required[id],sum/n)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,20,y));}height=next;}
     for(int k=0;k<BottomSurface;k++)world[10][k]=m.MultiplyPoint3x4(local[10][k]+up*height[k]);
    }
    float lift=0;for(int k=0;k<BottomSurface;k++)lift=Mathf.Max(lift,height[k]*up.magnitude);
    scan.SamplePositions(world);scan.FindPairContacts(3,10,contacts);int bad=contacts.FindAll(h=>h.lengthB>=.25f).Count;int neighbours=0;
    var neighbourCounts=new int[3];int ni=0;
    foreach(int other in new[]{2,9,16}){int a=Mathf.Min(other,10),b=Mathf.Max(other,10);scan.FindPairContacts(a,b,contacts);int n=contacts.FindAll(h=>h.lengthA>=.25f&&h.lengthB>=.25f).Count;neighbourCounts[ni++]=n;neighbours+=n;}
    if(bad==0&&neighbours==0&&lift<=.1f)feasible++;
    table.Add(lower+","+angle+","+rollScale+","+lift+","+bad+","+neighbours+","+string.Join(",",neighbourCounts));
   }
   if(feasible==0){feasibleDomain=false;failures.Add("No accepted candidate at lower tilt "+lower+" degrees; upper tilt +/-"+amplitude+", roll scale 0..1, local lift <=0.10. See Domain.csv. This is failure of this candidate generator, not a proof that every local surface is impossible.");}
   Debug.Log("WANDER_FOLLOW_DOMAIN lower="+lower+" feasible="+feasible);File.WriteAllLines("Outputs/WanderFollow/Domain.csv",table);
   // Stop before production changes when an endpoint has no feasible candidate.
   if(!feasibleDomain)break;
  }
  File.WriteAllLines("Outputs/WanderFollow/DomainFailures.txt",failures);return feasibleDomain;
 }
}
}
#endif
