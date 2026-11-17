#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 internal const float OpenCrossProbeTime=14f; // Captured regression pose; adjacent seams use the complete amplitude envelope.
 const float OpenGap=.018f,OpenLimit=.65f;
 internal bool OpenOrderPassed { get; private set; }
 internal string OpenOrderResult { get; private set; }
 // One directed seam per petal: +X (right) covers the next petal's -X (left).
 // Never sort by current height: doing so silently accepts an already inverted seam.
 sealed class OpenSheet {
  public BottomPatch patch;
  public Vector3[] rest,points;
  public Vector3 up;public Matrix4x4 frame;
  public float[] height;
  public float radius,roll;
  public OpenHull[] left;
  public Vector3[][] world;
 }
 // Conservative projected hull of the actual render grid. A leaf is ONE render cell,
 // and returns its upper bound, rather than a ray's nearest hit or a finite ray window.
 sealed class OpenHull {
  struct Cell { public Bounds box;public int left,right,x,y; }
  readonly List<Cell> cells=new List<Cell>();
  readonly Vector3[] v;
  public OpenHull(Vector3[] points,int x1) {
   v=points;
   Build(0,x1,8,80);
  }
  int Build(int x0,int x1,int y0,int y1) {
   int id=cells.Count;cells.Add(default);
   var cell=new Cell{left=-1,right=-1,x=x0,y=y0};
   if(x1-x0>1||y1-y0>1) {
    if(x1-x0>y1-y0){int m=(x0+x1)/2;cell.left=Build(x0,m,y0,y1);cell.right=Build(m,x1,y0,y1);}
    else {int m=(y0+y1)/2;cell.left=Build(x0,x1,y0,m);cell.right=Build(x0,x1,m,y1);}
    cell.box=cells[cell.left].box;cell.box.Encapsulate(cells[cell.right].box);
   } else {
    cell.box=new Bounds(v[y0*129+x0],Vector3.zero);
    cell.box.Encapsulate(v[y0*129+x1]);cell.box.Encapsulate(v[y1*129+x0]);cell.box.Encapsulate(v[y1*129+x1]);
   }
   cells[id]=cell;return id;
  }
  public float ExactHeight(Vector3 point)=>ExactHeight(0,point);
  float ExactHeight(int id,Vector3 p) {
   var c=cells[id];var lo=c.box.min;var hi=c.box.max;
   if(p.x<lo.x||p.x>hi.x||p.z<lo.z||p.z>hi.z)return float.NegativeInfinity;
   if(c.left>=0)return Mathf.Max(ExactHeight(c.left,p),ExactHeight(c.right,p));
   int a=c.y*129+c.x,b=a+129;
   return Mathf.Max(TriangleHeight(v[a],v[b],v[a+1],p),TriangleHeight(v[a+1],v[b],v[b+1],p));
  }
  static float TriangleHeight(Vector3 a,Vector3 b,Vector3 c,Vector3 p) {
   float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
   if(Mathf.Abs(det)<1e-12f)return float.NegativeInfinity;
   float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/det;
   float w=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/det;
   return u>=0&&w>=0&&u+w<=1?u*a.y+w*b.y+(1-u-w)*c.y:float.NegativeInfinity;
  }
  public float Height(Vector3 point,float radius)=>Height(0,point,radius);
  float Height(int id,Vector3 p,float r) {
   var c=cells[id];var lo=c.box.min;var hi=c.box.max;
   if(p.x+r<lo.x||p.x-r>hi.x||p.z+r<lo.z||p.z-r>hi.z)return float.NegativeInfinity;
   if(c.left<0||(p.x-r<=lo.x&&p.x+r>=hi.x&&p.z-r<=lo.z&&p.z+r>=hi.z))return hi.y;
   int first=c.left,second=c.right;
   if(cells[first].box.max.y<cells[second].box.max.y){first=c.right;second=c.left;}
   float top=Height(first,p,r);
   return cells[second].box.max.y<=top?top:Mathf.Max(top,Height(second,p,r));
  }
 }
 Vector3 OpenDirection(int layer,int index,float phase) {
  var matrix=OpenMatrix(layer,index,phase,0,0);
  var across=BottomPoint(layer,index,phase,.5f,.015625f)-BottomPoint(layer,index,phase,.5f,-.015625f);
  var along=BottomPoint(layer,index,phase,.5125f,0)-BottomPoint(layer,index,phase,.4875f,0);
  var normal=matrix.MultiplyVector(Vector3.Cross(across,along)).normalized;
  var center=matrix.MultiplyPoint3x4(BottomPoint(layer,index,phase,.5f,0));
  if(Vector3.Dot(normal,new Vector3(center.x,Mathf.Max(0,phase-.2f)*5,center.z))<0)normal=-normal;
  return normal;
 }
 Matrix4x4 OpenMatrix(int layer,int index,float phase,float tilt,float roll) {
  var l=layers[layer];var p=l.petals[index];
  PathPetalPose(l,p,index,StandardLayerProgress(l.config,phase),out var position,out var rotation,out var scale);
  rotation*=Quaternion.Euler(tilt,0,roll);
  position.y=layers[0].config.rootHeight+.0045f+l.config.rootStagger*RootStaggerSign(index,l.petals.Length);
  return Matrix4x4.TRS(position,rotation,scale)*Matrix4x4.Translate(-BottomPoint(layer,index,phase,0,0));
 }
 float OpenRoll(int layer,int index)=>artLooks[2].asym*Mathf.Sin((index+1)*1.7f+layer*.9f);
 // Levels 0/1/2 partition the COMPLETE independent tilt interval. Radius bounds
 // cover every intermediate Wandering rigid pose. Standard poses are fitted separately.
 static int OpenNode(int depth,int bin)=>(1<<depth)-1+bin;
 float MotionRadius(OpenSheet s,int depth) {
  float halfAngle=Mathf.Abs(artProfiles[2].amplitude)/(1<<depth);
  return s.radius*2*Mathf.Sin(halfAngle*Mathf.Deg2Rad*.5f);
 }
 void BuildOpenHulls(OpenSheet s,float phase,Matrix4x4 coveringFrame) {
  s.left=new OpenHull[16];s.world=new Vector3[17][];
  for(int depth=0;depth<=3;depth++)for(int bin=0;bin<(1<<depth);bin++) {
   int node=OpenNode(depth,bin);float tilt=Mathf.Lerp(-artProfiles[2].amplitude,artProfiles[2].amplitude,(bin+.5f)/(1<<depth));
   var matrix=OpenMatrix(s.patch.layer,s.patch.index,phase,tilt,s.roll);
   s.world[node]=new Vector3[BottomSurface];
   for(int id=0;id<BottomSurface;id++)s.world[node][id]=matrix.MultiplyPoint3x4(s.points[id]);
   var projected=new Vector3[BottomSurface];for(int id=0;id<BottomSurface;id++)projected[id]=coveringFrame.MultiplyPoint3x4(s.world[node][id]);
   s.left[node]=new OpenHull(projected,64);
  }
  var standard=OpenMatrix(s.patch.layer,s.patch.index,phase,0,0);s.world[15]=new Vector3[BottomSurface];
  for(int id=0;id<BottomSurface;id++)s.world[15][id]=standard.MultiplyPoint3x4(s.points[id]);
  var standardProjected=new Vector3[BottomSurface];for(int id=0;id<BottomSurface;id++)standardProjected[id]=coveringFrame.MultiplyPoint3x4(s.world[15][id]);
  s.left[15]=new OpenHull(standardProjected,64);
  PetalArtMotion(new Vector4(0,0,1,0),OpenCrossProbeTime,s.patch.index*Mathf.PI*2/layers[s.patch.layer].petals.Length,s.patch.layer,s.patch.index,out float probeTilt,out float probeRoll);
  var probe=OpenMatrix(s.patch.layer,s.patch.index,phase,probeTilt,probeRoll);s.world[16]=new Vector3[BottomSurface];
  for(int id=0;id<BottomSurface;id++)s.world[16][id]=probe.MultiplyPoint3x4(s.points[id]);
 }
 float OpenDemand(OpenSheet a,OpenSheet b,int id,float phase,int da=0,int ia=0,int db=0,int ib=0) {
  var point=a.world[OpenNode(da,ia)][id];
  point=a.frame.MultiplyPoint3x4(point);
  float ra=MotionRadius(a,da),rb=MotionRadius(b,db);
  float top=b.left[OpenNode(db,ib)].Height(point,ra+rb);
  if(float.IsNegativeInfinity(top))return 0;
  float demand=top+ra+rb+OpenGap-point.y;
  if(demand<=0)return 0; // This whole angular rectangle already has clearance.
  if(da==3&&db==3)return demand;
  if(da<=db&&da<3)return Mathf.Max(OpenDemand(a,b,id,phase,da+1,ia*2,db,ib),OpenDemand(a,b,id,phase,da+1,ia*2+1,db,ib));
  return Mathf.Max(OpenDemand(a,b,id,phase,da,ia,db+1,ib*2),OpenDemand(a,b,id,phase,da,ia,db+1,ib*2+1));
 }
 internal int InspectOpenOrder(out float minimum) {
  minimum=float.PositiveInfinity;int failures=0;
  for(int l=0;l<layers.Length;l++) {
   var points=new Vector3[layers[l].petals.Length][];
   for(int i=0;i<points.Length;i++) {
    var mesh=PetalMesh(l,i).vertices;points[i]=new Vector3[BottomSurface];
    var matrix=generated.worldToLocalMatrix*layers[l].petals[i].body.localToWorldMatrix;
    for(int id=0;id<BottomSurface;id++)points[i][id]=matrix.MultiplyPoint3x4((mesh[id]+mesh[id+BottomSurface])*.5f);

   }
   for(int i=0;i<points.Length;i++) {
    var frame=Matrix4x4.Rotate(Quaternion.FromToRotation(OpenDirection(l,i,bloomProgress),Vector3.up));
    int neighbor=(i+1)%points.Length;
    var projected=new Vector3[BottomSurface];
    for(int id=0;id<BottomSurface;id++)projected[id]=frame.MultiplyPoint3x4(points[neighbor][id]);
    var hull=new OpenHull(projected,64);
    for(int y=16;y<=80;y+=4)for(int x=64;x<=128;x+=2) {
    var point=frame.MultiplyPoint3x4(points[i][y*129+x]);float top=hull.ExactHeight(point);
    if(float.IsNegativeInfinity(top))continue;
    float gap=point.y-top;minimum=Mathf.Min(minimum,gap);if(gap<.001f)failures++;
   }
   }
  }
  return failures;
 }
 internal GameObject OpenSeamMarkers() {
  var host=new GameObject("Seam markers: green RIGHT covers next red LEFT");
  for(int l=0;l<layers.Length;l++)for(int i=0;i<layers[l].petals.Length;i++) {
   var vertices=PetalMesh(l,i).vertices;
   foreach(int x in new[]{0,128}) {
    var mark=GameObject.CreatePrimitive(PrimitiveType.Sphere);mark.name=l+"-"+i+(x==0?" LEFT (covered)":" RIGHT (covering)");
    mark.transform.SetParent(host.transform);mark.transform.position=layers[l].petals[i].body.TransformPoint(vertices[56*129+x]);
    mark.transform.localScale=Vector3.one*.045f;Destroy(mark.GetComponent<Collider>());
    var material=new Material(Shader.Find("Unlit/Color"));material.color=x==0?Color.red:Color.green;mark.GetComponent<Renderer>().sharedMaterial=material;
   }
  }
  return host;
 }
 HashSet<string> OpenCrossContacts(out int self,float time=-1) {
  self=0;var pairs=new HashSet<string>();var scan=new FlowerSurfaceCollision(this);
  var hits=new List<FlowerSurfaceCollision.Contact>();
  foreach(float phase in new[]{.875f,.9375f,1f}) {
   if(time<0)scan.Sample(phase);
   else {
    var positions=new Vector3[scan.Petals.Length][];
    for(int p=0;p<positions.Length;p++) {
     var petal=scan.Petals[p];positions[p]=new Vector3[BottomSurface];SampleDiagnosticSurface(petal.layer,petal.index,phase,positions[p]);
     PetalArtMotion(new Vector4(0,0,1,0),time,petal.index*Mathf.PI*2/layers[petal.layer].petals.Length,petal.layer,petal.index,out float tilt,out float roll);
     var matrix=OpenMatrix(petal.layer,petal.index,phase,tilt,roll);
     for(int id=0;id<BottomSurface;id++)positions[p][id]=matrix.MultiplyPoint3x4(positions[p][id]);
    }
    scan.SamplePositions(positions);
   }
   scan.FindContacts(hits,parallel:true);
   foreach(var h in hits)if(!h.rootRegion) {
    if(h.petalA==h.petalB)self++;
    else if(scan.Petals[h.petalA].layer!=scan.Petals[h.petalB].layer)pairs.Add(h.petalA+":"+h.petalB);
   }
  }
  return pairs;
 }
 internal void BuildOpenClearancePatches() {
  EnsureArtLooks();OpenOrderPassed=false;
  Directory.CreateDirectory("Outputs/OpenClearance");
  var report=new List<string>{"phase,round,maxRequiredIncrement,limited,newCrossPairs,selfPairs"};
  var baselineCross=OpenCrossContacts(out int baselineSelf);
  baselineCross.UnionWith(OpenCrossContacts(out int probeSelf,OpenCrossProbeTime));baselineSelf+=probeSelf;
  var patches=bottomPatches.ToArray();
  var offsets=new Dictionary<BottomPatch,Vector3[][]>();
  foreach(var p in patches){var f=new Vector3[33][];for(int i=0;i<33;i++)f[i]=new Vector3[BottomSurface];offsets.Add(p,f);}
  bool feasible=true;
  var proposalScan=new FlowerSurfaceCollision(this);var proposalHits=new List<FlowerSurfaceCollision.Contact>();
  for(int pose=28;pose<=32;pose++) {
   float phase=pose/32f;var sheets=new OpenSheet[layers.Length][];
   for(int l=0;l<layers.Length;l++) {
    sheets[l]=new OpenSheet[layers[l].petals.Length];
    for(int i=0;i<sheets[l].Length;i++) {
     var patch=Array.Find(patches,p=>p.layer==l&&p.index==i);
     if(patch==null)throw new InvalidOperationException("Missing overlap patch "+l+"-"+i);
     var s=new OpenSheet{patch=patch,rest=new Vector3[BottomSurface],points=new Vector3[BottomSurface],height=new float[BottomSurface],roll=OpenRoll(l,i)};
     var matrix=OpenMatrix(l,i,phase,0,0);var normal=OpenDirection(l,i,phase);s.up=matrix.inverse.MultiplyVector(normal);s.frame=Matrix4x4.Rotate(Quaternion.FromToRotation(normal,Vector3.up));
     for(int id=0;id<BottomSurface;id++)s.rest[id]=LayerBase(l,i,phase,id)+ExtraDeltaAt(l,i,phase,id);
     sheets[l][i]=s;
    }
   }
   bool accepted=false,hitLimit=false;
   for(int round=0;round<24;round++) {
    foreach(var row in sheets)foreach(var s in row) {
     s.radius=0;
     var root=BottomPoint(s.patch.layer,s.patch.index,phase,0,0);
     var scale=OpenMatrix(s.patch.layer,s.patch.index,phase,0,0);
     var origin=scale.MultiplyPoint3x4(root);
     for(int id=0;id<BottomSurface;id++){s.points[id]=s.rest[id]+s.up*s.height[id];s.radius=Mathf.Max(s.radius,Vector3.Distance(scale.MultiplyPoint3x4(s.points[id]),origin));}
     var layerSheets=sheets[s.patch.layer];
     BuildOpenHulls(s,phase,layerSheets[(s.patch.index+layerSheets.Length-1)%layerSheets.Length].frame);
    }
    var proposalPositions=new Vector3[proposalScan.Petals.Length][];
    var crossSeeds=new Dictionary<OpenSheet,List<Vector2>>();
    int newCross=0,newSelf=0;
    foreach(int sample in new[]{15,16}) {
    for(int p=0;p<proposalPositions.Length;p++){var petal=proposalScan.Petals[p];proposalPositions[p]=sheets[petal.layer][petal.index].world[sample];}
    proposalScan.SamplePositions(proposalPositions);proposalScan.FindContacts(proposalHits,parallel:true);
    foreach(var hit in proposalHits)if(!hit.rootRegion) {
     if(hit.petalA==hit.petalB){newSelf++;continue;}
     var a=proposalScan.Petals[hit.petalA];var b=proposalScan.Petals[hit.petalB];
     if(a.layer==b.layer||baselineCross.Contains(hit.petalA+":"+hit.petalB))continue;
     newCross++;
     bool useA=a.layer>b.layer;var target=useA?a:b;
     // Keep the usual inner-ring cover at NEW contacts only, without ranking whole rings.
     var sheet=sheets[target.layer][target.index];
     if(!crossSeeds.TryGetValue(sheet,out var seeds)){seeds=new List<Vector2>();crossSeeds.Add(sheet,seeds);}
     seeds.Add(new Vector2(((useA?hit.widthA:hit.widthB)+1)*64,(useA?hit.lengthA:hit.lengthB)*80));
    }
    }
    var proposals=new Dictionary<OpenSheet,float[]>();float maximum=0;bool limited=false;
    var work=new List<OpenSheet>();foreach(var row in sheets)work.AddRange(row);
    var sync=new object();var pairReports=new System.Collections.Concurrent.ConcurrentQueue<string>();
    System.Threading.Tasks.Parallel.ForEach(work,new System.Threading.Tasks.ParallelOptions{MaxDegreeOfParallelism=4},s=> {
     int l=s.patch.layer,i=s.patch.index;
     var next=(float[])s.height.Clone();lock(sync)proposals.Add(s,next);
     float localMaximum=0;bool localLimited=false;
     // Certify at every render vertex in the free overlap band, including the tip.
     for(int y=8;y<=80;y++)for(int x=0;x<129;x++) {
      int id=y*129+x;float demand=0;
      if(x>=64&&y>=16) {
       var neighbor=sheets[l][(i+1)%sheets[l].Length];
       demand=OpenDemand(s,neighbor,id,phase);
       var point=s.frame.MultiplyPoint3x4(s.world[15][id]);float top=neighbor.left[15].Height(point,0);
       if(!float.IsNegativeInfinity(top))demand=Mathf.Max(demand,top+OpenGap-point.y);
      }
      // Cross-ring intersections are rejected against the accepted baseline below.
      localMaximum=Mathf.Max(localMaximum,demand);
      if(demand>.0005f) {
       float value=s.height[id]+demand/Mathf.Cos((Mathf.Abs(artProfiles[2].amplitude)+Mathf.Abs(s.roll))*Mathf.Deg2Rad);
       localLimited|=value>OpenLimit;next[id]=Mathf.Min(OpenLimit,value);
      }
     }
     if(crossSeeds.TryGetValue(s,out var seeds))foreach(var seed in seeds) {
      for(int y=8;y<=80;y++)for(int x=0;x<129;x++) {
       float dx=(x-seed.x)/26f,dy=(y-seed.y)/24f;
       float bump=.20f*Mathf.Exp(-(dx*dx+dy*dy)*2)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.2f,y/80f));
       int id=y*129+x;float target=s.height[id]+bump;
       localLimited|=target>OpenLimit;next[id]=Mathf.Max(next[id],Mathf.Min(OpenLimit,target));
      }
     }
     // A broad support avoids needle-like height peaks; roots stay unchanged.
     for(int pass=0;pass<80;pass++) {
      var smooth=(float[])next.Clone();
      for(int y=8;y<=80;y++)for(int x=0;x<129;x++) {
       int id=y*129+x;float sum=next[id]*2;int n=2;
       if(y>8){sum+=next[id-129];n++;}if(y<80){sum+=next[id+129];n++;}
       if(x>0){sum+=next[id-1];n++;}if(x<128){sum+=next[id+1];n++;}
       float support=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.2f,y/80f));
       // Same-layer corrections must never leak into the covered left edge.
       support*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.5f,0,x/64f-1));
       smooth[id]=Mathf.Max(next[id],sum/n*support);
      }
      next=smooth;
     }
     pairReports.Enqueue("OPEN_PAIR phase="+phase+" round="+round+" petal="+l+"-"+i+" max="+localMaximum);
     lock(sync){maximum=Mathf.Max(maximum,localMaximum);limited|=localLimited;proposals[s]=next;}
    });
    foreach(var line in pairReports)Debug.Log(line);
    hitLimit|=limited;
    report.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5}",phase,round,maximum,limited,newCross,newSelf));
    Debug.Log("OPEN_ORDER phase="+phase+" round="+round+" residual="+maximum+" limited="+limited+" cross="+newCross+" self="+newSelf);
    if(maximum<=.0005f&&newCross==0&&newSelf<=baselineSelf){accepted=true;break;}
    foreach(var proposal in proposals)proposal.Key.height=proposal.Value;
   }
   if(!accepted&&hitLimit) {
    OpenOrderResult="FAIL: phase="+phase+" reached the "+OpenLimit+" displacement limit without satisfying directed clearance. Accepted baseline retained; no cache writes.";
    File.WriteAllLines("Outputs/OpenClearance/Solver.csv",report);
    File.WriteAllText("Outputs/OpenClearance/OrderResult.txt",OpenOrderResult);
    if(!FlowerOpenClearancePreview.Requested)throw new InvalidOperationException(OpenOrderResult);
    return; // Offsets are still private proposals; no accepted patch/mesh has been changed.
   }
   feasible&=accepted;
   foreach(var row in sheets)foreach(var s in row)
    for(int id=0;id<BottomSurface;id++)offsets[s.patch][pose][id]=s.up*s.height[id];
  }
  // Feather only the entry band. The fully constrained region starts at 87.5% bloom.
  foreach(var entry in offsets) {
   for(int pose=25;pose<28;pose++){float gate=Mathf.SmoothStep(0,1,(pose-24)/4f);for(int id=0;id<BottomSurface;id++)entry.Value[pose][id]=entry.Value[28][id]*gate;}
   var p=entry.Key;var frames=entry.Value;
   for(int pose=0;pose<33;pose++)for(int k=0;k<p.extraIds.Length;k++)frames[pose][p.extraIds[k]]+=p.extra[pose][k];
   CacheBytes-=p.extraIds.Length*33L*12;var ids=new List<int>();
   for(int id=0;id<BottomSurface;id++){bool used=false;for(int pose=0;pose<33&&!used;pose++)used=frames[pose][id].sqrMagnitude>1e-12f;if(used)ids.Add(id);}
   p.extraIds=ids.ToArray();p.extra=new Vector3[33][];
   for(int pose=0;pose<33;pose++){p.extra[pose]=new Vector3[ids.Count];float radius=0;for(int k=0;k<ids.Count;k++){p.extra[pose][k]=frames[pose][ids[k]];radius=Mathf.Max(radius,p.extra[pose][k].magnitude);}p.radius[pose]+=radius;}
   CacheBytes+=ids.Count*33L*12;
  }
  bottomProgress=-1;ApplyBottomPatches();
  var finalCross=OpenCrossContacts(out int finalSelf);finalCross.UnionWith(OpenCrossContacts(out int finalProbeSelf,OpenCrossProbeTime));finalSelf+=finalProbeSelf;finalCross.ExceptWith(baselineCross);
  feasible&=finalCross.Count==0&&finalSelf<=baselineSelf;
  report.Add("crossRingAdded="+finalCross.Count+" selfBefore="+baselineSelf+" selfAfter="+finalSelf);
  OpenOrderPassed=feasible;
  OpenOrderResult=feasible?"PASS: directed clearance at render vertices over independent tilt intervals (87.5%-100% nodes; standard poses included); interpolation/runtime checks reported separately.":"FAIL: directed clearance did not converge within twenty-four rounds, displacement limit, or new cross-ring/self contacts; candidate is NOT approved for baking. Check Solver.csv for cross-ring/self checks.";
  File.WriteAllLines("Outputs/OpenClearance/Solver.csv",report);
  File.WriteAllText("Outputs/OpenClearance/OrderResult.txt",OpenOrderResult);
  if(!feasible&&!FlowerOpenClearancePreview.Requested)throw new InvalidOperationException(OpenOrderResult);
 }
}
}
#endif
