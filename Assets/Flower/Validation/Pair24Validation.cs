#if UNITY_EDITOR
using System;using System.Collections.Generic;using System.IO;using UnityEngine;
namespace MindState {public sealed partial class FlowerAssembler {
 // Shared steady-Wandering reference used by the existing adjacent-pair regressions.
 Matrix4x4 Pair24ValidationMatrix(float phase,float tilt,float roll){var l=layers[2];var p=l.petals[4];PathPetalPose(l,p,4,StandardLayerProgress(l.config,phase),out var position,out var rotation,out var scale);rotation=Quaternion.AngleAxis(-16.3266544f+Pair24End.y,Vector3.up)*rotation;scale=Vector3.Scale(scale,new Vector3(.9f*.711982548f,.8257039f,1.075196f));tilt=(tilt-Pair24TiltBias)*.5f+Pair24TiltBias+Pair24End.x;roll+=Pair24End.z;position.y=layers[0].config.rootHeight+.0045f+l.config.rootStagger*RootStaggerSign(4,l.petals.Length);return Matrix4x4.TRS(position,rotation*Quaternion.Euler(tilt,0,roll),scale)*Matrix4x4.Translate(-BottomPoint(2,4,phase,0,0));}
 float pair24ValidationProgress=float.NaN;Vector3[][] pair24ValidationVertices;
 // Cache immutable GPU source frames during offline contact scans; shader readback proof remains separate.
 sealed class Pair24FrameCache {internal PlaybackClip clip;internal readonly Dictionary<int,Vertex[]> frames=new Dictionary<int,Vertex[]>();internal readonly Queue<int> order=new Queue<int>();}
 Pair24FrameCache[] pair24Frames;
 Vector3[] Pair24ValidationSurface(int id,int layer,int index){
  if(!gpuPlayback)return PetalMesh(layer,index).vertices;
  var clip=playback[layer][index];if(pair24Frames==null)pair24Frames=new Pair24FrameCache[PetalCount];var cache=pair24Frames[id];if(cache==null||cache.clip!=clip)pair24Frames[id]=cache=new Pair24FrameCache{clip=clip};
  Vertex[] Frame(int number){if(cache.frames.TryGetValue(number,out var data))return data;data=new Vertex[clip.count];clip.buffer.GetData(data,0,number*clip.count,clip.count);if(cache.order.Count>=32)cache.frames.Remove(cache.order.Dequeue());cache.frames.Add(number,data);cache.order.Enqueue(number);return data;}
  var frame=PlaybackFrame(clip,id==17?Pair24PlaybackProgress(bloomProgress):bloomProgress);var first=Frame((int)frame.x);var next=Frame((int)frame.y);var values=new Vertex[clip.count];for(int i=0;i<values.Length;i++)Interpolate(ref first[i],ref next[i],frame.z,out values[i]);
  if(id==10)ApplyFlexVertices(values,flexWeight);if(id==17)ApplyPair24CurlVertices(values);return Array.ConvertAll(values,v=>v.p);
 }
 void ValidatePair24Contacts(FlowerSurfaceCollision scan,Vertex[] gpuTarget=null,bool both=false){
  var expectedScale=Vector3.Scale(PetalScale(layers[2].config,layers[2].petals[4],4,1),Pair24FixedScale);if((layers[2].petals[4].body.localScale-expectedScale).sqrMagnitude>1e-12f)throw new Exception("L2-P4 intrinsic scale changed");scan.RefreshRenderRootTrim();bool changed=pair24ValidationProgress!=bloomProgress;if(pair24ValidationVertices==null)pair24ValidationVertices=new Vector3[PetalCount][];int sheets=both?2:1;var world=new Vector3[sheets][][];
  for(int sheet=0;sheet<sheets;sheet++){world[sheet]=new Vector3[PetalCount][];}
  for(int i=0;i<PetalCount;i++){var item=scan.Petals[i];Vector3[] vertices;if(i==17&&gpuTarget!=null)vertices=Array.ConvertAll(gpuTarget,v=>v.p);else if(changed||i==10||i==17||pair24ValidationVertices[i]==null){vertices=Pair24ValidationSurface(i,item.layer,item.index);pair24ValidationVertices[i]=vertices;}else vertices=pair24ValidationVertices[i];var matrix=layers[item.layer].petals[item.index].body.localToWorldMatrix;if(i==17){var root=matrix.MultiplyPoint3x4((vertices[64]+vertices[BottomSurface+64])*.5f);if(Vector3.Distance(root,layers[2].petals[4].anchor.position)>1e-5f)throw new Exception("Actual L2-P4 surface root detached");}
   for(int sheet=0;sheet<sheets;sheet++){var values=new Vector3[BottomSurface];for(int k=0;k<BottomSurface;k++){var point=both?vertices[k+sheet*BottomSurface]:(vertices[k]+vertices[k+BottomSurface])*.5f;values[k]=matrix.MultiplyPoint3x4(point);}world[sheet][i]=values;}
  }
  pair24ValidationProgress=bloomProgress;
  for(int otherSheet=0;otherSheet<sheets;otherSheet++){scan.SamplePositions(world[otherSheet]);for(int sheet=0;sheet<sheets;sheet++){
   scan.SamplePetalPositions(17,world[sheet][17]);for(int other=0;other<PetalCount;other++)if(other!=17&&scan.PairHasVisibleContact(Mathf.Min(other,17),Mathf.Max(other,17),out var hit))throw new Exception("L2-P4 contact other="+other+" time="+artTime+" weights="+ArtWeights+" lengths="+hit.lengthA+","+hit.lengthB+" sheets="+sheet+","+otherSheet);
  }
  }
  if(RootAnchorError()>1e-5f)throw new Exception("Pair24 root drift");
 }
 internal void VerifyPair24ScaleBounds(){var saved=ArtWeights;foreach(int state in new[]{0,1,3})for(int step=0;step<=1000;step++){float w=step/1000f;var weights=Vector4.zero;weights[state]=1-w;weights.z=w;PreparePair24Pose(weights,false);if(pair24Scale!=Pair24FixedScale)throw new Exception("L2-P4 size changed with state weight");}PreparePair24Pose(saved,false);Directory.CreateDirectory("Outputs/Pair24");File.WriteAllText("Outputs/Pair24/ScaleBounds.txt","PASS: fixed intrinsic scale at 3003 state-weight samples; no state-dependent sizing.");}
 void VerifyPair24FreezeResume(){var p=layers[2].petals[4];var held=p.body.localToWorldMatrix;float clock=artTime;var weights=ArtWeights;SetFrozen(true);for(int i=0;i<3;i++)MotionStep(.1f);if(clock!=artTime||weights!=ArtWeights||held!=p.body.localToWorldMatrix)throw new Exception("Frozen L2-P4 moved");SetFrozen(false);MotionStep(0);for(int i=0;i<16;i++)if(Mathf.Abs(held[i]-p.body.localToWorldMatrix[i])>1e-6f)throw new Exception("L2-P4 jumped on resume");if(clock!=artTime||weights!=ArtWeights)throw new Exception("L2-P4 phase reset on resume");}
 internal void VerifyPair24Transitions(){
  VerifyPair24ScaleBounds();bool quick=Array.IndexOf(Environment.GetCommandLineArgs(),"-pair24Quick")>=0;var scan=new FlowerSurfaceCollision(this);int frames=0,checks=0,caseIndex=0;float maxTurn=0,maxTip=0;var args=Environment.GetCommandLineArgs();int startArg=Array.IndexOf(args,"-pair24CaseStart"),caseStart=startArg<0?0:int.Parse(args[startArg+1]);
  foreach(int hz in quick?new[]{60}:new[]{60,30,120})foreach(float phase in hz==60?new[]{10f,14f,0f,2.6f,5f}:new[]{0f})foreach(var state in new[]{FlowerArtState.Clarity,FlowerArtState.Gathering,FlowerArtState.Quiet})foreach(bool entering in new[]{true,false}){
   if(caseIndex++<caseStart)continue;Debug.Log("PAIR24_GENTLE_BEGIN phase="+phase+" state="+state+" entering="+entering);
   SetArtPreviewImmediate(entering?state:FlowerArtState.Wandering);MotionStep(phase);SetArtState(entering?FlowerArtState.Wandering:state);
   var previous=layers[2].petals[4].anchor.localRotation;float previousTip=pair24TipAngle;var expected=layers[2].petals[4].body.localScale;
   for(int step=0;step<=hz*6;step++){if(step>0)MotionStep(1f/hz);var current=layers[2].petals[4].anchor.localRotation;float turn=Quaternion.Angle(previous,current),tip=Mathf.Abs(pair24TipAngle-previousTip);if(step>0){maxTurn=Mathf.Max(maxTurn,turn*hz);maxTip=Mathf.Max(maxTip,tip*hz);if(turn>45f/hz+.05f||tip>45f/hz+.001f)throw new Exception("L2-P4 abrupt turn/fold: "+turn*hz+","+tip*hz);}previous=current;previousTip=pair24TipAngle;if(layers[2].petals[4].body.localScale!=expected)throw new Exception("L2-P4 frame scale changed");if(!quick&&step==hz*3)VerifyPair24FreezeResume();if(step%(hz/10)==0){ValidatePair24Contacts(scan,null,!quick||step%(hz/2)==0);checks++;}frames++;if(RootAnchorError()>1e-5f)throw new Exception("Root drift");}
   Debug.Log("PAIR24_GENTLE_PASS hz="+hz+" phase="+phase+" state="+state+" entering="+entering);
  }
  if(!quick)foreach(var state in new[]{FlowerArtState.Clarity,FlowerArtState.Gathering,FlowerArtState.Quiet}){SetArtPreviewImmediate(state);SetArtState(FlowerArtState.Wandering);for(int step=0;step<=720;step++){if(step==180)SetArtState(state);if(step==360)SetArtState(FlowerArtState.Wandering);if(step>0)MotionStep(1f/60);if(step%6==0){ValidatePair24Contacts(scan,null,true);checks++;}frames++;}}
  SetArtPreviewImmediate(FlowerArtState.Quiet);MotionStep(4);var held=layers[2].petals[4].body.localToWorldMatrix;var heldNeighbor=layers[2].petals[3].body.localToWorldMatrix;float clock=artTime;SetFrozen(true);for(int i=0;i<30;i++)MotionStep(.1f);if(clock!=artTime||held!=layers[2].petals[4].body.localToWorldMatrix||heldNeighbor!=layers[2].petals[3].body.localToWorldMatrix)throw new Exception("Frozen pair moved");SetFrozen(false);
  File.WriteAllText("Outputs/Pair24/"+(quick?"GentleQuick":"GentleTransitions")+".txt","PASS: "+frames+" motion frames, "+checks+" strict contact samples; roots, mid-transition freeze/resume, fixed intrinsic scale; maximum anchor turn="+maxTurn+" deg/s, tip fold="+maxTip+" deg/s.");File.WriteAllText("Outputs/Pair24/GentleValidatedSource.txt",FlowerCacheProvenance.Signature());
 }
 internal void VerifyPair24(){var scan=new FlowerSurfaceCollision(this);SetArtPreviewImmediate(FlowerArtState.Wandering);for(int i=0;i<=1200;i++){if(i>0)MotionStep(.1f);ValidatePair24Contacts(scan,null,true);if(i%100==0)Debug.Log("PAIR24_STEADY "+i/10);}File.WriteAllText("Outputs/Pair24/GentleSteady.txt","PASS: 120 seconds of strict L2-P4 runtime contacts, including sheet checks.");}
 internal void VerifyPair24Envelope(){var scan=new FlowerSurfaceCollision(this);foreach(var state in new[]{FlowerArtState.Clarity,FlowerArtState.Gathering,FlowerArtState.Quiet}){SetArtPreviewImmediate(state);for(int i=0;i<=140;i++){if(i>0)MotionStep(.1f);ValidatePair24Contacts(scan,null,true);}}File.WriteAllText("Outputs/Pair24/GentleOtherStates.txt","PASS: complete breathing cycles of the other three stable states.");}
 internal void VerifyPair24CurlGpu(){
  if(!UsesGpuPlayback)throw new Exception("Curl proof requires GPU playback");
  var before=WanderFlexPreview.StandardHashes(this);SetArtPreviewImmediate(FlowerArtState.Wandering);EnsurePair24CurlTopology();
  var shader=Resources.Load<ComputeShader>("FlowerPlaybackVerify");int kernel=shader.FindKernel("SampleFlex");float maxPosition=0,maxAngle=0;int cases=0,sheetChecks=0;var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();
  void CheckSheets(Vertex[] rendered,Vertex[] baseline){ValidatePair24Contacts(scan,rendered,true);sheetChecks++;}
  try{
   foreach(var state in new[]{FlowerArtState.Clarity,FlowerArtState.Gathering,FlowerArtState.Quiet})foreach(bool entering in new[]{true,false})foreach(float time in new[]{0f,.5f,.9f,1f,2f,2.6f,3.3f,4f,4.5f,5f,5.2f,5.8f,6f}){
    SetArtPreviewImmediate(entering?state:FlowerArtState.Wandering);SetArtState(entering?FlowerArtState.Wandering:state);for(float elapsed=0;elapsed<time;){float dt=Mathf.Min(1f/60,time-elapsed);MotionStep(dt);elapsed+=dt;}var clip=playback[2][4];var expected=SamplePlayback(clip,Pair24PlaybackProgress(bloomProgress));var baseline=(Vertex[])expected.Clone();ApplyPair24CurlVertices(expected);if(ArtWeights.z==1){for(int i=20*129;i<BottomSurface;i++)if(expected[i].p!=baseline[i].p||expected[i+BottomSurface].p!=baseline[i+BottomSurface].p)throw new Exception("Stable Wandering sheet changed");PetalArtMotion(ArtWeights,artTime,4*Mathf.PI*2/5,2,4,out float tilt,out float roll);var reference=Pair24ValidationMatrix(1,tilt,roll);var actual=layers[2].petals[4].anchor.parent.worldToLocalMatrix*layers[2].petals[4].body.localToWorldMatrix;for(int i=0;i<16;i++)if(Mathf.Abs(reference[i]-actual[i])>2e-5f)throw new Exception("Stable Wandering pose changed");}
    using(var output=new ComputeBuffer(clip.count,40)){
     shader.SetBuffer(kernel,"_FlowerPoses",clip.buffer);shader.SetVector("_FlowerFrame",PlaybackFrame(clip,Pair24PlaybackProgress(bloomProgress)));shader.SetBuffer(kernel,"_FlexNormals",flexNormalsBuffer);shader.SetBuffer(kernel,"_FlexWall",pair24CurlWallBuffer);shader.SetFloat("_FlexWeight",0);shader.SetInt("_FlexVertexCount",clip.count);shader.SetFloat(Pair24CurlId,pair24CurlAmount);shader.SetVector(Pair24CurlRootId,pair24CurlRoot);shader.SetFloat(Pair24TipId,pair24TipAngle);shader.SetInt("_Count",clip.count);shader.SetBuffer(kernel,"_Result",output);shader.Dispatch(kernel,(clip.count+63)/64,1,1);var result=new Vertex[clip.count];output.GetData(result);
     var culling=clip.mesh.bounds;culling.Expand(.0002f);var motion=clip.motionBounds;motion.Expand(.0002f);
     for(int i=0;i<result.Length;i++){if(!Finite(result[i].p.sqrMagnitude)||!Finite(result[i].n.sqrMagnitude)||!Finite(new Vector3(result[i].t.x,result[i].t.y,result[i].t.z).sqrMagnitude))throw new Exception("Nonfinite curl vertex");maxPosition=Mathf.Max(maxPosition,Vector3.Distance(result[i].p,expected[i].p));maxAngle=Mathf.Max(maxAngle,DirectionError(result[i].n,expected[i].n),DirectionError(new Vector3(result[i].t.x,result[i].t.y,result[i].t.z),new Vector3(expected[i].t.x,expected[i].t.y,expected[i].t.z)));if(!culling.Contains(result[i].p)||!motion.Contains(result[i].p))throw new Exception("Curl vertex outside runtime bounds");}
     CheckSheets(result,baseline);
    }
    cases++;if(cases%13==0)Debug.Log("PAIR24_GPU_CURL_CASES "+cases);
   }
  }finally{shader.SetFloat(Pair24CurlId,0);}
  if(maxPosition>.0001f||maxAngle>1)throw new Exception("Curl GPU mismatch: "+maxPosition+", "+maxAngle);
  SetArtPreviewImmediate(FlowerArtState.Clarity);ClipBlend(new Vector4(.5f,0,0,.5f));for(int i=0;i<60;i++)MotionStep(1f/60);int uploads=MeshUpdates;long allocatedBefore=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<240;i++)MotionStep(1f/60);long allocated=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;if(allocated!=0||MeshUpdates!=uploads)throw new Exception("Active GPU curl allocated or uploaded vertices: "+allocated);
  var after=WanderFlexPreview.StandardHashes(this);for(int i=0;i<before.Length;i++)if(before[i]!=after[i])throw new Exception("Curl changed standard geometry at sample "+i);
  Directory.CreateDirectory("Outputs/Pair24");File.WriteAllText("Outputs/Pair24/GpuCurl.txt","PASS: "+cases+" complete GPU vertex readbacks and "+sheetChecks+" two-sided contact checks; maximum position error="+maxPosition+", normal/tangent error="+maxAngle+" degrees; conservative bounds; fixed intrinsic scale and gentle closed-state fold; 240 warmed active-curl frames: GC=0, vertex uploads=0; all 18 standard petals at 23 opening samples byte-identical before/after.");File.WriteAllText("Outputs/Pair24/GpuValidatedSource.txt",FlowerCacheProvenance.Signature());
 }

