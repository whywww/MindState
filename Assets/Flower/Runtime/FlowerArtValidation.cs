#if UNITY_EDITOR || FLOWER_VALIDATION
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 public bool VerifyGatheringPath(float clock) {
 SetArtPreviewImmediate(FlowerArtState.Gathering); artTime=clock; UpdateArt(0);
 float progress=bloomProgress;
 var surfaces=new Vector3[SharedMeshCount][];
 for(int i=0;i<surfaces.Length;i++) surfaces[i]=SharedMesh(i).vertices;
 var petals=GetDiagnosticPetals(); var matrices=new Matrix4x4[petals.Length];
 for(int i=0;i<petals.Length;i++) matrices[i]=petals[i].body.localToWorldMatrix;
 SetBloomProgress(progress);
 bool equal=generated.localScale==Vector3.one;
 for(int i=0;i<surfaces.Length;i++) { var vertices=SharedMesh(i).vertices; for(int j=0;j<vertices.Length;j++) equal&=(vertices[j]-surfaces[i][j]).sqrMagnitude<1e-12f; }
 for(int i=0;i<petals.Length;i++) { var matrix=petals[i].body.localToWorldMatrix; for(int j=0;j<16;j++) equal&=Mathf.Abs(matrix[j]-matrices[i][j])<1e-6f; }
 return equal;
 }
}

