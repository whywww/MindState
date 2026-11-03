using System;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

namespace MindState
{
    // Layer pose caches share a small set of deterministic petal surface variants.
    public sealed partial class FlowerAssembler : MonoBehaviour
    {
        public const int PoseCount=33;
        public FlowerPreset preset;
        public HeroPetal petalPrefab;
        public Material coreMaterial,stamenMaterial;
        [Range(0,1)] public float bloomProgress;
        public bool autoPlay=true,frozen;
        public bool materialBreathing=true;
        public int PetalCount=>preset!=null?preset.PetalCount:0;
        public double InterpolateMs,UploadMs,TransformMs,MaterialMs;
        static double Milliseconds(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        public int MeshUpdates { get; private set; }
        public double LastUpdateMilliseconds { get; private set; }
        public long LastUpdateAllocation { get; private set; }
        public long CacheBytes { get; private set; }
        public double InitializationMilliseconds { get; private set; }
        public long GeometryBytes { get { long bytes=CacheBytes; if(layers!=null) foreach(var l in layers) foreach(var g in l.variants) bytes+=g.data.Length*40L+UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(g.mesh); if(coreMesh!=null) bytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(coreMesh); if(stamenMesh!=null) bytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(stamenMesh); return bytes; } }
        public float MaxCachePositionError { get; private set; }
        public float MaxCacheNormalAngle { get; private set; }
        public int LayerCount=>layers==null?0:layers.Length;
        public int SharedMeshCount { get { int count=0; if(layers!=null) foreach(var l in layers) count+=l.variants.Length; return count; } }
        public bool Ready=>layers!=null;
        public float BloomDuration { get; private set; }=1;
        public Bounds FullBloomBounds { get; private set; }
        public Bounds WorldBounds { get { var b=new Bounds(transform.position+transform.up*.7f,Vector3.zero); if(layers!=null) foreach(var l in layers) foreach(var p in l.petals) b.Encapsulate(p.renderer.bounds); return b; } }
        static readonly ProfilerMarker UpdateMarker=new ProfilerMarker("MindState.Flower.Update");
        static readonly int BreathId=Shader.PropertyToID("_Breath");
        [StructLayout(LayoutKind.Sequential)] struct Vertex { public Vector3 p,n; public Vector4 t; }
        sealed class Pose { public Vertex[] data; public Vector3 root; public Bounds bounds; }
        sealed class Petal { public Transform anchor,body; public MeshRenderer renderer; public MaterialPropertyBlock block; public Color originalColor; public float flowPhase; public float azimuth,tilt,roll,heightOffset,authoredTilt,widthFactor,lengthFactor; public Vector3 scale; public Matrix4x4 fullMatrix; }
        sealed class Variant { public Pose[] poses=new Pose[PoseCount]; public Vertex[] data; public Mesh mesh; public Vector3 root; }
        sealed class Layer { public FlowerLayer config; public Variant[] variants; public Petal[] petals; public float progress=-1; }
        static Variant SurfaceFor(Layer layer,int petal)=>layer.variants[petal%layer.variants.Length];
        Layer[] layers;
        readonly bool rootBaseline=Array.IndexOf(Environment.GetCommandLineArgs(),"-rootBaseline")>=0;
        Transform generated;
        Mesh coreMesh,stamenMesh;
        int previousCascades;
        bool qualitySaved;

        void Start() { previousCascades=QualitySettings.shadowCascades; qualitySaved=true; QualitySettings.shadowCascades=0; RebuildFlower(); if(!ArtDiagnosticsRequested()) SetArtState(FlowerArtState.Clarity); }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public void SetBloomProgress(float progress)
        { if(!Finite(progress)) return; LeaveArt(); autoPlay=false; bloomProgress=Mathf.Clamp01(progress); ApplyPose(); }
        public void PlayBloom() { LeaveArt(); bloomProgress=0; frozen=false; autoPlay=true; ApplyPose(); }
        public void SetFrozen(bool value)=>frozen=value;
        [ContextMenu("Rebuild Flower / 重建缓存与组装")]
        public void RebuildFlower()
        {
            if(!Application.isPlaying||preset==null||petalPrefab==null) return;
            long initStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            Release(); CacheBytes=0; MaxCachePositionError=0; MaxCacheNormalAngle=0; BloomDuration=1;
            generated=new GameObject("Generated · shared layer meshes").transform; generated.SetParent(transform,false);
            var source=Instantiate(petalPrefab,generated); source.name="Temporary pose sampler";
            source.GetComponent<MeshRenderer>().enabled=false; source.enabled=false;
            var random=new System.Random(preset.randomSeed);
            layers=new Layer[preset.layers.Length];
            for(int i=0;i<layers.Length;i++)
            {
                var l=new Layer { config=JsonUtility.FromJson<FlowerLayer>(JsonUtility.ToJson(preset.layers[i])) }; layers[i]=l;
                l.config.Constrain();
                BloomDuration=Mathf.Max(BloomDuration,l.config.delay+l.config.duration);
                l.variants=new Variant[l.config.surfaceProfile.softFolds&&l.config.count>1?2:1];
                for(int variant=0;variant<l.variants.Length;variant++)
                {
                    var g=new Variant(); l.variants[variant]=g;
                    int foldSeed=unchecked(preset.randomSeed*486187739+i*104729+variant*15485863);
                    for(int j=0;j<PoseCount;j++)
                    {
                        source.SetShapeImmediateLimited(l.config.openingAngle,l.config.curl,l.config.softness,j/(float)(PoseCount-1),l.config.maximumCurl,l.config.edgeFoldScale,l.config.surfaceProfile,foldSeed);
                        var m=source.RuntimeMesh;
                        var positions=m.vertices; var normals=m.normals; var tangents=m.tangents;
                        var p=new Pose { data=new Vertex[positions.Length],bounds=m.bounds };
                        for(int k=0;k<positions.Length;k++) p.data[k]=new Vertex { p=positions[k],n=normals[k],t=tangents[k] };
                        int center=HeroPetal.WidthSegments/2,surface=(HeroPetal.WidthSegments+1)*(HeroPetal.LengthSegments+1);
                        p.root=(p.data[center].p+p.data[center+surface].p)*.5f; g.poses[j]=p;
                        CacheBytes+=p.data.Length*40L;
                    }
                    // Midpoint comparisons against the unchanged generator, during initialization only.
                    for(int j=0;j<PoseCount-1;j++)
                    {
                        source.SetShapeImmediateLimited(l.config.openingAngle,l.config.curl,l.config.softness,(j+.5f)/(PoseCount-1),l.config.maximumCurl,l.config.edgeFoldScale,l.config.surfaceProfile,foldSeed);
                        var exact=source.RuntimeMesh.vertices; var normals=source.RuntimeMesh.normals;
                        for(int k=0;k<exact.Length;k++)
                        {
                            MaxCachePositionError=Mathf.Max(MaxCachePositionError,Vector3.Distance(exact[k],(g.poses[j].data[k].p+g.poses[j+1].data[k].p)*.5f));
                            // Boundary normals at a narrow shell seam are not a visible surface crease.
                            if(k<2*(HeroPetal.WidthSegments+1)*(HeroPetal.LengthSegments+1))
                                MaxCacheNormalAngle=Mathf.Max(MaxCacheNormalAngle,Vector3.Angle(normals[k],(g.poses[j].data[k].n+g.poses[j+1].data[k].n).normalized));
                        }
                    }
                    int count=g.poses[0].data.Length;
                    g.data=new Vertex[count];
                    g.mesh=new Mesh { name="Flower shared layer "+i+" variant "+variant }; g.mesh.MarkDynamic();
                    g.mesh.SetVertexBufferParams(count,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3,0),new VertexAttributeDescriptor(VertexAttribute.Tangent,VertexAttributeFormat.Float32,4,0),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2,1));
                    g.mesh.SetVertexBufferData(g.poses[0].data,0,0,count,0,MeshUpdateFlags.DontRecalculateBounds);
                    g.mesh.SetVertexBufferData(source.RuntimeMesh.uv,0,0,count,1,MeshUpdateFlags.DontRecalculateBounds);
                    g.mesh.triangles=source.RuntimeMesh.triangles;
                }
                l.petals=new Petal[Mathf.Clamp(l.config.count,1,10)];
                var variation=new float[l.petals.Length,6]; var mean=new float[6];
                for(int p=0;p<l.petals.Length;p++) for(int q=0;q<6;q++)
                {
                    variation[p,q]=(float)random.NextDouble()*2-1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || FLOWER_VALIDATION
                    if(diagnosticExtremeVariation&&q<5) variation[p,q]=p==(q*2+i)%l.petals.Length?1:-1;
#endif
                    mean[q]+=variation[p,q]/l.petals.Length;
                }
                for(int j=0;j<l.petals.Length;j++)
                {
                    var anchor=new GameObject(l.config.name+" · "+(j+1)).transform; anchor.SetParent(generated,false);
                    var body=new GameObject("Silk petal").transform; body.SetParent(anchor,false);
                    body.gameObject.AddComponent<MeshFilter>().sharedMesh=SurfaceFor(l,j).mesh;
                    var renderer=body.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=petalPrefab.pearlMaterial;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On; renderer.receiveShadows=true;
                    var offset=l.config.OffsetAt(j);
                    var p=new Petal { anchor=anchor,body=body,renderer=renderer,block=new MaterialPropertyBlock(),azimuth=l.config.azimuth+360f*j/l.petals.Length+(variation[j,0]-mean[0])*l.config.azimuthVariation,tilt=(variation[j,1]-mean[1])*l.config.tiltVariation,roll=(variation[j,2]-mean[2])*l.config.rollVariation,widthFactor=1+(variation[j,3]-mean[3])*l.config.widthVariation,lengthFactor=1+(variation[j,4]-mean[4])*l.config.lengthVariation,heightOffset=offset?.height??0,authoredTilt=offset?.tilt??0 };
                    p.scale=PetalScale(l.config,p,j);
                    l.petals[j]=p;
                    float tint=(variation[j,5]-mean[5])*preset.colorVariation;
                    p.originalColor=l.config.pearl+new Color(tint,tint*.4f,-tint*.3f,0);
                    p.block.SetColor("_Color",p.originalColor);
                    p.block.SetColor("_RootColor",l.config.root); p.block.SetColor("_PinkColor",l.config.pink);
                    p.block.SetFloat("_BumpStrength",petalPrefab.pearlMaterial.GetFloat("_BumpStrength")*l.config.microNormalScale);
                    p.block.SetFloat("_FineCount",petalPrefab.finePleatCount); p.block.SetFloat("_Breath",0); renderer.SetPropertyBlock(p.block);
                    body.localScale=p.scale;
                }
            }
            Destroy(source.gameObject); BuildHeart(); RefreshAppearance();
            float discRadius=0;foreach(var layer in layers)discRadius=Mathf.Max(discRadius,layer.config.rootRadius);
            foreach(var layer in layers)foreach(var petal in layer.petals)petal.block.SetVector("_RootTrim",new Vector4(1,layers[0].config.rootHeight,(discRadius+.04f)*.92f,layers[0].config.rootHeight+.012f));
            float initialProgress=bloomProgress; bloomProgress=1; ApplyPose(); FullBloomBounds=WorldBounds;
            foreach(var l in layers) foreach(var p in l.petals) p.fullMatrix=p.body.localToWorldMatrix;
            bloomProgress=initialProgress; ApplyPose();
#if UNITY_EDITOR || DEVELOPMENT_BUILD || FLOWER_VALIDATION
            ApplyDiagnosticColors();
#endif
            InitializationMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-initStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        void Update()
        {
            if(layers==null) return;
            using var marker=UpdateMarker.Auto();
            long start=System.Diagnostics.Stopwatch.GetTimestamp(),bytes=GC.GetAllocatedBytesForCurrentThread();
            if(autoPlay&&!frozen) bloomProgress=Mathf.Min(1,bloomProgress+Time.deltaTime/BloomDuration);
            InterpolateMs=UploadMs=TransformMs=MaterialMs=0;
            if(artActive) { UpdateArt(Time.deltaTime); LastUpdateAllocation=GC.GetAllocatedBytesForCurrentThread()-bytes; LastUpdateMilliseconds=Milliseconds(start); return; }
            ApplyPose();
            long materialStart=System.Diagnostics.Stopwatch.GetTimestamp();
            if(materialBreathing)
            {
                float breath=Mathf.Sin(Time.time*.65f);
                foreach(var l in layers) foreach(var p in l.petals) { p.block.SetFloat(BreathId,breath); p.renderer.SetPropertyBlock(p.block); }
            }
            MaterialMs=Milliseconds(materialStart);
            LastUpdateAllocation=GC.GetAllocatedBytesForCurrentThread()-bytes;
            LastUpdateMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        void ApplyPose()=>ApplyPose(bloomProgress);
        void ApplyPose(float globalProgress,bool applyTransforms=true)
        {
            if(layers==null) return;
            float elapsed=Mathf.Clamp01(globalProgress)*BloomDuration;
            foreach(var l in layers)
            {
                float progress=Mathf.Clamp01((elapsed-l.config.delay)/Mathf.Max(1,l.config.duration));
                if(progress==l.progress) continue;
                l.progress=progress; float frame=progress*(PoseCount-1); int a=Mathf.Min((int)frame,PoseCount-2); float f=frame-a;
                foreach(var g in l.variants)
                {
                    long interpolateStart=System.Diagnostics.Stopwatch.GetTimestamp();
                    Pose p0=g.poses[a],p1=g.poses[a+1];
                    for(int i=0;i<g.data.Length;i++) Interpolate(ref p0.data[i],ref p1.data[i],f,out g.data[i]);
                    InterpolateMs+=Milliseconds(interpolateStart);
                    long uploadStart=System.Diagnostics.Stopwatch.GetTimestamp();
                    g.root=Vector3.LerpUnclamped(p0.root,p1.root,f);
                    g.mesh.SetVertexBufferData(g.data,0,0,g.data.Length,0,MeshUpdateFlags.DontRecalculateBounds);
                    Bounds bounds=p0.bounds; bounds.Encapsulate(p1.bounds); g.mesh.bounds=bounds; MeshUpdates++; UploadMs+=Milliseconds(uploadStart);
                }
                if(!applyTransforms) continue;
                long transformStart=System.Diagnostics.Stopwatch.GetTimestamp();
                for(int petalIndex=0;petalIndex<l.petals.Length;petalIndex++)
                {
                    var p=l.petals[petalIndex];
                    PathPetalPose(l,p,petalIndex,progress,out var position,out var rotation,out var scale);
                    ApplyPetalPose(l,p,petalIndex,position,rotation,scale);
                }
                TransformMs+=Milliseconds(transformStart);
            }
        }
        static void PathPetalPose(Layer l,Petal p,int index,float progress,out Vector3 position,out Quaternion rotation,out Vector3 scale)
        {
            var c=l.config; float weight=c.OffsetWeight(progress);
            float tilt=Mathf.Lerp(c.closedTilt,c.openTilt,Mathf.SmoothStep(0,1,progress));
            PetalFrame(c,p.azimuth,tilt+p.tilt+p.authoredTilt*weight,p.roll,p.heightOffset*weight,index,l.petals.Length,progress,out position,out rotation,c.RollAt(progress));
            scale=PetalScale(c,p,index,progress,weight);
        }
        void ApplyPetalPose(Layer l,Petal p,int index,Vector3 position,Quaternion rotation,Vector3 scale)
        {
            // A common embedded root height applies to legacy and all art-state poses.
            if(!rootBaseline) position.y=layers[0].config.rootHeight+.0045f;
            p.anchor.SetLocalPositionAndRotation(position,rotation);
            p.scale=scale; p.body.localScale=scale;
            p.body.localPosition=-Vector3.Scale(SurfaceFor(l,index).root,scale);
            p.block.SetMatrix("_RootTrimWorldToFlower",p.anchor.parent.worldToLocalMatrix);
            p.renderer.SetPropertyBlock(p.block);
        }
        static Vertex Interpolate(Vertex a,Vertex b,float f) { Vertex r; Interpolate(ref a,ref b,f,out r); return r; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static void Interpolate(ref Vertex a,ref Vertex b,float f,out Vertex r)
        {
            r.p.x=a.p.x+(b.p.x-a.p.x)*f; r.p.y=a.p.y+(b.p.y-a.p.y)*f; r.p.z=a.p.z+(b.p.z-a.p.z)*f;
            float nx=a.n.x+(b.n.x-a.n.x)*f,ny=a.n.y+(b.n.y-a.n.y)*f,nz=a.n.z+(b.n.z-a.n.z)*f;
            float length=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);
            if(length>1e-5f) { float inv=1f/length; nx*=inv;ny*=inv;nz*=inv; } else nx=ny=nz=0;
            float tx=a.t.x+(b.t.x-a.t.x)*f,ty=a.t.y+(b.t.y-a.t.y)*f,tz=a.t.z+(b.t.z-a.t.z)*f;
            float dot=tx*nx+ty*ny+tz*nz; tx-=nx*dot; ty-=ny*dot; tz-=nz*dot;
            length=(float)Math.Sqrt(tx*tx+ty*ty+tz*tz);
            if(length>1e-5f) { float inv=1f/length; tx*=inv;ty*=inv;tz*=inv; } else tx=ty=tz=0;
            r.n=new Vector3(nx,ny,nz);r.t=new Vector4(tx,ty,tz,a.t.w);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || FLOWER_VALIDATION
        public string VerifyPackedInterpolation()
        {
            float maxPosition=0,maxAngle=0; bool handedness=true;
            foreach(var l in layers) foreach(var g in l.variants) for(int pose=0;pose<PoseCount-1;pose++) foreach(float f in new[]{0f,.5f,1f}) for(int k=0;k<g.data.Length;k++)
            {
                var a=g.poses[pose].data[k];var b=g.poses[pose+1].data[k]; var result=Interpolate(a,b,f);
                var position=Vector3.LerpUnclamped(a.p,b.p,f);var normal=Vector3.LerpUnclamped(a.n,b.n,f).normalized;
                var tangent=new Vector3(a.t.x+(b.t.x-a.t.x)*f,a.t.y+(b.t.y-a.t.y)*f,a.t.z+(b.t.z-a.t.z)*f);tangent=(tangent-normal*Vector3.Dot(tangent,normal)).normalized;
                maxPosition=Mathf.Max(maxPosition,Vector3.Distance(position,result.p));
                maxAngle=Mathf.Max(maxAngle,DirectionError(normal,result.n),DirectionError(tangent,new Vector3(result.t.x,result.t.y,result.t.z)));handedness&=a.t.w==result.t.w;
            }
            if(Marshal.SizeOf(typeof(Vertex))!=40||maxPosition>1e-6f||maxAngle>.05f||!handedness) throw new InvalidOperationException("Packed interpolation mismatch");
            return "PASS stride=40, maxPosition="+maxPosition+", maxAngle="+maxAngle+", handedness="+handedness;
        }
        static float DirectionError(Vector3 a,Vector3 b)=>(float)(Math.Atan2(Vector3.Cross(a,b).magnitude,Vector3.Dot(a,b))*180/Math.PI);
#endif
        static Vector3 PetalScale(FlowerLayer c,Petal p,int index,float progress=1,float? cachedWeight=null)=>c.size*new Vector3(Mathf.Clamp(c.width*p.widthFactor,.4f,1.08f),p.lengthFactor*Mathf.Lerp(1,c.OffsetAt(index)?.lengthScale??1,(cachedWeight??c.OffsetWeight(progress))),1);
        static void PetalFrame(FlowerLayer c,float azimuth,float tilt,float roll,float heightOffset,int index,int count,float progress,out Vector3 position,out Quaternion rotation,float? cachedRoll=null)
        {
            float az=azimuth*Mathf.Deg2Rad,beta=tilt*Mathf.Deg2Rad;
            Vector3 radial=new Vector3(Mathf.Sin(az),0,Mathf.Cos(az)),tangent=new Vector3(Mathf.Cos(az),0,-Mathf.Sin(az));
            Vector3 direction=radial*Mathf.Sin(beta)+Vector3.up*Mathf.Cos(beta),depth=Vector3.Cross(tangent,direction);
            position=radial*c.rootRadius+Vector3.up*(c.rootHeight+c.rootStagger*RootStaggerSign(index,count)+heightOffset)+tangent*c.tangentOffset+depth*c.depthOffset;
            rotation=Quaternion.LookRotation(depth,direction)*Quaternion.Euler(0,cachedRoll??c.RollAt(progress),0)*Quaternion.Euler(0,0,roll);
        }
        static float RootStaggerSign(int index,int count)=>count%2==1&&index==count-1?0:index%2==0?-1:1;
        // Concealed roots and curved filaments are generated once and share two renderers.
        void BuildHeart()
        {
            coreMesh=CombineHeart(false); stamenMesh=CombineHeart(true);
            AddHeartRenderer("Concealed filament roots",coreMesh,coreMaterial);
            AddHeartRenderer("Layered champagne anthers",stamenMesh,stamenMaterial);
        }
        Mesh CombineHeart(bool anthers)
        {
            var vertices=new System.Collections.Generic.List<Vector3>();
            var uv=new System.Collections.Generic.List<Vector2>();
            var triangles=new System.Collections.Generic.List<int>();
            Action<Vector3,Vector3,int,int,float> ellipsoid=(center,scale,rings,sides,groove)=>
            {
                int start=vertices.Count;
                for(int y=0;y<=rings;y++) for(int x=0;x<=sides;x++)
                {
                    float v=y/(float)rings,u=x/(float)sides,t=v*Mathf.PI,a=u*Mathf.PI*2;
                    float g=1-groove*Mathf.Pow(Mathf.Abs(Mathf.Cos(a)),16);
                    vertices.Add(center+Vector3.Scale(new Vector3(Mathf.Sin(t)*Mathf.Cos(a)*g,Mathf.Cos(t),Mathf.Sin(t)*Mathf.Sin(a)),scale)); uv.Add(new Vector2(u,v));
                }
                for(int y=0;y<rings;y++) for(int x=0;x<sides;x++)
                {
                    int q=start+y*(sides+1)+x;
                    if(y>0) { triangles.Add(q);triangles.Add(q+1);triangles.Add(q+sides+1); }
                    if(y<rings-1) { triangles.Add(q+1);triangles.Add(q+sides+2);triangles.Add(q+sides+1); }
                }
            };
            float floorY=layers[0].config.rootHeight;
            float floorRadius=0; foreach(var layer in layers) floorRadius=Mathf.Max(floorRadius,layer.config.rootRadius);
            floorRadius+=.04f;
            if(!anthers)
                ellipsoid(new Vector3(0,floorY,0),new Vector3(floorRadius,.012f,floorRadius),12,32,0);
            var random=new System.Random(preset.randomSeed+913);
            for(int j=0;j<30;j++)
            {
                float a=j*2.39996323f;
                float r=.025f+.105f*Mathf.Sqrt((j+.5f)/30f);
                float height=.87f+.075f*(float)random.NextDouble();
                var tip=new Vector3(Mathf.Cos(a)*r,height,Mathf.Sin(a)*r);
                if(anthers) ellipsoid(tip,new Vector3(.008f,.028f,.006f),12,20,.16f);
                else
                {
                    int start=vertices.Count; const int steps=8,sides=8;
                    for(int y=0;y<=steps;y++)
                    {
                        float t=y/(float)steps; var c=new Vector3(tip.x*(.65f+.35f*t*t),Mathf.Lerp(floorY+.004f,height,t),tip.z*(.65f+.35f*t*t));
                        for(int x=0;x<=sides;x++) { float angle=x*Mathf.PI*2/sides; vertices.Add(c+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.005f);uv.Add(new Vector2(x/(float)sides,t)); }
                    }
                    for(int y=0;y<steps;y++) for(int x=0;x<sides;x++) { int q=start+y*(sides+1)+x;triangles.Add(q);triangles.Add(q+sides+1);triangles.Add(q+1);triangles.Add(q+1);triangles.Add(q+sides+1);triangles.Add(q+sides+2); }
                }
            }
            var mesh=new Mesh { name=anthers?"Thirty grooved champagne anthers":"Closed receptacle and curved filaments" };
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void AddHeartRenderer(string title,Mesh mesh,Material material)
        { var go=new GameObject(title); go.transform.SetParent(generated,false); go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=material; }
        // Called only when choosing a view, never in the animation update. Fit the actual full-bloom
        // vertices rather than the empty corners of a large whole-flower AABB.
        public float FitCameraDistance(Quaternion rotation,float fieldOfView,float aspect)
        {
            float tan=Mathf.Tan(fieldOfView*Mathf.Deg2Rad*.5f),distance=1;
            Matrix4x4 view=Matrix4x4.TRS(FullBloomBounds.center,rotation,Vector3.one).inverse;
            foreach(var l in layers) for(int j=0;j<l.petals.Length;j++)
            {
                var p=l.petals[j];
                Matrix4x4 matrix=view*p.fullMatrix; var v=SurfaceFor(l,j).poses[PoseCount-1].data;
                for(int i=0;i<v.Length;i++)
                {
                    Vector3 point=matrix.MultiplyPoint3x4(v[i].p);
                    distance=Mathf.Max(distance,Mathf.Max(Mathf.Abs(point.y)/tan-point.z,Mathf.Abs(point.x)/(tan*aspect)-point.z));
                }
            }
            return distance*1.16f;
        }
        // Compatibility: the original layer query returns its first shared variant.
        public Mesh LayerMesh(int index)=>layers[index].variants[0].mesh;
        public Mesh PetalMesh(int layer,int index)=>SurfaceFor(layers[layer],index).mesh;
        public Mesh SharedMesh(int index)
        {
            foreach(var l in layers) { if(index<l.variants.Length) return l.variants[index].mesh; index-=l.variants.Length; }
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        public float RootAnchorError()
        {
            float max=0; foreach(var l in layers) for(int j=0;j<l.petals.Length;j++)
            { var p=l.petals[j]; max=Mathf.Max(max,Vector3.Distance(p.body.TransformPoint(SurfaceFor(l,j).root),p.anchor.position)); }
            return max;
        }
        void Release()
        {
            if(generated!=null) { generated.gameObject.SetActive(false); Destroy(generated.gameObject); }
            if(layers!=null) foreach(var l in layers) if(l?.variants!=null) foreach(var g in l.variants) if(g?.mesh!=null) Destroy(g.mesh);
            if(coreMesh!=null) Destroy(coreMesh); if(stamenMesh!=null) Destroy(stamenMesh);
            layers=null;
        }
        void OnDestroy() { ReleaseFlowPalette(); Release(); if(qualitySaved) QualitySettings.shadowCascades=previousCascades; }
    }
}





