#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace MindState {
public sealed class FullBloomLocalPreview:MonoBehaviour {
 public static bool Requested=>Array.IndexOf(Environment.GetCommandLineArgs(),"-fullLocal")>=0;
 public static void Run(){SessionState.SetBool(FlowerOpenClearancePreview.Key,true);SessionState.SetBool(FlowerOpenClearancePreview.BaselineKey,true);UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");EditorApplication.EnterPlaymode();}
 public static void RunGpu(){SessionState.SetBool(FlowerOpenClearancePreview.Key,false);SessionState.SetBool(FlowerOpenClearancePreview.BaselineKey,false);UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");EditorApplication.EnterPlaymode();}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Requested)Camera.main.gameObject.AddComponent<FullBloomLocalPreview>();}
 IEnumerator Start(){
  yield return null;yield return null;
  var f=FindObjectOfType<FlowerAssembler>();f.enabled=false;f.SetBloomProgress(1);
  var gallery=FindObjectOfType<FlowerGallery>();gallery.showInterface=false;gallery.SetView(22,25);gallery.SetView(22,25,gallery.distance*.72f);
  Directory.CreateDirectory("Outputs/FullLocal");
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fullLocalGpu")>=0){
   if(!f.UsesGpuPlayback)throw new Exception("Expected GPU playback");
   Shot("GpuAfter");var check=new FlowerSurfaceCollision(f);check.SampleRendered();var found=new List<FlowerSurfaceCollision.Contact>();check.FindPairContacts(3,10,found);
   if(found.Exists(h=>h.lengthB>=.25f))throw new Exception("GPU target intersection");
   for(int step=0;step<=22;step++){
    f.SetBloomProgress(.78f+step*.01f);check=new FlowerSurfaceCollision(f);check.SampleRendered();check.FindPairContacts(3,10,found);
    if(found.Exists(h=>h.lengthB>=.25f))throw new Exception("GPU target intersection at "+f.bloomProgress);
   }
   f.SetBloomProgress(.78f);Shot("Gpu78");
   File.WriteAllText("Outputs/FullLocal/GpuResult.txt","PASS: fresh GPU playback; target free sheet clear at each 1% from 78% to 100%.");
   f.enabled=true;gallery.showInterface=true;if(Application.isBatchMode)EditorApplication.Exit(0);yield break;
  }
  f.RestoreFullLocalBaseline(File.ReadAllBytes("Outputs/FullLocalBaseline.bytes"));f.SetBloomProgress(.99f);f.SetBloomProgress(1);
  Shot("Before");
  var scan=new FlowerSurfaceCollision(f);scan.SampleRendered();var hits=new List<FlowerSurfaceCollision.Contact>();scan.FindContacts(hits,parallel:true);
  var lines=new List<string>();
  foreach(var h in hits)if(!h.rootRegion&&scan.Petals[h.petalA].layer==0&&scan.Petals[h.petalB].layer==1){
   lines.Add(h.petalA+","+h.petalB+","+h.lengthA+","+h.widthA+","+h.lengthB+","+h.widthB);
   f.SoloPetal(0,scan.Petals[h.petalA].index);f.SetPetalVisible(1,scan.Petals[h.petalB].index,true);Shot("Pair"+h.petalA+"_"+h.petalB);
  }
  var originalPairs=new HashSet<string>();foreach(var h in hits)if(!h.rootRegion&&(h.petalA==10||h.petalB==10))originalPairs.Add(h.petalA+":"+h.petalB);
  f.ShowAllPetals();File.WriteAllLines("Outputs/FullLocal/Contacts.csv",lines);
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-diagnoseFullLocal")>=0){EditorApplication.Exit(0);yield break;}
  var rootHits=new List<FlowerSurfaceCollision.Contact>();scan.FindPairContacts(3,10,rootHits);File.WriteAllLines("Outputs/FullLocal/OriginalTarget.csv",rootHits.ConvertAll(h=>h.lengthA+","+h.lengthB+","+h.widthB));
  var nearby=new Dictionary<float,HashSet<string>>();
  foreach(float progress in new[]{.78f,.89f,1f}){
   f.SetBloomProgress(progress);scan=new FlowerSurfaceCollision(f);scan.SampleRendered();scan.FindContacts(hits,parallel:true);
   var pairs=new HashSet<string>();foreach(var h in hits)if((h.petalA==10||h.petalB==10)&&h.lengthA>=.25f&&h.lengthB>=.25f)pairs.Add(h.petalA+":"+h.petalB);
   nearby.Add(progress,pairs);
  }
  f.SetBloomProgress(1);
  var roots=f.PetalMesh(1,3).vertices;
  f.BuildFullLocalPatch();f.SetBloomProgress(.99f);f.SetBloomProgress(1);Shot("After");
  var fixedVertices=f.PetalMesh(1,3).vertices;for(int id=0;id<9*129;id++)if((roots[id]-fixedVertices[id]).sqrMagnitude>1e-12f)throw new Exception("Fixed root changed");
  scan=new FlowerSurfaceCollision(f);scan.SampleRendered();scan.FindContacts(hits,parallel:true);
  File.WriteAllLines("Outputs/FullLocal/AfterContacts.csv",hits.ConvertAll(h=>h.petalA+","+h.petalB+","+h.lengthA+","+h.lengthB+","+h.rootRegion));
  if(hits.Exists(h=>!h.rootRegion&&h.petalA==3&&h.petalB==10&&h.lengthB>=.25f))throw new Exception("Target L0-P3 / L1-P3 still intersects");
  if(hits.Exists(h=>!h.rootRegion&&(h.petalA==10||h.petalB==10)&&!originalPairs.Contains(h.petalA+":"+h.petalB)))throw new Exception("New contact involving modified petal");
  foreach(var entry in nearby){
   f.SetBloomProgress(entry.Key);scan=new FlowerSurfaceCollision(f);scan.SampleRendered();scan.FindContacts(hits,parallel:true);
   if(hits.Exists(h=>(h.petalA==10||h.petalB==10)&&h.lengthA>=.25f&&h.lengthB>=.25f&&!entry.Value.Contains(h.petalA+":"+h.petalB)))throw new Exception("New neighbour contact at "+entry.Key);
  }
  for(int i=0;i<=120;i++){f.SetBloomProgress(.78f+.22f*Mathf.Sin(i*Mathf.PI/120));if(f.RootAnchorError()>1e-5f)throw new Exception("Root detached");if(i%20==0)yield return null;}
  foreach(float progress in new[]{.78f,.8f,.85f,.9f,.95f,1f}){f.SetBloomProgress(progress);Shot("Fixed"+Mathf.RoundToInt(progress*100));}
  f.SetBloomProgress(.78f);gallery.showInterface=true;f.enabled=true;
  File.WriteAllText("Outputs/FullLocal/Result.txt","PASS: target clear at every 1% from 78% to 100%; neighbour checks at 78/89/100; 78-100-78 root sweep passed.");
  if(Application.isBatchMode)EditorApplication.Exit(0);
 }
 static void Shot(string name){
  var c=Camera.main;var r=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf){antiAliasing=4};var t=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=c.targetTexture;
  try{r.Create();c.targetTexture=r;c.Render();using(var read=new FlowerCaptureReadback(1280,720))read.Read(r,t);File.WriteAllBytes("Outputs/FullLocal/"+name+".png",t.EncodeToPNG());}
  finally{c.targetTexture=old;r.Release();Destroy(r);Destroy(t);}
 }
}

}
#endif
