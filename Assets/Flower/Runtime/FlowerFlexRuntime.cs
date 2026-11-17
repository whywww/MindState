using System;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 const string FlexFormat="FlowerFlex/1";
 sealed class FlexFrame { internal Vector3 parameter;internal float tilt;internal float[] controls; }
 sealed class FlexNode { internal Vector3 min,max;internal int[] corners;internal int[] children; }
 sealed class FlexData { internal float amplitude,roll;internal int vertices;internal Vector3[] normals;internal int[] wall;internal FlexFrame[] frames;internal FlexNode[] nodes;internal Vector3[] profileMotion;internal Vector2[] lookMotion; }
 FlexData flexData;
 FlexField flexField;
 ComputeBuffer flexNormalsBuffer,flexWallBuffer;
 Vector2[][] desiredArtMotion;
 float flexWeight,flexPreviousWeight;
#if UNITY_EDITOR
 internal bool flexBypass;
#endif
 static readonly int FlexWeightId=Shader.PropertyToID("_FlexWeight"),FlexControlsId=Shader.PropertyToID("_FlexControls"),FlexNormalsId=Shader.PropertyToID("_FlexNormals"),FlexWallId=Shader.PropertyToID("_FlexWall"),FlexCountId=Shader.PropertyToID("_FlexVertexCount");
 static string FlexPath=>Path.Combine(Application.streamingAssetsPath,"WanderingFlex.bin");
 string FlexBaseSignature(){
#if UNITY_EDITOR
  return File.ReadAllText(Path.Combine(Application.dataPath,"Flower/Resources/BottomOverlapProvenance.txt")).Trim();
#else
  return Resources.Load<TextAsset>("BottomOverlapProvenance").text.Trim();
#endif
 }
 string FlexMotionSignature(){EnsureArtLooks();return string.Join(";",Array.ConvertAll(artProfiles,p=>JsonUtility.ToJson(p)))+"|"+string.Join(";",Array.ConvertAll(artLooks,p=>JsonUtility.ToJson(p)));}
 float FlexGate=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6875f,.75f,bloomProgress));
 void LoadFlex(string path){
  using(var r=new BinaryReader(File.OpenRead(path))){
   if(r.ReadString()!=FlexFormat||r.ReadString()!=BottomConfigHash()||r.ReadString()!=FlexBaseSignature()||r.ReadString()!=FlexMotionSignature())throw new InvalidDataException("Wandering flex cache source mismatch; rebuild complete caches.");
   var data=new FlexData{amplitude=r.ReadSingle(),roll=r.ReadSingle(),vertices=ReadCount(r,100000)};
   if(!Finite(data.amplitude)||data.amplitude<=0||data.vertices!=layers[1].petals[3].body.GetComponent<MeshFilter>().sharedMesh.vertexCount)throw new InvalidDataException("Wandering flex topology/parameter mismatch");
   data.normals=new Vector3[BottomSurface];for(int i=0;i<BottomSurface;i++){data.normals[i]=ReadVector(r);if(!Finite(data.normals[i].sqrMagnitude))throw new InvalidDataException("Invalid flex basis");}
   data.wall=new int[data.vertices-BottomSurface*2];for(int i=0;i<data.wall.Length;i++){data.wall[i]=r.ReadInt32();if(data.wall[i]<0||data.wall[i]>=BottomSurface*2)throw new InvalidDataException("Invalid flex wall map");}
   data.frames=new FlexFrame[ReadCount(r,100000)];for(int i=0;i<data.frames.Length;i++){var frame=new FlexFrame{parameter=ReadVector(r),tilt=r.ReadSingle(),controls=new float[FlexField.Count]};for(int j=0;j<frame.controls.Length;j++){frame.controls[j]=r.ReadSingle();if(!Finite(frame.controls[j]))throw new InvalidDataException("Invalid flex controls");}if(!Finite(frame.tilt)||!Finite(frame.parameter.sqrMagnitude))throw new InvalidDataException("Invalid flex angle");data.frames[i]=frame;}
   data.nodes=new FlexNode[ReadCount(r,100000)];for(int i=0;i<data.nodes.Length;i++){var node=new FlexNode{min=ReadVector(r),max=ReadVector(r),corners=new int[8]};for(int j=0;j<8;j++){node.corners[j]=r.ReadInt32();if(node.corners[j]<0||node.corners[j]>=data.frames.Length)throw new InvalidDataException("Invalid flex frame index");}if(r.ReadBoolean()){node.children=new int[8];for(int j=0;j<8;j++){node.children[j]=r.ReadInt32();if(node.children[j]<=i||node.children[j]>=data.nodes.Length)throw new InvalidDataException("Invalid flex child index");}}data.nodes[i]=node;}
   if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Unexpected flex cache payload");AttachFlex(data);
  }
 }
 void AttachFlex(FlexData data){data.profileMotion=Array.ConvertAll(artProfiles,p=>new Vector3(p.amplitude,p.period,p.openness));data.lookMotion=Array.ConvertAll(artLooks,p=>new Vector2(p.droop,p.asym));flexNormalsBuffer?.Release();flexWallBuffer?.Release();flexData=data;flexField=new FlexField();flexNormalsBuffer=new ComputeBuffer(data.normals.Length,12);flexNormalsBuffer.SetData(data.normals);flexWallBuffer=new ComputeBuffer(data.wall.Length,4);flexWallBuffer.SetData(data.wall);}
 void EnsureFlex(){if(flexData!=null)return;
#if UNITY_EDITOR
  if(WanderFlexPreview.Requested||Array.IndexOf(Environment.GetCommandLineArgs(),"-bakeFlower")>=0)return;
#endif
  LoadFlex(FlexPath);
 }
 static void QueryFlex(FlexData data,Vector3 parameter,float[] controls,out float tilt){
  int index=0;for(int depth=0;depth<16;depth++){var n=data.nodes[index];if(n.children==null)break;var mid=(n.min+n.max)*.5f;index=n.children[(parameter.x>=mid.x?1:0)|(parameter.y>=mid.y?2:0)|(parameter.z>=mid.z?4:0)];if(depth==15)throw new InvalidDataException("Flex lookup too deep");}
  var node=data.nodes[index];float x=Mathf.InverseLerp(node.min.x,node.max.x,parameter.x),y=Mathf.InverseLerp(node.min.y,node.max.y,parameter.y),z=Mathf.InverseLerp(node.min.z,node.max.z,parameter.z);Array.Clear(controls,0,controls.Length);tilt=0;
  for(int i=0;i<8;i++){float weight=((i&1)!=0?x:1-x)*((i&2)!=0?y:1-y)*((i&4)!=0?z:1-z);var frame=data.frames[node.corners[i]];tilt+=frame.tilt*weight;for(int j=0;j<controls.Length;j++)controls[j]+=frame.controls[j]*weight;}
 }
 Vector3 FlexParameter(){PetalArtMotion(new Vector4(0,0,1,0),artTime,3*Mathf.PI*2/layers[0].petals.Length,0,3,out float lower,out _);PetalArtMotion(new Vector4(0,0,1,0),artTime,3*Mathf.PI*2/layers[1].petals.Length,1,3,out float upper,out _);PetalArtMotion(new Vector4(0,0,1,0),artTime,2*Mathf.PI*2/layers[1].petals.Length,1,2,out float neighbour,out _);return new Vector3(lower,upper,neighbour);}
 void PrepareArtMotion(){
  if(desiredArtMotion==null||desiredArtMotion.Length!=layers.Length){desiredArtMotion=new Vector2[layers.Length][];for(int k=0;k<layers.Length;k++)desiredArtMotion[k]=new Vector2[layers[k].petals.Length];}
  for(int k=0;k<layers.Length;k++)for(int j=0;j<layers[k].petals.Length;j++){PetalArtMotion(ArtWeights,artTime,j*Mathf.PI*2/layers[k].petals.Length,k,j,out float tilt,out float roll);desiredArtMotion[k][j]=new Vector2(tilt,roll);}
  flexWeight=Mathf.Clamp01(ArtWeights.z)*FlexGate;
#if UNITY_EDITOR || FLOWER_VALIDATION
  if(suppressArtMotion)flexWeight=0;
#endif
#if UNITY_EDITOR
  if(flexBypass)flexWeight=0;
#endif
  if(flexWeight<=0)return;EnsureFlex();if(flexData==null){flexWeight=0;return;}
#if UNITY_EDITOR
  if(flexBestFrames!=null){QueryBestFlex(artTime,flexField.controls,out float previewTilt);desiredArtMotion[1][3].x+=flexWeight*(previewTilt-FlexParameter().y);return;}
#endif
  if(artProfiles.Length!=flexData.profileMotion.Length||artLooks.Length!=flexData.lookMotion.Length)throw new InvalidOperationException("Motion profile count changed after flex cache loading");
  for(int i=0;i<artProfiles.Length;i++)if(artProfiles[i].amplitude!=flexData.profileMotion[i].x||artProfiles[i].period!=flexData.profileMotion[i].y||artProfiles[i].openness!=flexData.profileMotion[i].z||artLooks[i].droop!=flexData.lookMotion[i].x||artLooks[i].asym!=flexData.lookMotion[i].y)throw new InvalidOperationException("Motion parameters changed after flex cache loading");
  var parameter=FlexParameter();QueryFlex(flexData,parameter,flexField.controls,out float correctedTilt);desiredArtMotion[1][3].x+=flexWeight*(correctedTilt-parameter.y);
 }
 void ApplyFlexVertices(Vertex[] values,float weight){
  if(weight<=0||flexData==null)return;var h=new float[BottomSurface];for(int id=0;id<BottomSurface;id++){h[id]=flexField.Height(id)*weight;if(Mathf.Abs(h[id])<1e-7f)h[id]=0;var d=flexData.normals[id]*h[id];values[id].p+=d;values[id+BottomSurface].p+=d;}
  for(int y=8;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x,l=y*129+Mathf.Max(x-1,0),r=y*129+Mathf.Min(x+1,128),b=Mathf.Max(y-1,0)*129+x,t=Mathf.Min(y+1,80)*129+x;if(h[id]==0&&h[l]==0&&h[r]==0&&h[b]==0&&h[t]==0)continue;var across=values[r].p-values[l].p;var along=values[t].p-values[b].p;var tangent=across.normalized;var normal=Vector3.Cross(along,tangent).normalized;values[id].n=normal;values[id+BottomSurface].n=-normal;values[id].t=new Vector4(tangent.x,tangent.y,tangent.z,-1);values[id+BottomSurface].t=new Vector4(tangent.x,tangent.y,tangent.z,1);}
  for(int k=BottomSurface*2;k<values.Length;k++)values[k].p=values[flexData.wall[k-BottomSurface*2]].p;
  for(int k=BottomSurface*2;k<values.Length;k+=2){int id=flexData.wall[k-BottomSurface*2]%BottomSurface;int x=id%129,y=id/129;if(h[id]==0&&h[y*129+Mathf.Max(x-1,0)]==0&&h[y*129+Mathf.Min(x+1,128)]==0&&h[Mathf.Max(y-1,0)*129+x]==0&&h[Mathf.Min(y+1,80)*129+x]==0)continue;int previous=k==BottomSurface*2?values.Length-2:k-2,next=k+2>=values.Length?BottomSurface*2:k+2;var edge=(values[next].p-values[previous].p).normalized;var normal=Vector3.Cross(edge,-values[id].n).normalized;values[k].n=values[k+1].n=normal;values[k].t=values[k+1].t=new Vector4(edge.x,edge.y,edge.z,1);}
 }
 void FinishFlex(){
  var petal=layers[1].petals[3];petal.block.SetFloat(FlexWeightId,flexWeight);
  if(flexData!=null){
   if(gpuPlayback){petal.block.SetBuffer(FlexNormalsId,flexNormalsBuffer);petal.block.SetBuffer(FlexWallId,flexWallBuffer);petal.block.SetInt(FlexCountId,flexData.vertices);petal.block.SetFloatArray(FlexControlsId,flexField.controls);var clip=playback[1][3];var bounds=clip.mesh.bounds;bounds.Expand(.2f*flexWeight);clip.mesh.bounds=bounds;var motion=clip.motionBounds;motion.Expand(.2f*flexWeight);clip.motionBounds=motion;}
   else if(flexWeight>0||flexPreviousWeight>0){var patch=bottomPatches.Find(p=>p.layer==1&&p.index==3);ApplyBottomPatch(patch);ApplyFlexVertices(patch.work,flexWeight);patch.mesh.SetVertexBufferData(patch.work,0,0,patch.work.Length);var bounds=patch.mesh.bounds;bounds.Expand(.2f*flexWeight);patch.mesh.bounds=bounds;}
  }
  flexPreviousWeight=flexWeight;petal.renderer.SetPropertyBlock(petal.block);
 }
 void ReleaseFlex(){flexNormalsBuffer?.Release();flexWallBuffer?.Release();flexNormalsBuffer=flexWallBuffer=null;flexData=null;flexField=null;desiredArtMotion=null;flexWeight=flexPreviousWeight=0;}
}
}