public sealed class FlowerArtValidation:MonoBehaviour {
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() { if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyArt")>=0) Camera.main.gameObject.AddComponent<FlowerArtValidation>(); }
 void Require(bool ok,string message) { File.AppendAllText(path+"/Checks.txt",(ok?"PASS ":"FAIL ")+message+"\n"); if(!ok) throw new Exception(message); }
 string path; Camera cam; RenderTexture rt; Texture2D tex;
 void Shot(string file) { cam.Render(); var old=RenderTexture.active; RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); tex.Apply(); RenderTexture.active=old; File.WriteAllBytes(file,tex.EncodeToJPG(90)); }
 IEnumerator Start() {
 path=Path.Combine(Application.dataPath,"../../../Evidence/FlowerArt"); Directory.CreateDirectory(path); File.WriteAllText(path+"/Complete.txt","Running"); File.WriteAllText(path+"/Checks.txt","");
 yield return null; var f=FindObjectOfType<FlowerAssembler>(); var g=FindObjectOfType<FlowerGallery>(); g.showInterface=false;
 cam=GetComponent<Camera>(); cam.enabled=false; rt=new RenderTexture(960,540,24){antiAliasing=4}; rt.Create(); cam.targetTexture=rt; tex=new Texture2D(960,540,TextureFormat.RGB24,false);
 g.SetView(22,55,9); QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
 for(int i=0;i<4;i++) { var w=FlowerAssembler.BrainArtWeights(i<2?1:0,i%2==0?1:0); Require(w[i]==1,"Brain corner "+i); }
 Require(Mathf.Abs(FlowerAssembler.BrainArtWeights(.5f,.5f).sqrMagnitude-.25f)<1e-6f,"Balanced blend");
 f.SetArtPreviewImmediate(FlowerArtState.Clarity); yield return null;
 int updates=f.MeshUpdates; f.SetFrozen(true); float clock=f.ArtTime; yield return null; Require(f.ArtTime==clock,"Frozen clock");
 f.SetBrainMetrics(float.NaN,1); Require(f.ArtWeights.x==1,"Invalid data does not map to low attention");
 f.SetFrozen(false); f.SetBloomProgress(.5f); Require(!f.ArtActive,"Legacy API exits art"); f.PlayBloom(); Require(f.autoPlay,"Legacy replay");
 for(int sample=0;sample<3;sample++) Require(f.VerifyGatheringPath(sample*1.25f),"Original cached path equality at bud progress "+(sample*.02f));
 Time.captureFramerate=20;
 for(int i=0;i<4;i++) {
 f.SetArtPreviewImmediate((FlowerArtState)i); string folder=path+"/"+((FlowerArtState)i); Directory.CreateDirectory(folder);
 yield return null; Shot(path+"/"+((FlowerArtState)i)+".jpg"); int before=f.MeshUpdates;
 for(int frame=0;frame<(Array.IndexOf(Environment.GetCommandLineArgs(),"-artScreensOnly")>=0?0:400);frame++) { Shot(folder+"/"+frame.ToString("0000")+".jpg"); yield return null; }
 Require(i==1 ? (Array.IndexOf(Environment.GetCommandLineArgs(),"-artScreensOnly")>=0||f.MeshUpdates>before) : before==f.MeshUpdates,i==1?"Gathering breath uses cached mesh uploads":"No art mesh uploads "+i); Require(f.RootAnchorError()<.0001f,"Attached roots "+i);
 }
 if(Array.IndexOf(Environment.GetCommandLineArgs(),"-artScreensOnly")>=0) { Application.Quit(); yield break; }
 // Record entry and all three exits with the actual four-second weight smoothing.
 for(int clip=0;clip<4;clip++) {
 if(clip==0) { f.SetArtPreviewImmediate(FlowerArtState.Clarity); f.SetArtState(FlowerArtState.Gathering); }
 else { f.SetArtPreviewImmediate(FlowerArtState.Gathering); f.SetArtState(clip==1?FlowerArtState.Clarity:clip==2?FlowerArtState.Wandering:FlowerArtState.Quiet); }
 string folder=path+"/"+(clip==0?"EnterGathering":"ExitGathering"+clip); Directory.CreateDirectory(folder);
 for(int frame=0;frame<400;frame++) { Shot(folder+"/"+frame.ToString("0000")+".jpg"); yield return null; }
 }
 f.SetArtPreviewImmediate(FlowerArtState.Gathering); f.SetFrozen(true);
 foreach(float elevation in new[]{90f,8f,55f}) { g.SetView(22,elevation,9); yield return null; Shot(path+"/Bud_View_"+elevation+".jpg"); }
 int frozenUpdates=f.MeshUpdates; float frozenClock=f.ArtTime; yield return null;
 Require(frozenUpdates==f.MeshUpdates&&f.ArtTime==frozenClock,"Frozen bud has no mesh uploads or clock change"); f.SetFrozen(false); g.SetView(22,55,9);
 f.SetArtPreviewImmediate(FlowerArtState.Clarity);
 for(int frame=0;frame<1800;frame++) { if(frame%100==0) Shot(path+"/Orbit_"+frame.ToString("0000")+".jpg"); yield return null; }
 Require(Mathf.Abs(f.ArtTime-90)<.2f,"Full clarity revolution duration");
 Time.captureFramerate=0;
 for(int a=0;a<4;a++) for(int b=0;b<4;b++) if(a!=b) {
 f.SetArtPreviewImmediate((FlowerArtState)a); f.SetArtState((FlowerArtState)b);
 for(int t=0;t<90;t++) { cam.Render(); yield return null; }
 var weights=f.ArtWeights; Require(Mathf.Abs(weights.x+weights.y+weights.z+weights.w-1)<.0001f,"Normalized transition "+a+" to "+b);
 }
 f.SetBrainMetrics(1,1); yield return new WaitForSeconds(5.2f); Require(f.ArtStatus=="正在重新感知","Stale external signal");
 f.SetBrainMetrics(1,1); Require(f.ArtStatus=="外部脑电","External reconnect");
 int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length, mats=Resources.FindObjectsOfTypeAll<Material>().Length;
 f.SimulateArtBrain(); var samples=new List<float>(); long maxGc=0; double interpolationTotal=0,uploadTotal=0; int startUpdates=f.MeshUpdates;
 float until=Time.realtimeSinceStartup+600,reportAt=Time.realtimeSinceStartup+30;
 while(Time.realtimeSinceStartup<until) {
 cam.Render(); samples.Add((float)f.LastUpdateMilliseconds); interpolationTotal+=f.InterpolateMs; uploadTotal+=f.UploadMs; maxGc=Math.Max(maxGc,f.LastUpdateAllocation);
 if(Time.realtimeSinceStartup>=reportAt) { File.WriteAllText(path+"/Progress.txt",(600-(until-Time.realtimeSinceStartup)).ToString("F0")+" seconds"); reportAt+=30; }
 yield return null;
 }
 Require(f.MeshUpdates>startUpdates,"Cached path uploads during ten-minute blend"); Require(meshes==Resources.FindObjectsOfTypeAll<Mesh>().Length,"Stable mesh count"); Require(mats==Resources.FindObjectsOfTypeAll<Material>().Length,"Stable material count"); Require(maxGc==0,"Art update zero GC");
 samples.Sort(); File.WriteAllText(path+"/Performance.txt","samples="+samples.Count+" updateP95ms="+samples[(int)(samples.Count*.95f)]+" maxUpdateGc="+maxGc+" interpolationMeanMs="+interpolationTotal/samples.Count+" uploadMeanMs="+uploadTotal/samples.Count+" meshUploads="+(f.MeshUpdates-startUpdates));
 File.WriteAllText(path+"/Complete.txt","Complete"); Application.Quit();
 }
 void OnDestroy(){ if(rt!=null){rt.Release();Destroy(rt);} if(tex!=null)Destroy(tex); }
}}
#endif
