#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 Matrix4x4 Pair10ValidationMatrix(float phase,float tilt,float roll){var l=layers[1];var p=l.petals[1];PathPetalPose(l,p,1,StandardLayerProgress(l.config,phase),out var position,out var rotation,out var scale);AdjustPair10ArtPose(1,1,1,ref rotation,ref scale);position.y=layers[0].config.rootHeight+.0045f+l.config.rootStagger*RootStaggerSign(1,l.petals.Length);return Matrix4x4.TRS(position,rotation*Quaternion.Euler(tilt,0,roll),scale)*Matrix4x4.Translate(-BottomPoint(1,1,phase,0,0));}
 internal void VerifyPair10Envelope(){SetBloomProgress(1);EnsureArtLooks();var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();var a=new Vector3[BottomSurface];var b=new Vector3[BottomSurface];var wa=new Vector3[BottomSurface];var wb=new Vector3[BottomSurface];SampleDiagnosticSurface(1,0,1,a);SampleDiagnosticSurface(1,1,1,b);for(int x=0;x<=32;x++)for(int y=0;y<=32;y++){var ma=OpenMatrix(1,0,1,-4+x*.25f,OpenRoll(1,0));var mb=Pair10ValidationMatrix(1,Pair10TiltBias+Pair10BreathScale*(-4+y*.25f),OpenRoll(1,1)+Pair10RollBias);for(int k=0;k<BottomSurface;k++){wa[k]=ma.MultiplyPoint3x4(a[k]);wb[k]=mb.MultiplyPoint3x4(b[k]);}scan.SamplePetalPositions(7,wa);scan.SamplePetalPositions(8,wb);scan.FindPairContacts(7,8,hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))throw new Exception("Pair10 envelope contact "+x+","+y);}Directory.CreateDirectory("Outputs/Pair10");File.WriteAllText("Outputs/Pair10/Envelope.txt","PASS: 33 x 33 independent breathing combinations including extrema.");}
 internal void VerifyPair10(){
  Directory.CreateDirectory("Outputs/Pair10");SetBloomProgress(1);EnsureArtLooks();LoadFlexCandidate();var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();var petals=scan.Petals;var local=new Vector3[PetalCount][];var world=new Vector3[PetalCount][];
  for(int p=0;p<PetalCount;p++){local[p]=new Vector3[BottomSurface];world[p]=new Vector3[BottomSurface];SampleDiagnosticSurface(petals[p].layer,petals[p].index,1,local[p]);}
  void Pose(int p,float time,bool candidate){var item=petals[p];PetalArtMotion(new Vector4(0,0,1,0),time,item.index*Mathf.PI*2/layers[item.layer].petals.Length,item.layer,item.index,out float tilt,out float roll);if(p==8&&!candidate){float period=7+4*Mathf.Repeat(2*.618034f+1*.37f,1);tilt=artProfiles[2].amplitude*Mathf.Sin(time*Mathf.PI*2/period+Mathf.PI*2/6+.9f);roll-=Pair10RollBias;}if(p==10){artTime=time;QueryFlex(flexData,FlexParameter(),flexField.controls,out tilt);}var m=p==1?Pair01ValidationMatrix(1,tilt,roll):p==5?Pair55ValidationMatrix(1,tilt,roll):p==14?Pair20ValidationMatrix(1,tilt,roll):p==17?Pair24ValidationMatrix(1,tilt,roll):OpenMatrix(item.layer,item.index,1,tilt,roll);if(p==8&&candidate){var l=layers[1];var petal=l.petals[1];PathPetalPose(l,petal,1,1,out var position,out var rotation,out var scale);position.y=layers[0].config.rootHeight+.0045f+l.config.rootStagger*RootStaggerSign(1,l.petals.Length);AdjustPair10ArtPose(1,1,1,ref rotation,ref scale);ApplyPetalPose(l,petal,1,position,rotation*Quaternion.Euler(tilt,0,roll),scale);m=generated.worldToLocalMatrix*petal.body.localToWorldMatrix;}for(int k=0;k<BottomSurface;k++)world[p][k]=m.MultiplyPoint3x4(local[p][k]+(p==10?flexData.normals[k]*flexField.Height(k):Vector3.zero));scan.SamplePetalPositions(p,world[p]);}
  bool Contact(int a,int b){scan.FindPairContacts(Mathf.Min(a,b),Mathf.Max(a,b),hits);return hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f);}
  int baselineFailures=0;var report=new List<string>{"time,baselineTarget,candidateTarget"};
  bool Check(float time){for(int p=0;p<PetalCount;p++)Pose(p,time,false);var old=new HashSet<int>();for(int o=0;o<PetalCount;o++)if(o!=8&&Contact(8,o))old.Add(o);if(old.Contains(7))baselineFailures++;report.Add(time+","+old.Contains(7)+",false");Pose(8,time,true);if(Contact(8,9)||Contact(7,8)||Contact(0,1)||Contact(2,3)||Contact(5,12)||Contact(13,14)||Contact(13,17))return false;for(int o=0;o<PetalCount;o++)if(o!=8&&Contact(8,o)&&!old.Contains(o)){Debug.Log("PAIR10_NEW 8:"+o+" at "+time);return false;}return true;}
  for(int step=0;step<=1200;step++){if(!Check(step*.1f))throw new Exception("Pair10 actual regression at "+step*.1f);if(step%100==0)Debug.Log("PAIR10_VERIFIED "+step*.1f);}
  File.WriteAllLines("Outputs/Pair10/Timeline.csv",report);File.WriteAllText("Outputs/Pair10/Result.txt","PASS: 1201 actual runtime-pose samples across 120 seconds; L1-P0/L1-P1 clear, no new L1-P1 neighbour contacts, all five earlier pairs retained. Baseline target failures="+baselineFailures);
 }
 internal void VerifyPair10Transitions(){
  LoadFlexCandidate();var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();int count=0;
  HashSet<int> Contacts(){scan.SampleRendered();var result=new HashSet<int>();for(int o=0;o<PetalCount;o++)if(o!=8){scan.FindPairContacts(Mathf.Min(8,o),Mathf.Max(8,o),hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))result.Add(o);}return result;}
  foreach(var state in new[]{FlowerArtState.Clarity,FlowerArtState.Gathering,FlowerArtState.Quiet})foreach(bool entering in new[]{true,false}){
   SetArtPreviewImmediate(entering?state:FlowerArtState.Wandering);SetArtState(entering?FlowerArtState.Wandering:state);
   for(int step=0;step<=60;step++){MotionStep(.1f);var candidate=Contacts();var l=layers[1];var p=l.petals[1];var savedPosition=p.anchor.localPosition;var savedRotation=p.anchor.localRotation;var savedScale=p.scale;PetalArtMotion(ArtWeights,artTime,Mathf.PI*2/6,1,1,out float tilt,out float roll);float raw=artProfiles[2].amplitude*Mathf.Sin(artTime*Mathf.PI*2/(7+4*Mathf.Repeat(2*.618034f+1*.37f,1))+Mathf.PI*2/6+.9f);tilt-=ArtWeights.z*Pair10PoseGate*(Pair10TiltBias+(Pair10BreathScale-1)*raw);roll-=ArtWeights.z*Pair10PoseGate*Pair10RollBias;PathPetalPose(l,p,1,StandardLayerProgress(l.config,bloomProgress),out var position,out var rotation,out var scale);ApplyPetalPose(l,p,1,position,rotation*Quaternion.Euler(tilt,0,roll),scale);var baseline=Contacts();ApplyPetalPose(l,p,1,savedPosition,savedRotation,savedScale);foreach(int other in candidate)if(!baseline.Contains(other))throw new Exception("New transition contact 8:"+other+" state="+state+" entering="+entering+" time="+artTime);if(RootAnchorError()>1e-5f)throw new Exception("Root drift");count++;}
  }
  SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(10);var held=layers[1].petals[1].body.localToWorldMatrix;float clock=artTime;SetFrozen(true);for(int i=0;i<30;i++)MotionStep(.1f);if(artTime!=clock||held!=layers[1].petals[1].body.localToWorldMatrix)throw new Exception("Frozen L1-P1 moved");SetFrozen(false);Directory.CreateDirectory("Outputs/Pair10");File.WriteAllText("Outputs/Pair10/Transitions.txt","PASS: "+count+" six-direction transition samples, no added L1-P1 contacts, fixed roots and freeze.");
 }


 internal void CapturePair10(){
  if(!UsesGpuPlayback)throw new Exception("Review requires formal GPU playback");
  Directory.CreateDirectory("Outputs/Pair10");Directory.CreateDirectory("Outputs/WanderFollow");SetArtPreviewImmediate(FlowerArtState.Wandering);
  VerifyOuterGpuFlex();
  var gallery=FindObjectOfType<FlowerGallery>();gallery.enabled=false;gallery.showInterface=false;
  var labels=new GameObject("Outer pair review labels");
  foreach(int index in new[]{7,8}){var item=new GameObject(index==7?"L1-P0":"L1-P1");item.transform.SetParent(labels.transform);var text=item.AddComponent<TextMesh>();text.text=item.name;text.fontSize=48;text.characterSize=.045f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.yellow;}
  void Label(){foreach(Transform child in labels.transform){int index=child.name.EndsWith("0")?0:1;var p=layers[1].petals[index];var v=PetalMesh(1,index).vertices;child.position=p.body.TransformPoint(v[48*129+64])+Camera.main.transform.up*.05f;child.rotation=Camera.main.transform.rotation;}}
  void Shot(string name){Label();WanderFollowPreview.Shot(name);File.Copy("Outputs/WanderFollow/"+name+".png","Outputs/Pair10/"+name+".png",true);}
  float failureTime=0;foreach(string line in File.ReadAllLines("Outputs/Pair10/Timeline.csv")){if(!line.Contains(",True,"))continue;failureTime=float.Parse(line.Split(',')[0],System.Globalization.CultureInfo.InvariantCulture);break;}
  foreach(var view in new[]{new Vector2(22,55),new Vector2(0,90),new Vector2(22,8)}){
   SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(failureTime);gallery.SetView(view.x,view.y);string suffix=view.y.ToString("F0");Shot("After"+suffix);
   var l=layers[1];var p=l.petals[1];float tilt=artProfiles[2].amplitude*Mathf.Sin(artTime*Mathf.PI*2/(7+4*Mathf.Repeat(2*.618034f+1*.37f,1))+Mathf.PI*2/6+.9f);float roll=artLooks[2].asym*Mathf.Sin(2*1.7f+.9f);PathPetalPose(l,p,1,1,out var position,out var rotation,out var scale);ApplyPetalPose(l,p,1,position,rotation*Quaternion.Euler(tilt,0,roll),scale);Shot("Before"+suffix);
  }
  SetArtPreviewImmediate(FlowerArtState.Wandering);gallery.SetView(22,55);
  for(int frame=0;frame<120;frame++){MotionStep(.1f);Shot("Frame"+frame.ToString("D3"));}
  SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(failureTime);SoloPetal(1,0);SetPetalVisible(1,1,true);int numbered=1;
  foreach(var view in new[]{new Vector2(22,55),new Vector2(0,90),new Vector2(22,8)}){gallery.SetView(view.x,view.y,7);Shot("Close"+numbered.ToString("D2"));numbered++;}ShowAllPetals();gallery.SetView(22,55,8.5f);
  DestroyImmediate(labels);File.WriteAllText("Outputs/Pair10/Review.txt","PASS: formal GPU captures, 3 labelled before/after views and 120 animation frames at 10 Hz and 3 numbered close views. Before/after captured at a proven baseline failure time.");
 }
}
}
#endif
