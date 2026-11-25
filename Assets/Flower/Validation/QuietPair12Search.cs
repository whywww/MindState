#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 // Quiet L1-P1/L1-P2 rigid calibration search. Reads cached geometry only;
 // publishes no cache and changes no runtime constant.
 internal Matrix4x4 QuietPair12Matrix(int index,float progress,float tilt,float roll,float yaw,Vector3 extraScale){
  var l=layers[1];var p=l.petals[index];
  PathPetalPose(l,p,index,StandardLayerProgress(l.config,progress),out var position,out var rotation,out var scale);
  rotation=Quaternion.AngleAxis(yaw,Vector3.up)*rotation*Quaternion.Euler(tilt,0,roll);
  position.y=layers[0].config.rootHeight+.0045f+l.config.rootStagger*RootStaggerSign(index,l.petals.Length);
  return Matrix4x4.TRS(position,rotation,Vector3.Scale(scale,extraScale))*Matrix4x4.Translate(-BottomPoint(1,index,progress,0,0));
 }
 internal void SearchQuietPair12(){
  Directory.CreateDirectory("Outputs/QuietPair12");
  EnsureArtLooks();SetArtPreviewImmediate(FlowerArtState.Quiet);
  float progress=artProfiles[3].openness,amp=artProfiles[3].amplitude,droop=artLooks[3].droop*.7f;
  var scan=new FlowerSurfaceCollision(this);var petals=scan.Petals;int count=petals.Length;
  var quiet=new Vector4(0,0,0,1);
  var local=new Vector3[count][];var world=new Vector3[count][];
  for(int p=0;p<count;p++){
   local[p]=new Vector3[BottomSurface];world[p]=new Vector3[BottomSurface];
   SampleDiagnosticSurface(petals[p].layer,petals[p].index,progress,local[p]);
   var item=petals[p];
   PetalArtMotion(quiet,0,item.index*Mathf.PI*2/layers[item.layer].petals.Length,item.layer,item.index,out float tilt,out float roll);
   var m=OpenMatrix(item.layer,item.index,progress,tilt,roll);
   for(int k=0;k<BottomSurface;k++)world[p][k]=m.MultiplyPoint3x4(local[p][k]);
  }
  scan.SamplePositions(world);
  var hits=new List<FlowerSurfaceCollision.Contact>();
  bool PairContact(int a,int b,out float length){
   scan.FindPairContacts(Mathf.Min(a,b),Mathf.Max(a,b),hits);length=0;bool found=false;
   foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f){found=true;length=Mathf.Max(length,Mathf.Min(h.lengthA,h.lengthB));}
   return found;
  }
  void Place(int target,float tiltT,float rollT,int other,float tiltO,float rollO,float yaw,Vector3 extra){
   var mt=QuietPair12Matrix(petals[target].index,progress,tiltT,rollT,yaw,extra);
   var mo=OpenMatrix(petals[other].layer,petals[other].index,progress,tiltO,rollO);
   for(int k=0;k<BottomSurface;k++){world[target][k]=mt.MultiplyPoint3x4(local[target][k]);world[other][k]=mo.MultiplyPoint3x4(local[other][k]);}
   scan.SamplePetalPositions(target,world[target]);scan.SamplePetalPositions(other,world[other]);
  }
  float Off(int i,int steps)=>-amp+2*amp*i/steps;
  float AabbGap(int a,int b){
   Vector3 minA=world[a][0],maxA=minA,minB=world[b][0],maxB=minB;
   for(int k=1;k<BottomSurface;k++){minA=Vector3.Min(minA,world[a][k]);maxA=Vector3.Max(maxA,world[a][k]);minB=Vector3.Min(minB,world[b][k]);maxB=Vector3.Max(maxB,world[b][k]);}
   float gx=Mathf.Max(0,Mathf.Max(minA.x-maxB.x,minB.x-maxA.x)),gy=Mathf.Max(0,Mathf.Max(minA.y-maxB.y,minB.y-maxA.y)),gz=Mathf.Max(0,Mathf.Max(minA.z-maxB.z,minB.z-maxA.z));
   return Mathf.Sqrt(gx*gx+gy*gy+gz*gz);
  }
  var report=new List<string>();
  // Baseline: does the uncorrected Quiet pose actually cross, in the diagnostic
  // mid-surface and in the rendered sheets?
  int envelopeCross=0;float envelopeWorst=0;
  for(int x=0;x<=8;x++)for(int y=0;y<=8;y++){
   Place(9,droop+Off(y,8),0,8,droop+Off(x,8),0,0,Vector3.one);
   if(PairContact(8,9,out float length)){envelopeCross++;envelopeWorst=Mathf.Max(envelopeWorst,length);}
  }
  int motionCross=0;float motionWorst=0,firstFailure=-1;
  for(int step=0;step<=140;step++){
   float t=step*.1f;
   PetalArtMotion(quiet,t,1*Mathf.PI*2/layers[1].petals.Length,1,1,out float t8,out float r8);
   PetalArtMotion(quiet,t,2*Mathf.PI*2/layers[1].petals.Length,1,2,out float t9,out float r9);
   Place(9,t9,r9,8,t8,r8,0,Vector3.one);
   if(PairContact(8,9,out float length)){motionCross++;motionWorst=Mathf.Max(motionWorst,length);if(firstFailure<0)firstFailure=t;}
  }
  SetArtPreviewImmediate(FlowerArtState.Quiet);MotionStep(firstFailure>=0?firstFailure:5);
  scan.SampleRendered();bool renderedCross=PairContact(8,9,out float renderedLength);
  report.Add("baseline envelope crossings="+envelopeCross+"/81 worstLength="+envelopeWorst);
  report.Add("baseline one-period motion crossings="+motionCross+"/141 worstLength="+motionWorst+" firstFailure="+firstFailure);
  report.Add("baseline rendered-sheet crossing at first failure="+renderedCross+" length="+renderedLength);
  File.WriteAllLines("Outputs/QuietPair12/SearchBaseline.txt",report);
  // SampleRendered above left every petal in rendered world space (cluster lean,
  // lift, bud scale, breathing at firstFailure). Candidate placement below writes
  // only the target pair in untransformed local space, so restore all petals to
  // the same consistent space before evaluating neighbours.
  for(int p=0;p<count;p++){
   var item=petals[p];
   PetalArtMotion(quiet,0,item.index*Mathf.PI*2/layers[item.layer].petals.Length,item.layer,item.index,out float tilt,out float roll);
   var m=OpenMatrix(item.layer,item.index,progress,tilt,roll);
   for(int k=0;k<BottomSurface;k++)world[p][k]=m.MultiplyPoint3x4(local[p][k]);
  }
  scan.SamplePositions(world);
  // Neighbour petals never move during the search; cache their AABBs so each
  // envelope point only runs surface collision against boxes that actually overlap.
  Bounds BoundsOf(int p){var b=new Bounds(world[p][0],Vector3.zero);for(int k=1;k<BottomSurface;k++)b.Encapsulate(world[p][k]);return b;}
  float Separation(Bounds a,Bounds b)=>Mathf.Max(Mathf.Max(a.min.x-b.max.x,b.min.x-a.max.x),Mathf.Max(Mathf.Max(a.min.y-b.max.y,b.min.y-a.max.y),Mathf.Max(a.min.z-b.max.z,b.min.z-a.max.z)));
  var fixedBounds=new Bounds[count];for(int p=0;p<count;p++)fixedBounds[p]=BoundsOf(p);
  if(envelopeCross==0&&!renderedCross){
   File.WriteAllText("Outputs/QuietPair12/SearchResult.txt","FAIL: no baseline L1-P1/L1-P2 crossing found in the Quiet diagnostic envelope or rendered sheets; nothing to calibrate. Inspect SearchBaseline.txt.");
   Debug.Log("QUIET_PAIR12_SEARCH no baseline crossing");return;
  }
  var csv=new List<string>{"target,tiltBias,breathScale,rollBias,yaw,scale,margin,phase"};
  string bestLine=null;float bestMargin=-1;int bestTarget=0;float bestBias=0,bestBreath=1,bestRoll=0,bestYaw=0,bestScale=1;
  int evaluated=0;
  // Per-target baseline contact sets on the coarse envelope, uncorrected.
  HashSet<int>[,] Baseline(int target,int other,int steps){
   var result=new HashSet<int>[steps+1,steps+1];
   for(int x=0;x<=steps;x++)for(int y=0;y<=steps;y++){
    Place(target,droop+Off(y,steps),0,other,droop+Off(x,steps),0,0,Vector3.one);
    var set=new HashSet<int>();var tb=BoundsOf(target);
    for(int o=0;o<count;o++){if(o==target)continue;if(o!=other&&Separation(tb,fixedBounds[o])>1e-4f)continue;if(PairContact(target,o,out _))set.Add(o);}
    result[x,y]=set;
   }
   return result;
  }
  bool Evaluate(int target,int other,float bias,float breath,float rollBias,float yaw,Vector3 extra,int steps,HashSet<int>[,] baseline,out float margin){
   margin=float.PositiveInfinity;
   for(int x=0;x<=steps;x++)for(int y=0;y<=steps;y++){
    Place(target,droop+bias+Off(y,steps)*breath,rollBias,other,droop+Off(x,steps),0,yaw,extra);
    if(PairContact(target,other,out _))return false;
    var tb=BoundsOf(target);
    for(int o=0;o<count;o++){if(o==target||o==other)continue;if(Separation(tb,fixedBounds[o])>1e-4f)continue;if(PairContact(target,o,out _)&&!baseline[x,y].Contains(o))return false;}
   }
   for(int x=0;x<=steps;x++)for(int y=0;y<=steps;y++){
    Place(target,droop+bias+Off(y,steps)*breath,rollBias,other,droop+Off(x,steps),0,yaw,extra);
    margin=Mathf.Min(margin,AabbGap(target,other));
   }
   return true;
  }
  void Offer(int target,float bias,float breath,float rollBias,float yaw,float scale,float margin,string phase){
   csv.Add(target+","+bias.ToString("R")+","+breath.ToString("R")+","+rollBias.ToString("R")+","+yaw.ToString("R")+","+scale.ToString("R")+","+margin.ToString("R")+","+phase);
   if(margin>bestMargin){bestMargin=margin;bestTarget=target;bestBias=bias;bestBreath=breath;bestRoll=rollBias;bestYaw=yaw;bestScale=scale;
    bestLine="target="+target+" tiltBias="+bias.ToString("R")+" breathScale="+breath.ToString("R")+" rollBias="+rollBias.ToString("R")+" yaw="+yaw.ToString("R")+" scale="+scale.ToString("R")+" margin="+margin.ToString("R")+" phase="+phase;}
  }
  foreach(int target in new[]{9,8}){
   int other=target==9?8:9;
   var baseline=Baseline(target,other,8);
   var accepted=new List<(float bias,float breath,float roll,float margin)>();
   for(float bias=-12;bias<=12.001f;bias+=1.5f)for(float breath=0;breath<=1.001f;breath+=.1f)for(float roll=-12;roll<=12.001f;roll+=2){
    evaluated++;
    if(Evaluate(target,other,bias,breath,roll,0,Vector3.one,8,baseline,out float margin))accepted.Add((bias,breath,roll,margin));
    if(evaluated%500==0)Debug.Log("QUIET_PAIR12_SEARCH evaluated="+evaluated+" accepted="+accepted.Count+" target="+target);
   }
   report.Add("target="+target+" coarse accepted="+accepted.Count);
   accepted.Sort((a,b)=>b.margin.CompareTo(a.margin));
   var fineBaseline=accepted.Count>0?Baseline(target,other,16):null;
   for(int seed=0;seed<Mathf.Min(1,accepted.Count);seed++){
    var s=accepted[seed];
    for(float bias=s.bias-1.5f;bias<=s.bias+1.501f;bias+=.3f)for(float breath=Mathf.Max(0,s.breath-.1f);breath<=Mathf.Min(1,s.breath+.1f)+1e-4f;breath+=.05f)for(float roll=s.roll-2;roll<=s.roll+2.001f;roll+=.5f){
     evaluated++;
     if(Evaluate(target,other,bias,breath,roll,0,Vector3.one,16,fineBaseline,out float margin))Offer(target,bias,breath,roll,0,1,margin,"fine");
    }
   }
   if(accepted.Count>0&&bestTarget==target)break;
  }
  // Rigid tilt/breath/roll found nothing: try yaw and uniform scale variants.
  if(bestLine==null){
   foreach(int target in new[]{9,8}){
    int other=target==9?8:9;
    var baseline=Baseline(target,other,8);
    foreach(float yaw in new[]{-20f,-10f,10f,20f})foreach(float scale in new[]{.85f,.95f,1.05f}){
     var extra=new Vector3(scale,scale,scale);
     for(float bias=-12;bias<=12.001f;bias+=3)foreach(float breath in new[]{0f,.5f,1f})for(float roll=-12;roll<=12.001f;roll+=4){
      evaluated++;
      if(Evaluate(target,other,bias,breath,roll,yaw,extra,8,baseline,out float margin))Offer(target,bias,breath,roll,yaw,scale,margin,"variant");
     }
    }
    if(bestLine!=null)break;
   }
  }
  File.WriteAllLines("Outputs/QuietPair12/Search.csv",csv);
  report.Add("evaluated="+evaluated);
  if(bestLine==null){
   report.Add("RESULT: no rigid calibration clears the Quiet L1-P1/L1-P2 envelope without new contacts.");
   File.WriteAllLines("Outputs/QuietPair12/SearchReport.txt",report);
   File.WriteAllText("Outputs/QuietPair12/SearchResult.txt","FAIL: no acceptable rigid calibration for either petal (including yaw/scale variants). See SearchReport.txt; flex deformation is the remaining option.");
   Debug.Log("QUIET_PAIR12_SEARCH FAIL no candidate");return;
  }
  report.Add("winner "+bestLine);
  File.WriteAllLines("Outputs/QuietPair12/SearchReport.txt",report);
  File.WriteAllText("Outputs/QuietPair12/SearchResult.txt","PASS: "+bestLine+"\nBaseline: "+envelopeCross+"/81 envelope crossings, "+motionCross+"/141 motion crossings. Candidate only; no runtime constant changed.");
  Debug.Log("QUIET_PAIR12_SEARCH PASS "+bestLine);
 }
 // Records every new contact across all six transition directions and the Quiet
 // steady timeline instead of throwing at the first one.
 internal void DiagnoseQuietPair12(){
  Directory.CreateDirectory("Outputs/QuietPair12");
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  var csv=new List<string>{"phase,state,entering,time,w,z,progress,pair,minLen,pointX,pointY,pointZ"};
  int correctedCross=0;
  HashSet<int> Contacts(int p){var set=new HashSet<int>();for(int o=0;o<PetalCount;o++){if(o==p)continue;scan.FindPairContacts(Mathf.Min(p,o),Mathf.Max(p,o),hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))set.Add(o);}return set;}
  void Check(string phase,FlowerArtState state,bool entering){
   scan.SampleRendered();
   var c9=Contacts(9);var c8=Contacts(8);
   scan.FindPairContacts(8,9,hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))correctedCross++;
   var det=new Dictionary<int,(float len,Vector3 pt)>();
   void Capture(int p,HashSet<int> set){foreach(int o in set){scan.FindPairContacts(Mathf.Min(p,o),Mathf.Max(p,o),hits);float len=0;Vector3 pt=Vector3.zero;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f&&Mathf.Min(h.lengthA,h.lengthB)>len){len=Mathf.Min(h.lengthA,h.lengthB);pt=h.point;}det[p*100+o]=(len,pt);}}
   Capture(9,c9);Capture(8,c8);
   quietPair12Bypass=true;MotionStep(0);scan.SampleRendered();
   var b9=Contacts(9);var b8=Contacts(8);
   quietPair12Bypass=false;MotionStep(0);
   var c=System.Globalization.CultureInfo.InvariantCulture;
   void Dump(int p,int o,HashSet<int> baseline){
    if(baseline.Contains(o))return;
    var d=det[p*100+o];
    csv.Add(phase+","+state+","+entering+","+artTime.ToString("R",c)+","+ArtWeights.w.ToString("R",c)+","+ArtWeights.z.ToString("R",c)+","+bloomProgress.ToString("R",c)+","+p+":"+o+","+d.len.ToString("R",c)+","+d.pt.x.ToString("R",c)+","+d.pt.y.ToString("R",c)+","+d.pt.z.ToString("R",c));
   }
   foreach(int o in c9)Dump(9,o,b9);
   foreach(int o in c8)Dump(8,o,b8);
  }
  foreach(var state in new[]{FlowerArtState.Clarity,FlowerArtState.Gathering,FlowerArtState.Wandering})foreach(bool entering in new[]{true,false}){
   SetArtPreviewImmediate(entering?state:FlowerArtState.Quiet);SetArtState(entering?FlowerArtState.Quiet:state);
   for(int step=0;step<=120;step++){MotionStep(.05f);Check("transition",state,entering);}
   Debug.Log("QUIET_PAIR12_DIAG transition done "+state+" entering="+entering+" violations="+csv.Count);
  }
  SetArtPreviewImmediate(FlowerArtState.Quiet);
  for(int step=0;step<=1200;step++){if(step>0)MotionStep(.1f);Check("steady",FlowerArtState.Quiet,false);if(step%200==0)Debug.Log("QUIET_PAIR12_DIAG steady "+artTime);}
  File.WriteAllLines("Outputs/QuietPair12/Diag.csv",csv);
  File.WriteAllText("Outputs/QuietPair12/DiagSummary.txt","violations="+(csv.Count-1)+" corrected89CrossSamples="+correctedCross);
  Debug.Log("QUIET_PAIR12_DIAG done violations="+(csv.Count-1));
 }
 // Compares OpenMatrix placement vs rendered body matrix for the same Quiet pose.
 internal void ProbeQuietPair12Pose(){
  Directory.CreateDirectory("Outputs/QuietPair12");
  SetArtPreviewImmediate(FlowerArtState.Quiet);MotionStep(0);
  float progress=artProfiles[3].openness,droop=artLooks[3].droop*.7f;
  var report=new List<string>();
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  // Rendered contacts for 9:2 and 9:3
  scan.SampleRendered();
  scan.FindPairContacts(2,9,hits);float r29=0;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f)r29=Mathf.Max(r29,Mathf.Min(h.lengthA,h.lengthB));
  scan.FindPairContacts(3,9,hits);float r39=0;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f)r39=Mathf.Max(r39,Mathf.Min(h.lengthA,h.lengthB));
  report.Add("rendered 9:2="+r29+" 9:3="+r39);
  // Diagnostic placement for petal 9 (corrected) and petals 2,3
  var local9=new Vector3[BottomSurface];SampleDiagnosticSurface(1,2,progress,local9);
  PetalArtMotion(new Vector4(0,0,0,1),0,2*Mathf.PI*2/layers[1].petals.Length,1,2,out float t9,out float r9);
  var m9=QuietPair12Matrix(2,progress,t9,r9,0,Vector3.one);
  var w9=new Vector3[BottomSurface];for(int k=0;k<BottomSurface;k++)w9[k]=m9.MultiplyPoint3x4(local9[k]);
  var local2=new Vector3[BottomSurface];SampleDiagnosticSurface(0,2,progress,local2);
  PetalArtMotion(new Vector4(0,0,0,1),0,2*Mathf.PI*2/layers[0].petals.Length,0,2,out float t2,out float r2);
  var m2=OpenMatrix(0,2,progress,t2,r2);
  var w2=new Vector3[BottomSurface];for(int k=0;k<BottomSurface;k++)w2[k]=m2.MultiplyPoint3x4(local2[k]);
  var local3=new Vector3[BottomSurface];SampleDiagnosticSurface(0,3,progress,local3);
  PetalArtMotion(new Vector4(0,0,0,1),0,3*Mathf.PI*2/layers[0].petals.Length,0,3,out float t3,out float r3);
  var m3=OpenMatrix(0,3,progress,t3,r3);
  var w3=new Vector3[BottomSurface];for(int k=0;k<BottomSurface;k++)w3[k]=m3.MultiplyPoint3x4(local3[k]);
  scan.SamplePetalPositions(9,w9);scan.SamplePetalPositions(2,w2);scan.SamplePetalPositions(3,w3);
  scan.FindPairContacts(2,9,hits);float d29=0;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f)d29=Mathf.Max(d29,Mathf.Min(h.lengthA,h.lengthB));
  scan.FindPairContacts(3,9,hits);float d39=0;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f)d39=Mathf.Max(d39,Mathf.Min(h.lengthA,h.lengthB));
  report.Add("diagnostic 9:2="+d29+" 9:3="+d39);
  // Compare world positions of petal 9: rendered vs diagnostic
  var rendered=scan.Petals;
  var bodyMat=rendered[9].body.localToWorldMatrix;
  var rv=new Vector3[BottomSurface];var v=PetalMesh(1,2).vertices;int count=(HeroPetal.WidthSegments+1)*(HeroPetal.LengthSegments+1);
  for(int k=0;k<BottomSurface;k++)rv[k]=bodyMat.MultiplyPoint3x4((v[k]+v[k+count])*.5f);
  float maxDiff=0;for(int k=0;k<BottomSurface;k++)maxDiff=Mathf.Max(maxDiff,Vector3.Distance(rv[k],w9[k]));
  report.Add("petal9 maxWorldDiff="+maxDiff);
  report.Add("petal9 rendered[0]="+rv[0]+" diagnostic[0]="+w9[0]);
  report.Add("petal9 tilt="+t9+" roll="+r9+" droop="+droop);
  // Also compare petal 2
  var bodyMat2=rendered[2].body.localToWorldMatrix;
  var rv2=new Vector3[BottomSurface];var v2=PetalMesh(0,2).vertices;
  for(int k=0;k<BottomSurface;k++)rv2[k]=bodyMat2.MultiplyPoint3x4((v2[k]+v2[k+count])*.5f);
  float maxDiff2=0;for(int k=0;k<BottomSurface;k++)maxDiff2=Mathf.Max(maxDiff2,Vector3.Distance(rv2[k],w2[k]));
  report.Add("petal2 maxWorldDiff="+maxDiff2);
  report.Add("petal2 rendered[0]="+rv2[0]+" diagnostic[0]="+w2[0]);
  // Uncorrected target placement at the search baseline grid point nearest t=0.
  float Off11=-1.4f+2.8f*11/32;
  var mu=QuietPair12Matrix(2,progress,droop+Off11,0,0,Vector3.one);
  var wu=new Vector3[BottomSurface];for(int k=0;k<BottomSurface;k++)wu[k]=mu.MultiplyPoint3x4(local9[k]);
  scan.SamplePetalPositions(9,wu);
  scan.FindPairContacts(2,9,hits);float u29=0;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f)u29=Mathf.Max(u29,Mathf.Min(h.lengthA,h.lengthB));
  scan.FindPairContacts(3,9,hits);float u39=0;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f)u39=Mathf.Max(u39,Mathf.Min(h.lengthA,h.lengthB));
  report.Add("uncorrected grid y=11 tilt="+(droop+Off11)+" 9:2="+u29+" 9:3="+u39);
  // Rendered bypass for comparison.
  quietPair12Bypass=true;MotionStep(0);scan.SampleRendered();
  scan.FindPairContacts(2,9,hits);float rb29=0;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f)rb29=Mathf.Max(rb29,Mathf.Min(h.lengthA,h.lengthB));
  scan.FindPairContacts(3,9,hits);float rb39=0;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f)rb39=Mathf.Max(rb39,Mathf.Min(h.lengthA,h.lengthB));
  report.Add("rendered bypass 9:2="+rb29+" 9:3="+rb39);
  quietPair12Bypass=false;MotionStep(0);
  File.WriteAllLines("Outputs/QuietPair12/PoseProbe.txt",report);
  Debug.Log("QUIET_PAIR12_POSE_PROBE done");
 }
 // Re-evaluates the chosen candidate exactly as the search does, logging the
 // first neighbour rejection or full acceptance.
 internal void RecheckQuietPair12Candidate(){
  Directory.CreateDirectory("Outputs/QuietPair12");
  EnsureArtLooks();SetArtPreviewImmediate(FlowerArtState.Quiet);
  float progress=artProfiles[3].openness,amp=artProfiles[3].amplitude,droop=artLooks[3].droop*.7f;
  var scan=new FlowerSurfaceCollision(this);var petals=scan.Petals;int count=petals.Length;
  var quiet=new Vector4(0,0,0,1);
  var local=new Vector3[count][];var world=new Vector3[count][];
  for(int p=0;p<count;p++){
   local[p]=new Vector3[BottomSurface];world[p]=new Vector3[BottomSurface];
   SampleDiagnosticSurface(petals[p].layer,petals[p].index,progress,local[p]);
   var item=petals[p];
   PetalArtMotion(quiet,0,item.index*Mathf.PI*2/layers[item.layer].petals.Length,item.layer,item.index,out float tilt,out float roll);
   var m=OpenMatrix(item.layer,item.index,progress,tilt,roll);
   for(int k=0;k<BottomSurface;k++)world[p][k]=m.MultiplyPoint3x4(local[p][k]);
  }
  scan.SamplePositions(world);
  var hits=new List<FlowerSurfaceCollision.Contact>();
  bool PairContact(int a,int b){scan.FindPairContacts(Mathf.Min(a,b),Mathf.Max(a,b),hits);return hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f);}
  float Off(int i,int steps)=>-amp+2*amp*i/steps;
  void Place(int target,float tiltT,float rollT,int other,float tiltO){
   var mt=QuietPair12Matrix(petals[target].index,progress,tiltT,rollT,0,Vector3.one);
   var mo=OpenMatrix(petals[other].layer,petals[other].index,progress,tiltO,0);
   for(int k=0;k<BottomSurface;k++){world[target][k]=mt.MultiplyPoint3x4(local[target][k]);world[other][k]=mo.MultiplyPoint3x4(local[other][k]);}
   scan.SamplePetalPositions(target,world[target]);scan.SamplePetalPositions(other,world[other]);
  }
  var report=new List<string>{"gpuPlayback="+gpuPlayback};
  int target=9,other=8,steps=32;
  Bounds BoundsOf(int p){var b=new Bounds(world[p][0],Vector3.zero);for(int k=1;k<BottomSurface;k++)b.Encapsulate(world[p][k]);return b;}
  float Separation(Bounds a,Bounds b)=>Mathf.Max(Mathf.Max(a.min.x-b.max.x,b.min.x-a.max.x),Mathf.Max(Mathf.Max(a.min.y-b.max.y,b.min.y-a.max.y),Mathf.Max(a.min.z-b.max.z,b.min.z-a.max.z)));
  var fixedBounds=new Bounds[count];for(int p=0;p<count;p++)fixedBounds[p]=BoundsOf(p);
  var baseline=new HashSet<int>[steps+1,steps+1];
  for(int x=0;x<=steps;x++)for(int y=0;y<=steps;y++){
   Place(target,droop+Off(y,steps),0,other,droop+Off(x,steps));
   var set=new HashSet<int>();var tb=BoundsOf(target);
   for(int o=0;o<count;o++){if(o==target)continue;if(o!=other&&Separation(tb,fixedBounds[o])>1e-4f)continue;if(PairContact(target,o))set.Add(o);}
   baseline[x,y]=set;
  }
  report.Add("baseline[0,8] contains 2="+baseline[0,8].Contains(2)+" 3="+baseline[0,8].Contains(3));
  string firstAccepted=null;
  foreach(var cand in new[]{(bias:-9.6f,breath:.1f,roll:-9.5f),(bias:-9.9f,breath:.1f,roll:-9.5f),(bias:-10.2f,breath:.1f,roll:-10f),(bias:-9.3f,breath:.1f,roll:-9f),(bias:-10.5f,breath:0f,roll:-10.5f)}){
   string verdict="ACCEPTED";
   for(int x=0;x<=steps&&verdict=="ACCEPTED";x++)for(int y=0;y<=steps&&verdict=="ACCEPTED";y++){
    Place(target,droop+cand.bias+Off(y,steps)*cand.breath,cand.roll,other,droop+Off(x,steps));
    if(PairContact(target,other)){verdict="target contact at "+x+","+y;break;}
    var tb=BoundsOf(target);
    for(int o=0;o<count;o++){if(o==target||o==other)continue;
     if(Separation(tb,fixedBounds[o])>1e-4f)continue;
     if(PairContact(target,o)&&!baseline[x,y].Contains(o)){verdict="new neighbour "+o+" at "+x+","+y;break;}
    }
   }
   report.Add("candidate bias="+cand.bias+" breath="+cand.breath+" roll="+cand.roll+" verdict="+verdict);
   if(verdict=="ACCEPTED"&&firstAccepted==null)firstAccepted=cand.bias+","+cand.breath+","+cand.roll;
  }
  report.Add("firstAccepted="+(firstAccepted??"none"));
  File.WriteAllLines("Outputs/QuietPair12/Recheck.txt",report);
  Debug.Log("QUIET_PAIR12_RECHECK firstAccepted="+(firstAccepted??"none"));
 }
}
}
#endif