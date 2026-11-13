using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace MindState
{
    // A dim companion flower that summarizes the states from 10-45 seconds ago.
    public sealed partial class FlowerAssembler
    {
        // 128 samples at 0.5 s cover 64 s, enough for the 10-45 s window below.
        const float EchoScale=.50f,EchoWindow=45f,EchoRecent=10f,EchoSampleInterval=.5f,EchoFadeDuration=1.5f,EchoAccentSmoothing=1.5f;
        const int EchoCols=33,EchoRows=21,EchoStride=4;
        // Horizontal gap from the main bloom, in bloom radii. The camera fit reuses it.
        static readonly Vector3 EchoOffsetUnit=new Vector3(-1.70f,.35f,-.20f);
        // The companion slides between the open gap and the closed-bud gap with the bloom.
        const float EchoGapOpen=1.70f,EchoGapClosed=1.30f;
        Vector3 echoOffset=EchoOffsetUnit*1.7f;
        float echoBloomRadius=1.7f;
        Color echoAccentSmooth=Color.white;
        sealed class EchoVariant { public Variant source; public Mesh mesh; public Vertex[] data; public int[] ids; public Vector3 root; public float lastProgress=-1; }
        sealed class EchoPetal { public Petal source; public int variant; public Transform anchor,body; public MeshRenderer renderer; public MaterialPropertyBlock block; }
        struct EchoSample { public float clock,bloom; public Vector4 weights,light; }
        const int EchoCapacity=128;
        readonly EchoSample[] echoSamples=new EchoSample[EchoCapacity];
        Transform echoRoot;
        EchoVariant[] echoVariants;
        int[] echoVariantBase;
        EchoPetal[] echoPetals;
        int echoHead,echoCount;
        float lastEchoSample=float.NegativeInfinity,echoFade,echoYaw;
        internal Vector4 EchoWeights { get; private set; }=new Vector4(1,0,0,0);
        internal int EchoPetalCount=>echoPetals?.Length??0;
        internal bool EchoVisible=>echoRoot!=null&&echoRoot.gameObject.activeSelf;
        internal float EchoScaleValue=>EchoScale;
        // Centre distance over the two current radii, so the check still holds when the pair closes.
        internal float EchoClearance()
        {
            if(echoRoot==null) return 0f;
            var local=echoRoot.localPosition;
            float radius=CurrentBloomRadius();
            return new Vector2(local.x,local.z).magnitude/(radius*(1+EchoScale));
        }
        internal void SyncEchoHistory(){ResetEchoHistory();}
        internal bool EchoMeshFinite()
        {
            if(echoVariants==null) return false;
            foreach(var variant in echoVariants) foreach(var vertex in variant.data)
                if(float.IsNaN(vertex.p.x)||float.IsNaN(vertex.p.y)||float.IsNaN(vertex.p.z)||float.IsNaN(vertex.n.x)||float.IsNaN(vertex.t.x)) return false;
            return true;
        }
        // Built on first use so the cached geometry sources stay untouched.
        void ShowArtExtras(){EnsureArtExtras();ShowEcho();ShowRibbons();}
        void EnsureArtExtras()
        {
            if(echoPetals==null) BuildEcho();
            if(ribbonThread==null) BuildRibbons();
        }
        void HideArtExtras(){HideEcho();HideRibbons();}
        void ShowEcho()
        {
            if(echoRoot==null) return;
            echoRoot.gameObject.SetActive(true);
            echoFade=0;
            ResetEchoHistory();
        }
        void HideEcho()
        {
            echoFade=0;
            if(echoRoot!=null) echoRoot.gameObject.SetActive(false);
        }
        void BuildEcho()
        {
            ReleaseEcho();
            if(layers==null||petalPrefab==null||petalPrefab.pearlMaterial==null) return;
            echoBloomRadius=MeasureBloomRadius();
            echoOffset=EchoOffsetUnit*echoBloomRadius;
            echoRoot=new GameObject("Echo · recent memory").transform;
            echoRoot.SetParent(transform,false);
            echoRoot.localPosition=echoOffset;
            echoRoot.localScale=Vector3.one*EchoScale;
            int variantTotal=0;
            foreach(var l in layers) variantTotal+=l.variants.Length;
            echoVariants=new EchoVariant[variantTotal];
            echoVariantBase=new int[layers.Length];
            int variantCursor=0;
            for(int k=0;k<layers.Length;k++)
            {
                var l=layers[k];
                echoVariantBase[k]=variantCursor;
                for(int v=0;v<l.variants.Length;v++) echoVariants[variantCursor++]=BuildEchoVariant(l,v);
            }
            int total=0;
            foreach(var l in layers) total+=l.petals.Length;
            echoPetals=new EchoPetal[total];
            int petalCursor=0;
            for(int k=0;k<layers.Length;k++)
            {
                var l=layers[k];
                for(int j=0;j<l.petals.Length;j++)
                {
                    var src=l.petals[j];
                    var anchor=new GameObject(l.config.name+" echo · "+(j+1)).transform;
                    anchor.SetParent(echoRoot,false);
                    var body=new GameObject("Silk echo petal").transform;
                    body.SetParent(anchor,false);
                    var echoVariant=echoVariants[echoVariantBase[k]+j%l.variants.Length];
                    body.gameObject.AddComponent<MeshFilter>().sharedMesh=echoVariant.mesh;
                    var renderer=body.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=petalPrefab.pearlMaterial;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;
                    renderer.receiveShadows=false;
                    var block=new MaterialPropertyBlock();
                    src.renderer.GetPropertyBlock(block);
                    block.SetVector("_FlowSettings",Vector4.zero);
                    block.SetFloat("_Breath",0);
                    block.SetVector("_ArtWeights",Vector4.zero);
                    block.SetVector("_ArtLightWeights",Vector4.zero);
                    renderer.SetPropertyBlock(block);
                    body.localScale=src.scale;
                    echoPetals[petalCursor++]=new EchoPetal { source=src,variant=echoVariantBase[k]+j%l.variants.Length,anchor=anchor,body=body,renderer=renderer,block=block };
                }
            }
            // Petal bodies are placed every frame; keep the companion out of the frame until art mode starts.
            echoRoot.gameObject.SetActive(false);
            ResetEchoHistory();
        }
        EchoVariant BuildEchoVariant(Layer layer,int variant)
        {
            var source=layer.variants[variant];
            int cols=EchoCols,rows=EchoRows,fullCols=HeroPetal.WidthSegments+1;
            int surface=fullCols*(HeroPetal.LengthSegments+1),front=cols*rows,total=front*2;
            var ids=new int[total];
            for(int y=0;y<rows;y++) for(int x=0;x<cols;x++)
            {
                int id=y*EchoStride*fullCols+x*EchoStride;
                ids[y*cols+x]=id;
                ids[front+y*cols+x]=id+surface;
            }
            var data=new Vertex[total];
            for(int i=0;i<total;i++) data[i]=source.poses[0].data[ids[i]];
            var uv=new Vector2[total];
            for(int y=0;y<rows;y++) for(int x=0;x<cols;x++)
            {
                var coordinate=new Vector2(x/(cols-1f),y/(rows-1f));
                uv[y*cols+x]=coordinate;
                uv[front+y*cols+x]=coordinate;
            }
            var triangles=new int[(cols-1)*(rows-1)*12];
            int cursor=0;
            for(int y=0;y<rows-1;y++) for(int x=0;x<cols-1;x++)
            {
                int a=y*cols+x,c=a+cols;
                triangles[cursor++]=a;triangles[cursor++]=c;triangles[cursor++]=a+1;
                triangles[cursor++]=a+1;triangles[cursor++]=c;triangles[cursor++]=c+1;
                a+=front;c+=front;
                triangles[cursor++]=a;triangles[cursor++]=a+1;triangles[cursor++]=c;
                triangles[cursor++]=a+1;triangles[cursor++]=c+1;triangles[cursor++]=c;
            }
            var mesh=new Mesh { name="Echo shared petal "+variant };
            mesh.MarkDynamic();
            mesh.SetVertexBufferParams(total,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3,0),new VertexAttributeDescriptor(VertexAttribute.Tangent,VertexAttributeFormat.Float32,4,0),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2,1));
            mesh.SetVertexBufferData(data,0,0,total,0,MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetVertexBufferData(uv,0,0,total,1,MeshUpdateFlags.DontRecalculateBounds);
            mesh.triangles=triangles;
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*3f);
            return new EchoVariant { source=source,mesh=mesh,data=data,ids=ids,root=Vector3.zero };
        }
        void ReleaseEcho()
        {
            if(echoRoot!=null) Destroy(echoRoot.gameObject);
            echoRoot=null;
            if(echoVariants!=null) foreach(var variant in echoVariants) if(variant?.mesh!=null) Destroy(variant.mesh);
            echoVariants=null;
            echoVariantBase=null;
            echoPetals=null;
            echoHead=echoCount=0;
            lastEchoSample=float.NegativeInfinity;
            echoFade=0;
        }
        void ResetEchoHistory()
        {
            echoHead=echoCount=0;
            lastEchoSample=artTime;
            echoYaw=artYaw;
            AppendEchoSample();
            echoAccentSmooth=CommittedAccent();
        }
        void AppendEchoSample()
        {
            echoSamples[echoHead]=new EchoSample { clock=artTime,bloom=bloomProgress,weights=ArtWeights,light=ArtLightWeights };
            echoHead=(echoHead+1)%EchoCapacity;
            if(echoCount<EchoCapacity) echoCount++;
        }
        void PushEchoHistory()
        {
            if(frozen) return;
            if(echoCount>0&&artTime-lastEchoSample<EchoSampleInterval) return;
            lastEchoSample=artTime;
            AppendEchoSample();
        }
        // A sliding mean of the 10-45 s old window: how much time was spent in each state, not a
        // snapshot from a fixed delay. The most recent 10 s never participate.
        void AverageEchoHistory(out Vector4 weights,out Vector4 light,out float bloom)
        {
            weights=Vector4.zero; light=Vector4.zero; bloom=0;
            int oldest=(echoHead-echoCount+EchoCapacity)%EchoCapacity,count=0;
            float from=artTime-EchoWindow,to=artTime-EchoRecent;
            for(int i=0;i<echoCount;i++)
            {
                var sample=echoSamples[(oldest+i)%EchoCapacity];
                if(sample.clock<from||sample.clock>to) continue;
                weights+=sample.weights; light+=sample.light; bloom+=sample.bloom; count++;
            }
            // Before 10 s of history exists, hold the oldest remembered state instead of the live one.
            if(count==0)
            {
                if(echoCount>0) { var sample=echoSamples[oldest]; weights=sample.weights; light=sample.light; bloom=sample.bloom; }
                else { weights=ArtWeights; light=ArtLightWeights; bloom=bloomProgress; }
                return;
            }
            weights/=count; light/=count; bloom/=count;
        }
        void UpdateEcho(float dt)
        {
            if(echoRoot==null) return;
            if(!artActive) { echoRoot.gameObject.SetActive(false); return; }
            echoRoot.gameObject.SetActive(true);
            echoFade=Mathf.MoveTowards(echoFade,1,dt/EchoFadeDuration);
            PushEchoHistory();
            AverageEchoHistory(out var weights,out var light,out var bloom);
            EchoWeights=weights;
            var look=BlendLook(weights);
            if(!frozen) echoYaw=Mathf.Repeat(echoYaw+look.yawSpeed*dt,360);
            // Follow the echo's own averaged bloom: a large memory flower keeps the full gap and
            // stays behind the main bloom, and only an echo that has closed slides in as a small
            // companion.
            float open=Mathf.SmoothStep(.10f,.55f,bloom);
            float gap=Mathf.Lerp(EchoGapClosed,EchoGapOpen,open)/EchoGapOpen;
            // The camera never dollies, so the companion only nudges itself: a remembered bud grows
            // slightly and steps toward the viewer, but stays on its secondary side of the main bloom.
            float bud=BudFactor(bloom);
            // While the main bloom is closed the companion also steps back along the view axis
            // (depth only, no page shift) and drifts toward the main, keeping it clearly secondary.
            float close=BudFactor(bloomProgress);
            echoRoot.localScale=Vector3.one*EchoScale*(1f+.08f*bud);
            echoRoot.localPosition=new Vector3(echoOffset.x*gap,echoOffset.y,echoOffset.z)+viewToCamera*(.12f*bud)
                -viewToCamera*(viewDistance*EchoBackGain*close)+viewPageRight*(viewDistance*EchoSideGain*close)
                +new Vector3(0,.005f*weights.z-.025f*weights.w,0);
            echoRoot.localRotation=Quaternion.Euler(4f*weights.w,echoYaw,5f*weights.z);
            // The companion keeps the averaged silhouette but reads as a cool afterimage,
            // not as a second colored flower.
            var tint=new Vector4(.55f*echoFade,.40f,0,0);
            Color memory=new Color(.58f,.66f,.78f);
            if(!frozen) echoAccentSmooth=Color.Lerp(echoAccentSmooth,artLooks[LeadingState(weights)].accent,1-Mathf.Exp(-dt/EchoAccentSmoothing));
            var accent=(Vector4)Color.Lerp(memory,echoAccentSmooth,.22f).linear;
            var surface=new Vector4(.88f,look.rim*.30f,0f,look.gloss*.30f);
            var worldToEcho=echoRoot.worldToLocalMatrix;
            for(int k=0;k<layers.Length;k++)
            {
                var l=layers[k];
                float progress=StandardLayerProgress(l.config,Mathf.Clamp01(bloom));
                for(int j=0;j<l.petals.Length;j++)
                {
                    int petalIndex=LayerPetalOffset(k)+j;
                    var petal=echoPetals[petalIndex];
                    var variant=echoVariants[petal.variant];
                    UpdateEchoMesh(variant,progress);
                    PathPetalPose(l,petal.source,j,progress,out var position,out var rotation,out var scale);
                    position.y=layers[0].config.rootHeight+.0045f+l.config.rootStagger*RootStaggerSign(j,l.petals.Length);
                    float az=j*Mathf.PI*2/l.petals.Length;
                    PetalArtMotion(weights,artTime,az,k,j,out float tilt,out float roll);
                    rotation=rotation*Quaternion.Euler(tilt,0,roll);
                    petal.anchor.SetLocalPositionAndRotation(position,rotation);
                    petal.body.localScale=scale;
                    petal.body.localPosition=-Vector3.Scale(variant.root,scale);
                    petal.block.SetVector("_FlowSettings",new Vector4(0,appearance!=null?appearance.bandWidth:.65f,0,0));
                    petal.block.SetVector("_StateTint",tint);
                    petal.block.SetVector("_StateAccent",accent);
                    petal.block.SetVector("_StateSurface",surface);
                    petal.block.SetVector("_ArtWeights",weights);
                    petal.block.SetVector("_ArtLightWeights",light);
                    petal.block.SetFloat("_ArtClock",artTime);
                    petal.block.SetFloat("_ArtPhase",az+k*.5f);
                    petal.block.SetMatrix("_RootTrimWorldToFlower",worldToEcho);
                    petal.renderer.SetPropertyBlock(petal.block);
                }
            }
        }
        int LayerPetalOffset(int layer)
        {
            int offset=0;
            for(int i=0;i<layer;i++) offset+=layers[i].petals.Length;
            return offset;
        }
        void UpdateEchoMesh(EchoVariant variant,float progress)
        {
            if(Mathf.Abs(progress-variant.lastProgress)<.00015f) return;
            variant.lastProgress=progress;
            float frame=progress*(PoseCount-1);
            int a=Mathf.Min((int)frame,PoseCount-2);
            float f=frame-a;
            var first=variant.source.poses[a];
            var next=variant.source.poses[a+1];
            for(int i=0;i<variant.ids.Length;i++) Interpolate(ref first.data[variant.ids[i]],ref next.data[variant.ids[i]],f,out variant.data[i]);
            variant.mesh.SetVertexBufferData(variant.data,0,0,variant.data.Length,0,MeshUpdateFlags.DontRecalculateBounds);
            int center=EchoCols/2,front=EchoCols*EchoRows;
            Vector3 frontRoot=Vector3.LerpUnclamped(first.data[variant.ids[center]].p,next.data[variant.ids[center]].p,f);
            Vector3 backRoot=Vector3.LerpUnclamped(first.data[variant.ids[front+center]].p,next.data[variant.ids[front+center]].p,f);
            variant.root=(frontRoot+backRoot)*.5f;
        }
    }
}
