#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 Matrix4x4 Pair01ValidationMatrix(float phase,float tilt,float roll){
  var l=layers[0];var p=l.petals[1];PathPetalPose(l,p,1,StandardLayerProgress(l.config,phase),out var position,out var rotation,out var scale);AdjustPair01ArtPose(0,1,1,ref rotation,ref scale);position.y=layers[0].config.rootHeight+.0045f+l.config.rootStagger*RootStaggerSign(1,l.petals.Length);return Matrix4x4.TRS(position,rotation*Quaternion.Euler(tilt,0,roll),scale)*Matrix4x4.Translate(-BottomPoint(0,1,phase,0,0));
 }
 internal void VerifyPair01(){
  Directory.CreateDirectory("Outputs/Pair01");SetBloomProgress(1);EnsureArtLooks();LoadFlexCandidate();var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();var petals=scan.Petals;var local=new Vector3[PetalCount][];var world=new Vector3[PetalCount][];
  for(int p=0;p<PetalCount;p++){local[p]=new Vector3[BottomSurface];world[p]=new Vector3[BottomSurface];SampleDiagnosticSurface(petals[p].layer,petals[p].index,1,local[p]);}
  void Pose(int p,float time,bool baseline){var item=petals[p];PetalArtMotion(new Vector4(0,0,1,0),time,item.index*Mathf.PI*2/layers[item.layer].petals.Length,item.layer,item.index,out float tilt,out float roll);if(p==1&&baseline){float period=7f+4*Mathf.Repeat(2*.618034f,1);tilt=artProfiles[2].amplitude*Mathf.Sin(time*Mathf.PI*2/period+Mathf.PI*2/7);roll-=OuterPair01RollBias;}if(p==10){artTime=time;QueryFlex(flexData,FlexParameter(),flexField.controls,out tilt);}var m=p==5?Pair55ValidationMatrix(1,tilt,roll):p==14?Pair20ValidationMatrix(1,tilt,roll):p==17?Pair24ValidationMatrix(1,tilt,roll):p==8?Pair10ValidationMatrix(1,tilt,roll):OpenMatrix(item.layer,item.index,1,tilt,roll);if(p==1&&!baseline){var layer=layers[0];var petal=layer.petals[1];PathPetalPose(layer,petal,1,1,out var position,out var rotation,out var scale);AdjustPair01ArtPose(0,1,1,ref rotation,ref scale);ApplyPetalPose(layer,petal,1,position,rotation*Quaternion.Euler(tilt,0,roll),scale);m=generated.worldToLocalMatrix*petal.body.localToWorldMatrix;}for(int k=0;k<BottomSurface;k++)world[p][k]=m.MultiplyPoint3x4(local[p][k]+(p==10?flexData.normals[k]*flexField.Height(k):Vector3.zero));scan.SamplePetalPositions(p,world[p]);}
  bool Contact(int a,int b){scan.FindPairContacts(Mathf.Min(a,b),Mathf.Max(a,b),hits);return hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f);}
  int baselineHits=0;for(int step=0;step<=1200;step++){float time=step*.1f;for(int p=0;p<PetalCount;p++)Pose(p,time,true);if(Contact(0,1))baselineHits++;var old=new HashSet<int>();for(int o=0;o<PetalCount;o++)if(o!=1&&Contact(1,o))old.Add(o);Pose(1,time,false);if(Contact(0,1)||Contact(2,3))throw new Exception("Outer target contact at "+time);for(int o=2;o<PetalCount;o++)if(Contact(1,o)&&!old.Contains(o))throw new Exception("New P1 neighbour "+o+" at "+time);if(step%100==0)Debug.Log("PAIR01_VERIFIED "+time);}
  File.WriteAllText("Outputs/Pair01/Result.txt","PASS: 1201 samples across 120 seconds; pair 01 clear, pair 23 retained, no new P1 neighbour contact. Baseline target contact samples="+baselineHits);
 }
 internal void VerifyPair01Transitions(){
  LoadFlexCandidate();var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();int count=0;
  HashSet<int> Contacts(){scan.SampleRendered();var result=new HashSet<int>();for(int o=0;o<PetalCount;o++)if(o!=1){scan.FindPairContacts(Mathf.Min(1,o),Mathf.Max(1,o),hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))result.Add(o);}return result;}
  foreach(var state in new[]{FlowerArtState.Clarity,FlowerArtState.Gathering,FlowerArtState.Quiet})foreach(bool entering in new[]{true,false}){
   SetArtPreviewImmediate(entering?state:FlowerArtState.Wandering);SetArtState(entering?FlowerArtState.Wandering:state);
   for(int step=0;step<=60;step++){MotionStep(.1f);var candidate=Contacts();var l=layers[0];var p=l.petals[1];var savedPosition=p.anchor.localPosition;var savedRotation=p.anchor.localRotation;var savedScale=p.scale;PetalArtMotion(ArtWeights,artTime,Mathf.PI*2/7,0,1,out float tilt,out float roll);float raw=artProfiles[2].amplitude*Mathf.Sin(artTime*Mathf.PI*2/(7+4*Mathf.Repeat(2*.618034f,1))+Mathf.PI*2/7);tilt-=ArtWeights.z*WanderingSeamPoseGate*(OuterPair01TiltBias+(OuterPair01BreathScale-1)*raw);roll-=ArtWeights.z*WanderingSeamPoseGate*OuterPair01RollBias;PathPetalPose(l,p,1,StandardLayerProgress(l.config,bloomProgress),out var position,out var rotation,out var scale);ApplyPetalPose(l,p,1,position,rotation*Quaternion.Euler(tilt,0,roll),scale);var baseline=Contacts();ApplyPetalPose(l,p,1,savedPosition,savedRotation,savedScale);foreach(int other in candidate)if(!baseline.Contains(other))throw new Exception("New transition contact 1:"+other+" state="+state+" entering="+entering+" time="+artTime);if(RootAnchorError()>1e-5f)throw new Exception("Root drift");count++;}
  }
  SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(10);var held=layers[0].petals[1].body.localToWorldMatrix;float clock=artTime;SetFrozen(true);for(int i=0;i<30;i++)MotionStep(.1f);if(artTime!=clock||held!=layers[0].petals[1].body.localToWorldMatrix)throw new Exception("Frozen P1 moved");SetFrozen(false);Directory.CreateDirectory("Outputs/Pair01");File.WriteAllText("Outputs/Pair01/Transitions.txt","PASS: "+count+" six-direction transition samples, no added P1 contacts, fixed roots and freeze.");
 }
 internal void VerifyPair01Envelope(){
  SetBloomProgress(1);EnsureArtLooks();var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();var a=new Vector3[BottomSurface];var b=new Vector3[BottomSurface];var wa=new Vector3[BottomSurface];var wb=new Vector3[BottomSurface];SampleDiagnosticSurface(0,0,1,a);SampleDiagnosticSurface(0,1,1,b);
  for(int x=0;x<=16;x++)for(int y=0;y<=16;y++){var ma=OpenMatrix(0,0,1,-4+x*.5f,OpenRoll(0,0));var l=layers[0];var p=l.petals[1];PathPetalPose(l,p,1,1,out var position,out var rotation,out var scale);AdjustPair01ArtPose(0,1,1,ref rotation,ref scale);ApplyPetalPose(l,p,1,position,rotation*Quaternion.Euler(OuterPair01TiltBias+OuterPair01BreathScale*(-4+y*.5f),0,OpenRoll(0,1)+OuterPair01RollBias),scale);var mb=generated.worldToLocalMatrix*p.body.localToWorldMatrix;for(int k=0;k<BottomSurface;k++){wa[k]=ma.MultiplyPoint3x4(a[k]);wb[k]=mb.MultiplyPoint3x4(b[k]);}scan.SamplePetalPositions(0,wa);scan.SamplePetalPositions(1,wb);scan.FindPairContacts(0,1,hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))throw new Exception("Independent pair01 envelope contact "+x+","+y);}
  Directory.CreateDirectory("Outputs/Pair01");File.WriteAllText("Outputs/Pair01/Envelope.txt","PASS: 17 x 17 independent tilt combinations including all breathing extrema.");
 }
 internal void CapturePair01(){
  if(!UsesGpuPlayback)throw new Exception("Review requires formal GPU playback");
  Directory.CreateDirectory("Outputs/Pair01");Directory.CreateDirectory("Outputs/WanderFollow");SetArtPreviewImmediate(FlowerArtState.Wandering);
  VerifyOuterGpuFlex();
  var gallery=FindObjectOfType<FlowerGallery>();gallery.enabled=false;gallery.showInterface=false;
  var labels=new GameObject("Outer pair review labels");
  foreach(int index in new[]{0,1}){var item=new GameObject("L0-P"+index);item.transform.SetParent(labels.transform);var text=item.AddComponent<TextMesh>();text.text=item.name;text.fontSize=48;text.characterSize=.045f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.yellow;}
  void Label(){foreach(Transform child in labels.transform){int index=child.name.EndsWith("0")?0:1;var p=layers[0].petals[index];var v=PetalMesh(0,index).vertices;child.position=p.body.TransformPoint(v[48*129+64])+Camera.main.transform.up*.05f;child.rotation=Camera.main.transform.rotation;}}
  void Shot(string name){Label();WanderFollowPreview.Shot(name);File.Copy("Outputs/WanderFollow/"+name+".png","Outputs/Pair01/"+name+".png",true);}
  foreach(var view in new[]{new Vector2(22,55),new Vector2(0,90),new Vector2(22,8)}){
   SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(10);gallery.SetView(view.x,view.y);string suffix=view.y.ToString("F0");Shot("After"+suffix);
   var l=layers[0];var p=l.petals[1];float tilt=artProfiles[2].amplitude*Mathf.Sin(artTime*Mathf.PI*2/(7+4*Mathf.Repeat(2*.618034f,1))+Mathf.PI*2/7);float roll=artLooks[2].asym*Mathf.Sin(2*1.7f);PathPetalPose(l,p,1,1,out var position,out var rotation,out var scale);ApplyPetalPose(l,p,1,position,rotation*Quaternion.Euler(tilt,0,roll),scale);Shot("Before"+suffix);
  }
  SetArtPreviewImmediate(FlowerArtState.Wandering);gallery.SetView(22,55);
  for(int frame=0;frame<120;frame++){MotionStep(.1f);Shot("Frame"+frame.ToString("D3"));}
  DestroyImmediate(labels);File.WriteAllText("Outputs/Pair01/Review.txt","PASS: formal GPU captures, 3 labelled before/after views and 120 animation frames at 10 Hz.");
 }
}
}
#endif
