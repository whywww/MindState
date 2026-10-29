using UnityEngine;

namespace MindState
{
    public enum PetalInputMode { SimulatedBrain, ExternalBrain, DirectShape }

    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class HeroPetal : MonoBehaviour
    {
        public const int LengthSegments=80, WidthSegments=128;
        const int Cols=WidthSegments+1, Rows=LengthSegments+1, SurfaceCount=Cols*Rows;
        public Material pearlMaterial;
        public PetalInputMode inputMode=PetalInputMode.SimulatedBrain;
        public bool frozen,grayPreview;
        [Range(90,150)] public float targetOpeningAngle=125;
        [Range(0,1)] public float targetCurlAmount=0.2f,targetPleatSoftness=0.7f;
        [Range(20,40)] public int finePleatCount=32;
        [Range(0,1)] public float targetAttention=0.5f,targetRelaxation=0.65f;
        public float OpeningAngle { get; private set; }=125;
        public float CurlAmount { get; private set; }=0.2f;
        public float PleatSoftness { get; private set; }=0.7f;
        public float Attention { get; private set; }=0.5f;
        public float Relaxation { get; private set; }=0.65f;
        public float OpeningProgress { get; private set; }
        public float Growth=>OpeningProgress; // Compatibility: unfolding, never mesh truncation.
        public bool automatic { get=>inputMode==PetalInputMode.SimulatedBrain; set=>inputMode=value?PetalInputMode.SimulatedBrain:PetalInputMode.ExternalBrain; }
        public Mesh RuntimeMesh=>mesh;
        public double LastUpdateMilliseconds { get; private set; }
        public long LastUpdateAllocation { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Only the diagnostics runner selects 1.0 for the preserved A baseline.
        internal float diagnosticPleatScale=0.8f;
#endif
        static readonly Unity.Profiling.ProfilerMarker UpdateMarker=new Unity.Profiling.ProfilerMarker("MindState.Petal.Update");

        Mesh mesh;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock properties;
        Vector3[] surface,vertices,normals;
        Vector4[] tangents;
        Vector2[] uv;
        int[] boundary;
        readonly float[] sinTheta=new float[Cols],cosTheta=new float[Cols],rimRadius=new float[Cols],widePleat=new float[Cols],rimWave=new float[Cols],leftFold=new float[Cols],rightFold=new float[Cols];
        float elapsed,nextSample;
        static readonly int BreathId=Shader.PropertyToID("_Breath"),FineCountId=Shader.PropertyToID("_FineCount"),GrayId=Shader.PropertyToID("_GrayMode");

        void Awake()
        {
            mesh=new Mesh { name="Silk fan — closed thin shell" }; mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh=mesh;
            meshRenderer=GetComponent<MeshRenderer>(); meshRenderer.sharedMaterial=pearlMaterial;
            properties=new MaterialPropertyBlock();
            surface=new Vector3[SurfaceCount]; boundary=new int[2*(LengthSegments+WidthSegments)];
            int b=0;
            for(int x=0;x<Cols-1;x++) boundary[b++]=x;
            for(int y=0;y<Rows-1;y++) boundary[b++]=y*Cols+Cols-1;
            for(int x=Cols-1;x>0;x--) boundary[b++]=(Rows-1)*Cols+x;
            for(int y=Rows-1;y>0;y--) boundary[b++]=y*Cols;
            int count=SurfaceCount*2+boundary.Length*2;
            vertices=new Vector3[count]; normals=new Vector3[count]; tangents=new Vector4[count]; uv=new Vector2[count];
            for(int y=0;y<Rows;y++) for(int x=0;x<Cols;x++)
            {
                int i=y*Cols+x; uv[i]=uv[i+SurfaceCount]=new Vector2(x/(float)WidthSegments,y/(float)LengthSegments);
            }
            var triangles=new int[LengthSegments*WidthSegments*12+boundary.Length*6]; int k=0;
            for(int y=0;y<LengthSegments;y++) for(int x=0;x<WidthSegments;x++)
            {
                int a=y*Cols+x,c=a+Cols;
                triangles[k++]=a; triangles[k++]=c; triangles[k++]=a+1;
                triangles[k++]=a+1; triangles[k++]=c; triangles[k++]=c+1;
                a+=SurfaceCount; c+=SurfaceCount;
                triangles[k++]=a; triangles[k++]=a+1; triangles[k++]=c;
                triangles[k++]=a+1; triangles[k++]=c+1; triangles[k++]=c;
            }
            for(int i=0;i<boundary.Length;i++)
            {
                int a=SurfaceCount*2+i*2,c=SurfaceCount*2+(i+1)%boundary.Length*2;
                triangles[k++]=a; triangles[k++]=c; triangles[k++]=a+1;
                triangles[k++]=c; triangles[k++]=c+1; triangles[k++]=a+1;
            }
            mesh.vertices=vertices; mesh.uv=uv; mesh.triangles=triangles;
            Rebuild();
        }

        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        /// <summary>Main thread, degrees / normalized curl / normalized softness. Selects DirectShape.</summary>
        public void SetShapeParameters(float openingAngleDegrees,float curlAmount01,float pleatSoftness01)
        {
            inputMode=PetalInputMode.DirectShape;
            if(Finite(openingAngleDegrees)) targetOpeningAngle=Mathf.Clamp(openingAngleDegrees,90,150);
            if(Finite(curlAmount01)) targetCurlAmount=Mathf.Clamp01(curlAmount01);
            if(Finite(pleatSoftness01)) targetPleatSoftness=Mathf.Clamp01(pleatSoftness01);
        }
        /// <summary>Main thread, normalized device targets. Selects ExternalBrain and holds the last sample.</summary>
        public void SetBrainMetrics(float attention01,float relaxation01)
        {
            inputMode=PetalInputMode.ExternalBrain;
            AcceptBrain(attention01,relaxation01);
        }
        void AcceptBrain(float a,float r)
        {
            if(Finite(a)) targetAttention=Mathf.Clamp01(a);
            if(Finite(r)) targetRelaxation=Mathf.Clamp01(r);
            targetOpeningAngle=90+60*targetRelaxation;
            targetCurlAmount=0.85f-0.75f*targetRelaxation;
            targetPleatSoftness=0.9f-0.65f*targetAttention;
        }
        public void RestartOpening() { OpeningProgress=0; frozen=false; }
        public void RestartGrowth()=>RestartOpening();
        public void SetShapeImmediate(float angle,float curl,float softness,float progress=1)
        {
            SetShapeParameters(angle,curl,softness);
            OpeningAngle=targetOpeningAngle; CurlAmount=targetCurlAmount; PleatSoftness=targetPleatSoftness;
            OpeningProgress=Finite(progress)?Mathf.Clamp01(progress):1; frozen=false;
            if(mesh!=null) Rebuild();
        }
        public void SetImmediate(float attention,float relaxation,float growth=1)
        {
            SetBrainMetrics(attention,relaxation); Attention=targetAttention; Relaxation=targetRelaxation;
            SetShapeImmediate(targetOpeningAngle,targetCurlAmount,targetPleatSoftness,growth);
            inputMode=PetalInputMode.ExternalBrain;
        }
        public void ResetDemo()
        {
            elapsed=0; nextSample=0; SetShapeImmediate(125,0.2f,0.7f,0);
            targetAttention=0.5f; targetRelaxation=0.65f; inputMode=PetalInputMode.SimulatedBrain;
        }
        void Update()
        {
            using var profile=UpdateMarker.Auto();
            long started=System.Diagnostics.Stopwatch.GetTimestamp(),allocated=System.GC.GetAllocatedBytesForCurrentThread();
            float dt=Time.deltaTime; elapsed+=dt;
            if(inputMode==PetalInputMode.SimulatedBrain&&elapsed>=nextSample)
            {
                nextSample=elapsed+1;
                AcceptBrain(0.5f+0.40f*Mathf.Sin(elapsed*0.19f),0.65f+0.3f*Mathf.Sin(elapsed*0.13f));
            }
            if(!frozen)
            {
                float blend=1-Mathf.Exp(-dt/2);
                Attention=Mathf.Lerp(Attention,targetAttention,blend); Relaxation=Mathf.Lerp(Relaxation,targetRelaxation,blend);
                OpeningAngle=Mathf.Lerp(OpeningAngle,Mathf.Clamp(targetOpeningAngle,90,150),blend);
                CurlAmount=Mathf.Lerp(CurlAmount,Mathf.Clamp01(targetCurlAmount),blend);
                PleatSoftness=Mathf.Lerp(PleatSoftness,Mathf.Clamp01(targetPleatSoftness),blend);
                OpeningProgress=Mathf.Min(1,OpeningProgress+dt/12);
                Rebuild();
            }
            properties.SetFloat(BreathId,Mathf.Sin(elapsed*0.65f));
            properties.SetFloat(FineCountId,Mathf.Clamp(finePleatCount,20,40));
            properties.SetFloat(GrayId,grayPreview?1:0); meshRenderer.SetPropertyBlock(properties);
            LastUpdateAllocation=System.GC.GetAllocatedBytesForCurrentThread()-allocated;
            LastUpdateMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }

        void Rebuild()
        {
            float curl=Mathf.Lerp(1,CurlAmount,Mathf.SmoothStep(0,1,OpeningProgress));
            float amplitude=Mathf.Lerp(0.145f,0.075f,PleatSoftness);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            amplitude*=diagnosticPleatScale;
#else
            amplitude*=0.8f;
#endif
            for(int x=0;x<Cols;x++)
            {
                // OpeningAngle describes the resting fan. Curl gathers its ribs while
                // the two 3D warps roll the complete sheet; no vertices are clipped.
                float u=x/(float)WidthSegments,s=u*2-1,theta=s*OpeningAngle*Mathf.Deg2Rad*0.5f*(1-0.45f*curl);
                sinTheta[x]=Mathf.Sin(theta); cosTheta[x]=Mathf.Cos(theta);
                rimRadius[x]=1+0.042f*Mathf.Sin(s*6.8f+0.6f)+0.018f*Mathf.Sin(s*16.6f-0.9f)+0.007f*Mathf.Cos(s*32.7f);
                float phase=2*Mathf.PI*(u*7+0.075f*Mathf.Sin(u*8.2f+0.4f));
                widePleat[x]=Mathf.Cos(phase)+Mathf.Lerp(0.16f,0.025f,PleatSoftness)*Mathf.Cos(phase*2+0.3f);
                rimWave[x]=0.085f*Mathf.Sin(s*9.3f+0.2f)+0.026f*Mathf.Sin(s*22.3f);
                leftFold[x]=Mathf.Exp(-Mathf.Pow((s+0.72f)/0.29f,2));
                rightFold[x]=Mathf.Exp(-Mathf.Pow((s-0.80f)/0.23f,2));
            }
            // Compose two cylindrical coordinate warps. Each remains invertible in the
            // supported range; additive competing bends can pinch the parameter surface.
            float crossCurvature=0.04f+0.24f*curl,radialCurvature=0.06f+0.66f*curl;
            for(int y=0;y<Rows;y++)
            {
                float t=y/(float)LengthSegments,t2=t*t,t6=t2*t2*t2;
                float pleatEnvelope=t2,foldEnvelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.55f,1,t));
                float radius=0.025f+2.55f*t;
                for(int x=0;x<Cols;x++)
                {
                    float s=x/(float)WidthSegments*2-1,rimScale=1+(rimRadius[x]-1)*Mathf.SmoothStep(0,1,t);
                    float rho=radius*rimScale;
                    float flare=0.72f+0.28f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.05f,0.8f,t));
                    float flatX=rho*sinTheta[x]*flare;
                    flatX+=0.075f*t2*Mathf.Sin(t*4.2f+0.2f);
                    float flatY=rho*cosTheta[x];
                    float height=amplitude*pleatEnvelope*widePleat[x];
                    height+=0.11f*s*pleatEnvelope+0.055f*t2*Mathf.Sin(s*4.7f+0.4f)+t6*rimWave[x];
                    float localAngle=foldEnvelope*(0.72f*leftFold[x]*(0.4f+0.6f*curl)-0.90f*rightFold[x]*(0.45f+0.55f*curl));
                    float tail=2.55f*Mathf.Max(0,t-0.55f);
                    height+=tail*Mathf.Sin(localAngle);
                    float radialRadius=1/radialCurvature+height;
                    float py=-1.10f+Mathf.Sin(radialCurvature*flatY)*radialRadius;
                    float radialZ=Mathf.Cos(radialCurvature*flatY)*radialRadius-1/radialCurvature;
                    float crossRadius=1/crossCurvature+radialZ;
                    float px=Mathf.Sin(crossCurvature*flatX)*crossRadius;
                    float pz=Mathf.Cos(crossCurvature*flatX)*crossRadius-1/crossCurvature;
                    surface[y*Cols+x]=new Vector3(px,py,pz);
                }
            }
            Vector3 min=Vector3.one*float.MaxValue,max=-min;
            for(int y=0;y<Rows;y++) for(int x=0;x<Cols;x++)
            {
                int i=y*Cols+x;
                Vector3 across=surface[y*Cols+Mathf.Min(x+1,Cols-1)]-surface[y*Cols+Mathf.Max(x-1,0)];
                Vector3 along=surface[Mathf.Min(y+1,Rows-1)*Cols+x]-surface[Mathf.Max(y-1,0)*Cols+x];
                Vector3 tangent=across.normalized,n=Vector3.Cross(along.normalized,tangent).normalized;
                float t=y/(float)LengthSegments,halfThickness=Mathf.Lerp(0.0012f,0.0007f,Mathf.Sqrt(t));
                vertices[i]=surface[i]+n*halfThickness; vertices[i+SurfaceCount]=surface[i]-n*halfThickness;
                normals[i]=n; normals[i+SurfaceCount]=-n;
                tangents[i]=new Vector4(tangent.x,tangent.y,tangent.z,-1); tangents[i+SurfaceCount]=new Vector4(tangent.x,tangent.y,tangent.z,1);
                min=Vector3.Min(min,Vector3.Min(vertices[i],vertices[i+SurfaceCount]));
                max=Vector3.Max(max,Vector3.Max(vertices[i],vertices[i+SurfaceCount]));
            }
            for(int i=0;i<boundary.Length;i++)
            {
                int source=boundary[i],a=SurfaceCount*2+i*2;
                Vector3 edge=(surface[boundary[(i+1)%boundary.Length]]-surface[boundary[(i+boundary.Length-1)%boundary.Length]]).normalized;
                Vector3 n=Vector3.Cross(edge,-normals[source]).normalized;
                vertices[a]=vertices[source]; vertices[a+1]=vertices[source+SurfaceCount];
                normals[a]=normals[a+1]=n; tangents[a]=tangents[a+1]=new Vector4(edge.x,edge.y,edge.z,1);
                uv[a]=uv[a+1]=uv[source];
            }
            mesh.vertices=vertices; mesh.normals=normals; mesh.tangents=tangents; mesh.uv=uv;
            mesh.bounds=new Bounds((min+max)*0.5f,max-min+Vector3.one*0.015f);
        }
        void OnDestroy() { if(mesh!=null) Destroy(mesh); }
    }
}
