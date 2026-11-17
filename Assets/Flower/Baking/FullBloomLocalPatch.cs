#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 // Standard full bloom: L1-P3 left tip crosses L0-P3. No other petal is moved.
 internal void RestoreFullLocalBaseline(byte[] bytes){ReleaseBottomPatches();if(!LoadBottomCache(bytes))throw new InvalidOperationException("Baseline load failed");bottomProgress=-1;ApplyBottomPatches();}
 internal void BuildFullLocalPatch() {
  var patch=bottomPatches.Find(p=>p.layer==1&&p.index==3);
  if(patch==null)throw new InvalidOperationException("L1-P3 compensation missing");
  var frames=new Vector3[33][];
  for(int pose=0;pose<33;pose++){
   frames[pose]=new Vector3[BottomSurface];float phase=pose/32f;
   float gate=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6875f,.75f,phase));if(gate<=0)continue;
   var matrix=BottomMatrix(1,3,phase);var tree=new BottomTree(this,0,3,phase,BottomMatrix(0,3,phase),true,true);
   var required=new float[BottomSurface];
   for(int y=8;y<=80;y++)for(int x=0;x<=64;x++){
    int id=y*129+x;var point=matrix.MultiplyPoint3x4(LayerBase(1,3,phase,id)+ExtraDeltaAt(1,3,phase,id));
    float d=tree.OutsideDistance(point,Vector3.up);
    if(d>-.02f&&d<.18f)required[id]=Mathf.Max(0,d+.025f)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,16,y));
   }
   for(int y=8;y<=80;y++)for(int x=0;x<=64;x++){
    int id=y*129+x;float sx=(x-22)/24f;
    required[id]+=.06f*Mathf.Exp(-sx*sx*2)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,24,y));
   }
   var height=(float[])required.Clone();
   for(int pass=0;pass<140;pass++){
    var next=(float[])height.Clone();
    for(int y=8;y<=80;y++)for(int x=0;x<=88;x++){
     int id=y*129+x;float sum=height[id]*2;int n=2;
     if(y>8){sum+=height[id-129];n++;}if(y<80){sum+=height[id+129];n++;}
     if(x>0){sum+=height[id-1];n++;}if(x<88){sum+=height[id+1];n++;}
     next[id]=y==8?0:Mathf.Max(required[id],sum/n);
    }height=next;
   }
   var up=matrix.inverse.MultiplyVector(Vector3.up);
   for(int id=8*129;id<BottomSurface;id++)frames[pose][id]=up*(height[id]*gate);
  }
  for(int pose=0;pose<33;pose++)for(int k=0;k<patch.extraIds.Length;k++)frames[pose][patch.extraIds[k]]+=patch.extra[pose][k];
  CacheBytes-=patch.extraIds.Length*33L*12;var ids=new List<int>();
  for(int id=0;id<BottomSurface;id++){bool used=false;for(int pose=0;pose<33&&!used;pose++)used=frames[pose][id].sqrMagnitude>1e-12f;if(used)ids.Add(id);}
  patch.extraIds=ids.ToArray();patch.extra=new Vector3[33][];
  for(int pose=0;pose<33;pose++){patch.extra[pose]=new Vector3[ids.Count];float radius=0;for(int k=0;k<ids.Count;k++){patch.extra[pose][k]=frames[pose][ids[k]];radius=Mathf.Max(radius,patch.extra[pose][k].magnitude);}patch.radius[pose]+=radius;}
  CacheBytes+=ids.Count*33L*12;bottomProgress=-1;ApplyBottomPatches();
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  for(int step=0;step<=22;step++){float phase=.78f+step*.01f;scan.Sample(phase);scan.FindPairContacts(3,10,hits);
  if(hits.Exists(h=>h.lengthB>=.25f))throw new InvalidOperationException("L1-P3 / L0-P3 intersection at "+phase);}
  // The fixed/attached lower quarter already overlaps in the accepted baseline.

 }
}
}
#endif
