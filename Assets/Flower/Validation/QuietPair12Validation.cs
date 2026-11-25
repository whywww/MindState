#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 internal void VerifyQuietPair12Envelope(){
  Directory.CreateDirectory("Outputs/QuietPair12");EnsureArtLooks();SetArtPreviewImmediate(FlowerArtState.Quiet);
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
  bool Contact(int a,int b){scan.FindPairContacts(Mathf.Min(a,b),Mathf.Max(a,b),hits);return hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f);}
  float Off(int i)=>-amp+2*amp*i/32;
  void Place(float tilt9,float roll9,float tilt8){
   var m9=QuietPair12Matrix(2,progress,tilt9,roll9,0,Vector3.one);
   var m8=OpenMatrix(1,1,progress,tilt8,0);
   for(int k=0;k<BottomSurface;k++){world[9][k]=m9.MultiplyPoint3x4(local[9][k]);world[8][k]=m8.MultiplyPoint3x4(local[8][k]);}
   scan.SamplePetalPositions(9,world[9]);scan.SamplePetalPositions(8,world[8]);
  }
  var baseline=new HashSet<int>[33,33];
  for(int x=0;x<=32;x++)for(int y=0;y<=32;y++){
   Place(droop+Off(y),0,droop+Off(x));
   var set=new HashSet<int>();
   for(int o=0;o<count;o++)if(o!=9&&Contact(9,o))set.Add(o);
   baseline[x,y]=set;
  }
  for(int x=0;x<=32;x++)for(int y=0;y<=32;y++){
   Place(droop+QuietPair12TiltBias+Off(y)*QuietPair12BreathScale,QuietPair12RollBias,droop+Off(x));
   if(Contact(8,9))throw new Exception("Quiet Pair12 envelope contact "+x+","+y);
   for(int o=0;o<count;o++){if(o==9||o==8)continue;if(Contact(9,o)&&!baseline[x,y].Contains(o))throw new Exception("Quiet Pair12 new envelope contact 9:"+o+" at "+x+","+y);}
  }
  File.WriteAllText("Outputs/QuietPair12/Envelope.txt","PASS: 33 x 33 independent Quiet breathing combinations at openness "+progress+"; L1-P1/L1-P2 clear, no new L1-P2 neighbour contacts.");
 }
 internal void VerifyQuietPair12(){
  Directory.CreateDirectory("Outputs/QuietPair12");SetArtPreviewImmediate(FlowerArtState.Quiet);
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  HashSet<int> Contacts(int p){var set=new HashSet<int>();for(int o=0;o<PetalCount;o++){if(o==p)continue;scan.FindPairContacts(Mathf.Min(p,o),Mathf.Max(p,o),hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))set.Add(o);}return set;}
  bool Cross(int a,int b,out float length){scan.FindPairContacts(Mathf.Min(a,b),Mathf.Max(a,b),hits);length=0;bool found=false;foreach(var h in hits)if(h.lengthA>=.25f&&h.lengthB>=.25f){found=true;length=Mathf.Max(length,Mathf.Min(h.lengthA,h.lengthB));}return found;}
  int baselineFailures=0;float worstTime=0,worstLength=0;
  var report=new List<string>{"time,baselineCrossing,correctedCrossing"};
  for(int step=0;step<=1200;step++){
   if(step>0)MotionStep(.1f);
   scan.SampleRendered();
   if(Cross(8,9,out _))throw new Exception("Quiet Pair12 crossing at "+artTime);
   var c9=Contacts(9);var c8=Contacts(8);
   quietPair12Bypass=true;MotionStep(0);scan.SampleRendered();
   bool baselineCross=Cross(8,9,out float baselineLength);var b9=Contacts(9);var b8=Contacts(8);
   quietPair12Bypass=false;MotionStep(0);
   if(baselineCross){baselineFailures++;if(baselineLength>worstLength){worstLength=baselineLength;worstTime=artTime;}}
   foreach(int o in c9)if(!b9.Contains(o))throw new Exception("New Quiet P2 contact 9:"+o+" at "+artTime);
   foreach(int o in c8)if(!b8.Contains(o))throw new Exception("New Quiet P1 contact 8:"+o+" at "+artTime);
   if(RootAnchorError()>1e-5f)throw new Exception("Root drift");
   report.Add(artTime.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+baselineCross+",false");
   if(step%100==0)Debug.Log("QUIET_PAIR12_VERIFIED "+artTime);
  }
  File.WriteAllLines("Outputs/QuietPair12/Timeline.csv",report);
  File.WriteAllText("Outputs/QuietPair12/Result.txt","PASS: 1201 actual-pose samples across 120 seconds of Quiet. L1-P1/L1-P2 clear, no added contacts for either petal, roots fixed. Baseline crossing samples="+baselineFailures);
  File.WriteAllText("Outputs/QuietPair12/WorstTime.txt",worstTime.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
 }
 internal void VerifyQuietPair12Transitions(){
  Directory.CreateDirectory("Outputs/QuietPair12");
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  HashSet<int> Contacts(int p){var set=new HashSet<int>();for(int o=0;o<PetalCount;o++){if(o==p)continue;scan.FindPairContacts(Mathf.Min(p,o),Mathf.Max(p,o),hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))set.Add(o);}return set;}
  int count=0;
  foreach(var state in new[]{FlowerArtState.Clarity,FlowerArtState.Gathering,FlowerArtState.Wandering})foreach(bool entering in new[]{true,false}){
   SetArtPreviewImmediate(entering?state:FlowerArtState.Quiet);SetArtState(entering?FlowerArtState.Quiet:state);
   for(int step=0;step<=120;step++){
    MotionStep(.05f);scan.SampleRendered();
    var c9=Contacts(9);var c8=Contacts(8);
    quietPair12Bypass=true;MotionStep(0);scan.SampleRendered();
    var b9=Contacts(9);var b8=Contacts(8);
    quietPair12Bypass=false;MotionStep(0);
    foreach(int o in c9)if(!b9.Contains(o))throw new Exception("New transition contact 9:"+o+" state="+state+" entering="+entering+" time="+artTime);
    foreach(int o in c8)if(!b8.Contains(o))throw new Exception("New transition contact 8:"+o+" state="+state+" entering="+entering+" time="+artTime);
    if(RootAnchorError()>1e-5f)throw new Exception("Root drift");
    count++;
   }
  }
  SetArtPreviewImmediate(FlowerArtState.Quiet);MotionStep(10);
  var held=layers[1].petals[2].body.localToWorldMatrix;var clock=artTime;
  SetFrozen(true);for(int i=0;i<30;i++)MotionStep(.1f);
  if(artTime!=clock||held!=layers[1].petals[2].body.localToWorldMatrix)throw new Exception("Frozen pose moved");
  SetFrozen(false);MotionStep(.1f);
  if(artTime<=clock||RootAnchorError()>1e-5f)throw new Exception("Resume failed");
  File.WriteAllText("Outputs/QuietPair12/Transitions.txt","PASS: "+count+" samples at 0.05 s across six Quiet transition directions. No new L1-P1/L1-P2 contacts; roots, frozen pose and resume verified.");
 }
 internal void CaptureQuietPair12(){
  Directory.CreateDirectory("Outputs/QuietPair12");Directory.CreateDirectory("Outputs/WanderFollow");
  SetArtPreviewImmediate(FlowerArtState.Quiet);
  float time=File.Exists("Outputs/QuietPair12/WorstTime.txt")?float.Parse(File.ReadAllText("Outputs/QuietPair12/WorstTime.txt"),System.Globalization.CultureInfo.InvariantCulture):5;
  MotionStep(time);
  var gallery=FindObjectOfType<FlowerGallery>();gallery.enabled=false;gallery.showInterface=false;
  var labels=new GameObject("QuietPair12 review labels");
  foreach(int index in new[]{1,2}){var item=new GameObject("L1-P"+index);item.transform.SetParent(labels.transform);var text=item.AddComponent<TextMesh>();text.text=item.name;text.fontSize=48;text.characterSize=.045f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.yellow;}
  void Shot(string name){
   foreach(Transform child in labels.transform){int index=child.name.EndsWith("1")?1:2;var p=layers[1].petals[index];child.position=p.body.TransformPoint(PetalMesh(1,index).vertices[48*129+64])+Camera.main.transform.up*.05f;child.rotation=Camera.main.transform.rotation;}
   WanderFollowPreview.Shot(name);File.Copy("Outputs/WanderFollow/"+name+".png","Outputs/QuietPair12/"+name+".png",true);
  }
  foreach(var view in new[]{new Vector2(22,55),new Vector2(0,90),new Vector2(22,8)}){
   gallery.SetView(view.x,view.y);string suffix=view.y.ToString("F0");
   Shot("After"+suffix);
   quietPair12Bypass=true;MotionStep(0);Shot("Before"+suffix);quietPair12Bypass=false;MotionStep(0);
  }
  gallery.SetView(22,55);SoloPetal(1,1);SetPetalVisible(1,2,true);
  int number=1;foreach(var view in new[]{new Vector2(22,55),new Vector2(0,90),new Vector2(22,8)}){gallery.SetView(view.x,view.y,7);Shot("Close"+number.ToString("D2"));number++;}
  ShowAllPetals();DestroyImmediate(labels);
  File.WriteAllText("Outputs/QuietPair12/Review.txt","PASS: before/after at the worst baseline crossing time "+time+" s, 3 views and 3 numbered close-ups. Before restores the uncorrected Quiet motion.");
 }
}
}
#endif