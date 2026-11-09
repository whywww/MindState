#if UNITY_EDITOR || FLOWER_VALIDATION
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MindState {
// Opt-in, offline regression. Never runs during normal playback or in delivery builds.
public sealed class FlowerShingleValidation : MonoBehaviour {
#if UNITY_EDITOR
 public static void Run() {
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");
  UnityEditor.EditorApplication.EnterPlaymode();
 }
#endif
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() {
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-shingleRegression")>=0)
   Camera.main.gameObject.AddComponent<FlowerShingleValidation>();
 }
 static string Arg(string key,string fallback) { var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,key); return i>=0&&i+1<args.Length?args[i+1]:fallback; }
 static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
 FlowerAssembler flower; FlowerGallery gallery; Camera cam; RenderTexture rt; Texture2D image;
 FlowerSurfaceCollision rimScan; StringBuilder rimCsv;
 string output; readonly List<string> checks=new List<string>();
 void Check(bool ok,string message) { checks.Add((ok?"PASS ":"FAIL ")+message); File.WriteAllLines(Path.Combine(output,"Checks.txt"),checks); }
 void Shot(string name,float az=22,float el=55,float distance=6.2f) {
  gallery.SetView(az,el,distance); cam.Render(); var old=RenderTexture.active; RenderTexture.active=rt;
  image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply(); RenderTexture.active=old;
  File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
  if(rimScan!=null&&!name.Contains("_IDs")&&!name.Contains("_WrapPair")) {
   rimScan.SampleRendered();var petals=rimScan.Petals;
   for(int a=0;a<petals.Length;a++) {
    int next=(petals[a].index+1)%flower.preset.layers[petals[a].layer].count;
    int b=Array.FindIndex(petals,q=>q.layer==petals[a].layer&&q.index==next);
    rimScan.NeighborVisibleLeftEdge(cam,a,b,out int exposed,out int covered);
    rimCsv.AppendFormat(Inv,"{0},{1},{2},{3},{4}\n",name,petals[a].id,petals[b].id,exposed,covered);
   }
  }
 }
 Material DiagnosticMaterial(){var shader=Shader.Find("Unlit/Color");return shader!=null?new Material(shader):new Material(flower.petalPrefab.pearlMaterial);}
 void Snapshot(string name) {
  using(var writer=new BinaryWriter(File.Create(Path.Combine(output,name+".bin"))))
   for(int i=0;i<flower.SharedMeshCount;i++) foreach(var v in flower.SharedMesh(i).vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
 }
 IEnumerator Start() {
  yield return null;
  output=Arg("-shingleOutput","Evidence/ShingleOverlap/Candidate"); Directory.CreateDirectory(output);
  flower=FindObjectOfType<FlowerAssembler>(); gallery=FindObjectOfType<FlowerGallery>();
  gallery.showInterface=false; flower.materialBreathing=false; flower.SetFrozen(true);
  cam=GetComponent<Camera>(); cam.enabled=false; rt=new RenderTexture(960,960,24){antiAliasing=4}; rt.Create(); cam.targetTexture=rt;
  image=new Texture2D(960,960,TextureFormat.RGB24,false); QualitySettings.vSyncCount=0;
  if(Arg("-bottomTrace","0")=="1") {
   var trace=new FlowerSurfaceCollision(flower);var contacts=new List<FlowerSurfaceCollision.Contact>();var csv=new StringBuilder("progress,petalA,petalB,lengthA,lengthB,sA,sB,x,y,z\n");
   foreach(float p in new[]{0f,.1f,.25f,.5f,.75f,1f}){
    trace.Sample(p);trace.FindAllPairContacts(contacts);
    foreach(var h in contacts)if(!h.rootRegion)csv.AppendFormat(Inv,"{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}\n",p,trace.Petals[h.petalA].id,trace.Petals[h.petalB].id,h.lengthA,h.lengthB,h.widthA,h.widthB,h.point.x,h.point.y,h.point.z);
    File.WriteAllText(Path.Combine(output,"AllContacts.csv"),csv.ToString());yield return null;
   }
#if UNITY_EDITOR
   UnityEditor.EditorApplication.Exit(0);
#else
   Application.Quit(0);
#endif
   yield break;
  }
  if(Arg("-bottomPerf","0")=="1"||Arg("-upperPerf","0")=="1"||Arg("-layerPerf","0")=="1") {
   var report=new StringBuilder();
   foreach(bool enabled in new[]{false,true}){
    if(Arg("-layerPerf","0")=="1"){flower.BottomPatchPreview(true);flower.LayerPatchPreview(enabled);}else if(Arg("-upperPerf","0")=="1"){flower.BottomPatchPreview(true);flower.UpperCornerPreview(enabled);}else flower.BottomPatchPreview(enabled);flower.SetBloomProgress(.03f);long maxGc=0;double total=0,max=0;
    for(int i=0;i<120;i++){long gc=GC.GetAllocatedBytesForCurrentThread(),start=System.Diagnostics.Stopwatch.GetTimestamp();flower.SetBloomProgress(i/119f*.25f);double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;maxGc=Math.Max(maxGc,GC.GetAllocatedBytesForCurrentThread()-gc);total+=ms;max=Math.Max(max,ms);}
    report.AppendLine((enabled?"Patched":"Baseline")+" update CPU meanMs="+total/120+" maxMs="+max+" maxGc="+maxGc);
   }
   flower.SetFrozen(true);int uploads=flower.MeshUpdates;for(int i=0;i<20;i++)flower.SetBloomProgress(.25f);
   report.AppendLine("Frozen/repeated-pose uploads="+(flower.MeshUpdates-uploads));report.AppendLine("Geometry bytes="+flower.GeometryBytes+" sparse cache="+flower.BottomPatchCacheBytes+" working="+flower.BottomWorkingBytes);
   File.WriteAllText(Path.Combine(output,"Performance.txt"),report.ToString());
#if UNITY_EDITOR
   UnityEditor.EditorApplication.Exit(0);
#else
   Application.Quit(0);
#endif
   yield break;
  }
  flower.SetBloomProgress(0);flower.UpperCornerPreview(false);gallery.SetView(22,55,6.2f);
  var debug=new StringBuilder("petal,t,s,x,y,z,screenX,screenY\n");var vertices=new Vector3[129*81];
  for(int i=0;i<7;i++){flower.SampleDiagnosticSurface(0,i,0,vertices);var matrix=flower.DiagnosticMatrix(0,i,0);for(int y=60;y<=80;y++)for(int x=0;x<129;x++){var world=matrix.MultiplyPoint3x4(vertices[y*129+x]);var screen=cam.WorldToScreenPoint(world);debug.AppendFormat(Inv,"{0},{1},{2},{3},{4},{5},{6},{7}\n",i,y/80f,x/64f-1,world.x,world.y,world.z,screen.x,960-screen.y);}}
  File.WriteAllText(Path.Combine(output,"UpperGeometry.csv"),debug.ToString());flower.UpperCornerPreview(true);
  flower.diagnosticMaterial=DiagnosticMaterial();flower.diagnosticColors=true;flower.ApplyDiagnosticColors();Shot("Upper_IDs");flower.diagnosticColors=false;flower.ApplyDiagnosticColors();Destroy(flower.diagnosticMaterial);
  foreach(float p in new[]{.25f,.4f,.5f,.6f,.75f,1f}){flower.SetBloomProgress(p);foreach(float el in new[]{90f,55f}){flower.LayerPatchPreview(false);Shot("Layer_Before_"+p+"_"+el,0,el,10);flower.LayerPatchPreview(true);Shot("Layer_After_"+p+"_"+el,0,el,10);}}
  flower.SetBloomProgress(.4f);for(int az=0;az<360;az+=45){flower.LayerPatchPreview(false);Shot("Layer_Orbit_Before_"+az,az,55,10);flower.LayerPatchPreview(true);Shot("Layer_Orbit_After_"+az,az,55,10);}
  for(int l=0;l<2;l++)for(int i=0;i<flower.preset.layers[l].count;i++){flower.SoloPetal(l,i);flower.SetPetalVisible(l,(i+flower.preset.layers[l].count-1)%flower.preset.layers[l].count,true);Shot("Layer_Pair_"+l+"_"+i,0,90,10);flower.ShowAllPetals();}
  foreach(float phase in new[]{0f,.04f,.1f,.2f,.25f}){flower.SetBloomProgress(phase);flower.UpperCornerPreview(false);Shot("Upper_Before_"+(phase*1000).ToString("0000"));flower.UpperCornerPreview(true);Shot("Upper_After_"+(phase*1000).ToString("0000"));}
  flower.SetBloomProgress(0);
  for(int i=0;i<7;i++){
   float az=22+i*360f/7;flower.UpperCornerPreview(false);Shot("Upper_P"+i+"_Before",az,55);flower.UpperCornerPreview(true);Shot("Upper_P"+i+"_After",az,55);
   flower.SoloPetal(0,i);flower.SetPetalVisible(0,(i+6)%7,true);Shot("Upper_P"+i+"_Pair",az,55);flower.ShowAllPetals();
  }
  if(Arg("-upperScan","0")=="1"){
   var upperScan=new FlowerSurfaceCollision(flower);var upperHits=new List<FlowerSurfaceCollision.Contact>();var upperCsv=new StringBuilder("progress,petal,neighbor,beforeContacts,afterContacts\n");
   for(int frame=0;frame<=64;frame++){float phase=frame/64f;var before=new int[7];var after=new int[7];foreach(bool enabled in new[]{false,true}){flower.UpperCornerPreview(enabled);upperScan.Sample(phase,null,0);for(int i=0;i<7;i++){upperScan.FindPairContacts(i,(i+6)%7,upperHits);foreach(var h in upperHits)if(h.lengthA>=.925f&&h.widthA<-.55f){if(enabled)after[i]++;else before[i]++;}}}for(int i=0;i<7;i++)upperCsv.AppendFormat(Inv,"{0},{1},{2},{3},{4}\n",phase,i,(i+6)%7,before[i],after[i]);}
   File.WriteAllText(Path.Combine(output,"UpperContacts.csv"),upperCsv.ToString());flower.UpperCornerPreview(true);
  }
  File.WriteAllText(Path.Combine(output,"BottomPatches.txt"),flower.BottomPatchReport());
  flower.SetBloomProgress(0);flower.BottomPatchPreview(false);Shot("Bottom_Before",22,-25);flower.BottomPatchPreview(true);Shot("Bottom_After",22,-25);
  flower.diagnosticMaterial=DiagnosticMaterial();flower.diagnosticColors=true;flower.ApplyDiagnosticColors();Shot("Bottom_IDs",22,-25);flower.diagnosticColors=false;flower.ApplyDiagnosticColors();Destroy(flower.diagnosticMaterial);
  for(int i=0;i<8;i++)Shot("Bottom_Orbit_"+i,i*45,-15);
  Check(flower.PetalCount==18&&flower.LayerCount==3,"Preserved 7/6/5 layout");
  Check(flower.MaxCachePositionError<=.00255f,"Cache midpoint error "+flower.MaxCachePositionError.ToString("G8",Inv)+" <= .00255");
  File.WriteAllText(Path.Combine(output,"PackedInterpolation.txt"),flower.VerifyPackedInterpolation());
  foreach(var l in flower.preset.layers) Check(l.RollAt(1)==(l.bloomRoll!=null&&l.bloomRoll.Length>=2?l.bloomRoll[l.bloomRoll.Length-1]:l.roll),"Full bloom exactly preserves authored roll: "+l.name);
  flower.SetBloomProgress(1); Snapshot("FullBloom");
  var petals=flower.GetDiagnosticPetals();
  rimScan=new FlowerSurfaceCollision(flower);rimCsv=new StringBuilder("capture,petalA,petalB,exposed,covered\n");
  bool scanOnly=Arg("-shingleScanOnly","0")=="1";
  if(!scanOnly) {
  foreach(float p in new[]{0f,.04f,.1f,.25f,.5f,.75f,1f}) {
   flower.SetBloomProgress(p); string name="Bloom_"+(p*1000).ToString("0000");
   Shot(name); Shot(name+"_Top",0,90); Shot(name+"_Inside",202,35);
   if(p==0||p==.25f||p==.5f) {
    flower.diagnosticMaterial=DiagnosticMaterial(); flower.diagnosticColors=true; flower.ApplyDiagnosticColors(); Shot(name+"_IDs");
    foreach(var q in petals) q.renderer.enabled=q.layer==0&&(q.index==0||q.index==6);
    Shot(name+"_WrapPair",180,35); flower.ShowAllPetals();
    flower.diagnosticColors=false; flower.ApplyDiagnosticColors(); Destroy(flower.diagnosticMaterial);
   }
   Check(flower.RootAnchorError()<1e-5f,"Root attachment at "+p);Check(flower.BottomIsolationCheck(),"Actual per-petal protected vertices unchanged at "+p);
   yield return null;
  }
  for(int i=0;i<8;i++) { flower.SetBloomProgress(.25f); Shot("Orbit_"+i,i*45,40); yield return null; }
  // Check root vertices against the unchanged generator stored in the baseline capture.
  using(var writer=new BinaryWriter(File.Create(Path.Combine(output,"Roots.bin"))))
   for(int k=0;k<=64;k++) {
    flower.SetBloomProgress(k/64f);
    for(int i=0;i<flower.SharedMeshCount;i++) { var v=flower.SharedMesh(i).vertices; int shell=129*81;
     for(int side=0;side<2;side++) for(int j=0;j<8*129;j++) { var p=v[side*shell+j]; writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
    }
   }
  using(var fixedWriter=new BinaryWriter(File.Create(Path.Combine(output,"FixedSurface.bin"))))
  using(var matrixWriter=new BinaryWriter(File.Create(Path.Combine(output,"Transforms.bin"))))
   for(int k=0;k<=64;k++) {
    flower.SetBloomProgress(k/64f);
    foreach(var petal in petals) { var m=petal.body.localToWorldMatrix; for(int j=0;j<16;j++) matrixWriter.Write(m[j]); }
    for(int i=0;i<flower.SharedMeshCount;i++) { var v=flower.SharedMesh(i).vertices; int shell=129*81;
     for(int side=0;side<2;side++) for(int y=0;y<=80;y++) for(int x=0;x<=128;x++)
      if(y<40||(x>=26&&x<=102)) { var p=v[side*shell+y*129+x]; fixedWriter.Write(p.x);fixedWriter.Write(p.y);fixedWriter.Write(p.z); }
    }
   }
  }
  {
   var scan=new FlowerSurfaceCollision(flower); var hits=new List<FlowerSurfaceCollision.Contact>();
   var times=new SortedSet<float>(); for(int k=0;k<=64;k++) times.Add(k/64f);
   foreach(var l in flower.preset.layers) for(int k=0;k<=64;k++) times.Add(Mathf.Clamp01((l.delay+l.duration*k/64f)/flower.BloomDuration));
   if(Arg("-shingleQuick","0")=="1"||Arg("-shingleScreens","0")=="1") times=new SortedSet<float>(new[]{0f,.1f,.25f,.5f,.75f,1f});
   int orderReversed=0,orderCovered=0;
   var order=new StringBuilder("progress,petalA,petalB,covered,reversed\n");
   var csv=new StringBuilder("progress,petalA,petalB,kind,lengthA,lengthB,sA,sB\n");
   var summary=new StringBuilder("progress,same,cross,self,root\n"); int step=0,selfContacts=0;
   foreach(float p in times) {
    scan.Sample(p); scan.FindContacts(hits,parallel:true); int same=0,cross=0,self=0,roots=0;
    foreach(var h in hits) { var a=petals[h.petalA]; var b=petals[h.petalB]; string kind=h.rootRegion?"root":h.petalA==h.petalB?"self":a.layer==b.layer?"same":"cross";
     if(kind=="root") roots++; else if(kind=="self") self++; else if(kind=="same") same++; else cross++;
     csv.AppendFormat(Inv,"{0},{1},{2},{3},{4},{5},{6},{7}\n",p,a.id,b.id,kind,h.lengthA,h.lengthB,h.widthA,h.widthB);
    }
    for(int a=0;a<petals.Length;a++) {
     int next=(petals[a].index+1)%flower.preset.layers[petals[a].layer].count;
     int b=Array.FindIndex(petals,q=>q.layer==petals[a].layer&&q.index==next);
     scan.NeighborRadialOrder(a,b,out int covered,out int reversed);
     var layer=flower.preset.layers[petals[a].layer];
     float local=Mathf.Clamp01((p*flower.BloomDuration-layer.delay)/layer.duration);
     if(local<=.35f) { orderReversed+=reversed; orderCovered+=covered; }
     order.AppendFormat(Inv,"{0},{1},{2},{3},{4}\n",p,petals[a].id,petals[b].id,covered,reversed);
    }
    selfContacts+=self;
    summary.AppendFormat(Inv,"{0},{1},{2},{3},{4}\n",p,same,cross,self,roots);
    if(++step%4==0) { File.WriteAllText(Path.Combine(output,"Progress.txt"),step+"/"+times.Count); yield return null; }
   }
   Check(selfContacts==0,"No self-intersections in sampled cached poses and midpoints");
   File.WriteAllText(Path.Combine(output,"RadialSummary.txt"),orderCovered+" covered rays, "+orderReversed+" reversed; diagnostic only, silhouette takes priority.");
   File.WriteAllText(Path.Combine(output,"RadialOrder.csv"),order.ToString());
   File.WriteAllText(Path.Combine(output,"Contacts.csv"),csv.ToString()); File.WriteAllText(Path.Combine(output,"ContactSummary.csv"),summary.ToString());
  }
  // Exercise the real six-second art blend in fixed 1/20s simulation steps.
  var update=typeof(FlowerAssembler).GetMethod("UpdateArt",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  var clock=typeof(FlowerAssembler).GetField("artTime",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  foreach(var state in Arg("-shingleQuick","0")=="1"||scanOnly?new FlowerArtState[0]:new[]{FlowerArtState.Clarity,FlowerArtState.Wandering,FlowerArtState.Quiet}) {
   var artScan=new FlowerSurfaceCollision(flower); var artHits=new List<FlowerSurfaceCollision.Contact>();
   var artContacts=new StringBuilder("clip,frame,same,cross,self,root\n");
   flower.SetArtPreviewImmediate(state); flower.SetArtState(FlowerArtState.Gathering); flower.SetFrozen(false);
   for(int frame=0;frame<=360;frame++) { update.Invoke(flower,new object[]{.05f}); if(frame%60==0) { Shot("Enter_"+state+"_"+frame); ArtContacts(artScan,artHits,artContacts,"Enter",frame); } CheckFrame(); }
   flower.SetArtPreviewImmediate(FlowerArtState.Gathering);
   for(int frame=0;frame<=100;frame++) { clock.SetValue(flower,frame*.05f); update.Invoke(flower,new object[]{0f}); if(frame%25==0) { Shot("Breath_"+state+"_"+frame); ArtContacts(artScan,artHits,artContacts,"Breath",frame); } CheckFrame(); }
   flower.SetArtState(state);
   for(int frame=0;frame<=360;frame++) { update.Invoke(flower,new object[]{.05f}); if(frame%60==0) { Shot("Exit_"+state+"_"+frame); ArtContacts(artScan,artHits,artContacts,"Exit",frame); } CheckFrame(); }
   File.WriteAllText(Path.Combine(output,"ArtContacts_"+state+".csv"),artContacts.ToString());
   Check(artRootsOk,"Root attachment throughout Gathering entry, breath and exit: "+state); yield return null;
  }
  string baseline=Arg("-shingleBaseline","");
  if(baseline.Length>0&&!scanOnly) foreach(string name in new[]{"FullBloom","Roots","FixedSurface","Transforms"})
   Check(Convert.ToBase64String(File.ReadAllBytes(Path.Combine(output,name+".bin")))==Convert.ToBase64String(File.ReadAllBytes(Path.Combine(baseline,name+".bin"))),name+" byte-identical to baseline");
#if UNITY_EDITOR
  if(!checks.Exists(c=>c.StartsWith("FAIL")))flower.SaveBottomCache();
#endif
  File.WriteAllText(Path.Combine(output,"VisibleRims.csv"),rimCsv.ToString());
  File.WriteAllText(Path.Combine(output,"Complete.txt"),"Complete");
  bool failed=checks.Exists(s=>s.StartsWith("FAIL"));
#if UNITY_EDITOR
  UnityEditor.EditorApplication.Exit(failed?2:0);
#else
  Application.Quit(failed?2:0);
#endif
 }
 void ArtContacts(FlowerSurfaceCollision scan,List<FlowerSurfaceCollision.Contact> hits,StringBuilder csv,string clip,int frame) {
  scan.SampleRendered(); scan.FindContacts(hits,parallel:true); int same=0,cross=0,self=0,root=0;
  foreach(var h in hits) { if(h.rootRegion) root++; else if(h.petalA==h.petalB) self++; else if(scan.Petals[h.petalA].layer==scan.Petals[h.petalB].layer) same++; else cross++; }
  csv.AppendFormat(Inv,"{0},{1},{2},{3},{4},{5}\n",clip,frame,same,cross,self,root);
  Check(self==0,"No self-intersection in rendered art pose "+clip+" "+frame);
 }
 bool artRootsOk=true;
 void CheckFrame() { artRootsOk&=flower.RootAnchorError()<1e-5f; }
 void OnDestroy() { if(rt!=null){rt.Release();Destroy(rt);} if(image!=null)Destroy(image); }
}}
#endif
