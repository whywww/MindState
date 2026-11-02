#if UNITY_EDITOR || FLOWER_VALIDATION
using UnityEngine;
using System;
using System.IO;
using System.Collections;
namespace MindState {
public class FlowerHeartValidation:MonoBehaviour {
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyHeart")>=0) Camera.main.gameObject.AddComponent<FlowerHeartValidation>();}
IEnumerator Start(){
yield return null;
var f=FindObjectOfType<FlowerAssembler>();var g=FindObjectOfType<FlowerGallery>();var cam=GetComponent<Camera>();g.showInterface=false;cam.enabled=false;
var dir=Path.Combine(Application.dataPath,"../../../Evidence/FlowerHeart");Directory.CreateDirectory(dir);
File.WriteAllText(Path.Combine(dir,"Complete.txt"),"Running");
var rt=new RenderTexture(1920,1080,24){antiAliasing=4};rt.Create();cam.targetTexture=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
Action<string> shot=name=>{cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();bool video=name.StartsWith("Motion")||name.StartsWith("Transition");File.WriteAllBytes(Path.Combine(dir,name+(video?".jpg":".png")),video?tex.EncodeToJPG(85):tex.EncodeToPNG());RenderTexture.active=null;};
// Inspect the actual built mesh, not just the generation parameters.
var heart=f.transform.Find("Generated · shared layer meshes/Concealed filament roots").GetComponent<MeshFilter>().sharedMesh;
var points=heart.vertices; const int discVertices=13*33,rootStride=9*9;
if(points.Length!=discVertices+30*rootStride) throw new Exception("Unexpected disc/filament topology");
var discBounds=new Bounds(points[0],Vector3.zero);for(int i=1;i<discVertices;i++)discBounds.Encapsulate(points[i]);
float worst=0;for(int j=0;j<30;j++)for(int i=0;i<9;i++){
var p=points[discVertices+j*rootStride+i]-discBounds.center;var e=discBounds.extents;
worst=Mathf.Max(worst,p.x*p.x/(e.x*e.x)+p.y*p.y/(e.y*e.y)+p.z*p.z/(e.z*e.z));}
if(worst>=.9f)throw new Exception("Filament roots do not overlap the disc interior");
File.WriteAllText(Path.Combine(dir,"Connection.txt"),"PASS one thin disc, no spherical closure; 30 filament root rings embedded; max normalized squared radius="+worst+"; disc bounds="+discBounds);
if(Array.IndexOf(Environment.GetCommandLineArgs(),"-trimPreview")>=0){
foreach(var state in new[]{FlowerArtState.Gathering,FlowerArtState.Clarity,FlowerArtState.Wandering,FlowerArtState.Quiet}){
f.SetArtPreviewImmediate(state);f.SetFrozen(true);g.SetView(22,-35,5);shot("Trim_"+state+"_BottomSide");g.SetView(22,-90,5);shot("Trim_"+state+"_Bottom");g.SetView(22,8,5);shot("Trim_"+state+"_Side");}Application.Quit();yield break;}
if(Array.IndexOf(Environment.GetCommandLineArgs(),"-heartPreview")>=0){
f.SetArtPreviewImmediate(FlowerArtState.Clarity);f.SetFrozen(true);g.SetView(135,55,5);shot("ConnectionCloseup");g.SetView(0,8);shot("ConnectionSide");g.SetView(22,55);shot("ConnectionDefault");Application.Quit();yield break;}
for(int state=0;state<4;state++){
f.SetArtPreviewImmediate((FlowerArtState)state);f.SetFrozen(true);
foreach(float el in new[]{90f,55f,8f,-35f}) for(int az=0;az<360;az+=45){g.SetView(az,el);shot("State"+state+"_"+el+"_"+az);}
g.SetView(22,55);shot("State"+state);f.SetFrozen(false);
if(Array.IndexOf(Environment.GetCommandLineArgs(),"-trimCapture")>=0)g.SetView(22,-35,5);
Time.captureFramerate=20;
for(int frame=0;frame<400;frame++){shot("Motion"+state+"_"+frame.ToString("0000"));yield return null;}
Time.captureFramerate=0;
}
if(Array.IndexOf(Environment.GetCommandLineArgs(),"-heartViewsOnly")<0) for(int a=0;a<4;a++)for(int b=0;b<4;b++)if(a!=b){f.SetArtPreviewImmediate((FlowerArtState)a);f.SetArtState((FlowerArtState)b);g.SetView(22,Array.IndexOf(Environment.GetCommandLineArgs(),"-trimCapture")>=0?-35:55,5);Time.captureFramerate=20;for(int j=0;j<240;j++){yield return null;shot("Transition"+a+"_"+b+"_"+j.ToString("0000"));}Time.captureFramerate=0;}
for(int j=0;j<=4;j++){f.SetBloomProgress(j*.25f);g.SetView(22,55);shot("Bloom"+j);g.SetView(22,-35,5);shot("TrimBloom"+j);}
f.SetArtPreviewImmediate(FlowerArtState.Clarity);g.SetView(0,90,4);shot("HeartCloseup");
File.WriteAllText(Path.Combine(dir,"Complete.txt"),"Complete");rt.Release();Destroy(rt);Destroy(tex);Application.Quit();
}
}}
#endif
