#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {

 Vector3 LayerBase(int l,int i,float phase,int id)=>BottomPoint(l,i,phase,(id/129)/80f,(id%129)/64f-1)+BottomDeltaAt(l,i,phase,id)+UpperDeltaAt(l,i,phase,id);
 // Fair the complete accepted outer surface. The obstacle constraint retains the
 // existing outward clearance instead of flattening a raised edge back into its neighbour.
 // Add clearance to the accepted, already faired surface; keep the earlier caches intact.
 // Fair the complete accepted middle surface in space and time. Every projection
 // check samples all six petals together, avoiding order-dependent neighbours.
 void SmoothMiddleFlow(){
  const int count=6;var rest=new Vector3[count][][];var fair=new Vector3[count][][];var normals=new Vector3[count][][];var matrices=new Matrix4x4[count][];
  for(int i=0;i<count;i++){rest[i]=new Vector3[33][];fair[i]=new Vector3[33][];normals[i]=new Vector3[33][];matrices[i]=new Matrix4x4[33];
   for(int pose=0;pose<33;pose++){float phase=pose/32f;var v=new Vector3[BottomSurface];SampleDiagnosticSurface(1,i,phase,v);rest[i][pose]=v;fair[i][pose]=(Vector3[])v.Clone();matrices[i][pose]=BottomMatrix(1,i,phase);normals[i][pose]=new Vector3[BottomSurface];
    for(int y=8;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x;var across=v[y*129+Mathf.Min(128,x+1)]-v[y*129+Mathf.Max(0,x-1)];var along=v[Mathf.Min(80,y+1)*129+x]-v[Mathf.Max(0,y-1)*129+x];var n=matrices[i][pose].MultiplyVector(Vector3.Cross(across,along)).normalized;var point=matrices[i][pose].MultiplyPoint3x4(v[id]);if(Vector3.Dot(n,new Vector3(point.x,Mathf.Max(0,phase-.2f)*5,point.z))<0)n=-n;normals[i][pose][id]=n;}
   }
  }
  for(int pass=0;pass<180;pass++){
   var next=new Vector3[count][][];
   for(int i=0;i<count;i++){next[i]=(Vector3[][])fair[i].Clone();for(int pose=9;pose<=16;pose++){
    var v=fair[i][pose];var dst=(Vector3[])v.Clone();next[i][pose]=dst;float phase=pose/32f;float gate=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.3125f,phase))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46875f,.53125f,phase)));
    for(int y=8;y<=80;y++)for(int x=0;x<129;x++){
     int id=y*129+x;var left=x>0?v[id-1]:2*v[id]-v[id+1];var right=x<128?v[id+1]:2*v[id]-v[id-1];var below=v[id-129];var above=y<80?v[id+129]:2*v[id]-below;
     var spatial=(left+right+above+below)*.25f;var temporal=(fair[i][pose-1][id]+fair[i][pose+1][id])*.5f;
     var target=v[id]+.45f*(spatial-v[id])+.08f*(temporal-v[id]);float along=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.3f,y/80f));var localNormal=matrices[i][pose].inverse.MultiplyVector(normals[i][pose][40*129+64]).normalized;float outward=Mathf.Max(0,Vector3.Dot(target-rest[i][pose][id],localNormal));float side=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.4f,.2f,x/64f-1));dst[id]=rest[i][pose][id]+localNormal*Mathf.Min(outward,.06f*gate*along*side);
    }
   }}fair=next;
  }
  var report=new StringBuilder("petal,phase,maxAddedOffset\n");
  foreach(var patch in bottomPatches)if(patch.layer==1){int i=patch.index;var frames=new Vector3[33][];
   for(int pose=0;pose<33;pose++){frames[pose]=new Vector3[BottomSurface];for(int k=0;k<patch.extraIds.Length;k++)frames[pose][patch.extraIds[k]]=patch.extra[pose][k];float maximum=0;for(int id=8*129;id<BottomSurface;id++){var d=fair[i][pose][id]-rest[i][pose][id];frames[pose][id]+=d;maximum=Mathf.Max(maximum,d.magnitude);}report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2}\n",i,pose/32f,maximum);}
   CacheBytes-=patch.extraIds.Length*33L*12;var ids=new List<int>();for(int id=0;id<BottomSurface;id++){bool used=Array.BinarySearch(patch.extraIds,id)>=0;for(int pose=0;pose<33&&!used;pose++)used|=frames[pose][id].sqrMagnitude>1e-12f;if(used)ids.Add(id);}patch.extraIds=ids.ToArray();patch.extra=new Vector3[33][];
   for(int pose=0;pose<33;pose++){patch.extra[pose]=new Vector3[ids.Count];float radius=0;for(int k=0;k<ids.Count;k++){patch.extra[pose][k]=frames[pose][ids[k]];radius=Mathf.Max(radius,patch.extra[pose][k].magnitude);}patch.radius[pose]+=radius;}CacheBytes+=ids.Count*33L*12;
  }
  // Project back into the collision-free neighbourhood of the accepted surface.
  // A global step reduction keeps both spatial and temporal continuity intact.
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();var phases=new SortedSet<float>();for(int k=0;k<=50;k++)phases.Add(.25f+k*.005f);for(int k=16;k<=34;k++)phases.Add(k/64f);
  float factor=1;bool feasible=false;
  for(int attempt=0;attempt<4;attempt++){
   feasible=true;foreach(float phase in phases){scan.Sample(phase);scan.FindContacts(hits,focusLayer:1,parallel:true);if(hits.Exists(h=>h.petalA==h.petalB&&!h.rootRegion)){feasible=false;break;}
    if(phase<.47f)for(int i=0;i<count;i++){scan.FindPairContacts(7+i,7+(i+5)%count,hits);if(hits.Exists(h=>!h.rootRegion&&h.lengthA>.25f&&h.lengthB>.25f)){feasible=false;break;}}if(!feasible)break;
   }
   if(feasible)break;float previous=factor;factor*=.5f;foreach(var patch in bottomPatches)if(patch.layer==1)for(int pose=9;pose<=16;pose++)for(int k=0;k<patch.extraIds.Length;k++){int id=patch.extraIds[k];patch.extra[pose][k]-=(fair[patch.index][pose][id]-rest[patch.index][pose][id])*(previous-factor);}
  }
  if(!feasible)throw new InvalidOperationException("Middle flow proposal could not retain accepted clearance; cache not publishable.");
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Outputs/Bake/MiddleFlow"));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Flow.csv"),report.ToString());File.WriteAllText(Path.Combine(path,"Constraint.txt"),"Accepted proposal scale="+factor);
 }
 void SmoothInnerFlow(){
  const int count=5;var rest=new Vector3[count][][];var fair=new Vector3[count][][];var normals=new Vector3[count][][];var matrices=new Matrix4x4[count][];
  for(int i=0;i<count;i++){rest[i]=new Vector3[33][];fair[i]=new Vector3[33][];normals[i]=new Vector3[33][];matrices[i]=new Matrix4x4[33];
   for(int pose=0;pose<33;pose++){float phase=pose/32f;var v=new Vector3[BottomSurface];SampleDiagnosticSurface(2,i,phase,v);rest[i][pose]=v;fair[i][pose]=(Vector3[])v.Clone();matrices[i][pose]=BottomMatrix(2,i,phase);normals[i][pose]=new Vector3[BottomSurface];
    for(int y=8;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x;var across=v[y*129+Mathf.Min(128,x+1)]-v[y*129+Mathf.Max(0,x-1)];var along=v[Mathf.Min(80,y+1)*129+x]-v[Mathf.Max(0,y-1)*129+x];var n=matrices[i][pose].MultiplyVector(Vector3.Cross(across,along)).normalized;var point=matrices[i][pose].MultiplyPoint3x4(v[id]);if(Vector3.Dot(n,new Vector3(point.x,Mathf.Max(0,phase-.2f)*5,point.z))<0)n=-n;normals[i][pose][id]=n;}
   }
  }
  for(int pass=0;pass<180;pass++){
   var next=new Vector3[count][][];
   for(int i=0;i<count;i++){next[i]=(Vector3[][])fair[i].Clone();for(int pose=0;pose<=20;pose++){
    var v=fair[i][pose];var dst=(Vector3[])v.Clone();next[i][pose]=dst;float phase=pose/32f;float gate=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.59375f,.65625f,phase)));
    for(int y=8;y<=80;y++)for(int x=0;x<129;x++){
     int id=y*129+x;var left=x>0?v[id-1]:2*v[id]-v[id+1];var right=x<128?v[id+1]:2*v[id]-v[id-1];var below=v[id-129];var above=y<80?v[id+129]:2*v[id]-below;
     var spatial=(left+right+above+below)*.25f;var temporal=(fair[i][Mathf.Max(0,pose-1)][id]+fair[i][pose+1][id])*.5f;
     var target=v[id]+.45f*(spatial-v[id])+.08f*(temporal-v[id]);float along=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.3f,y/80f));dst[id]=rest[i][pose][id]+Vector3.ClampMagnitude(target-rest[i][pose][id],.06f*gate*along);
    }
   }}fair=next;
  }
  var report=new StringBuilder("petal,phase,maxAddedOffset\n");
  foreach(var patch in bottomPatches)if(patch.layer==2){int i=patch.index;var frames=new Vector3[33][];
   for(int pose=0;pose<33;pose++){frames[pose]=new Vector3[BottomSurface];for(int k=0;k<patch.extraIds.Length;k++)frames[pose][patch.extraIds[k]]=patch.extra[pose][k];float maximum=0;for(int id=8*129;id<BottomSurface;id++){var d=fair[i][pose][id]-rest[i][pose][id];frames[pose][id]+=d;maximum=Mathf.Max(maximum,d.magnitude);}report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2}\n",i,pose/32f,maximum);}
   CacheBytes-=patch.extraIds.Length*33L*12;var ids=new List<int>();for(int id=0;id<BottomSurface;id++){bool used=Array.BinarySearch(patch.extraIds,id)>=0;for(int pose=0;pose<33&&!used;pose++)used|=frames[pose][id].sqrMagnitude>1e-12f;if(used)ids.Add(id);}patch.extraIds=ids.ToArray();patch.extra=new Vector3[33][];
   for(int pose=0;pose<33;pose++){patch.extra[pose]=new Vector3[ids.Count];float radius=0;for(int k=0;k<ids.Count;k++){patch.extra[pose][k]=frames[pose][ids[k]];radius=Mathf.Max(radius,patch.extra[pose][k].magnitude);}patch.radius[pose]+=radius;}CacheBytes+=ids.Count*33L*12;
  }
  // Project back into the collision-free neighbourhood of the accepted surface.
  // A global step reduction keeps both spatial and temporal continuity intact.
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();var phases=new SortedSet<float>();for(int k=0;k<=65;k++)phases.Add(k/100f);for(int k=0;k<=42;k++)phases.Add(k/64f);
  float factor=1;bool feasible=false;
  for(int attempt=0;attempt<4;attempt++){
   feasible=true;foreach(float phase in phases){scan.Sample(phase);scan.FindContacts(hits,focusLayer:2,parallel:true);if(hits.Exists(h=>h.petalA==h.petalB&&!h.rootRegion)){feasible=false;break;}
    if(phase<.6f)for(int i=0;i<count;i++){scan.FindPairContacts(13+i,13+(i+4)%count,hits);if(hits.Exists(h=>!h.rootRegion&&h.lengthA>.25f&&h.lengthB>.25f)){feasible=false;break;}}if(!feasible)break;
   }
   if(feasible)break;float previous=factor;factor*=.5f;foreach(var patch in bottomPatches)if(patch.layer==2)for(int pose=0;pose<=20;pose++)for(int k=0;k<patch.extraIds.Length;k++){int id=patch.extraIds[k];patch.extra[pose][k]-=(fair[patch.index][pose][id]-rest[patch.index][pose][id])*(previous-factor);}
  }
  if(!feasible)throw new InvalidOperationException("Inner flow proposal could not retain accepted clearance; cache not publishable.");
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Outputs/Bake/InnerFlow"));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Flow.csv"),report.ToString());File.WriteAllText(Path.Combine(path,"Constraint.txt"),"Accepted proposal scale="+factor);
 }
 void BuildInnerResidualPatches(){
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  var report=new StringBuilder("petal,phase,contacts,maxAddedOffset,dx,dy,dz\n");
  var additions=new Dictionary<int,Vector3[][]>();
  foreach(var patch in bottomPatches)if(patch.layer==2){
   int i=patch.index,neighbor=(i+4)%5;var frames=new Vector3[33][];additions[i]=frames;
   for(int pose=0;pose<33;pose++){
    frames[pose]=new Vector3[BottomSurface];float phase=pose/32f;
    float gate=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.59375f,.65625f,phase));if(gate<=0)continue;
    var mask=new bool[BottomSurface];int contacts=0;
    foreach(float sample in new[]{Mathf.Max(0,phase-.015625f),phase,Mathf.Min(.65625f,phase+.015625f)}){scan.Sample(sample,null,2);scan.FindPairContacts(13+i,13+neighbor,hits);
    foreach(var h in hits){if(h.rootRegion||h.lengthA<.12f)continue;contacts++;int sx=Mathf.RoundToInt((h.widthA+1)*64),sy=Mathf.RoundToInt(h.lengthA*80);
     for(int y=Mathf.Max(8,sy-24);y<=Mathf.Min(80,sy+24);y++)for(int x=Mathf.Max(0,sx-24);x<=Mathf.Min(128,sx+24);x++)mask[y*129+x]=true;
    }
    }
    var matrix=BottomMatrix(2,i,phase);var inverse=matrix.inverse;
    var required=new float[BottomSurface];var height=new float[BottomSurface];
    // Lift the covering sheet rather than pushing the underlying sheet through it.
    var surface=new Vector3[BottomSurface];SampleDiagnosticSurface(2,i,phase,surface);
    var direction=matrix.MultiplyVector(Vector3.Cross(surface[40*129+65]-surface[40*129+63],surface[41*129+64]-surface[39*129+64])).normalized;
    var center=matrix.MultiplyPoint3x4(surface[40*129+64]);if(Vector3.Dot(direction,new Vector3(center.x,0,center.z))<0)direction=-direction;
    var directions=new Vector3[BottomSurface];for(int id=0;id<BottomSurface;id++)directions[id]=direction;
    foreach(float sample in new[]{Mathf.Max(0,phase-.015625f),phase,Mathf.Min(.65625f,phase+.015625f)}){
     var sampleMatrix=BottomMatrix(2,i,sample);var sampleTree=new BottomTree(this,2,neighbor,sample,BottomMatrix(2,neighbor,sample),true,true);
     for(int id=8*129;id<BottomSurface;id++)if(mask[id]){
      var point=sampleMatrix.MultiplyPoint3x4(LayerBase(2,i,sample,id)+ExtraDeltaAt(2,i,sample,id));float d=sampleTree.OutsideDistance(point,directions[id]);
      if(d>-.015f&&d<.16f)required[id]=Mathf.Max(required[id],Mathf.Min(.16f,Mathf.Max(0,d+.018f)));height[id]=required[id];
     }
    }
    for(int pass=0;pass<140;pass++){var next=(float[])height.Clone();for(int y=8;y<=80;y++)for(int x=0;x<129;x++){
     int id=y*129+x;float sum=height[id]*2;int count=2;foreach(int n in new[]{y>8?id-129:-1,y<80?id+129:-1,x>0?id-1:-1,x<128?id+1:-1})if(n>=0){sum+=height[n];count++;}
     next[id]=Mathf.Max(required[id],sum/count);
    }height=next;}
    // Translate the curled tip as a broad strip; a narrow height peak can fold
    // the last few rows back through their own surface during interpolation.
    for(int x=0;x<129;x++){float tipHeight=0;for(int y=64;y<=80;y++)tipHeight=Mathf.Max(tipHeight,height[y*129+x]);for(int y=56;y<=80;y++){int id=y*129+x;float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(56,64,y));height[id]=Mathf.Lerp(height[id],Mathf.Max(height[id],tipHeight),blend);}}
    float maximum=0;for(int id=8*129;id<BottomSurface;id++){float h=height[id]*gate*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.2f,(id/129)/80f));frames[pose][id]=inverse.MultiplyVector(directions[id]*h);maximum=Mathf.Max(maximum,frames[pose][id].magnitude);}
    report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6}\n",i,phase,contacts,maximum,direction.x,direction.y,direction.z);
   }
  }
  // Commit together so all pairs are fitted against the same accepted surface.
  foreach(var patch in bottomPatches)if(patch.layer==2){var frames=additions[patch.index];for(int pose=0;pose<33;pose++)for(int k=0;k<patch.extraIds.Length;k++)frames[pose][patch.extraIds[k]]+=patch.extra[pose][k];
   CacheBytes-=patch.extraIds.Length*33L*12;var ids=new List<int>();for(int id=0;id<BottomSurface;id++){bool used=false;for(int pose=0;pose<33;pose++)used|=frames[pose][id].sqrMagnitude>1e-12f;if(used)ids.Add(id);}patch.extraIds=ids.ToArray();patch.extra=new Vector3[33][];
   for(int pose=0;pose<33;pose++){patch.extra[pose]=new Vector3[ids.Count];float radius=0;for(int k=0;k<ids.Count;k++){patch.extra[pose][k]=frames[pose][ids[k]];radius=Mathf.Max(radius,patch.extra[pose][k].magnitude);}patch.radius[pose]+=radius;}CacheBytes+=ids.Count*33L*12;
  }
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Outputs/Bake/Inner60"));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Regions.csv"),report.ToString());
 }
 void BuildMiddleResidualPatches(){
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  var report=new StringBuilder("petal,phase,contacts,maxAddedOffset\n");
  var additions=new Dictionary<int,Vector3[][]>();
  foreach(var patch in bottomPatches)if(patch.layer==1){
   int i=patch.index,neighbor=(i+5)%6;var frames=new Vector3[33][];additions[i]=frames;
   for(int pose=0;pose<33;pose++){
    frames[pose]=new Vector3[BottomSurface];float phase=pose/32f;
    float gate=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46875f,.53125f,phase));if(gate<=0)continue;
    var mask=new bool[BottomSurface];int contacts=0;
    foreach(float sample in new[]{Mathf.Max(0,phase-.015625f),phase,Mathf.Min(.53125f,phase+.015625f)}){scan.Sample(sample,null,1);scan.FindPairContacts(7+i,7+neighbor,hits);
    foreach(var h in hits){if(h.rootRegion||h.lengthA<.12f)continue;contacts++;int sx=Mathf.RoundToInt((h.widthA+1)*64),sy=Mathf.RoundToInt(h.lengthA*80);
     for(int y=Mathf.Max(8,sy-24);y<=Mathf.Min(80,sy+24);y++)for(int x=Mathf.Max(0,sx-24);x<=Mathf.Min(128,sx+24);x++)mask[y*129+x]=true;
    }
    }
    var matrix=BottomMatrix(1,i,phase);var inverse=matrix.inverse;
    var required=new float[BottomSurface];var height=new float[BottomSurface];
    // Lift the covering sheet rather than pushing the underlying sheet through it.
    var surface=new Vector3[BottomSurface];SampleDiagnosticSurface(1,i,phase,surface);
    var direction=matrix.MultiplyVector(Vector3.Cross(surface[40*129+65]-surface[40*129+63],surface[41*129+64]-surface[39*129+64])).normalized;
    var center=matrix.MultiplyPoint3x4(surface[40*129+64]);if(Vector3.Dot(direction,new Vector3(center.x,Mathf.Max(0,phase-.2f)*5,center.z))<0)direction=-direction;
    var directions=new Vector3[BottomSurface];for(int id=0;id<BottomSurface;id++){float tip=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.75f,.95f,(id/129)/80f))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.3f,phase)));directions[id]=Vector3.Lerp(direction,Vector3.up,tip).normalized;}
    foreach(float sample in new[]{Mathf.Max(0,phase-.015625f),phase,Mathf.Min(.53125f,phase+.015625f)}){
     var sampleMatrix=BottomMatrix(1,i,sample);var sampleTree=new BottomTree(this,1,neighbor,sample,BottomMatrix(1,neighbor,sample),true,true);
     for(int id=8*129;id<BottomSurface;id++)if(mask[id]){
      var point=sampleMatrix.MultiplyPoint3x4(LayerBase(1,i,sample,id)+ExtraDeltaAt(1,i,sample,id));float d=sampleTree.OutsideDistance(point,directions[id]);
      if(d>-.015f&&d<.16f)required[id]=Mathf.Max(required[id],Mathf.Min(.16f,Mathf.Max(0,d+.018f)));height[id]=required[id];
     }
    }
    for(int pass=0;pass<140;pass++){var next=(float[])height.Clone();for(int y=8;y<=80;y++)for(int x=0;x<129;x++){
     int id=y*129+x;float sum=height[id]*2;int count=2;foreach(int n in new[]{y>8?id-129:-1,y<80?id+129:-1,x>0?id-1:-1,x<128?id+1:-1})if(n>=0){sum+=height[n];count++;}
     next[id]=Mathf.Max(required[id],sum/count);
    }height=next;}
    // Translate the curled tip as a broad strip; a narrow height peak can fold
    // the last few rows back through their own surface during interpolation.
    for(int x=0;x<129;x++){float tipHeight=0;for(int y=64;y<=80;y++)tipHeight=Mathf.Max(tipHeight,height[y*129+x]);for(int y=56;y<=80;y++){int id=y*129+x;float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(56,64,y))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.328125f,phase))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.375f,.40625f,phase)));height[id]=Mathf.Lerp(height[id],Mathf.Max(height[id],tipHeight),blend);}}
    float maximum=0;for(int id=8*129;id<BottomSurface;id++){float h=height[id]*gate*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.2f,(id/129)/80f));frames[pose][id]=inverse.MultiplyVector(directions[id]*h);maximum=Mathf.Max(maximum,frames[pose][id].magnitude);}
    report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3}\n",i,phase,contacts,maximum);
   }
  }
  // Commit together so all pairs are fitted against the same accepted surface.
  foreach(var patch in bottomPatches)if(patch.layer==1){var frames=additions[patch.index];for(int pose=0;pose<33;pose++)for(int k=0;k<patch.extraIds.Length;k++)frames[pose][patch.extraIds[k]]+=patch.extra[pose][k];
   CacheBytes-=patch.extraIds.Length*33L*12;var ids=new List<int>();for(int id=0;id<BottomSurface;id++){bool used=false;for(int pose=0;pose<33;pose++)used|=frames[pose][id].sqrMagnitude>1e-12f;if(used)ids.Add(id);}patch.extraIds=ids.ToArray();patch.extra=new Vector3[33][];
   for(int pose=0;pose<33;pose++){patch.extra[pose]=new Vector3[ids.Count];float radius=0;for(int k=0;k<ids.Count;k++){patch.extra[pose][k]=frames[pose][ids[k]];radius=Mathf.Max(radius,patch.extra[pose][k].magnitude);}patch.radius[pose]+=radius;}CacheBytes+=ids.Count*33L*12;
  }
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Outputs/Bake/Middle47"));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Regions.csv"),report.ToString());
 }
 void BuildOuterResidualPatches(){
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  var report=new StringBuilder("petal,phase,contacts,maxAddedOffset\n");
  var additions=new Dictionary<int,Vector3[][]>();
  foreach(var patch in bottomPatches)if(patch.layer==0){
   int i=patch.index,neighbor=(i+1)%7;var frames=new Vector3[33][];additions[i]=frames;
   for(int pose=0;pose<33;pose++){
    frames[pose]=new Vector3[BottomSurface];float phase=pose/32f;
    float gate=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4375f,.53125f,phase))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65625f,.78125f,phase)));if(gate<=0)continue;
    scan.Sample(phase,null,0);scan.FindPairContacts(i,neighbor,hits);
    var mask=new bool[BottomSurface];int contacts=0;
    foreach(var h in hits){if(h.rootRegion||h.lengthA<.3f)continue;contacts++;int sx=Mathf.RoundToInt((h.widthA+1)*64),sy=Mathf.RoundToInt(h.lengthA*80);
     for(int y=Mathf.Max(24,sy-24);y<=Mathf.Min(80,sy+24);y++)for(int x=Mathf.Max(0,sx-24);x<=Mathf.Min(128,sx+24);x++)mask[y*129+x]=true;
    }
    var matrix=BottomMatrix(0,i,phase);var inverse=matrix.inverse;var other=BottomMatrix(0,neighbor,phase);var tree=new BottomTree(this,0,neighbor,phase,other,true,true);
    var required=new float[BottomSurface];var height=new float[BottomSurface];
    // Lift the covering sheet rather than pushing the underlying sheet through it.
    var direction=Vector3.up;
    for(int id=24*129;id<BottomSurface;id++)if(mask[id]){
     var point=matrix.MultiplyPoint3x4(LayerBase(0,i,phase,id)+ExtraDeltaAt(0,i,phase,id));float d=tree.OutsideDistance(point,direction);
     if(d>-.015f&&d<.16f)required[id]=Mathf.Min(.16f,Mathf.Max(0,d+.018f));height[id]=required[id];
    }
    for(int pass=0;pass<140;pass++){var next=(float[])height.Clone();for(int y=24;y<=80;y++)for(int x=0;x<129;x++){
     int id=y*129+x;float sum=height[id]*2;int count=2;foreach(int n in new[]{y>24?id-129:-1,y<80?id+129:-1,x>0?id-1:-1,x<128?id+1:-1})if(n>=0){sum+=height[n];count++;}
     next[id]=Mathf.Max(required[id],sum/count);
    }height=next;}
    float maximum=0;for(int id=24*129;id<BottomSurface;id++){float h=height[id]*gate*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.4f,(id/129)/80f));frames[pose][id]=inverse.MultiplyVector(direction*h);maximum=Mathf.Max(maximum,frames[pose][id].magnitude);}
    report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3}\n",i,phase,contacts,maximum);
   }
  }
  // Commit together so all pairs are fitted against the same accepted surface.
  foreach(var patch in bottomPatches)if(patch.layer==0){var frames=additions[patch.index];for(int pose=0;pose<33;pose++)for(int k=0;k<patch.extraIds.Length;k++)frames[pose][patch.extraIds[k]]+=patch.extra[pose][k];
   CacheBytes-=patch.extraIds.Length*33L*12;var ids=new List<int>();for(int id=0;id<BottomSurface;id++){bool used=false;for(int pose=0;pose<33;pose++)used|=frames[pose][id].sqrMagnitude>1e-12f;if(used)ids.Add(id);}patch.extraIds=ids.ToArray();patch.extra=new Vector3[33][];
   for(int pose=0;pose<33;pose++){patch.extra[pose]=new Vector3[ids.Count];float radius=0;for(int k=0;k<ids.Count;k++){patch.extra[pose][k]=frames[pose][ids[k]];radius=Mathf.Max(radius,patch.extra[pose][k].magnitude);}patch.radius[pose]+=radius;}CacheBytes+=ids.Count*33L*12;
  }
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Outputs/Bake/Outer58"));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Regions.csv"),report.ToString());
 }
 void SmoothOuterPatches(){
  var report=new StringBuilder("petal,phase,maxAddedOffset,vertices\n");
  foreach(var patch in bottomPatches)if(patch.layer==0){
   var frames=new Vector3[33][];
   for(int pose=0;pose<33;pose++){
    float phase=pose/32f;var rest=new Vector3[BottomSurface];for(int id=0;id<BottomSurface;id++)rest[id]=LayerBase(0,patch.index,phase,id);
    for(int k=0;k<patch.extraIds.Length;k++)rest[patch.extraIds[k]]+=patch.extra[pose][k];
    var fair=(Vector3[])rest.Clone();var normal=new Vector3[BottomSurface];var matrix=BottomMatrix(0,patch.index,phase);var inverse=matrix.inverse;
    for(int y=12;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x;var across=rest[y*129+Mathf.Min(x+1,128)]-rest[y*129+Mathf.Max(x-1,0)];var along=rest[Mathf.Min(y+1,80)*129+x]-rest[Mathf.Max(y-1,0)*129+x];var n=Vector3.Cross(across,along).normalized;var point=matrix.MultiplyPoint3x4(rest[id]);if(Vector3.Dot(matrix.MultiplyVector(n),new Vector3(point.x,Mathf.Max(0,phase-.3f)*4,point.z))<0)n=-n;normal[id]=n;}
    for(int pass=0;pass<180;pass++){
     var next=(Vector3[])fair.Clone();
     for(int y=12;y<=80;y++)for(int x=0;x<129;x++){
      int id=y*129+x;var left=x>0?fair[id-1]:2*fair[id]-fair[id+1];var right=x<128?fair[id+1]:2*fair[id]-fair[id-1];Vector3 sum=left+right;float weight=2;if(y>12){sum+=fair[id-129];weight++;}if(y<80){sum+=fair[id+129];weight++;}
      var d=Vector3.Lerp(fair[id],sum/weight,.45f)-rest[id];float inward=Vector3.Dot(d,normal[id]);if(inward<0)d-=normal[id]*inward;
      next[id]=rest[id]+Vector3.ClampMagnitude(d,.06f);
     }fair=next;
    }
    frames[pose]=new Vector3[BottomSurface];for(int k=0;k<patch.extraIds.Length;k++)frames[pose][patch.extraIds[k]]=patch.extra[pose][k];float max=0;int active=0;
    float gate=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,phase));
    for(int y=12;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x;float along=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.35f,y/80f));float side=.3f+.7f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.7f,Mathf.Abs(x/64f-1)));var d=(fair[id]-rest[id])*gate*along*side;frames[pose][id]+=d;max=Mathf.Max(max,d.magnitude);if(d.sqrMagnitude>1e-12f)active++;}
    report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3}\n",patch.index,phase,max,active);
   }
   CacheBytes-=patch.extraIds.Length*33L*12;var ids=new List<int>();for(int id=0;id<BottomSurface;id++){bool used=false;for(int pose=0;pose<33;pose++)used|=frames[pose][id].sqrMagnitude>1e-12f;if(used)ids.Add(id);}patch.extraIds=ids.ToArray();patch.extra=new Vector3[33][];
   for(int pose=0;pose<33;pose++){patch.extra[pose]=new Vector3[ids.Count];float radius=0;foreach(var d in frames[pose])radius=Mathf.Max(radius,d.magnitude);patch.radius[pose]+=radius;for(int k=0;k<ids.Count;k++)patch.extra[pose][k]=frames[pose][ids[k]];}CacheBytes+=ids.Count*33L*12;
  }
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Outputs/Bake/OuterFlow"));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Flow.csv"),report.ToString());
 }
 void BuildLayerPatches(){
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();var report=new StringBuilder("layer,petal,neighbor,phase,contacts,vertices,limited,maxOffset\n");
  foreach(var patch in bottomPatches)if(patch.layer<2){
   int layer=patch.layer,i=patch.index,count=layers[layer].petals.Length,neighbor=(i+count-1)%count;
   int a=Array.FindIndex(scan.Petals,p=>p.layer==layer&&p.index==i),b=Array.FindIndex(scan.Petals,p=>p.layer==layer&&p.index==neighbor);
   var frames=new Vector3[33][];
   for(int pose=0;pose<33;pose++){
    float phase=pose/32f;frames[pose]=new Vector3[BottomSurface];if(pose==32)continue;scan.Sample(phase);scan.FindPairContacts(a,b,hits);
    var matrix=BottomMatrix(layer,i,phase);var other=BottomMatrix(layer,neighbor,phase);var inverse=matrix.inverse;var tree=new BottomTree(this,layer,neighbor,phase,other,true);var seeds=new HashSet<int>();var mask=new bool[BottomSurface];int limited=0,contacts=0;
    foreach(var h in hits){if(h.lengthA<.25f||h.rootRegion)continue;contacts++;int sx=Mathf.Clamp(Mathf.RoundToInt((h.widthA+1)*64),0,128),sy=Mathf.Clamp(Mathf.RoundToInt(h.lengthA*80),20,80);if(!seeds.Add((sy/3)*43+sx/3))continue;
     float t=h.lengthB,s=h.widthB;var across=other.MultiplyVector(BottomPoint(layer,neighbor,phase,t,s+.015625f)-BottomPoint(layer,neighbor,phase,t,s-.015625f));var along=other.MultiplyVector(BottomPoint(layer,neighbor,phase,t+.0125f,s)-BottomPoint(layer,neighbor,phase,t-.0125f,s));var normal=Vector3.Cross(across,along).normalized;var center=other.MultiplyPoint3x4(BottomPoint(layer,neighbor,phase,t,s));if(Vector3.Dot(normal,new Vector3(center.x,Mathf.Max(0,phase-.3f)*4,center.z))<0)normal=-normal;
     int x0=sx<22?0:Mathf.Max(0,sx-4),x1=sx>106?128:Mathf.Min(128,sx+4);
     for(int y=Mathf.Max(20,sy-4);y<=Mathf.Min(80,sy+4);y++)for(int x=x0;x<=x1;x++){
      int id=y*129+x;var point=matrix.MultiplyPoint3x4(LayerBase(layer,i,phase,id));float distance=tree.OutsideDistance(point,normal);if(distance<0||distance>.18f)continue;var delta=inverse.MultiplyVector(normal*(distance+.025f));if(delta.magnitude>.08f)limited++;delta=Vector3.ClampMagnitude(delta,layer==1?.16f:.08f)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.3f,y/80f));if(delta.sqrMagnitude>frames[pose][id].sqrMagnitude)frames[pose][id]=delta;mask[id]=true;
     }
    }
    // Broad smooth bending support replaces the previous two-cell hard feather.
    var ring=new int[BottomSurface];var queue=new Queue<int>();for(int id=0;id<BottomSurface;id++){ring[id]=mask[id]?0:-1;if(mask[id])queue.Enqueue(id);}
    while(queue.Count>0){int id=queue.Dequeue();if(ring[id]==(layer==0?2:12))continue;foreach(int n in new[]{id-129,id+129,id%129>0?id-1:-1,id%129<128?id+1:-1})if(n>=20*129&&n<BottomSurface&&ring[n]<0){ring[n]=ring[id]+1;queue.Enqueue(n);}}
    for(int pass=0;pass<(layer==0?3:60);pass++){var smooth=(Vector3[])frames[pose].Clone();for(int id=20*129;id<BottomSurface;id++)if(ring[id]>=0){Vector3 sum=frames[pose][id]*2;int n0=2;foreach(int n in new[]{id-129,id+129,id%129>0?id-1:-1,id%129<128?id+1:-1})if(n>=0&&n<BottomSurface){sum+=frames[pose][n];n0++;}smooth[id]=ring[id]==(layer==0?2:12)?Vector3.zero:sum/n0;}frames[pose]=smooth;}
    // Relax sharp local surface curvature in the middle ring without changing its transform.
    if(layer==1){var rest=new Vector3[BottomSurface];for(int id=0;id<BottomSurface;id++)rest[id]=LayerBase(layer,i,phase,id);var fair=(Vector3[])rest.Clone();for(int id=0;id<BottomSurface;id++){fair[id]+=frames[pose][id];float side=Mathf.Abs((id%129)/64f-1);float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,side));float along=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.6f,(id/129)/80f));fair[id].z+=(rest[(id/129)*129+64].z-rest[id].z)*Mathf.Lerp(.46f,.38f,Mathf.SmoothStep(0,1,(id%129)/128f))*edge*along;}
     for(int pass=0;pass<180;pass++){var next=(Vector3[])fair.Clone();for(int y=24;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x;Vector3 left=x>0?fair[id-1]:2*fair[id]-fair[id+1],right=x<128?fair[id+1]:2*fair[id]-fair[id-1];Vector3 sum=left+right;float weight=2;if(y>24){sum+=fair[id-129];weight++;}if(y<80){sum+=fair[id+129];weight++;}next[id]=Vector3.Lerp(fair[id],sum/weight,.45f);}fair=next;}
     float gate=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.25f,phase))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,.8f,phase)));
     for(int y=24;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x;float blend=gate*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.5f,y/80f));frames[pose][id]=Vector3.ClampMagnitude(Vector3.Lerp(frames[pose][id],fair[id]-rest[id],blend),.16f);}
    }
    int active=0;float maximum=0;foreach(var d in frames[pose])if(d.sqrMagnitude>1e-12f){active++;maximum=Mathf.Max(maximum,d.magnitude);}report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7}\n",layer,i,neighbor,phase,contacts,active,limited,maximum);
   }
   var ids=new List<int>();for(int id=0;id<BottomSurface;id++){bool used=false;for(int pose=0;pose<33;pose++)used|=frames[pose][id].sqrMagnitude>1e-12f;if(used)ids.Add(id);}patch.extraIds=ids.ToArray();patch.extra=new Vector3[33][];
   for(int pose=0;pose<33;pose++){patch.extra[pose]=new Vector3[ids.Count];float radius=0;for(int k=0;k<ids.Count;k++){var d=frames[pose][ids[k]];patch.extra[pose][k]=d;radius=Mathf.Max(radius,d.magnitude);}patch.radius[pose]+=radius;}CacheBytes+=ids.Count*33L*12;
  }
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Outputs/Bake/LayerOverlap"));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Regions.csv"),report.ToString());
 }

}
}
#endif
