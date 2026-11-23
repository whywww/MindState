using System;
using System.IO;
using UnityEngine;
namespace MindState {
public sealed partial class FlowerAssembler {
 const string FlexFormat="FlowerFlex/2";
 sealed class FlexFrame { internal Vector4 parameter;internal float tilt;internal float[] controls; }
 sealed class FlexNode { internal Vector4 min,max;internal int[] corners;internal int[] children; }
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
 string FlexMotionSignature(){EnsureArtLooks();return string.Join(";",Array.ConvertAll(artProfiles,p=>JsonUtility.ToJson(p)))+"|"+string.Join(";",Array.ConvertAll(artLooks,p=>JsonUtility.ToJson(p)))+"|outerPair="+OuterPairTiltBias.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
 float FlexGate=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6875f,.75f,bloomProgress));
 static Vector4 ReadFlexParameter(BinaryReader r)=>new Vector4(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
 void LoadFlex(string path){
  using(var r=new BinaryReader(File.OpenRead(path))){
   if(r.ReadString()!=FlexFormat||r.ReadString()!=BottomConfigHash()||r.ReadString()!=FlexBaseSignature()||r.ReadString()!=FlexMotionSignature())throw new InvalidDataException("Wandering flex cache source mismatch; rebuild complete caches.");
   var data=new FlexData{amplitude=r.ReadSingle(),roll=r.ReadSingle(),vertices=ReadCount(r,100000)};
   if(!Finite(data.amplitude)||data.amplitude<=0||data.vertices!=layers[1].petals[3].body.GetComponent<MeshFilter>().sharedMesh.vertexCount)throw new InvalidDataException("Wandering flex topology/parameter mismatch");
   data.normals=new Vector3[BottomSurface];for(int i=0;i<BottomSurface;i++){data.normals[i]=ReadVector(r);if(!Finite(data.normals[i].sqrMagnitude))throw new InvalidDataException("Invalid flex basis");}
   data.wall=new int[data.vertices-BottomSurface*2];for(int i=0;i<data.wall.Length;i++){data.wall[i]=r.ReadInt32();if(data.wall[i]<0||data.wall[i]>=BottomSurface*2)throw new InvalidDataException("Invalid flex wall map");}
   data.frames=new FlexFrame[ReadCount(r,100000)];for(int i=0;i<data.frames.Length;i++){var frame=new FlexFrame{parameter=ReadFlexParameter(r),tilt=r.ReadSingle(),controls=new float[FlexField.Count]};for(int j=0;j<frame.controls.Length;j++){frame.controls[j]=r.ReadSingle();if(!Finite(frame.controls[j]))throw new InvalidDataException("Invalid flex controls");}if(!Finite(frame.tilt)||!Finite(frame.parameter.sqrMagnitude))throw new InvalidDataException("Invalid flex angle");data.frames[i]=frame;}
   data.nodes=new FlexNode[ReadCount(r,100000)];for(int i=0;i<data.nodes.Length;i++){var node=new FlexNode{min=ReadFlexParameter(r),max=ReadFlexParameter(r),corners=new int[16]};for(int j=0;j<16;j++){node.corners[j]=r.ReadInt32();if(node.corners[j]<0||node.corners[j]>=data.frames.Length)throw new InvalidDataException("Invalid flex frame index");}if(r.ReadBoolean()){node.children=new int[16];for(int j=0;j<16;j++){node.children[j]=r.ReadInt32();if(node.children[j]<=i||node.children[j]>=data.nodes.Length)throw new InvalidDataException("Invalid flex child index");}}data.nodes[i]=node;}
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
 static void QueryFlex(FlexData data,Vector4 parameter,float[] controls,out float tilt){
  int index=0;for(int depth=0;depth<16;depth++){var n=data.nodes[index];if(n.children==null)break;var mid=(n.min+n.max)*.5f;index=n.children[(parameter.x>=mid.x?1:0)|(parameter.y>=mid.y?2:0)|(parameter.z>=mid.z?4:0)|(parameter.w>=mid.w?8:0)];if(depth==15)throw new InvalidDataException("Flex lookup too deep");}
  var node=data.nodes[index];float x=Mathf.InverseLerp(node.min.x,node.max.x,parameter.x),y=Mathf.InverseLerp(node.min.y,node.max.y,parameter.y),z=Mathf.InverseLerp(node.min.z,node.max.z,parameter.z),w=Mathf.InverseLerp(node.min.w,node.max.w,parameter.w);Array.Clear(controls,0,controls.Length);tilt=0;
  for(int i=0;i<16;i++){float weight=((i&1)!=0?x:1-x)*((i&2)!=0?y:1-y)*((i&4)!=0?z:1-z)*((i&8)!=0?w:1-w);var frame=data.frames[node.corners[i]];tilt+=frame.tilt*weight;for(int j=0;j<controls.Length;j++)controls[j]+=frame.controls[j]*weight;}
 }
 Vector4 FlexParameter(){PetalArtMotion(new Vector4(0,0,1,0),artTime,3*Mathf.PI*2/layers[0].petals.Length,0,3,out float lower,out _);PetalArtMotion(new Vector4(0,0,1,0),artTime,3*Mathf.PI*2/layers[1].petals.Length,1,3,out float upper,out _);PetalArtMotion(new Vector4(0,0,1,0),artTime,2*Mathf.PI*2/layers[1].petals.Length,1,2,out float neighbour,out _,ArtWeights.z);PetalArtMotion(new Vector4(0,0,1,0),artTime,2*Mathf.PI*2/layers[0].petals.Length,0,2,out float outer,out _);return new Vector4(lower,upper,neighbour,outer);}
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
  RecalculateFlexSurface(values,h,flexData.wall);
 }
 void RecalculateFlexSurface(Vertex[] values,float[] h,int[] wall,int firstRow=8){
  for(int y=firstRow;y<=80;y++)for(int x=0;x<129;x++){int id=y*129+x,l=y*129+Mathf.Max(x-1,0),r=y*129+Mathf.Min(x+1,128),b=Mathf.Max(y-1,0)*129+x,t=Mathf.Min(y+1,80)*129+x;if(h[id]==0&&h[l]==0&&h[r]==0&&h[b]==0&&h[t]==0)continue;var across=values[r].p-values[l].p;var along=values[t].p-values[b].p;var tangent=across.normalized;var normal=Vector3.Cross(along,tangent).normalized;values[id].n=normal;values[id+BottomSurface].n=-normal;values[id].t=new Vector4(tangent.x,tangent.y,tangent.z,-1);values[id+BottomSurface].t=new Vector4(tangent.x,tangent.y,tangent.z,1);}
  for(int k=BottomSurface*2;k<values.Length;k++)values[k].p=values[wall[k-BottomSurface*2]].p;
  for(int k=BottomSurface*2;k<values.Length;k+=2){int id=wall[k-BottomSurface*2]%BottomSurface;int x=id%129,y=id/129;if(h[id]==0&&h[y*129+Mathf.Max(x-1,0)]==0&&h[y*129+Mathf.Min(x+1,128)]==0&&h[Mathf.Max(y-1,0)*129+x]==0&&h[Mathf.Min(y+1,80)*129+x]==0)continue;int previous=k==BottomSurface*2?values.Length-2:k-2,next=k+2>=values.Length?BottomSurface*2:k+2;var edge=(values[next].p-values[previous].p).normalized;var normal=Vector3.Cross(edge,-values[id].n).normalized;values[k].n=values[k+1].n=normal;values[k].t=values[k+1].t=new Vector4(edge.x,edge.y,edge.z,1);}
 }

 void FinishFlex(){
  var petal=layers[1].petals[3];petal.block.SetFloat(FlexWeightId,flexWeight);
  if(flexData!=null){
   if(gpuPlayback){petal.block.SetBuffer(FlexNormalsId,flexNormalsBuffer);petal.block.SetBuffer(FlexWallId,flexWallBuffer);petal.block.SetInt(FlexCountId,flexData.vertices);petal.block.SetFloatArray(FlexControlsId,flexField.controls);var clip=playback[1][3];var bounds=clip.mesh.bounds;bounds.Expand(.2f*flexWeight);clip.mesh.bounds=bounds;var motion=clip.motionBounds;motion.Expand(.2f*flexWeight);clip.motionBounds=motion;}
   else if(flexWeight>0||flexPreviousWeight>0){var patch=bottomPatches.Find(p=>p.layer==1&&p.index==3);ApplyBottomPatch(patch);ApplyFlexVertices(patch.work,flexWeight);patch.mesh.SetVertexBufferData(patch.work,0,0,patch.work.Length);var bounds=patch.mesh.bounds;bounds.Expand(.2f*flexWeight);patch.mesh.bounds=bounds;}
  }
  flexPreviousWeight=flexWeight;petal.renderer.SetPropertyBlock(petal.block);
 }
 // A small attached root bend and a gradual closed-state tip fold; no scale pulse.
 static readonly int Pair24CurlId=Shader.PropertyToID("_Pair24Curl"),Pair24CurlRootId=Shader.PropertyToID("_Pair24CurlRoot"),Pair24TipId=Shader.PropertyToID("_Pair24TipCurl");
 float pair24CurlAmount,pair24CurlPrevious,pair24TipAngle;
 Vector3 pair24CurlRoot;
 int[] pair24CurlWall;
 ComputeBuffer pair24CurlWallBuffer;
 float[] pair24CurlDirty;
#if UNITY_EDITOR || FLOWER_VALIDATION
 internal bool pair24CurlBypass;
#endif
 static float Pair24RootWeight(int row)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(row/6f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((20-row)/14f));
 static float Pair24TipWeight(int row)=>Mathf.SmoothStep(0,1,Mathf.Clamp01((row-48)/32f));
 // Per-row deformation is affine. Bounds follow the sheet without inflating the
 // continuous envelope used by echo/ribbon spacing.
 Bounds Pair24RowBounds(Bounds source,int row,Vector3 hinge){
  var center=source.center+pair24CurlRoot*(Pair24RootWeight(row)*pair24CurlAmount);var extents=source.extents;float angle=pair24TipAngle*Mathf.Deg2Rad*Pair24TipWeight(row),c=Mathf.Cos(angle),s=Mathf.Sin(angle);var d=center-hinge;
  center=hinge+new Vector3(d.x,d.y*c-d.z*s,d.y*s+d.z*c);extents=new Vector3(extents.x,Mathf.Abs(c)*extents.y+Mathf.Abs(s)*extents.z,Mathf.Abs(s)*extents.y+Mathf.Abs(c)*extents.z);return new Bounds(center,extents*2);
 }
 Bounds Pair24PlaybackBounds(PlaybackClip clip,bool interval){
  var frame=PlaybackFrame(clip,Pair24PlaybackProgress(bloomProgress));int first=(int)frame.x,next=(int)frame.y;var hinge=Vector3.LerpUnclamped(clip.pair24TipCenters[first],clip.pair24TipCenters[next],frame.z);var result=new Bounds();
  for(int row=0;row<=80;row++){var a=clip.pair24Rows[first*81+row];var b=clip.pair24Rows[next*81+row];Bounds source;if(interval){source=a;source.Encapsulate(b);}else source=InterpolatePlaybackBounds(a,b,frame.z);var transformed=Pair24RowBounds(source,row,hinge);if(row==0)result=transformed;else result.Encapsulate(transformed);}
  result.Expand(.0001f);return result;
 }
 void EnsurePair24CurlTopology(){
  if(pair24CurlWall==null){var mesh=layers[2].petals[4].body.GetComponent<MeshFilter>().sharedMesh;var uv=mesh.uv;pair24CurlWall=new int[uv.Length-BottomSurface*2];
   for(int i=0;i<pair24CurlWall.Length;i++){int id=BottomSurface*2+i;pair24CurlWall[i]=Mathf.RoundToInt(uv[id].y*80)*129+Mathf.RoundToInt(uv[id].x*128)+(id%2==0?0:BottomSurface);if(pair24CurlWall[i]<0||pair24CurlWall[i]>=BottomSurface*2)throw new InvalidDataException("Invalid L2-P4 curl wall topology");}
  }
  if(gpuPlayback&&pair24CurlWallBuffer==null){pair24CurlWallBuffer=new ComputeBuffer(pair24CurlWall.Length,4);pair24CurlWallBuffer.SetData(pair24CurlWall);}
 }
 void ApplyPair24CurlVertices(Vertex[] values){
  if(pair24CurlAmount<=0)return;EnsurePair24CurlTopology();if(pair24CurlDirty==null)pair24CurlDirty=new float[BottomSurface];var hinge=values[48*129+64].p;
  Vector3 Bend(Vector3 point,int row){point+=pair24CurlRoot*(Pair24RootWeight(row)*pair24CurlAmount);float angle=pair24TipAngle*Mathf.Deg2Rad*Pair24TipWeight(row);if(angle==0)return point;var d=point-hinge;return hinge+new Vector3(d.x,d.y*Mathf.Cos(angle)-d.z*Mathf.Sin(angle),d.y*Mathf.Sin(angle)+d.z*Mathf.Cos(angle));}
  for(int id=0;id<BottomSurface;id++){int row=id/129;pair24CurlDirty[id]=pair24CurlAmount*(Pair24RootWeight(row)+Pair24TipWeight(row));values[id].p=Bend(values[id].p,row);values[id+BottomSurface].p=Bend(values[id+BottomSurface].p,row);}
  RecalculateFlexSurface(values,pair24CurlDirty,pair24CurlWall,0);
 }
 void FinishPair24Curl(bool suppressed=false){
  float remaining=ArtWeights.x+ArtWeights.y+ArtWeights.w;
  float closed=remaining>0?(ArtWeights.y+ArtWeights.w)/remaining*(1-Mathf.SmoothStep(0,1,ArtWeights.z*ArtWeights.z)):0;pair24TipAngle=suppressed?0:-28*closed;pair24CurlAmount=suppressed?0:1;
#if UNITY_EDITOR || FLOWER_VALIDATION
  if(pair24CurlBypass)pair24CurlAmount=0;
#endif
  var petal=layers[2].petals[4];petal.block.SetFloat(Pair24CurlId,pair24CurlAmount);petal.block.SetFloat(Pair24TipId,pair24TipAngle);
  if(pair24CurlAmount<=0&&pair24CurlPrevious<=0)return;
  pair24CurlRoot=petal.body.InverseTransformVector(petal.anchor.parent.TransformVector(Vector3.up*.018f));
  if(pair24CurlAmount>0){if(gpuPlayback)EnsureFlex();EnsurePair24CurlTopology();petal.block.SetVector(Pair24CurlRootId,pair24CurlRoot);}
  if(gpuPlayback){var clip=playback[2][4];if(pair24CurlAmount>0){petal.block.SetBuffer(FlexWallId,pair24CurlWallBuffer);petal.block.SetInt(FlexCountId,clip.count);if(flexNormalsBuffer!=null)petal.block.SetBuffer(FlexNormalsId,flexNormalsBuffer);}if(pair24CurlAmount>0){clip.mesh.bounds=Pair24PlaybackBounds(clip,true);clip.motionBounds=Pair24PlaybackBounds(clip,false);}}
  else {var patch=bottomPatches.Find(p=>p.layer==2&&p.index==4);ApplyBottomPatch(patch);ApplyPair24CurlVertices(patch.work);patch.mesh.SetVertexBufferData(patch.work,0,0,patch.work.Length);var bounds=new Bounds(patch.work[0].p,Vector3.zero);for(int i=1;i<patch.work.Length;i++)bounds.Encapsulate(patch.work[i].p);bounds.Expand(.0001f);patch.mesh.bounds=bounds;}
  pair24CurlPrevious=pair24CurlAmount;petal.renderer.SetPropertyBlock(petal.block);
 }
 void ReleaseFlex(){pair24CurlWallBuffer?.Release();pair24CurlWallBuffer=null;pair24CurlWall=null;pair24CurlDirty=null;pair24CurlAmount=pair24CurlPrevious=0;flexNormalsBuffer?.Release();flexWallBuffer?.Release();flexNormalsBuffer=flexWallBuffer=null;flexData=null;flexField=null;desiredArtMotion=null;flexWeight=flexPreviousWeight=0;}
}
}
