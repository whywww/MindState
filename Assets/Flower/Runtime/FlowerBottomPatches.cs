using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
namespace MindState {
public sealed partial class FlowerAssembler {
 sealed class BottomPatch { public int layer,index; public Mesh mesh; public Vertex[] work; public int[] ids,wall;public float[] radius;public bool[] dirty; public Vector3[][] delta; public int[] upperIds=new int[0];public Vector3[][] upper; public int[] extraIds=new int[0];public Vector3[][] extra; }
 readonly List<BottomPatch> bottomPatches=new List<BottomPatch>();
 const int BottomCols=129,BottomSurface=129*81;
 bool bottomPreviewEnabled=true;
 float bottomProgress=-1,bottomLow,bottomHigh;
 bool upperPreview=true,extraPreview=true;
 Vector3 BottomPoint(int l,int i,float p,float t,float s) {
  var layer=layers[l];var g=SurfaceFor(layer,i);float q=Mathf.Clamp01((p*BloomDuration-layer.config.delay)/layer.config.duration)*32;
  int a=Mathf.Min((int)q,31);float fx=Mathf.Clamp((s+1)*64,0,128),fy=Mathf.Clamp(t*80,0,80);
  int x=Mathf.Min((int)fx,127),y=Mathf.Min((int)fy,79);int k=y*129+x;
  var first=g.poses[a];var next=g.poses[a+1];
  Vector3 p0=Vector3.Lerp(Vector3.Lerp(BottomMid(first,k),BottomMid(first,k+1),fx-x),Vector3.Lerp(BottomMid(first,k+129),BottomMid(first,k+130),fx-x),fy-y);
  Vector3 p1=Vector3.Lerp(Vector3.Lerp(BottomMid(next,k),BottomMid(next,k+1),fx-x),Vector3.Lerp(BottomMid(next,k+129),BottomMid(next,k+130),fx-x),fy-y);
  return Vector3.Lerp(p0,p1,q-a);
 }
 static Vector3 BottomMid(Pose p,int k)=>(p.data[k].p+p.data[k+BottomSurface].p)*.5f;
 Matrix4x4 BottomMatrix(int l,int i,float p) {
  var layer=layers[l];var petal=layer.petals[i];float q=Mathf.Clamp01((p*BloomDuration-layer.config.delay)/layer.config.duration);
  PathPetalPose(layer,petal,i,q,out var pos,out var rot,out var scale);
  pos.y=layers[0].config.rootHeight+.0045f+layer.config.rootStagger*RootStaggerSign(i,layer.petals.Length);
  return Matrix4x4.TRS(pos,rot,scale)*Matrix4x4.Translate(-BottomPoint(l,i,p,0,0));
 }
 void BuildBottomPatches() {
#if UNITY_EDITOR
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-rebuildBottom")>=0) { BakeBottomPatches();return; }
  if(!FlowerCacheProvenance.Matches()) throw new InvalidOperationException("Geometry sources changed. Run Tools/Bake.ps1 before playback or build.");
#endif
  if(!LoadBottomCache()) throw new InvalidOperationException("Current BottomOverlap cache is missing or incompatible. Rebuild it in the Editor.");
 }

