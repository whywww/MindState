using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 Vector3 UpperDeltaAt(int layer,int index,float phase,int id){foreach(var patch in bottomPatches)if(patch.layer==layer&&patch.index==index){int k=Array.BinarySearch(patch.upperIds,id);if(k<0||patch.upper==null)return Vector3.zero;float q=phase*32;int a=Mathf.Min((int)q,31);return Vector3.Lerp(patch.upper[a][k],patch.upper[a+1][k],q-a);}return Vector3.zero;}
 internal void LayerPatchPreview(bool enabled){extraPreview=enabled;bottomProgress=-1;ApplyBottomPatches();}
#if UNITY_EDITOR
 Vector3 LayerBase(int l,int i,float phase,int id)=>BottomPoint(l,i,phase,(id/129)/80f,(id%129)/64f-1)+BottomDeltaAt(l,i,phase,id)+UpperDeltaAt(l,i,phase,id);
 // Fair the complete accepted outer surface. The obstacle constraint retains the
 // existing outward clearance instead of flattening a raised edge back into its neighbour.
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
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/OuterFlow/Bake"));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Flow.csv"),report.ToString());
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
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/LayerOverlap/Bake"));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Regions.csv"),report.ToString());
 }
#endif
}}
