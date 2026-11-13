using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace MindState {
public sealed partial class FlowerAssembler {
 const string PlaybackFormat="FlowerPlayback/1";
 sealed class PlaybackClip {
  public ComputeBuffer buffer;
  public float[] times;
  public Bounds[] bounds;
  public int count;
  public Mesh mesh,diagnostic;
  public Bounds motionBounds;
 }
 PlaybackClip[][] playback;
 bool gpuPlayback;
 long playbackGpuBytes;
 public bool UsesGpuPlayback=>gpuPlayback;
 public long PlaybackGpuBytes=>playbackGpuBytes;
 static readonly int PlaybackBufferId=Shader.PropertyToID("_FlowerPoses"),PlaybackFrameId=Shader.PropertyToID("_FlowerFrame"),PlaybackEnabledId=Shader.PropertyToID("_FlowerPlayback");
 bool ReferenceGeometryRequested(){
#if UNITY_EDITOR || FLOWER_VALIDATION
  if(forceCpuReference)return true;
  var args=Environment.GetCommandLineArgs();
  return Array.IndexOf(args,"-bakeFlower")>=0||Array.IndexOf(args,"-verifyProject")>=0||Array.IndexOf(args,"-flowerCpuReference")>=0;
#else
  return false;
#endif
 }
 static string PlaybackPath=>Path.Combine(Application.streamingAssetsPath,"FlowerPlayback.bin");
 BinaryReader OpenPlayback(){
  if(!File.Exists(PlaybackPath))throw new InvalidOperationException("Flower playback cache missing. Run Tools/Bake.ps1.");
  var r=new BinaryReader(new DeflateStream(File.OpenRead(PlaybackPath),CompressionMode.Decompress));
  try{if(r.ReadString()!=PlaybackFormat||r.ReadString()!=BottomConfigHash())throw new InvalidOperationException("Flower playback cache is incompatible. Run Tools/Bake.ps1.");return r;}catch{r.Dispose();throw;}
 }
 static Vertex[] ReadVertices(BinaryReader r,int count){
  int length=checked(count*40);var bytes=r.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();
  var data=new Vertex[count];var handle=GCHandle.Alloc(data,GCHandleType.Pinned);
  try{Marshal.Copy(bytes,0,handle.AddrOfPinnedObject(),length);}finally{handle.Free();}return data;
 }
 static Vector3 ReadVector(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
 static Bounds ReadBounds(BinaryReader r)=>new Bounds(ReadVector(r),ReadVector(r));
 static int ReadCount(BinaryReader r,int maximum){int n=r.ReadInt32();if(n<=0||n>maximum)throw new InvalidDataException("Invalid flower cache count: "+n);return n;}
 Mesh PlaybackMesh(string title,Vertex[] data,Vector2[] uv,int[] triangles,int[] sourceIds=null){
  var mesh=new Mesh{name=title};
  mesh.SetVertexBufferParams(data.Length,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3,0),new VertexAttributeDescriptor(VertexAttribute.Tangent,VertexAttributeFormat.Float32,4,0),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2,1),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,1,2));
  mesh.SetVertexBufferData(data,0,0,data.Length,0);mesh.SetVertexBufferData(uv,0,0,uv.Length,1);
  var ids=new float[data.Length];for(int i=0;i<ids.Length;i++)ids[i]=sourceIds==null?i:sourceIds[i];mesh.SetVertexBufferData(ids,0,0,ids.Length,2);
  mesh.triangles=triangles;return mesh;
 }
 BinaryReader LoadPlaybackBases(){
  if(SystemInfo.graphicsShaderLevel<45)throw new NotSupportedException("Flower GPU playback requires shader model 4.5.");
  #if UNITY_EDITOR
  if(!FlowerCacheProvenance.PlaybackMatches())throw new InvalidOperationException("Flower playback cache is stale. Run Tools/Bake.ps1.");
#endif
  var r=OpenPlayback();
  try{
   if(ReadCount(r,16)!=layers.Length)throw new InvalidDataException("Playback layers mismatch");
   foreach(var l in layers){
    if(ReadCount(r,10)!=l.variants.Length)throw new InvalidDataException("Playback variants mismatch");
    foreach(var g in l.variants){
     int count=ReadCount(r,100000);var uv=new Vector2[count];for(int i=0;i<count;i++)uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
     var triangles=new int[ReadCount(r,1000000)];for(int i=0;i<triangles.Length;i++)triangles[i]=r.ReadInt32();
     for(int i=0;i<PoseCount;i++){var pose=new Pose{root=ReadVector(r),bounds=ReadBounds(r),data=ReadVertices(r,count)};g.poses[i]=pose;CacheBytes+=count*40L;}
     g.data=new Vertex[count];g.mesh=PlaybackMesh("Static flower variant",g.poses[0].data,uv,triangles);
    }
   }
   return r;
  }catch{r.Dispose();throw;}
 }
 void LoadPlaybackPetals(BinaryReader r){
  playback=new PlaybackClip[layers.Length][];
  for(int l=0;l<layers.Length;l++){
   if(ReadCount(r,10)!=layers[l].petals.Length)throw new InvalidDataException("Playback petals mismatch");
   playback[l]=new PlaybackClip[layers[l].petals.Length];
   for(int p=0;p<playback[l].Length;p++){
    int frames=ReadCount(r,16384),count=ReadCount(r,100000);if(count!=SurfaceFor(layers[l],p).data.Length)throw new InvalidDataException("Playback topology mismatch");
    var clip=new PlaybackClip{count=count,times=new float[frames],bounds=new Bounds[frames]};playback[l][p]=clip;
    for(int f=0;f<frames;f++){clip.times[f]=r.ReadSingle();clip.bounds[f]=ReadBounds(r);if(!Finite(clip.times[f])||(f>0&&clip.times[f]<=clip.times[f-1]))throw new InvalidDataException("Unordered playback frames");}
    if(clip.times[0]!=0||clip.times[frames-1]!=1)throw new InvalidDataException("Playback endpoints missing");
    clip.buffer=new ComputeBuffer(checked(frames*count),40,ComputeBufferType.Structured);playbackGpuBytes+=frames*count*40L;
    for(int f=0;f<frames;f++)clip.buffer.SetData(ReadVertices(r,count),0,f*count,count);
    clip.mesh=Instantiate(SurfaceFor(layers[l],p).mesh);clip.mesh.name="GPU flower "+l+"-"+p;
    layers[l].petals[p].body.GetComponent<MeshFilter>().sharedMesh=clip.mesh;
    BindPlayback(layers[l].petals[p].block,clip);
   }
  }
 }
 static Vector4 PlaybackFrame(PlaybackClip clip,float progress){
  int a=Array.BinarySearch(clip.times,Mathf.Clamp01(progress));if(a<0)a=~a-1;a=Mathf.Clamp(a,0,clip.times.Length-2);
  return new Vector4(a,a+1,Mathf.InverseLerp(clip.times[a],clip.times[a+1],progress),clip.count);
 }
 static Bounds InterpolatePlaybackBounds(Bounds a,Bounds b,float progress)=>new Bounds(Vector3.LerpUnclamped(a.center,b.center,progress),Vector3.LerpUnclamped(a.size,b.size,progress));
 static void BindPlayback(MaterialPropertyBlock block,PlaybackClip clip){block.SetFloat(PlaybackEnabledId,1);block.SetBuffer(PlaybackBufferId,clip.buffer);}
 void ApplyPlayback(float progress){
  if(playback==null)return;
  for(int l=0;l<layers.Length;l++)for(int p=0;p<layers[l].petals.Length;p++){
   var clip=playback[l][p];var frame=PlaybackFrame(clip,progress);int a=(int)frame.x;
   clip.motionBounds=InterpolatePlaybackBounds(clip.bounds[a],clip.bounds[a+1],frame.z);
   var bounds=clip.bounds[a];bounds.Encapsulate(clip.bounds[a+1]);clip.mesh.bounds=bounds;
   var petal=layers[l].petals[p];petal.block.SetVector(PlaybackFrameId,frame);petal.renderer.SetPropertyBlock(petal.block);
  }
 }
 Vertex[] SamplePlayback(PlaybackClip clip,float progress){
  var frame=PlaybackFrame(clip,progress);var first=new Vertex[clip.count];var next=new Vertex[clip.count];
  clip.buffer.GetData(first,0,(int)frame.x*clip.count,clip.count);clip.buffer.GetData(next,0,(int)frame.y*clip.count,clip.count);
  for(int i=0;i<first.Length;i++)Interpolate(ref first[i],ref next[i],frame.z,out first[i]);return first;
 }
 Mesh DiagnosticPlaybackMesh(int layer,int index){
  var clip=playback[layer][index];if(clip.diagnostic==null){clip.diagnostic=Instantiate(clip.mesh);clip.diagnostic.name="On demand diagnostic flower";}
  clip.diagnostic.SetVertexBufferData(SamplePlayback(clip,bloomProgress),0,0,clip.count,0);clip.diagnostic.bounds=clip.mesh.bounds;return clip.diagnostic;
 }
 void MaterializeBaseDiagnostic(Variant g,float progress){
  float frame=Mathf.Clamp01(progress)*(PoseCount-1);int a=Mathf.Min((int)frame,PoseCount-2);
  for(int i=0;i<g.data.Length;i++)Interpolate(ref g.poses[a].data[i],ref g.poses[a+1].data[i],frame-a,out g.data[i]);
  g.mesh.SetVertexBufferData(g.data,0,0,g.data.Length,0);
 }
 void ReleasePlayback(){
  if(playback!=null)foreach(var row in playback)if(row!=null)foreach(var clip in row)if(clip!=null){clip.buffer?.Release();if(clip.mesh!=null)Destroy(clip.mesh);if(clip.diagnostic!=null)Destroy(clip.diagnostic);}
  playback=null;playbackGpuBytes=0;
 }
}
}
