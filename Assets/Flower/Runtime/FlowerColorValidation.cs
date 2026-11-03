#if UNITY_EDITOR || FLOWER_VALIDATION
using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
namespace MindState {
 public sealed class FlowerColorValidation:MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() {
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyColor")>=0) Camera.main.gameObject.AddComponent<FlowerColorValidation>();
  }
  string GeometryDigest() {
   using(var stream=new MemoryStream()) using(var w=new BinaryWriter(stream)) {
    foreach(var p in f.GetDiagnosticPetals()) {
     foreach(var v in p.body.GetComponent<MeshFilter>().sharedMesh.vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
     var m=p.body.localToWorldMatrix;for(int i=0;i<16;i++)w.Write(m[i]);
     var b=new MaterialPropertyBlock();p.renderer.GetPropertyBlock(b);var trim=b.GetVector("_RootTrim");for(int i=0;i<4;i++)w.Write(trim[i]);
    }
    w.Flush();return Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(stream.ToArray()));
   }
  }
  string root; Camera cam; FlowerAssembler f; RenderTexture rt; Texture2D image;
  void Check(bool ok,string message) { File.AppendAllText(root+"/Checks.txt",(ok?"PASS ":"FAIL ")+message+"\n"); if(!ok) {Application.Quit(2); throw new Exception(message);} }
  byte[] Capture(string file=null) {
   cam.Render(); var old=RenderTexture.active; RenderTexture.active=rt;
   image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply(); RenderTexture.active=old;
   if(file!=null) File.WriteAllBytes(root+"/"+file,image.EncodeToJPG(94));
   return image.GetRawTextureData();
  }
  void Gray(bool on) { foreach(var r in f.GetComponentsInChildren<MeshRenderer>()) { var b=new MaterialPropertyBlock(); r.GetPropertyBlock(b); b.SetFloat("_GrayMode",on?1:0); if(r.sharedMaterial.shader.name!="MindState/Radial Pearl Silk") b.SetColor("_Color",on?Color.gray:(r.sharedMaterial==f.coreMaterial?new Color(.94f,.48f,.43f):new Color(1,.88f,.62f))); r.SetPropertyBlock(b); } }
  IEnumerator Start() {
   root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Evidence/NaturalLight")); Directory.CreateDirectory(root); File.WriteAllText(root+"/Complete.txt","Running"); File.WriteAllText(root+"/Performance.txt",""); File.WriteAllText(root+"/Checks.txt","");
   yield return null; f=FindObjectOfType<FlowerAssembler>(); Check(f.appearance!=null,"Appearance preset bound in built scene"); var g=FindObjectOfType<FlowerGallery>(); g.showInterface=false; g.SetView(22,55,9);
   cam=GetComponent<Camera>(); cam.enabled=false; rt=new RenderTexture(1920,1080,24){antiAliasing=4};rt.Create();cam.targetTexture=rt;
   image=new Texture2D(1920,1080,TextureFormat.RGB24,false); Time.captureFramerate=20;
   for(int state=0;state<4;state++) {
    f.SetArtPreviewImmediate((FlowerArtState)state); f.SetFrozen(true);
    f.appearanceEnabled=false; f.RefreshAppearance(); Gray(true); var digest=GeometryDigest(); cam.Render(); var before=Capture("GrayBefore"+state+".jpg");
    f.appearanceEnabled=true; f.RefreshAppearance(); Gray(true); var after=Capture("GrayAfter"+state+".jpg");
    bool equal=before.Length==after.Length; for(int n=0;n<before.Length&&equal;n++) equal=before[n]==after[n];
    File.WriteAllText(root+"/GrayDifference"+state+".txt",equal.ToString()); Check(digest==GeometryDigest(),"Exact vertices transforms and root trim "+state); Gray(false);
    Capture(((FlowerArtState)state)+".jpg");
    var pixels=image.GetPixels32(); Check(pixels[0].r==0&&pixels[0].g==0&&pixels[0].b==0,"Black background "+state);
    var spots=f.GetComponentsInChildren<Light>(); Check(spots.Length==3,"Three real color spotlights "+state); var spotPose=spots[0].transform.localToWorldMatrix; var spotColor=spots[0].color; float phase=f.ArtFlowPhase; float clock=f.ArtTime; var light=f.ArtLightWeights; yield return null;
    Check(spotPose==spots[0].transform.localToWorldMatrix&&spotColor==spots[0].color,"Frozen physical colored light "+state);
    Check(phase==f.ArtFlowPhase&&clock==f.ArtTime&&light==f.ArtLightWeights,"Frozen material feedback "+state);
   }
   for(int state=0;state<(Array.IndexOf(Environment.GetCommandLineArgs(),"-flowTransitionOnly")>=0?0:4);state++) {
    f.SetArtPreviewImmediate((FlowerArtState)state); f.SetFrozen(false); string folder="State"+state;Directory.CreateDirectory(root+"/"+folder);
    var petals=f.GetDiagnosticPetals(); var seen=new float[petals.Length];var blocks=new MaterialPropertyBlock[petals.Length];
    int textureCount=Resources.FindObjectsOfTypeAll<Texture2D>().Length;
    for(int j=0;j<petals.Length;j++) blocks[j]=new MaterialPropertyBlock();
    for(int n=0;n<1200;n++) {
     Capture(folder+"/"+n.ToString("0000")+".jpg");
     if(n==120) for(int j=0;j<petals.Length;j++){petals[j].renderer.GetPropertyBlock(blocks[j]);seen[j]=blocks[j].GetVector("_FlowSettings").x;}
     if(n==170) for(int j=0;j<petals.Length;j++){petals[j].renderer.GetPropertyBlock(blocks[j]);Check(Mathf.Abs(blocks[j].GetVector("_FlowSettings").x-seen[j])>.01f,"Petal phase advances "+state+"/"+j);}
     yield return null;
    }
    Check(Resources.FindObjectsOfTypeAll<Texture2D>().Length==textureCount,"Stable texture count for 60 seconds state "+state);
    f.SoloPetal(0,0); g.SetView(22,55,4);g.focus=petals[0].renderer.bounds.center;folder="Petal"+state;Directory.CreateDirectory(root+"/"+folder);
    for(int n=0;n<240;n++){Capture(folder+"/"+n.ToString("0000")+".jpg");yield return null;}
    f.ShowAllPetals();g.SetView(22,55,9);
   }
   f.SetArtPreviewImmediate(FlowerArtState.Clarity); f.SetFrozen(false); f.SetArtState(FlowerArtState.Gathering);
   for(int n=0;n<20;n++) yield return null;
   Check(f.ArtLightWeights.y>f.ArtWeights.y,"Material responds before geometry");
   var actualLight=f.GetComponentsInChildren<Light>()[0]; var beforeLight=actualLight.transform.localToWorldMatrix;
   f.SetBrainMetrics(.5f,.5f); for(int n=0;n<100;n++)yield return null; Capture("Mixed.jpg");
   Check(beforeLight!=actualLight.transform.localToWorldMatrix,"Actual light moves on continuous noise trajectory");
   f.SetBrainMetrics(.51f,.49f); for(int n=0;n<20;n++)yield return null; Capture("SmallChange.jpg");
   f.ResetArt(); Check(f.ArtFlowPhase==0,"Reset restores deterministic flow phase");
   for(int a=0;a<4;a++) for(int b=0;b<4;b++) if(a!=b) {
    f.SetArtPreviewImmediate((FlowerArtState)a); f.SetFrozen(false); f.SetArtState((FlowerArtState)b);
    string folder="Transition"+a+"_"+b; Directory.CreateDirectory(root+"/"+folder);
    for(int n=0;n<240;n++) { Capture(folder+"/"+n.ToString("0000")+".jpg"); yield return null; }
   }
   Time.captureFramerate=0; QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
   for(int mode=0;mode<2;mode++) {
    f.appearanceEnabled=mode==1;f.RefreshAppearance(); f.SimulateArtBrain();
    float end=Time.realtimeSinceStartup+5;while(Time.realtimeSinceStartup<end){cam.Render();yield return null;}
    int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,mats=Resources.FindObjectsOfTypeAll<Material>().Length;
    var samples=new List<float>(); var frameSamples=new List<float>(); long gc=0; end=Time.realtimeSinceStartup+20;
    while(Time.realtimeSinceStartup<end) { cam.Render(); samples.Add((float)f.LastUpdateMilliseconds); frameSamples.Add(Time.unscaledDeltaTime*1000); gc=Math.Max(gc,f.LastUpdateAllocation); yield return null; }
    samples.Sort();frameSamples.Sort();File.AppendAllText(root+"/Performance.txt","appearance="+mode+" CPU_component_P95_ms="+samples[(int)(samples.Count*.95f)]+" frameIntervalP95ms="+frameSamples[(int)(frameSamples.Count*.95f)]+" maxUpdateGC="+gc+" GPU_ms=unavailable\n");
    Check(gc==0&&meshes==Resources.FindObjectsOfTypeAll<Mesh>().Length&&mats==Resources.FindObjectsOfTypeAll<Material>().Length,"Stable resources and zero update GC "+mode);
   }
   File.WriteAllText(root+"/Complete.txt","Complete");Application.Quit();
  }
  void OnDestroy(){if(rt!=null){rt.Release();Destroy(rt);}if(image!=null)Destroy(image);}
 }
}
#endif
