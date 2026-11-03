#if UNITY_EDITOR || FLOWER_VALIDATION
using System;
using System.IO;
using System.Collections;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 public void RootValidationStep(float dt) { frozen=false; UpdateArt(dt); frozen=true; }
 public bool VerifyDiscContact(out string failure) {
  failure="";float r=0;foreach(var l in layers)r=Mathf.Max(r,l.config.rootRadius);r+=.04f;
  int cols=HeroPetal.WidthSegments+1,surface=cols*(HeroPetal.LengthSegments+1);
  foreach(var l in layers)for(int j=0;j<l.petals.Length;j++) {
   var petal=l.petals[j];var vertices=SurfaceFor(l,j).data;bool contact=false;
   // Inspect both physical shell surfaces near the root, after the actual pose transform and trim.
   for(int side=0;side<2;side++)for(int row=0;row<=2;row++)for(int x=cols/2-4;x<=cols/2+4;x++) {
    var v=generated.InverseTransformPoint(petal.body.TransformPoint(vertices[side*surface+row*cols+x].p));
    float y=v.y-layers[0].config.rootHeight,rad=v.x*v.x+v.z*v.z;
    if(y>=.004f && rad/(r*r)+y*y/(.012f*.012f)<1)contact=true;
   }
   if(!contact){failure=l.config.name+" petal "+j;return false;}
  }return true;
 }
}
public class RootConnectionValidation:MonoBehaviour {
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyRoots")>=0)Camera.main.gameObject.AddComponent<RootConnectionValidation>();}
 IEnumerator Start(){
 yield return null;var f=FindObjectOfType<FlowerAssembler>();var g=FindObjectOfType<FlowerGallery>();var cam=GetComponent<Camera>();g.showInterface=false;cam.enabled=false;
 bool baseline=Array.IndexOf(Environment.GetCommandLineArgs(),"-rootBaseline")>=0;
 string dir=Path.Combine(Application.dataPath,"../../../Evidence/RootConnection/"+(baseline?"Baseline":"Translation"));Directory.CreateDirectory(dir);
 var rt=new RenderTexture(960,540,24){antiAliasing=4};rt.Create();cam.targetTexture=rt;var tex=new Texture2D(960,540,TextureFormat.RGB24,false);
 Action<string> shot=name=>{cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(dir,name+".jpg"),tex.EncodeToJPG(88));RenderTexture.active=null;};
 if(Array.IndexOf(Environment.GetCommandLineArgs(),"-rootGray")>=0) {
  f.petalPrefab.pearlMaterial.SetFloat("_GrayMode",1);
  for(int state=0;state<4;state++){f.SetArtPreviewImmediate((FlowerArtState)state);foreach(int el in new[]{8,-35,-90,55}){g.SetView(22,el,5);shot("GrayState"+state+"_"+el);}}
  f.petalPrefab.pearlMaterial.SetFloat("_GrayMode",0);Application.Quit();yield break;
 }
 int samples=0;Action check=()=>{samples++;if(!baseline&&!f.VerifyDiscContact(out var error))throw new Exception("Disconnected visible shell: "+error);};
 Action<string> views=name=>{foreach(int el in new[]{8,-35,-90,55}){g.SetView(22,el,5);shot(name+"_"+el);}};
 for(int i=0;i<=40;i++){f.SetBloomProgress(i/40f);check();if(i%10==0)views("Bloom"+i);}
 f.SetFrozen(true);
 for(int state=0;state<4;state++){
  f.SetArtPreviewImmediate((FlowerArtState)state);views("State"+state);
  string frames=Path.Combine(dir,"State"+state);Directory.CreateDirectory(frames);g.SetView(22,-35,5);
  for(int frame=0;frame<400;frame++){f.RootValidationStep(.05f);check();if(frame%2==0)shot("State"+state+"/"+(frame/2).ToString("0000"));if(frame%20==0)yield return null;}
 }
 for(int a=0;a<4;a++)for(int b=0;b<4;b++)if(a!=b){
  f.SetArtPreviewImmediate((FlowerArtState)a);f.SetArtState((FlowerArtState)b);string name="Transition"+a+"_"+b;Directory.CreateDirectory(Path.Combine(dir,name));
  for(int frame=0;frame<240;frame++){f.RootValidationStep(.05f);check();if(frame%4==0)shot(name+"/"+(frame/4).ToString("0000"));if(frame%20==0)yield return null;}
 }
 File.WriteAllText(Path.Combine(dir,"Checks.txt"),"PASS samples="+samples+"; physical visible shell overlaps unchanged disc for all 14 petals (baseline excluded="+baseline+")");
 File.WriteAllText(Path.Combine(dir,"Complete.txt"),"Complete");rt.Release();Destroy(rt);Destroy(tex);Application.Quit();
 }
}
}
#endif
