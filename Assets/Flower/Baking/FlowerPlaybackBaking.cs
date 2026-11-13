#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MindState {
public sealed partial class FlowerAssembler {
 sealed class BakedFrame { public float time;public Vertex[] data;public Bounds bounds; }
 static void WriteVector(BinaryWriter w,Vector3 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
 static void WriteBounds(BinaryWriter w,Bounds b){WriteVector(w,b.center);WriteVector(w,b.size);}
 static void WriteVertices(BinaryWriter w,Vertex[] data){
  var bytes=new byte[data.Length*40];var handle=GCHandle.Alloc(data,GCHandleType.Pinned);
  try{Marshal.Copy(handle.AddrOfPinnedObject(),bytes,0,bytes.Length);}finally{handle.Free();}w.Write(bytes);
 }
 BakedFrame CapturePlaybackFrame(int layer,int index,float phase){
  bloomProgress=phase;ApplyPose(phase,false);var source=SurfaceFor(layers[layer],index);
  Vertex[] data=source.data;
  foreach(var patch in bottomPatches)if(patch.layer==layer&&patch.index==index){ApplyBottomPatch(patch);data=patch.work;break;}
  // Solver bounds contain interval-wide correction margins and jump at nodes.
  // Store the actual pose envelope; only rendering unions adjacent envelopes.
  var min=data[0].p;var max=min;
  for(int i=1;i<data.Length;i++){min=Vector3.Min(min,data[i].p);max=Vector3.Max(max,data[i].p);}
  var bounds=new Bounds((min+max)*.5f,max-min+Vector3.one*.00002f);
  return new BakedFrame{time=phase,data=(Vertex[])data.Clone(),bounds=bounds};
 }
 static bool PlaybackWithinTolerance(BakedFrame first,BakedFrame next,BakedFrame exact){
  float f=Mathf.InverseLerp(first.time,next.time,exact.time);
  for(int i=0;i<exact.data.Length;i++){
   var expected=exact.data[i];var v=Interpolate(first.data[i],next.data[i],f);
   if((v.p-expected.p).sqrMagnitude>1e-8f||DirectionError(v.n,expected.n)>1||DirectionError(new Vector3(v.t.x,v.t.y,v.t.z),new Vector3(expected.t.x,expected.t.y,expected.t.z))>1)return false;
  }
  return true;
 }
 void RefinePlayback(int layer,int petal,BakedFrame a,BakedFrame b,List<BakedFrame> frames,int depth){
  var mid=CapturePlaybackFrame(layer,petal,(a.time+b.time)*.5f);
  bool ok=PlaybackWithinTolerance(a,b,mid);
  if(ok){
   var q1=CapturePlaybackFrame(layer,petal,Mathf.Lerp(a.time,b.time,.25f));
   var q3=CapturePlaybackFrame(layer,petal,Mathf.Lerp(a.time,b.time,.75f));
   ok=PlaybackWithinTolerance(a,b,q1)&&PlaybackWithinTolerance(a,b,q3);
  }
  if(ok){frames.Add(b);return;}
  // Some legacy dirty masks change exactly at a node. Adjacent float times have
  // no representable interior: store both sides instead of smoothing that seam.
  if(mid.time<=a.time||mid.time>=b.time){frames.Add(b);return;}
  if(depth>=24)throw new InvalidOperationException("Playback cannot meet geometry tolerance: "+layer+"/"+petal+" at "+mid.time);
  RefinePlayback(layer,petal,a,mid,frames,depth+1);RefinePlayback(layer,petal,mid,b,frames,depth+1);
 }
 internal void SavePlaybackCache(){
  if(gpuPlayback)throw new InvalidOperationException("Bake requires CPU reference geometry");
  string path=PlaybackPath;Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
  var report=new List<string>{"Final geometry cache; Float32 positions/normals/tangents; quarter/mid/three-quarter refinement; position <= 0.0001, direction <= 1 degree"};
  try{
   using(var w=new BinaryWriter(new DeflateStream(File.Create(temp),System.IO.Compression.CompressionLevel.Fastest))){
    w.Write(PlaybackFormat);w.Write(BottomConfigHash());w.Write(layers.Length);
    foreach(var l in layers){w.Write(l.variants.Length);foreach(var g in l.variants){
     w.Write(g.data.Length);foreach(var uv in g.mesh.uv){w.Write(uv.x);w.Write(uv.y);}var triangles=g.mesh.triangles;w.Write(triangles.Length);foreach(int t in triangles)w.Write(t);
     foreach(var pose in g.poses){WriteVector(w,pose.root);WriteBounds(w,pose.bounds);WriteVertices(w,pose.data);}
    }}
    for(int l=0;l<layers.Length;l++){
     w.Write(layers[l].petals.Length);
     var nodes=new SortedSet<float>{0,1};var config=layers[l].config;
     for(int i=0;i<PoseCount;i++){nodes.Add(i/(float)(PoseCount-1));nodes.Add(Mathf.Clamp01((config.delay+config.duration*i/(PoseCount-1))/BloomDuration));}
     for(int p=0;p<layers[l].petals.Length;p++){
      var frames=new List<BakedFrame>();BakedFrame previous=null;
      foreach(float time in nodes){var next=CapturePlaybackFrame(l,p,time);if(previous==null)frames.Add(next);else RefinePlayback(l,p,previous,next,frames,0);previous=next;}
      w.Write(frames.Count);w.Write(frames[0].data.Length);foreach(var frame in frames){w.Write(frame.time);WriteBounds(w,frame.bounds);}foreach(var frame in frames)WriteVertices(w,frame.data);
      string line="layer="+l+" petal="+p+" frames="+frames.Count+" gpu_bytes="+(frames.Count*frames[0].data.Length*40L);report.Add(line);Debug.Log("PLAYBACK_BAKE "+line);
     }
    }
   }
   if(File.Exists(path))File.Delete(path);File.Move(temp,path);
   File.WriteAllText(Path.Combine(Application.dataPath,"Flower/Resources/FlowerPlaybackProvenance.txt"),FlowerCacheProvenance.Signature());
   report.Add("compressed_bytes="+new FileInfo(path).Length);File.WriteAllLines("Outputs/PlaybackBake.txt",report);
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
}
#endif
