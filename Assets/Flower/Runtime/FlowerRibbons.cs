using UnityEngine;
using UnityEngine.Rendering;
namespace MindState
{
    // One silk thread of light drawn around the bloom like a pen stroke: a bright head travels
    // forward around the ring while the tail behind it fades out.
    public sealed partial class FlowerAssembler
    {
        const int RibbonSegments=192;
        const float RibbonShowDuration=1.2f,RibbonIntensity=.38f;
        // Radii and heights are multiples of the bloom's horizontal radius, so the thread stays
        // outside the silhouette instead of grazing the petals.
        const float RibbonMinRadius=1.18f,RibbonMaskRadius=1.02f;
        // Visible trail length in turns: relaxed minds leave a longer stroke, capped at one and a
        // half turns. The nominal stroke stays around half a turn so it reads as a pen line.
        const float RibbonTrailMinTurns=.48f,RibbonTrailMaxTurns=1.5f,RibbonTrailPerRelax=.25f;
        sealed class SilkThread
        {
            public Transform root; public MeshRenderer renderer; public Mesh mesh; public MaterialPropertyBlock block;
            public Vector3[] vertices,tangents,positions,prevPositions; public Vector2[] uv;
        }
        Transform ribbonRoot;
        SilkThread ribbonThread;
        Material ribbonMaterial;
        float ribbonFade,ribbonBloomRadius=1.7f,ribbonCurrentRadius=1.7f;
        float ribbonTime,ribbonTurns,ribbonHeadVelocity,ribbonPrevHeadTurns,ribbonHeadTurn;
        bool ribbonStepValid,ribbonHeadValid;
        float ribbonTrailTurns,ribbonWaveAmplitude,ribbonMeanRadius,ribbonMeanHeight;
        internal int VisibleRibbonCount=>ribbonThread!=null&&ribbonRoot!=null&&ribbonRoot.gameObject.activeSelf&&ribbonThread.renderer.enabled?1:0;
        internal bool RibbonThreadVisible=>ribbonThread!=null&&ribbonThread.renderer.enabled&&ribbonThread.block!=null&&ribbonFade>.05f;
        internal float RibbonTrailTurns=>ribbonTrailTurns;
        internal float RibbonWaveAmplitude=>ribbonWaveAmplitude;
        internal float RibbonMeanRadius=>ribbonMeanRadius;
        internal float RibbonMeanHeight=>ribbonMeanHeight;
        internal float RibbonHeadVelocity=>ribbonHeadVelocity;
        internal float RibbonHeadTurn=>ribbonHeadTurn;
        internal float RibbonMaxVertexStep{get;private set;}
        internal bool RibbonVerticesFinite()
        {
            if(ribbonThread==null) return false;
            foreach(var vertex in ribbonThread.vertices)
                if(float.IsNaN(vertex.x)||float.IsNaN(vertex.y)||float.IsNaN(vertex.z)||float.IsInfinity(vertex.x)||float.IsInfinity(vertex.y)||float.IsInfinity(vertex.z)) return false;
            return true;
        }
        internal float RibbonBloomRadius=>ribbonCurrentRadius;
        internal float RibbonClearance()
        {
            if(ribbonThread==null||ribbonCurrentRadius<.01f) return 0f;
            float min=float.MaxValue;
            foreach(var vertex in ribbonThread.vertices)
            {
                var local=transform.InverseTransformPoint(ribbonThread.root.TransformPoint(vertex));
                min=Mathf.Min(min,new Vector2(local.x,local.z).magnitude);
            }
            return min==float.MaxValue?0f:min/ribbonCurrentRadius;
        }
        internal void ResetRibbonThread()
        {
            ribbonTime=0;ribbonTurns=0;ribbonHeadVelocity=0;ribbonPrevHeadTurns=0;
            ribbonHeadValid=false;ribbonStepValid=false;
        }
        void ShowRibbons()
        {
            if(ribbonRoot==null) return;
            ribbonFade=0;
            ribbonStepValid=false;
            ribbonRoot.gameObject.SetActive(true);
        }
        void HideRibbons()
        {
            ribbonFade=0;
            if(ribbonRoot!=null) ribbonRoot.gameObject.SetActive(false);
        }
        float MeasureBloomRadius()
        {
            var bounds=FullBloomBounds;float radius=0;
            for(int i=0;i<8;i++)
            {
                var corner=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,(i&4)==0?bounds.min.z:bounds.max.z);
                var local=transform.InverseTransformPoint(corner);
                radius=Mathf.Max(radius,Mathf.Abs(local.x),Mathf.Abs(local.z));
            }
            return radius>.1f?radius:1.7f;
        }
        float CurrentBloomRadius()
        {
            // Playback culling bounds deliberately union an interval. They are not a
            // motion signal: use the continuous pose envelope for the route and mask.
            if(gpuPlayback&&playback!=null)
            {
                float continuousRadius=0;
                var worldToFlower=transform.worldToLocalMatrix;
                for(int l=0;l<layers.Length;l++) for(int p=0;p<layers[l].petals.Length;p++)
                {
                    var poseBounds=playback[l][p].motionBounds;
                    var matrix=worldToFlower*layers[l].petals[p].body.localToWorldMatrix;
                    for(int i=0;i<8;i++)
                    {
                        var corner=new Vector3((i&1)==0?poseBounds.min.x:poseBounds.max.x,(i&2)==0?poseBounds.min.y:poseBounds.max.y,(i&4)==0?poseBounds.min.z:poseBounds.max.z);
                        var local=matrix.MultiplyPoint3x4(corner);
                        continuousRadius=Mathf.Max(continuousRadius,Mathf.Max(Mathf.Abs(local.x),Mathf.Abs(local.z)));
                    }
                }
                return continuousRadius>.1f?continuousRadius:ribbonBloomRadius;
            }
            var bounds=WorldBounds;float radius=0;
            for(int i=0;i<8;i++)
            {
                var corner=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,(i&4)==0?bounds.min.z:bounds.max.z);
                var local=transform.InverseTransformPoint(corner);
                radius=Mathf.Max(radius,Mathf.Abs(local.x),Mathf.Abs(local.z));
            }
            return radius>.1f?radius:ribbonBloomRadius;
        }
        void BuildRibbons()
        {
            ReleaseRibbons();
            var shader=Resources.Load<Shader>("SilkRibbon");
            if(shader==null) shader=Shader.Find("MindState/Silk Ribbon");
            if(shader==null) return;
            ribbonBloomRadius=MeasureBloomRadius();
            ribbonMaterial=new Material(shader) { name="Silk ribbon (runtime)",hideFlags=HideFlags.DontSave };
            ribbonMaterial.SetFloat("_Core",3f);
            ribbonRoot=new GameObject("Silk thread of light").transform;
            ribbonRoot.SetParent(transform,false);
            var root=new GameObject("Silk thread").transform;
            root.SetParent(ribbonRoot,false);
            var mesh=new Mesh { name="Silk thread" };
            mesh.MarkDynamic();
            var vertices=new Vector3[(RibbonSegments+1)*2];
            var tangents=new Vector3[(RibbonSegments+1)*2];
            var uv=new Vector2[(RibbonSegments+1)*2];
            var triangles=new int[RibbonSegments*6];
            for(int i=0;i<=RibbonSegments;i++)
            {
                uv[i*2]=new Vector2(i/(float)RibbonSegments,0);
                uv[i*2+1]=new Vector2(i/(float)RibbonSegments,1);
                if(i==RibbonSegments) break;
                int a=i*2,b=a+1,c=(i+1)*2,d=c+1;
                triangles[i*6]=a;triangles[i*6+1]=c;triangles[i*6+2]=b;
                triangles[i*6+3]=b;triangles[i*6+4]=c;triangles[i*6+5]=d;
            }
            mesh.vertices=vertices;
            mesh.normals=tangents;
            mesh.uv=uv;
            mesh.triangles=triangles;
            mesh.bounds=new Bounds(new Vector3(0,.2f*ribbonBloomRadius,0),new Vector3(4f,1.6f,4f)*ribbonBloomRadius);
            var renderer=root.gameObject.AddComponent<MeshRenderer>();
            var filter=root.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh=mesh;
            renderer.sharedMaterial=ribbonMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            ribbonThread=new SilkThread { root=root,renderer=renderer,mesh=mesh,block=new MaterialPropertyBlock(),vertices=vertices,tangents=tangents,positions=new Vector3[RibbonSegments+1],prevPositions=new Vector3[RibbonSegments+1],uv=uv };
            ResetRibbonThread();
            ribbonRoot.gameObject.SetActive(false);
        }
        void ReleaseRibbons()
        {
            if(ribbonRoot!=null) Destroy(ribbonRoot.gameObject);
            ribbonRoot=null;
            if(ribbonThread?.mesh!=null) Destroy(ribbonThread.mesh);
            ribbonThread=null;
            if(ribbonMaterial!=null) Destroy(ribbonMaterial);
            ribbonMaterial=null;
            ribbonFade=0;
        }
        void UpdateRibbons(float dt)
        {
            if(ribbonRoot==null||ribbonThread==null) return;
            if(frozen) dt=0;
            if(!artActive) { ribbonRoot.gameObject.SetActive(false); return; }
            ribbonRoot.gameObject.SetActive(true);
            ribbonFade=Mathf.MoveTowards(ribbonFade,1,dt/RibbonShowDuration);
            float attention=Mathf.Clamp01(ShapeAttention),relaxation=Mathf.Clamp01(ShapeRelaxation);
            float tense=attention*(1f-relaxation);
            ribbonCurrentRadius=CurrentBloomRadius();
            float radiusUnit=ribbonCurrentRadius;
            // The head only ever travels forward: EEG changes how fast the ring is circled, never
            // its direction. Accumulating dt/period (instead of dividing an absolute clock by a
            // moving period) also keeps the rate positive when attention or relaxation changes.
            float period=16f-4f*attention+4f*relaxation;
            float advance=0f;
            if(!frozen)
            {
                ribbonTime+=dt;
                advance=dt/Mathf.Max(1f,period);
                ribbonTurns+=advance;
            }
            float headTurns=ribbonTurns;
            ribbonHeadTurn=headTurns;
            if(dt>.00001f)
            {
                ribbonHeadVelocity=ribbonHeadValid?(headTurns-ribbonPrevHeadTurns)/dt:0f;
                ribbonPrevHeadTurns=headTurns;
                ribbonHeadValid=true;
            }
            float targetTrail=Mathf.Min(RibbonTrailMaxTurns,RibbonTrailMinTurns+RibbonTrailPerRelax*relaxation);
            // The trail may lengthen no faster than the head advances, so its far end never slides
            // backwards; shrinking follows immediately because the target itself moves smoothly.
            if(!ribbonStepValid||targetTrail<=ribbonTrailTurns) ribbonTrailTurns=targetTrail;
            else ribbonTrailTurns=Mathf.Min(targetTrail,ribbonTrailTurns+advance);
            // Shape parameters are continuous functions of the smoothed attention/relaxation, so
            // a violent raw signal only makes the stroke drift to a new route.
            float baseRadius=1.32f-.10f*attention+.08f*relaxation;
            float radiusRipple=.015f+.035f*tense+.010f*(1f-relaxation);
            float radiusRipple2=.006f+.012f*tense;
            float baseHeight=.10f+.24f*attention-.04f*relaxation;
            float heightWave=.015f+.090f*tense+.025f*(1f-relaxation);
            float heightWave2=.008f+.030f*tense;
            ribbonWaveAmplitude=(heightWave+heightWave2)*radiusUnit;
            float width=.042f+.012f*(1f-attention);
            float phase1=ribbonTime*.05f,phase2=ribbonTime*.031f+1.7f,phase3=ribbonTime*.04f+2.1f,phase4=ribbonTime*.027f+4.2f;
            float minRadius=RibbonMinRadius*radiusUnit;
            var thread=ribbonThread;
            float meanRadius=0,meanHeight=0;
            for(int i=0;i<=RibbonSegments;i++)
            {
                float s=i/(float)RibbonSegments;
                float turns=Mathf.Repeat(headTurns-ribbonTrailTurns*(1f-s),1f);
                float theta=turns*Mathf.PI*2;
                float r=baseRadius*(1f+radiusRipple*Mathf.Sin(theta*3f-phase1)+radiusRipple2*Mathf.Sin(theta*5f-phase2));
                r=Mathf.Max(r*radiusUnit,minRadius);
                float y=(baseHeight+heightWave*Mathf.Sin(theta*2f-phase3)+heightWave2*Mathf.Sin(theta*5f-phase4))*radiusUnit;
                meanRadius+=r;meanHeight+=y;
                thread.positions[i]=new Vector3(Mathf.Cos(theta)*r,y,Mathf.Sin(theta)*r);
            }
            float denominator=1f/(RibbonSegments+1);
            ribbonMeanRadius=meanRadius*denominator/Mathf.Max(.01f,radiusUnit);
            ribbonMeanHeight=meanHeight*denominator/Mathf.Max(.01f,radiusUnit);
            float maxStep=0;
            for(int i=0;i<=RibbonSegments;i++)
            {
                int before=Mathf.Max(0,i-1),after=Mathf.Min(RibbonSegments,i+1);
                Vector3 tangent=(thread.positions[after]-thread.positions[before]).normalized;
                thread.vertices[i*2]=thread.positions[i];
                thread.vertices[i*2+1]=thread.positions[i];
                thread.tangents[i*2]=tangent;
                thread.tangents[i*2+1]=tangent;
                if(ribbonStepValid) maxStep=Mathf.Max(maxStep,(thread.positions[i]-thread.prevPositions[i]).magnitude);
            }
            System.Array.Copy(thread.positions,thread.prevPositions,thread.positions.Length);
            ribbonStepValid=true;
            RibbonMaxVertexStep=maxStep;
            thread.mesh.SetVertices(thread.vertices);
            thread.mesh.SetNormals(thread.tangents);
            var accent=ArtAccentColor.linear;
            thread.block.SetVector("_Color",new Vector4(accent.r,accent.g,accent.b,1));
            thread.block.SetFloat("_Intensity",RibbonIntensity*ribbonFade);
            thread.block.SetFloat("_Width",width);
            var center=transform.position;
            thread.block.SetVector("_MaskCenter",new Vector4(center.x,center.y,center.z,0));
            thread.block.SetFloat("_MaskRadius",RibbonMaskRadius*radiusUnit);
            thread.renderer.enabled=true;
            thread.renderer.SetPropertyBlock(thread.block);
        }
    }
}
