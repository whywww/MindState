#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 Matrix4x4 Pair55ValidationMatrix(float phase,float tilt,float roll){var l=layers[0];var p=l.petals[5];PathPetalPose(l,p,5,StandardLayerProgress(l.config,phase),out var position,out var rotation,out var scale);AdjustPair55ArtPose(0,5,1,ref rotation,ref scale);position.y=layers[0].config.rootHeight+.0045f+l.config.rootStagger*RootStaggerSign(5,l.petals.Length);return Matrix4x4.TRS(position,rotation*Quaternion.Euler(tilt,0,roll),scale)*Matrix4x4.Translate(-BottomPoint(0,5,phase,0,0));}
 internal void VerifyPair55Envelope(){SetBloomProgress(1);EnsureArtLooks();var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();var a=new Vector3[BottomSurface];var b=new Vector3[BottomSurface];var wa=new Vector3[BottomSurface];var wb=new Vector3[BottomSurface];SampleDiagnosticSurface(0,5,1,a);SampleDiagnosticSurface(1,5,1,b);for(int x=0;x<=16;x++)for(int y=0;y<=16;y++){var ma=Pair55ValidationMatrix(1,Pair55TiltBias+Pair55BreathScale*(-4+x*.5f),OpenRoll(0,5)+Pair55RollBias);var mb=OpenMatrix(1,5,1,-4+y*.5f,OpenRoll(1,5));for(int k=0;k<BottomSurface;k++){wa[k]=ma.MultiplyPoint3x4(a[k]);wb[k]=mb.MultiplyPoint3x4(b[k]);}scan.SamplePetalPositions(5,wa);scan.SamplePetalPositions(12,wb);scan.FindPairContacts(5,12,hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))throw new Exception("Pair55 independent envelope contact "+x+","+y);}Directory.CreateDirectory("Outputs/Pair55");File.WriteAllText("Outputs/Pair55/Envelope.txt","PASS: 17 x 17 independent breathing tilt combinations including extrema.");}
 internal void VerifyPair55(){
  Directory.CreateDirectory("Outputs/Pair55");SetBloomProgress(1);EnsureArtLooks();LoadFlexCandidate();
  var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();var petals=scan.Petals;var local=new Vector3[PetalCount][];var world=new Vector3[PetalCount][];
  for(int p=0;p<PetalCount;p++){local[p]=new Vector3[BottomSurface];world[p]=new Vector3[BottomSurface];SampleDiagnosticSurface(petals[p].layer,petals[p].index,1,local[p]);}
  void Pose(int p,float time,bool candidate){var item=petals[p];PetalArtMotion(new Vector4(0,0,1,0),time,item.index*Mathf.PI*2/layers[item.layer].petals.Length,item.layer,item.index,out float tilt,out float roll);if(p==5&&!candidate){float period=7+4*Mathf.Repeat(6*.618034f,1);tilt=artProfiles[2].amplitude*Mathf.Sin(time*Mathf.PI*2/period+5*Mathf.PI*2/7);roll-=Pair55RollBias;}if(p==10){artTime=time;QueryFlex(flexData,FlexParameter(),flexField.controls,out tilt);}var m=p==1?Pair01ValidationMatrix(1,tilt,roll):p==14?Pair20ValidationMatrix(1,tilt,roll):p==17?Pair24ValidationMatrix(1,tilt,roll):p==8?Pair10ValidationMatrix(1,tilt,roll):OpenMatrix(item.layer,item.index,1,tilt,roll);if(p==5&&candidate){var l=layers[0];var petal=l.petals[5];PathPetalPose(l,petal,5,1,out var position,out var rotation,out var scale);position.y=layers[0].config.rootHeight+.0045f+l.config.rootStagger*RootStaggerSign(5,l.petals.Length);AdjustPair55ArtPose(0,5,1,ref rotation,ref scale);ApplyPetalPose(l,petal,5,position,rotation*Quaternion.Euler(tilt,0,roll),scale);m=generated.worldToLocalMatrix*petal.body.localToWorldMatrix;}for(int k=0;k<BottomSurface;k++)world[p][k]=m.MultiplyPoint3x4(local[p][k]+(p==10?flexData.normals[k]*flexField.Height(k):Vector3.zero));scan.SamplePetalPositions(p,world[p]);}
  bool Contact(int a,int b){scan.FindPairContacts(Mathf.Min(a,b),Mathf.Max(a,b),hits);return hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f);}
  bool Check(float time){for(int p=0;p<PetalCount;p++)Pose(p,time,false);var old=new HashSet<int>();for(int o=0;o<PetalCount;o++)if(o!=5&&Contact(5,o))old.Add(o);Pose(5,time,true);if(Contact(5,12)){foreach(var hit in hits)if(hit.lengthA>=.25f&&hit.lengthB>=.25f){Debug.Log("PAIR55_UV "+time+" outer="+hit.lengthA+","+hit.widthA+" middle="+hit.lengthB+","+hit.widthB);break;}return false;}if(Contact(0,1)||Contact(2,3))return false;for(int o=0;o<PetalCount;o++)if(o!=5&&Contact(5,o)&&!old.Contains(o)){Debug.Log("PAIR55_NEW 5:"+o+" at "+time);return false;}return true;}
  for(int step=0;step<=1200;step++){if(!Check(step*.1f))throw new Exception("Pair55 regression at "+step*.1f);if(step%100==0)Debug.Log("PAIR55_VERIFIED "+step*.1f);}
  File.WriteAllText("Outputs/Pair55/Result.txt","PASS: 1201 actual runtime-pose samples over 120 seconds; pair L0-P5/L1-P5 clear; no new L0-P5 neighbour contacts; P0/P1 and P2/P3 retained.");
 }
 internal void VerifyPair55Transitions(){
  LoadFlexCandidate();var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();int count=0;
  HashSet<int> Contacts(){scan.SampleRendered();var result=new HashSet<int>();for(int o=0;o<PetalCount;o++)if(o!=5){scan.FindPairContacts(Mathf.Min(5,o),Mathf.Max(5,o),hits);if(hits.Exists(h=>h.lengthA>=.25f&&h.lengthB>=.25f))result.Add(o);}return result;}
  foreach(var state in new[]{FlowerArtState.Clarity,FlowerArtState.Gathering,FlowerArtState.Quiet})foreach(bool entering in new[]{true,false}){
   SetArtPreviewImmediate(entering?state:FlowerArtState.Wandering);SetArtState(entering?FlowerArtState.Wandering:state);
   for(int step=0;step<=60;step++){MotionStep(.1f);var candidate=Contacts();var l=layers[0];var p=l.petals[5];var savedPosition=p.anchor.localPosition;var savedRotation=p.anchor.localRotation;var savedScale=p.scale;PetalArtMotion(ArtWeights,artTime,5*Mathf.PI*2/7,0,5,out float tilt,out float roll);float raw=artProfiles[2].amplitude*Mathf.Sin(artTime*Mathf.PI*2/(7+4*Mathf.Repeat(6*.618034f,1))+5*Mathf.PI*2/7);tilt-=ArtWeights.z*WanderingSeamPoseGate*(Pair55TiltBias+(Pair55BreathScale-1)*raw);roll-=ArtWeights.z*WanderingSeamPoseGate*Pair55RollBias;PathPetalPose(l,p,5,StandardLayerProgress(l.config,bloomProgress),out var position,out var rotation,out var scale);ApplyPetalPose(l,p,5,position,rotation*Quaternion.Euler(tilt,0,roll),scale);var baseline=Contacts();ApplyPetalPose(l,p,5,savedPosition,savedRotation,savedScale);foreach(int other in candidate)if(!baseline.Contains(other))throw new Exception("New transition contact 5:"+other+" state="+state+" entering="+entering+" time="+artTime);if(RootAnchorError()>1e-5f)throw new Exception("Root drift");count++;}
  }
  SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(10);var held=layers[0].petals[5].body.localToWorldMatrix;float clock=artTime;SetFrozen(true);for(int i=0;i<30;i++)MotionStep(.1f);if(artTime!=clock||held!=layers[0].petals[5].body.localToWorldMatrix)throw new Exception("Frozen P5 moved");SetFrozen(false);Directory.CreateDirectory("Outputs/Pair55");File.WriteAllText("Outputs/Pair55/Transitions.txt","PASS: "+count+" six-direction transition samples, no added P5 contacts, fixed roots and freeze.");
 }

 internal void CapturePair55(){
  if(!UsesGpuPlayback)throw new Exception("Review requires formal GPU playback");
  Directory.CreateDirectory("Outputs/Pair55");Directory.CreateDirectory("Outputs/WanderFollow");SetArtPreviewImmediate(FlowerArtState.Wandering);
  VerifyOuterGpuFlex();
  var gallery=FindObjectOfType<FlowerGallery>();gallery.enabled=false;gallery.showInterface=false;
  var labels=new GameObject("Outer pair review labels");
  foreach(int index in new[]{5,12}){var item=new GameObject(index==5?"L0-P5":"L1-P5");item.transform.SetParent(labels.transform);var text=item.AddComponent<TextMesh>();text.text=item.name;text.fontSize=48;text.characterSize=.045f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.yellow;}
  void Label(){foreach(Transform child in labels.transform){int layer=child.name.StartsWith("L0")?0:1;var p=layers[layer].petals[5];var v=PetalMesh(layer,5).vertices;child.position=p.body.TransformPoint(v[48*129+64])+Camera.main.transform.up*.05f;child.rotation=Camera.main.transform.rotation;}}
  void Shot(string name){Label();WanderFollowPreview.Shot(name);File.Copy("Outputs/WanderFollow/"+name+".png","Outputs/Pair55/"+name+".png",true);}
  foreach(var view in new[]{new Vector2(22,55),new Vector2(0,90),new Vector2(22,8)}){
   SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(10);gallery.SetView(view.x,view.y);string suffix=view.y.ToString("F0");Shot("After"+suffix);
   var l=layers[0];var p=l.petals[5];float tilt=artProfiles[2].amplitude*Mathf.Sin(artTime*Mathf.PI*2/(7+4*Mathf.Repeat(6*.618034f,1))+5*Mathf.PI*2/7);float roll=artLooks[2].asym*Mathf.Sin(6*1.7f);PathPetalPose(l,p,5,1,out var position,out var rotation,out var scale);ApplyPetalPose(l,p,5,position,rotation*Quaternion.Euler(tilt,0,roll),scale);Shot("Before"+suffix);
  }
  SetArtPreviewImmediate(FlowerArtState.Wandering);gallery.SetView(22,55);
  for(int frame=0;frame<120;frame++){MotionStep(.1f);Shot("Frame"+frame.ToString("D3"));}
  DestroyImmediate(labels);File.WriteAllText("Outputs/Pair55/Review.txt","PASS: formal GPU captures, 3 labelled before/after views and 120 animation frames at 10 Hz.");
 }
}
}
#endif
