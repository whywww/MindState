#if UNITY_EDITOR || FLOWER_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MindState {
public sealed partial class FlowerAssembler {
 internal bool forceCpuReference;
 internal void VerifyGpuPlayback(FlowerAssembler reference,List<string> report){
  var shader=Resources.Load<ComputeShader>("FlowerPlaybackVerify");int kernel=shader.FindKernel("Sample");
  float maxPosition=0,maxDirection=0;
  int initialUploads=MeshUpdates;
  foreach(float progress in new[]{0f,.0137f,.137f,.373f,.5f,.731f,.99f,1f,.42f,0f}){
   SetBloomProgress(progress);reference.SetBloomProgress(progress);
   if(RootAnchorError()>1e-5f)throw new Exception("GPU root attachment failed");
   for(int l=0;l<layers.Length;l++)for(int p=0;p<layers[l].petals.Length;p++){
    var clip=playback[l][p];var frame=PlaybackFrame(clip,progress);var expectedMesh=reference.PetalMesh(l,p);
    var positions=expectedMesh.vertices;var normals=expectedMesh.normals;var tangents=expectedMesh.tangents;
    using(var output=new ComputeBuffer(clip.count,40)){
     shader.SetBuffer(kernel,"_FlowerPoses",clip.buffer);shader.SetVector("_FlowerFrame",frame);shader.SetBuffer(kernel,"_Result",output);shader.SetInt("_Count",clip.count);shader.Dispatch(kernel,(clip.count+63)/64,1,1);
     var result=new Vertex[clip.count];output.GetData(result);
     for(int i=0;i<result.Length;i++){
      var v=result[i];maxPosition=Mathf.Max(maxPosition,Vector3.Distance(v.p,positions[i]));
      maxDirection=Mathf.Max(maxDirection,DirectionError(v.n,normals[i]),DirectionError(new Vector3(v.t.x,v.t.y,v.t.z),new Vector3(tangents[i].x,tangents[i].y,tangents[i].z)));
      if(!clip.mesh.bounds.Contains(v.p))throw new Exception("GPU vertex outside playback bounds");
      if(v.t.w!=tangents[i].w)throw new Exception("GPU tangent handedness changed");
     }
    }
   }
  }
  if(maxPosition>.0001f||maxDirection>1)throw new Exception("GPU/reference geometry mismatch: position="+maxPosition+" direction="+maxDirection);
  if(MeshUpdates!=initialUploads)throw new Exception("Playback uploaded flower vertices");
  report.Add("PASS shared vertex/shadow sampler GPU readback: position="+maxPosition+" direction="+maxDirection+"; roots, bounds, handedness, reverse/jump progress, zero uploads");
  for(int l=0;l<layers.Length;l++)for(int v=0;v<layers[l].variants.Length;v++){
   var echo=echoVariants[echoVariantBase[l]+v];var source=reference.layers[l].variants[v];
   foreach(float progress in new[]{0f,.137f,.731f,1f})using(var output=new ComputeBuffer(echo.playback.count,40)){
    shader.SetBuffer(kernel,"_FlowerPoses",echo.playback.buffer);shader.SetVector("_FlowerFrame",PlaybackFrame(echo.playback,progress));shader.SetBuffer(kernel,"_Result",output);shader.SetInt("_Count",echo.playback.count);shader.Dispatch(kernel,(echo.playback.count+63)/64,1,1);
    var result=new Vertex[echo.playback.count];output.GetData(result);float frame=progress*(PoseCount-1);int a=Mathf.Min((int)frame,PoseCount-2);
    foreach(int id in echo.ids){var expected=Interpolate(source.poses[a].data[id],source.poses[a+1].data[id],frame-a);if(Vector3.Distance(result[id].p,expected.p)>1e-5f||DirectionError(result[id].n,expected.n)>.05f)throw new Exception("Echo GPU/reference mismatch");}
   }
  }
  report.Add("PASS echo GPU sampler matches CPU reference at endpoints and intermediate progress");
  VerifyPlaybackRendering(reference,report);
  report.Add("gpu_bytes="+PlaybackGpuBytes+" initialization_ms="+InitializationMilliseconds);
  long initialBytes=PlaybackGpuBytes;Release();if(Ready||PlaybackGpuBytes!=0)throw new Exception("Playback resources not released");
  RebuildFlower();SetArtState(FlowerArtState.Clarity);if(PlaybackGpuBytes!=initialBytes)throw new Exception("Playback resources changed after rebuild");
  report.Add("PASS release and rebuild restore the same GPU allocation");
 }
 void VerifyPlaybackRendering(FlowerAssembler reference,List<string> report){
  var camera=Camera.main;var target=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);
  var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
  reference.transform.SetPositionAndRotation(transform.position,transform.rotation);reference.transform.localScale=transform.lossyScale;
  Directory.CreateDirectory("Outputs/PlaybackValidation");
  try{
   camera.targetTexture=target;
   foreach(float progress in new[]{0f,.5f,1f}){
    SetBloomProgress(progress);reference.SetBloomProgress(progress);
    for(int l=0;l<layers.Length;l++)for(int p=0;p<layers[l].petals.Length;p++){
     var other=reference.layers[l].petals[p];layers[l].petals[p].renderer.GetPropertyBlock(other.block);other.block.SetFloat(PlaybackEnabledId,0);other.renderer.SetPropertyBlock(other.block);
    }
    generated.gameObject.SetActive(true);reference.generated.gameObject.SetActive(false);camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();var gpu=pixels.GetPixels32();
    File.WriteAllBytes("Outputs/PlaybackValidation/Gpu"+progress+".png",pixels.EncodeToPNG());
    generated.gameObject.SetActive(false);reference.generated.gameObject.SetActive(true);camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();var cpu=pixels.GetPixels32();
    File.WriteAllBytes("Outputs/PlaybackValidation/Cpu"+progress+".png",pixels.EncodeToPNG());
    double difference=0;for(int i=0;i<gpu.Length;i++)difference+=Math.Abs(gpu[i].r-cpu[i].r)+Math.Abs(gpu[i].g-cpu[i].g)+Math.Abs(gpu[i].b-cpu[i].b);
    difference/=gpu.Length*3.0*255;if(difference>.005)throw new Exception("Rendered GPU/CPU image mismatch at "+progress+": "+difference);
    report.Add("PASS rendered color/shadow comparison progress="+progress+" mean_channel_error="+difference);
   }
  }finally{generated.gameObject.SetActive(true);reference.generated.gameObject.SetActive(false);camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Destroy(pixels);RenderTexture.ReleaseTemporary(target);}
 }
}
public sealed class FlowerPlaybackValidation:MonoBehaviour {
#if UNITY_EDITOR
 public static void Run(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Flower/Scenes/FlowerGallery.unity");UnityEditor.EditorApplication.EnterPlaymode();}
#endif
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyPlayback")>=0)new GameObject("Playback validation").AddComponent<FlowerPlaybackValidation>();}
 IEnumerator Start(){
  yield return null;int exit=0;var report=new List<string>();FlowerAssembler reference=null;
  try{
   var flower=FindObjectOfType<FlowerAssembler>();if(flower==null||!flower.Ready||!flower.UsesGpuPlayback)throw new Exception("GPU flower not ready");flower.enabled=false;
   reference=new GameObject("CPU geometry reference").AddComponent<FlowerAssembler>();reference.enabled=false;reference.forceCpuReference=true;
   reference.preset=flower.preset;reference.petalPrefab=flower.petalPrefab;reference.coreMaterial=flower.coreMaterial;reference.stamenMaterial=flower.stamenMaterial;reference.RebuildFlower();
   flower.VerifyGpuPlayback(reference,report);report.Add("PASS");
  }catch(Exception e){Debug.LogException(e);report.Add("FAIL "+e);exit=2;}
  finally{if(reference!=null)Destroy(reference.gameObject);}
  Directory.CreateDirectory("Outputs/PlaybackValidation");File.WriteAllLines("Outputs/PlaybackValidation/Result.txt",report);
#if UNITY_EDITOR
  if(Application.isEditor){UnityEditor.EditorApplication.Exit(exit);yield break;}
#endif
  Application.Quit(exit);
 }
}
}
#endif