 internal long BottomPatchCacheBytes {get{long bytes=0;foreach(var p in bottomPatches)bytes+=(p.ids.Length+p.upperIds.Length+p.extraIds.Length)*33L*12;return bytes;}}
 internal long BottomWorkingBytes {get{long bytes=0;foreach(var p in bottomPatches)bytes+=p.work.Length*40L+p.wall.Length*4L+p.dirty.Length+(p.ids.Length+p.upperIds.Length+p.extraIds.Length)*4L+UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(p.mesh);return bytes;}}
 void AttachBottomPatch(BottomPatch patch){
  patch.radius=new float[33];for(int pose=0;pose<33;pose++)foreach(var d in patch.delta[pose])patch.radius[pose]=Mathf.Max(patch.radius[pose],d.magnitude);
  if(patch.upper!=null)for(int pose=0;pose<33;pose++){float upperRadius=0;foreach(var d in patch.upper[pose])upperRadius=Mathf.Max(upperRadius,d.magnitude);patch.radius[pose]+=upperRadius;}
  if(patch.extra!=null)for(int pose=0;pose<33;pose++){float radius=0;foreach(var d in patch.extra[pose])radius=Mathf.Max(radius,d.magnitude);patch.radius[pose]+=radius;}
  var g=SurfaceFor(layers[patch.layer],patch.index);patch.mesh=Instantiate(g.mesh);patch.mesh.name="Local bottom overlap "+patch.layer+"-"+patch.index;patch.mesh.MarkDynamic();patch.work=new Vertex[g.data.Length];patch.dirty=new bool[BottomSurface];
  var uv=patch.mesh.uv;patch.wall=new int[uv.Length-BottomSurface*2];
  for(int k=BottomSurface*2;k<uv.Length;k++)patch.wall[k-BottomSurface*2]=Mathf.RoundToInt(uv[k].y*80)*129+Mathf.RoundToInt(uv[k].x*128)+(k%2==0?0:BottomSurface);
  layers[patch.layer].petals[patch.index].body.GetComponent<MeshFilter>().sharedMesh=patch.mesh;bottomPatches.Add(patch);
  CacheBytes+=(patch.ids.Length+patch.upperIds.Length+patch.extraIds.Length)*33L*12;
 }
 string BottomConfigHash(){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(preset))));}

 bool LoadBottomCache(byte[] data=null){
  if(data==null){var asset=Resources.Load<TextAsset>("BottomOverlap");if(asset==null)return false;data=asset.bytes;}
  using(var r=new BinaryReader(new MemoryStream(data))){
   if(r.ReadString()!="BottomOverlap/3"||r.ReadString()!=BottomConfigHash())return false;
   bottomLow=r.ReadSingle();bottomHigh=r.ReadSingle();int count=r.ReadInt32();if(count<0||count>PetalCount)return false;
   var pending=new List<BottomPatch>();
   for(int i=0;i<count;i++){
    int layer=r.ReadInt32(),index=r.ReadInt32(),length=r.ReadInt32();
    if(layer<0||layer>=layers.Length||index<0||index>=layers[layer].petals.Length||length<0||length>BottomSurface)return false;
    var p=new BottomPatch{layer=layer,index=index,ids=new int[length],delta=new Vector3[33][]};
    for(int k=0;k<length;k++){p.ids[k]=r.ReadInt32();if(p.ids[k]<0||p.ids[k]>=BottomSurface)return false;}
    for(int pose=0;pose<33;pose++){p.delta[pose]=new Vector3[length];for(int k=0;k<length;k++)p.delta[pose][k]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}int n=r.ReadInt32();if(n<0||n>BottomSurface)return false;p.upperIds=new int[n];p.upper=new Vector3[33][];for(int k=0;k<n;k++){p.upperIds[k]=r.ReadInt32();if(p.upperIds[k]<0||p.upperIds[k]>=BottomSurface)return false;}for(int pose=0;pose<33;pose++){p.upper[pose]=new Vector3[n];for(int k=0;k<n;k++)p.upper[pose][k]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}{int e=r.ReadInt32();p.extraIds=new int[e];p.extra=new Vector3[33][];for(int k=0;k<e;k++)p.extraIds[k]=r.ReadInt32();for(int pose=0;pose<33;pose++){p.extra[pose]=new Vector3[e];for(int k=0;k<e;k++)p.extra[pose][k]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}}pending.Add(p);
   }
   foreach(var p in pending)AttachBottomPatch(p);return true;
  }
 }
 internal bool BottomIsolationCheck(){
  foreach(var p in bottomPatches){var g=SurfaceFor(layers[p.layer],p.index);var mask=new bool[BottomSurface];foreach(int id in p.ids)mask[id]=true;foreach(int id in p.upperIds)mask[id]=true;foreach(int id in p.extraIds)mask[id]=true;
   for(int id=0;id<BottomSurface;id++)if(id/129<8||!mask[id])if(p.work[id].p!=g.data[id].p||p.work[id+BottomSurface].p!=g.data[id+BottomSurface].p)return false;
  }return true;
 }

 void ApplyBottomPatches() {
  if(!bottomPreviewEnabled||bottomProgress==bloomProgress)return;bottomProgress=bloomProgress;
  foreach(var patch in bottomPatches) {
   float frame=Mathf.Clamp01(bloomProgress)*32;int a=Mathf.Min((int)frame,31);float f=frame-a;
   var g=SurfaceFor(layers[patch.layer],patch.index);Array.Copy(g.data,patch.work,g.data.Length);Array.Clear(patch.dirty,0,patch.dirty.Length);
   for(int k=0;k<patch.ids.Length;k++){int id=patch.ids[k];var d=Vector3.Lerp(patch.delta[a][k],patch.delta[a+1][k],f);patch.work[id].p+=d;patch.work[id+BottomSurface].p+=d;if(d.sqrMagnitude>0){patch.dirty[id]=true;patch.dirty[Mathf.Max(0,id-129)]=true;patch.dirty[Mathf.Min(BottomSurface-1,id+129)]=true;if(id%129>0)patch.dirty[id-1]=true;if(id%129<128)patch.dirty[id+1]=true;}}
   if(upperPreview&&patch.upper!=null)for(int k=0;k<patch.upperIds.Length;k++){int id=patch.upperIds[k];var d=Vector3.Lerp(patch.upper[a][k],patch.upper[a+1][k],f);patch.work[id].p+=d;patch.work[id+BottomSurface].p+=d;if(d.sqrMagnitude>0){patch.dirty[id]=true;patch.dirty[Mathf.Max(0,id-129)]=true;patch.dirty[Mathf.Min(BottomSurface-1,id+129)]=true;if(id%129>0)patch.dirty[id-1]=true;if(id%129<128)patch.dirty[id+1]=true;}}
   if(extraPreview&&patch.extra!=null)for(int k=0;k<patch.extraIds.Length;k++){int id=patch.extraIds[k];var d=Vector3.Lerp(patch.extra[a][k],patch.extra[a+1][k],f);patch.work[id].p+=d;patch.work[id+BottomSurface].p+=d;if(d.sqrMagnitude>0){patch.dirty[id]=true;patch.dirty[Mathf.Max(0,id-129)]=true;patch.dirty[Mathf.Min(BottomSurface-1,id+129)]=true;if(id%129>0)patch.dirty[id-1]=true;if(id%129<128)patch.dirty[id+1]=true;}}
   for(int y=8;y<=80;y++)for(int x=0;x<129;x++) {
    int id=y*129+x;if(!patch.dirty[id])continue;Vector3 across=patch.work[y*129+Mathf.Min(x+1,128)].p-patch.work[y*129+Mathf.Max(x-1,0)].p;
    Vector3 along=patch.work[Mathf.Min(y+1,80)*129+x].p-patch.work[Mathf.Max(y-1,0)*129+x].p;
    var tangent=across.normalized;var n=Vector3.Cross(along,tangent).normalized;patch.work[id].n=n;patch.work[id+BottomSurface].n=-n;
    patch.work[id].t=new Vector4(tangent.x,tangent.y,tangent.z,-1);patch.work[id+BottomSurface].t=new Vector4(tangent.x,tangent.y,tangent.z,1);
   }
   for(int k=BottomSurface*2;k<patch.work.Length;k++)patch.work[k].p=patch.work[patch.wall[k-BottomSurface*2]].p;
   for(int k=BottomSurface*2;k<patch.work.Length;k+=2){int id=patch.wall[k-BottomSurface*2];if(!patch.dirty[id])continue;
    int previous=k==BottomSurface*2?patch.work.Length-2:k-2,next=k+2>=patch.work.Length?BottomSurface*2:k+2;
    var edge=(patch.work[next].p-patch.work[previous].p).normalized;var n=Vector3.Cross(edge,-patch.work[id].n).normalized;
    patch.work[k].n=patch.work[k+1].n=n;patch.work[k].t=patch.work[k+1].t=new Vector4(edge.x,edge.y,edge.z,1);
   }
   patch.mesh.SetVertexBufferData(patch.work,0,0,patch.work.Length,0,MeshUpdateFlags.DontRecalculateBounds);var bounds=g.mesh.bounds;bounds.Expand(2*Mathf.Max(patch.radius[a],patch.radius[a+1]));patch.mesh.bounds=bounds;MeshUpdates++;
  }
 }
 Vector3 BottomDeltaAt(int layer,int index,float phase,int id){foreach(var patch in bottomPatches)if(patch.layer==layer&&patch.index==index){int k=Array.BinarySearch(patch.ids,id);if(k<0)return Vector3.zero;float q=phase*32;int a=Mathf.Min((int)q,31);return Vector3.Lerp(patch.delta[a][k],patch.delta[a+1][k],q-a);}return Vector3.zero;}
 internal void UpperCornerPreview(bool enabled){upperPreview=enabled;bottomProgress=-1;ApplyBottomPatches();}

 internal string BottomPatchReport(){long bytes=0;float max=0;foreach(var p in bottomPatches){bytes+=p.ids.Length*33L*12+p.work.Length*40L;foreach(var pose in p.delta)foreach(var d in pose)max=Mathf.Max(max,d.magnitude);}return "bottomBand="+bottomLow+".."+bottomHigh+" patches="+bottomPatches.Count+" bytes="+bytes+" maxLocalOffset="+max;}
 internal void BottomPatchPreview(bool enabled){bottomPreviewEnabled=enabled;foreach(var p in bottomPatches)layers[p.layer].petals[p.index].body.GetComponent<MeshFilter>().sharedMesh=enabled?p.mesh:SurfaceFor(layers[p.layer],p.index).mesh;if(enabled){bottomProgress=-1;ApplyBottomPatches();}}
 void ReleaseBottomPatches(){foreach(var patch in bottomPatches)if(patch.mesh!=null)Destroy(patch.mesh);bottomPatches.Clear();bottomProgress=-1;}
}
}
