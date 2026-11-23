#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 const float FlexNormalClearance=.015f,FlexNormalMinimum=.0022f;
 internal void PrepareFlexBake(){
  if(flexData!=null)return;
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-retainFlexCandidate")>=0){BuildFlexCandidate();SetBloomProgress(1);return;}
  string source=FlowerCacheProvenance.Signature(),candidate="Outputs/WanderFlex/Candidate.bin";
  bool validated=File.Exists(candidate);
  foreach(string mode in new[]{"Cpu","Gpu"}){string result="Outputs/WanderFlex/"+mode+"Result.txt",stamp="Outputs/WanderFlex/"+mode+"ValidatedSource.txt",hash="Outputs/WanderFlex/"+mode+"ValidatedCandidate.sha256";validated=validated&&File.Exists(result)&&File.ReadAllText(result).StartsWith("PASS:")&&File.Exists(stamp)&&File.ReadAllText(stamp)==source&&File.Exists(hash)&&File.ReadAllText(hash)==FlexFileHash(candidate);}
  if(validated){
   var expected=File.ReadAllLines("Outputs/WanderFlex/CpuStandardHashes.txt");var actual=WanderFlexPreview.StandardHashes(this);if(expected.Length!=actual.Length)throw new InvalidOperationException("Static baseline reference count mismatch");for(int i=0;i<actual.Length;i++)if(actual[i]!=expected[i])throw new InvalidOperationException("Accepted static baseline changed before cache publication at sample "+i);LoadFlexCandidate();
  }else{
   // The standalone bake also prepares the local correction; it cannot silently
   // regenerate only the old geometry and omit this feature.
   BuildFlexCandidate();for(int i=0;i<=30;i++)if(!ValidateFlexAt(i*.5f,out string message))throw new InvalidOperationException("Flex CPU pre-publication failure: "+message);
   foreach(var target in new[]{FlowerArtState.Clarity,FlowerArtState.Wandering,FlowerArtState.Gathering,FlowerArtState.Wandering}){SetArtState(target);for(int i=0;i<360;i++){MotionStep(1f/60);if(i%60==59&&!ValidateFlexTransition(out string message))throw new InvalidOperationException("Flex transition pre-publication failure: "+message);}}
  }
  SetBloomProgress(1);
 }
 internal static string FlexFileHash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
 internal static bool FlexStagedPlaybackAvailable(){
  string candidate=Path.GetFullPath("Outputs/WanderFlex/Candidate.bin"),marker=Path.GetFullPath("Outputs/WanderFlex/GpuBase.sha256");if(!File.Exists(candidate)||!File.Exists(marker))return false;
  using(var r=new BinaryReader(File.OpenRead(candidate))){if(r.ReadString()!=FlexFormat)return false;r.ReadString();if(r.ReadString()!=File.ReadAllText(Path.Combine(Application.dataPath,"Flower/Resources/BottomOverlapProvenance.txt")).Trim())return false;}
  return File.ReadAllText(marker).Trim()==FlexFileHash(PlaybackPath);
 }
 internal void LoadFlexCandidate(){LoadFlex(Path.GetFullPath("Outputs/WanderFlex/Candidate.bin"));}
 internal void ValidateFlexShape(){
  SetBloomProgress(1);Vertex[] rest;if(gpuPlayback)rest=SamplePlayback(playback[1][3],1);else{var patch=bottomPatches.Find(p=>p.layer==1&&p.index==3);ApplyBottomPatch(patch);rest=(Vertex[])patch.work.Clone();}float worst=0;int worstFrame=0;
  var points=new Vector3[BottomSurface];var h=new float[BottomSurface];var saved=flexField.controls;
  try{for(int frame=0;frame<flexData.frames.Length;frame++){flexField.controls=flexData.frames[frame].controls;for(int id=0;id<BottomSurface;id++){h[id]=flexField.Height(id);if(Mathf.Abs(h[id])>.10001f)throw new InvalidDataException("Flex displacement exceeds 0.10 at frame "+frame);points[id]=rest[id].p+flexData.normals[id]*h[id];}for(int y=9;y<=80;y++)for(int x=0;x<129;x++){int a=y*129+x;for(int axis=0;axis<2;axis++){int b=axis==0?(x<128?a+1:a):(y<80?a+129:a);if(a==b||(h[a]==0&&h[b]==0))continue;float length=(rest[b].p-rest[a].p).magnitude;if(length<1e-6f)continue;float strain=Mathf.Abs((points[b]-points[a]).magnitude/length-1);if(strain>worst){worst=strain;worstFrame=frame;}}}}}finally{flexField.controls=saved;}
  File.WriteAllText("Outputs/WanderFlex/ShapeRegularity.txt","max relative edge length change="+worst+" frame="+worstFrame+"; displacement <=0.10; fixed root fade unchanged");
  if(worst>.5f)throw new InvalidDataException("Excessive local stretching at flex frame "+worstFrame+": "+worst);
 }
 internal bool ValidateFlexAt(float time,out string message){
  flexBypass=true;SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(time);var scan=new FlowerSurfaceCollision(this);var hits=new List<FlowerSurfaceCollision.Contact>();scan.SampleRendered();scan.FindContacts(hits,parallel:true);var oldPairs=new HashSet<string>();foreach(var h in hits)if((h.petalA==10||h.petalB==10)&&h.lengthA>=.25f&&h.lengthB>=.25f)oldPairs.Add(h.petalA+":"+h.petalB);bool oldSelf=hits.Exists(h=>h.petalA==10&&h.petalB==10&&!h.rootRegion);
  flexBypass=false;SetArtPreviewImmediate(FlowerArtState.Wandering);MotionStep(time);scan.SampleRendered();
  for(int other=0;other<scan.Petals.Length;other++)if(other!=10){scan.FindPairContacts(Mathf.Min(other,10),Mathf.Max(other,10),hits);bool targeted=Array.IndexOf(FlexNeighbours,other)>=0;var bad=hits.FindAll(h=>(other<10?h.lengthB:h.lengthA)>=.25f&&(other==3||(other<10?h.lengthA:h.lengthB)>=.25f));if(bad.Count>0&&(targeted||!oldPairs.Contains(Mathf.Min(other,10)+":"+Mathf.Max(other,10)))){message="time="+time+" contact="+other+":10 count="+bad.Count;return false;}}
  scan.FindContacts(hits,parallel:true);if(!oldSelf&&hits.Exists(h=>h.petalA==10&&h.petalB==10&&!h.rootRegion)){message="time="+time+" new self intersection";return false;}
  if(!ValidateFlexSheets(out message))return false;
  if(RootAnchorError()>1e-5f){message="time="+time+" root detached";return false;}
  message="time="+time+" target/neighbours/root clear";return true;
 }
 internal bool ValidateFlexSheets(out string message){
  var scan=new FlowerSurfaceCollision(this);scan.SampleRendered();var petals=scan.Petals;var target=PetalMesh(1,3).vertices;var a=new Vector3[BottomSurface];var b=new Vector3[BottomSurface];var hits=new List<FlowerSurfaceCollision.Contact>();var tm=petals[10].body.localToWorldMatrix;
  foreach(int other in FlexNeighbours){var item=petals[other];var vertices=PetalMesh(item.layer,item.index).vertices;var m=item.body.localToWorldMatrix;for(int sa=0;sa<2;sa++){for(int id=0;id<BottomSurface;id++)a[id]=tm.MultiplyPoint3x4(target[id+sa*BottomSurface]);scan.SamplePetalPositions(10,a);for(int sb=0;sb<2;sb++){for(int id=0;id<BottomSurface;id++)b[id]=m.MultiplyPoint3x4(vertices[id+sb*BottomSurface]);scan.SamplePetalPositions(other,b);scan.FindPairContacts(Mathf.Min(other,10),Mathf.Max(other,10),hits);if(hits.Exists(h=>(other<10?h.lengthB:h.lengthA)>=.25f&&(other==3||(other<10?h.lengthA:h.lengthB)>=.25f))){message="rendered sheet contact="+other+":10 sheets="+sa+","+sb;return false;}}}}
  message="both rendered sheets clear";return true;
 }
 internal float FlexMinimumGap(){
  var petals=GetDiagnosticPetals();var target=PetalMesh(1,3).vertices;var tm=petals[10].body.localToWorldMatrix;var a=new Vector3[BottomSurface];var b=new Vector3[BottomSurface];float gap=float.PositiveInfinity;
  for(int side=0;side<2;side++){for(int id=0;id<BottomSurface;id++)a[id]=tm.MultiplyPoint3x4(target[id+side*BottomSurface]);var tree=new FlexRayTree(a,20);foreach(int other in FlexNeighbours){var item=petals[other];var mesh=PetalMesh(item.layer,item.index).vertices;var matrix=item.body.localToWorldMatrix;for(int s=0;s<2;s++){for(int id=0;id<BottomSurface;id++)b[id]=matrix.MultiplyPoint3x4(mesh[id+s*BottomSurface]);gap=Mathf.Min(gap,tree.MinimumDistance(new FlexRayTree(b,other==3?0:20)));}}}return gap;
 }
 internal bool ValidateFlexCurrent(out string message){var scan=new FlowerSurfaceCollision(this);scan.SampleRendered();var hits=new List<FlowerSurfaceCollision.Contact>();foreach(int other in FlexNeighbours){scan.FindPairContacts(Mathf.Min(other,10),Mathf.Max(other,10),hits);if(hits.Exists(h=>(other<10?h.lengthB:h.lengthA)>=.25f&&(other==3||(other<10?h.lengthA:h.lengthB)>=.25f))){message="transition contact="+other+":10 progress="+bloomProgress+" W="+ArtWeights.z;return false;}}message="transition clear progress="+bloomProgress+" W="+ArtWeights.z;return true;}
 internal bool ValidateFlexTransition(out string message){
  if(RootAnchorError()>1e-5f){message="transition root detached";return false;}
  if(FlexGate<=0||ArtWeights.z<=1e-5f){message="baseline region retained progress="+bloomProgress+" W="+ArtWeights.z;return true;}
  // Compare to the identical mixed state. Existing non-Wandering animation is
  // retained in mixed states; pure Wandering must be clear of every constrained neighbour.
  flexBypass=true;UpdateArt(0);var scan=new FlowerSurfaceCollision(this);scan.SampleRendered();var hits=new List<FlowerSurfaceCollision.Contact>();var oldPairs=new HashSet<int>();foreach(int other in FlexNeighbours){scan.FindPairContacts(Mathf.Min(other,10),Mathf.Max(other,10),hits);if(hits.Exists(h=>(other<10?h.lengthB:h.lengthA)>=.25f&&(other==3||(other<10?h.lengthA:h.lengthB)>=.25f)))oldPairs.Add(other);}
  flexBypass=false;UpdateArt(0);scan.SampleRendered();foreach(int other in FlexNeighbours){scan.FindPairContacts(Mathf.Min(other,10),Mathf.Max(other,10),hits);if(hits.Exists(h=>(other<10?h.lengthB:h.lengthA)>=.25f&&(other==3||(other<10?h.lengthA:h.lengthB)>=.25f))&&(ArtWeights.z>=.9999f||!oldPairs.Contains(other))){message="new/mature-W transition contact="+other+":10 progress="+bloomProgress+" W="+ArtWeights.z;return false;}}
  message="transition no added contact progress="+bloomProgress+" W="+ArtWeights.z;return true;
 }
}
}
#endif