 internal void CapturePair24(){
  if(!UsesGpuPlayback)throw new Exception("Review requires production GPU playback");
  var args=Environment.GetCommandLineArgs();var state=Array.IndexOf(args,"-pair24QuietReview")>=0?FlowerArtState.Quiet:Array.IndexOf(args,"-pair24GatheringReview")>=0?FlowerArtState.Gathering:FlowerArtState.Clarity;string fullFolder="FixedSize"+state+"FullFrames",closeFolder="FixedSize"+state+"CloseFrames";
  Directory.CreateDirectory("Outputs/Pair24/"+fullFolder);Directory.CreateDirectory("Outputs/Pair24/"+closeFolder);Directory.CreateDirectory("Outputs/OuterPair");VerifyOuterGpuFlex();
  var gallery=FindObjectOfType<FlowerGallery>();gallery.enabled=false;gallery.showInterface=false;ShowAllPetals();gallery.SetView(22,55,8.5f);
  var camera=Camera.main;var rotation=camera.transform.rotation;var worldToView=Quaternion.Inverse(rotation);bool first=true;var framing=new Bounds();var contactScan=new FlowerSurfaceCollision(this);
  SetArtPreviewImmediate(state);SetArtState(FlowerArtState.Wandering);
  for(int frame=0;frame<1200;frame++){
   if(frame==840)SetArtState(state);MotionStep(1f/60);if(frame%6==0)ValidatePair24Contacts(contactScan,null,true);if(frame%12!=0&&frame!=1199)continue;
   foreach(int index in new[]{3,4}){var matrix=layers[2].petals[index].body.localToWorldMatrix;foreach(var vertex in PetalMesh(2,index).vertices){var point=worldToView*matrix.MultiplyPoint3x4(vertex);if(first){framing=new Bounds(point,Vector3.zero);first=false;}else framing.Encapsulate(point);}}
  }
  framing.Expand(.4f);var center=rotation*framing.center;float tan=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);float closeDistance=(Mathf.Max(framing.extents.y/tan,framing.extents.x/(tan*(1280f/720)))+framing.extents.z)*1.05f;
  var marker=new GameObject("L2-P4 curl review label");var label=marker.AddComponent<TextMesh>();label.text="L2-P4";label.fontSize=48;label.characterSize=.035f;label.anchor=TextAnchor.MiddleCenter;label.color=Color.yellow;
  var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf){antiAliasing=4};var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var oldTarget=camera.targetTexture;
  try{target.Create();using(var readback=new FlowerCaptureReadback(1280,720)){
   bool closeOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"-pair24CloseOnly")>=0;
   foreach(bool close in closeOnly?new[]{true}:new[]{false,true}){
    ShowAllPetals();gallery.SetView(22,55,8.5f);if(close){SoloPetal(2,3);SetPetalVisible(2,4,true);camera.transform.position=center-rotation*Vector3.forward*closeDistance;camera.transform.rotation=rotation;}
    SetArtPreviewImmediate(state);SetArtState(FlowerArtState.Wandering);string folder=close?closeFolder:fullFolder;
    for(int frame=0;frame<1200;frame++){
     if(frame==840)SetArtState(state);MotionStep(1f/60);
     if(close){if(echoRoot!=null)echoRoot.gameObject.SetActive(false);if(ribbonRoot!=null)ribbonRoot.gameObject.SetActive(false);}
     marker.transform.position=layers[2].petals[4].body.TransformPoint(PetalMesh(2,4).vertices[48*129+64])+camera.transform.up*.1f;marker.transform.rotation=camera.transform.rotation;
     camera.targetTexture=target;camera.Render();readback.Read(target,image);File.WriteAllBytes("Outputs/Pair24/"+folder+"/Frame"+frame.ToString("D3")+".png",image.EncodeToPNG());
     if(frame%120==0)Debug.Log("PAIR24_CAPTURE "+folder+" "+frame);
    }
   }
  }}finally{camera.targetTexture=oldTarget;target.Release();DestroyImmediate(target);DestroyImmediate(image);DestroyImmediate(marker);ShowAllPetals();}
  File.WriteAllText("Outputs/Pair24/FixedSize"+state+"Review.txt","PASS: production GPU rendering, 60 FPS fixed time step, "+(Array.IndexOf(Environment.GetCommandLineArgs(),"-pair24CloseOnly")>=0?"1200 close-up frames":"1200 full-flower frames and 1200 close-up frames")+". Six seconds enter Wandering, eight seconds hold stable breathing, six seconds return to "+state+". Two-sided contact scans through the complete sequence. No temporal interpolation or motion blur.");
 }

}
}
#endif
