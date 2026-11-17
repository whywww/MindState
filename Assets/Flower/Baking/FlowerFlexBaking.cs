#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 static readonly int[] FlexNeighbours={3,9,2,16};
 static readonly float[] FlexSigns={1,-1,1,-1};
 void SetFlexPose(Vector3 parameter,float correctedTilt){
  foreach(int id in new[]{3,9,10}){int l=id==3?0:1,i=id==3?3:id==9?2:3;var layer=layers[l];var item=layer.petals[i];float tilt=id==3?parameter.x:id==9?parameter.z:correctedTilt;PetalArtMotion(new Vector4(0,0,1,0),5,i*Mathf.PI*2/layer.petals.Length,l,i,out _,out float roll);PathPetalPose(layer,item,i,1,out var pos,out var rot,out var scale);ApplyPetalPose(layer,item,i,pos,rot*Quaternion.Euler(tilt,0,roll),scale);}
 }
 void SaveFlex(string path,FlexData data){
  Directory.CreateDirectory(Path.GetDirectoryName(path));using(var w=new BinaryWriter(File.Create(path))){w.Write(FlexFormat);w.Write(BottomConfigHash());w.Write(FlexBaseSignature());w.Write(FlexMotionSignature());w.Write(data.amplitude);w.Write(data.roll);w.Write(data.vertices);foreach(var n in data.normals)WriteVector(w,n);foreach(int id in data.wall)w.Write(id);w.Write(data.frames.Length);foreach(var frame in data.frames){WriteVector(w,frame.parameter);w.Write(frame.tilt);foreach(float c in frame.controls)w.Write(c);}w.Write(data.nodes.Length);foreach(var node in data.nodes){WriteVector(w,node.min);WriteVector(w,node.max);foreach(int c in node.corners)w.Write(c);w.Write(node.children!=null);if(node.children!=null)foreach(int c in node.children)w.Write(c);}}
 }
 internal void BuildFlexCandidate(){
  bool retain=Array.IndexOf(Environment.GetCommandLineArgs(),"-retainFlexCandidate")>=0;var retainedFailures=new List<string>();
  EnsureArtLooks();SetBloomProgress(1);var patch=bottomPatches.Find(p=>p.layer==1&&p.index==3);ApplyBottomPatch(patch);
  var data=new FlexData{amplitude=Mathf.Abs(artProfiles[2].amplitude),roll=artLooks[2].asym,vertices=patch.work.Length,normals=SmoothFlexNormals(patch.work),wall=(int[])patch.wall.Clone()};
  var frames=new List<FlexFrame>();var indices=new Dictionary<Vector3,int>();var masks=new bool[4][];for(int i=0;i<4;i++)masks[i]=new bool[BottomSurface];
  var buildReport=new List<string>{"frame,lower,requestedUpper,neighbour,correctedUpper"};
  // In the retained candidate the upper tilt follows the lower exactly.
  // Requested upper tilt therefore does not affect solved geometry.
  var retainedSolutions=new Dictionary<Vector2,FlexFrame>();
  SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(5);
  FlexFrame SolveNew(Vector3 parameter,int label){
   var retainedKey=new Vector2(parameter.x,parameter.z);
   if(retain&&retainedSolutions.TryGetValue(retainedKey,out var reused))return new FlexFrame{parameter=parameter,tilt=reused.tilt,controls=(float[])reused.controls.Clone()};
   FlexFrame best=null;float lastFailed=1,bestFraction=0;
   foreach(float fraction in (retain?new[]{0f}:new[]{1f,0f})){bool ok=SolveFlexPose(200+label,fraction,parameter,renderedContacts:retain);if(!ok&&retain){retainedFailures.Add("sample="+label+" parameter="+parameter+"; residual contact/order or displacement-limit failure; retained by explicit user instruction");Debug.LogWarning("FLEX_RETAINED_FAILURE "+parameter);}if(ok||retain){best=new FlexFrame{parameter=parameter,tilt=flexSolvedTilt,controls=(float[])flexSolvedControls.Clone()};bestFraction=fraction;for(int n=0;n<4;n++)for(int id=0;id<BottomSurface;id++)masks[n][id]|=flexSolvedMasks[n][id];break;}lastFailed=fraction;}
   if(best==null)throw new InvalidOperationException("Flex parameter sample has no accepted candidate: "+parameter+"; no formal cache published.");
   if(retain)retainedSolutions[retainedKey]=best;
   File.WriteAllLines("Outputs/WanderFlex/Controls"+(200+label)+".csv",Array.ConvertAll(best.controls,c=>c.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));return best;
  }
  string FrameRow(int index,FlexFrame frame)=>index+","+frame.parameter.x+","+frame.parameter.y+","+frame.parameter.z+","+frame.tilt;
  int Resolve(Vector3 parameter){if(indices.TryGetValue(parameter,out int found))return found;int index=frames.Count;var best=SolveNew(parameter,index);frames.Add(best);indices.Add(parameter,index);buildReport.Add(FrameRow(index,best));File.WriteAllLines("Outputs/WanderFlex/Build.csv",buildReport);Debug.Log("FLEX_FRAME "+index+" "+parameter+" corrected="+best.tilt);return index;}
  // Reuse previously accepted numerical solutions; rebuild their contact-band
  // coverage by inspecting the whole linear path from rest to each solution.
  if(!retain&&File.Exists("Outputs/WanderFlex/Checkpoint/Build.csv")){
   var invariant=System.Globalization.CultureInfo.InvariantCulture;
   foreach(var line in File.ReadAllLines("Outputs/WanderFlex/Checkpoint/Build.csv")){
    if(line.StartsWith("frame,"))continue;var parts=line.Split(',');int index=int.Parse(parts[0]);if(index!=frames.Count)throw new InvalidDataException("Unordered flex checkpoint");var parameter=new Vector3(float.Parse(parts[1],invariant),float.Parse(parts[2],invariant),float.Parse(parts[3],invariant));var controls=Array.ConvertAll(File.ReadAllLines("Outputs/WanderFlex/Checkpoint/Controls"+(200+index)+".csv"),v=>float.Parse(v,invariant));if(controls.Length!=FlexField.Count)throw new InvalidDataException("Checkpoint topology mismatch");frames.Add(new FlexFrame{parameter=parameter,tilt=float.Parse(parts[4],invariant),controls=controls});indices.Add(parameter,index);buildReport.Add(line);
   }
   var basis=new FlexField();var hits=new List<FlowerSurfaceCollision.Contact>();
   for(int frameIndex=0;frameIndex<frames.Count;frameIndex++){
    var frame=frames[frameIndex];basis.controls=frame.controls;var localMasks=new bool[4][];for(int n=0;n<4;n++)localMasks[n]=new bool[BottomSurface];
    foreach(float fraction in new[]{0f,.5f,1f}){var c=UpdateFlexContext(data,frame.parameter,frame.tilt,basis,fraction);for(int n=0;n<4;n++){int other=FlexNeighbours[n];c.scan.FindPairContacts(Mathf.Min(other,10),Mathf.Max(other,10),hits);foreach(var h in hits){float t=other<10?h.lengthB:h.lengthA,side=other<10?h.widthB:h.widthA,ot=other<10?h.lengthA:h.lengthB;if(t<.25f||(n!=0&&ot<.25f))continue;int sx=Mathf.RoundToInt((side+1)*64),sy=Mathf.RoundToInt(t*80);for(int y=Mathf.Max(20,sy-8);y<=Mathf.Min(80,sy+8);y++)for(int x=Mathf.Max(0,sx-12);x<=Mathf.Min(128,sx+12);x++)localMasks[n][y*129+x]=true;}}}
    if(!TestFlexFrame(data,frame.parameter,frame.tilt,basis,localMasks)){var replacement=SolveNew(frame.parameter,frameIndex);frame.tilt=replacement.tilt;frame.controls=replacement.controls;buildReport[frameIndex+1]=FrameRow(frameIndex,frame);Debug.Log("FLEX_REPAIRED "+frameIndex);}
    for(int n=0;n<4;n++)for(int id=0;id<BottomSurface;id++)masks[n][id]|=localMasks[n][id];
    File.WriteAllLines("Outputs/WanderFlex/Controls"+(200+frameIndex)+".csv",Array.ConvertAll(frame.controls,c=>c.ToString("R",invariant)));File.WriteAllLines("Outputs/WanderFlex/Build.csv",buildReport);
    if(frameIndex%40==0)Debug.Log("FLEX_RESUME_MASK "+frameIndex+"/"+frames.Count);
   }
   Debug.Log("FLEX_RESUMED "+frames.Count);
  }
  File.WriteAllLines("Outputs/WanderFlex/Build.csv",buildReport);
  var tester=new FlexField();bool passed=false;
  // Uniform dyadic refinement keeps shared faces identical. An independently
  // refined octree would introduce discontinuities at hanging-node boundaries.
  for(int depth=1;depth<=3;depth++){
   var nodes=new List<FlexNode>();
   Func<Vector3,Vector3,int,int> cell=null;cell=(min,max,remaining)=>{int index=nodes.Count;var node=new FlexNode{min=min,max=max,corners=new int[8]};nodes.Add(node);for(int c=0;c<8;c++)node.corners[c]=Resolve(new Vector3((c&1)!=0?max.x:min.x,(c&2)!=0?max.y:min.y,(c&4)!=0?max.z:min.z));if(remaining>0){node.children=new int[8];var mid=(min+max)*.5f;for(int c=0;c<8;c++){var lo=new Vector3((c&1)!=0?mid.x:min.x,(c&2)!=0?mid.y:min.y,(c&4)!=0?mid.z:min.z);var hi=new Vector3((c&1)!=0?max.x:mid.x,(c&2)!=0?max.y:mid.y,(c&4)!=0?max.z:mid.z);node.children[c]=cell(lo,hi,remaining-1);}}return index;};
   float a=data.amplitude;float droop=artLooks[2].droop;var centre=new Vector3(droop*1.2f,droop*.7f,droop*.7f);cell(centre-Vector3.one*a,centre+Vector3.one*a,depth);data.frames=frames.ToArray();data.nodes=nodes.ToArray();passed=true;
   foreach(var node in data.nodes)if(node.children==null){for(int z=0;z<=2&&passed;z++)for(int y=0;y<=2&&passed;y++)for(int x=0;x<=2&&passed;x++){if(x!=1&&y!=1&&z!=1)continue;var p=new Vector3(Mathf.Lerp(node.min.x,node.max.x,x*.5f),Mathf.Lerp(node.min.y,node.max.y,y*.5f),Mathf.Lerp(node.min.z,node.max.z,z*.5f));QueryFlex(data,p,tester.controls,out float tilt);if(!TestFlexFrame(data,p,tilt,tester,masks)){Debug.LogWarning("FLEX_INTERPOLATION refine depth="+depth+" at "+p+" "+flexTestFailure);passed=false;}}if(!passed)break;}
   Debug.Log("FLEX_GRID depth="+depth+" frames="+frames.Count+" passed="+passed);if(passed)break;
  }
  if(!passed&&retain){retainedFailures.Add("Maximum grid refinement: residual interpolation violations; candidate is NOT clearance approved.");Debug.LogWarning("FLEX_RETAINED_INTERPOLATION_FAILURE");}
  File.WriteAllLines("Outputs/WanderFlex/RetainedFailures.txt",retainedFailures);
  if(!passed&&!retain)throw new InvalidOperationException("Flex interpolation has residual contact/order violations at maximum local refinement; no formal cache published.");
  SaveFlex("Outputs/WanderFlex/Candidate.bin",data);AttachFlex(data);SetBloomProgress(1);Debug.Log("FLEX_CANDIDATE_READY frames="+frames.Count);
 }
 sealed class FlexTestContext {internal FlowerSurfaceCollision scan;internal Vector3[][] local,world;internal Matrix4x4[] matrices;internal FlexRayTree[] trees;}
 FlexTestContext flexTestContext;
 FlexTestContext FlexContext(){
  if(flexTestContext!=null)return flexTestContext;var context=new FlexTestContext{scan=new FlowerSurfaceCollision(this)};var petals=context.scan.Petals;context.local=new Vector3[petals.Length][];context.world=new Vector3[petals.Length][];context.matrices=new Matrix4x4[petals.Length];
  for(int p=0;p<petals.Length;p++){var item=petals[p];context.local[p]=new Vector3[BottomSurface];context.world[p]=new Vector3[BottomSurface];SampleDiagnosticSurface(item.layer,item.index,1,context.local[p]);PetalArtMotion(new Vector4(0,0,1,0),5,item.index*Mathf.PI*2/layers[item.layer].petals.Length,item.layer,item.index,out float tilt,out float roll);context.matrices[p]=OpenMatrix(item.layer,item.index,1,tilt,roll);for(int id=0;id<BottomSurface;id++)context.world[p][id]=context.matrices[p].MultiplyPoint3x4(context.local[p][id]);}
  context.scan.SamplePositions(context.world);context.trees=new FlexRayTree[4];for(int n=0;n<4;n++)context.trees[n]=new FlexRayTree(context.world[FlexNeighbours[n]]);flexTestContext=context;return context;
 }
 FlexTestContext UpdateFlexContext(FlexData data,Vector3 parameter,float tilt,FlexField field,float fraction=1){
  var c=FlexContext();foreach(int p in new[]{3,9,10}){int layer=p==3?0:1,index=p==9?2:3;float angle=p==3?parameter.x:p==9?parameter.z:tilt;var matrix=OpenMatrix(layer,index,1,angle,OpenRoll(layer,index));c.matrices[p]=matrix;for(int id=0;id<BottomSurface;id++){var point=c.local[p][id];if(p==10)point+=data.normals[id]*(field.Height(id)*fraction);c.world[p][id]=matrix.MultiplyPoint3x4(point);}c.scan.SamplePetalPositions(p,c.world[p]);}return c;
 }
 string flexTestFailure;
 bool TestFlexFrame(FlexData data,Vector3 parameter,float tilt,FlexField field,bool[][] masks){
  var c=UpdateFlexContext(data,parameter,tilt,field);
  c.trees[0].Update(c.world[3]);c.trees[1].Update(c.world[9]);var hits=new List<FlowerSurfaceCollision.Contact>();for(int n=0;n<4;n++){int other=FlexNeighbours[n];c.scan.FindPairContacts(Mathf.Min(other,10),Mathf.Max(other,10),hits);if(hits.Exists(h=>(other<10?h.lengthB:h.lengthA)>=.25f&&(n==0||(other<10?h.lengthA:h.lengthB)>=.25f))){flexTestFailure="contact pair="+other+":10";return false;}}
  var target=c.matrices[10];for(int n=0;n<4;n++)for(int id=20*129;id<BottomSurface;id++)if(masks[n][id]){var dir=target.MultiplyVector(data.normals[id])*FlexSigns[n];float d=c.trees[n].OutsideDistance(c.world[10][id],dir.normalized,out float alignment);if(d*alignment>-FlexNormalMinimum&&d<.2f){flexTestFailure="directed gap pair="+FlexNeighbours[n]+":10 uv="+(id%129)+","+(id/129)+" ray="+d+" alignment="+alignment;return false;}}return true;
 }
 internal void SavePreparedFlex(){if(flexData==null)throw new InvalidOperationException("Flex data not prepared");SaveFlex(FlexPath+".tmp",flexData);if(File.Exists(FlexPath))File.Delete(FlexPath);File.Move(FlexPath+".tmp",FlexPath);}
}
}
#endif
