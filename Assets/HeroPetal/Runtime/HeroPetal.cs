using UnityEngine;

namespace MindState
{
    public enum PetalInputMode { SimulatedBrain, ExternalBrain, DirectShape }

    [System.Serializable]
    public struct PetalSurfaceProfile
    {
        [Range(1,7)] public int widePleatCount;
        [Range(0,1)] public float surfaceDetail,rimVariation;
        public bool softFolds;
        [Range(0,90)] public float closureBendDegrees,closureSideLeftDegrees,closureSideRightDegrees;
        [Range(0,1)] public float foldStrength,edgeStrength;
        [Range(0,1)] public float cup,ruffle,tipRoll;
        [Range(0,1)] public float waveNestEdge,waveNestTip;
        [Range(0,1)] public float edgeTuck;
        [Range(0,1)] public float shingleLift;
        [Range(0,1)] public float rightCupRelease;
        [Range(0,1)] public float budNarrow;
        [Range(0,1)] public float budSpanNarrow;
        [Range(-1,1)] public float budRightOut;
        [Range(0,1)] public float tipRound;
        public static PetalSurfaceProfile Legacy=>new PetalSurfaceProfile { widePleatCount=7,surfaceDetail=1,rimVariation=1 };
        public PetalSurfaceProfile Constrained()=>new PetalSurfaceProfile {
            widePleatCount=Mathf.Clamp(widePleatCount,1,7), softFolds=softFolds,
            closureBendDegrees=float.IsNaN(closureBendDegrees)||float.IsInfinity(closureBendDegrees)?0:Mathf.Clamp(closureBendDegrees,0,90),
            closureSideLeftDegrees=Angle(closureSideLeftDegrees),closureSideRightDegrees=Angle(closureSideRightDegrees),
            foldStrength=Clean(foldStrength),edgeStrength=Clean(edgeStrength),
            cup=Clean(cup),ruffle=Clean(ruffle),tipRoll=Clean(tipRoll),
            waveNestEdge=Clean(waveNestEdge),waveNestTip=Clean(waveNestTip),
            edgeTuck=Clean(edgeTuck),shingleLift=Clean(shingleLift),rightCupRelease=Clean(rightCupRelease),budNarrow=Clean(budNarrow),budSpanNarrow=Clean(budSpanNarrow),budRightOut=CleanSigned(budRightOut),tipRound=Clean(tipRound),
            surfaceDetail=float.IsNaN(surfaceDetail)||float.IsInfinity(surfaceDetail)?1:Mathf.Clamp01(surfaceDetail),
            rimVariation=float.IsNaN(rimVariation)||float.IsInfinity(rimVariation)?1:Mathf.Clamp01(rimVariation) };
        static float Angle(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp(value,0,90);
        static float Clean(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);
        static float CleanSigned(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp(value,-1,1);
    }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD || FLOWER_VALIDATION
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
        readonly float[] sinTheta=new float[Cols],cosTheta=new float[Cols],thetaColumn=new float[Cols],rimRadius=new float[Cols],widePleat=new float[Cols],rimWave=new float[Cols],leftFold=new float[Cols],rightFold=new float[Cols],nestEdge=new float[Cols];
        readonly float[] foldCenter=new float[7],foldWidth=new float[7],foldPower=new float[7],foldBend=new float[7];
        float edgePhase;
        readonly float[] sectionRest=new float[Cols],sectionX=new float[Cols],sectionZ=new float[Cols];
        readonly float[] closureY=new float[257],closureZ=new float[257];
        int preparedSeed=int.MinValue,preparedCount;
        float elapsed,nextSample;
        float trajectoryCurlLimit=1,trajectoryFoldScale=1;
        float waveRoom=1f;
        PetalSurfaceProfile surfaceProfile=PetalSurfaceProfile.Legacy;
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
            trajectoryCurlLimit=1;
            trajectoryFoldScale=1;
            surfaceProfile=PetalSurfaceProfile.Legacy;
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
            trajectoryCurlLimit=1;
            trajectoryFoldScale=1;
            surfaceProfile=PetalSurfaceProfile.Legacy;
            if(Finite(a)) targetAttention=Mathf.Clamp01(a);
            if(Finite(r)) targetRelaxation=Mathf.Clamp01(r);
            targetOpeningAngle=90+60*targetRelaxation;
            targetCurlAmount=0.85f-0.75f*targetRelaxation;
            targetPleatSoftness=0.9f-0.65f*targetAttention;
        }
        public void RestartOpening() { OpeningProgress=0; frozen=false; }
        public void RestartGrowth()=>RestartOpening();
        public void SetShapeImmediate(float angle,float curl,float softness,float progress=1)
        { SetShapeImmediateLimited(angle,curl,softness,progress,1); }

        // Flower pose-cache sampling only. The existing single-petal API always uses limit=1.
        public void SetShapeImmediateLimited(float angle,float curl,float softness,float progress,float maximumCurl,float edgeFoldScale=1)
        { SetShapeImmediateLimited(angle,curl,softness,progress,maximumCurl,edgeFoldScale,PetalSurfaceProfile.Legacy); }

        public void SetShapeImmediateLimited(float angle,float curl,float softness,float progress,float maximumCurl,float edgeFoldScale,PetalSurfaceProfile profile)
        { SetShapeImmediateLimited(angle,curl,softness,progress,maximumCurl,edgeFoldScale,profile,0); }

        public void SetShapeImmediateLimited(float angle,float curl,float softness,float progress,float maximumCurl,float edgeFoldScale,PetalSurfaceProfile profile,int foldSeed,float waveRoom=1f)
        {
            SetShapeParameters(angle,curl,softness);
            surfaceProfile=profile.Constrained();
            if(surfaceProfile.softFolds) PrepareSoftFolds(foldSeed);
            trajectoryCurlLimit=Finite(maximumCurl)?Mathf.Clamp01(maximumCurl):1;
            trajectoryFoldScale=Finite(edgeFoldScale)?Mathf.Clamp01(edgeFoldScale):1;
            this.waveRoom=Finite(waveRoom)?Mathf.Clamp01(waveRoom):1f;
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

        void PrepareSoftFolds(int seed)
        {
            int count=surfaceProfile.widePleatCount;
            if(preparedSeed==seed&&preparedCount==count) return;
            preparedSeed=seed; preparedCount=count;
            var random=new System.Random(seed);
            edgePhase=(float)random.NextDouble()*Mathf.PI*2;
            for(int i=0;i<count;i++)
            {
                foldCenter[i]=.16f+.68f*(i+.5f+((float)random.NextDouble()-.5f)*.42f)/count;
                foldWidth[i]=(.29f/count+.035f)*Mathf.Lerp(.85f,1.2f,(float)random.NextDouble());
                foldPower[i]=Mathf.Lerp(.65f,1,(float)random.NextDouble());
                foldBend[i]=Mathf.Lerp(-.065f,.065f,(float)random.NextDouble());
            }
        }
        float SoftFold(float u,float t)
        {
            float value=0;
            for(int i=0;i<surfaceProfile.widePleatCount;i++)
            {
                float center=.5f+(foldCenter[i]-.5f)*Mathf.Lerp(.65f,1,t)+foldBend[i]*Mathf.Sin(t*Mathf.PI+i*.7f);
                float q=(u-center)/(foldWidth[i]*Mathf.Lerp(.8f,1.2f,t));
                value+=foldPower[i]*(Mathf.Exp(-q*q)-.18f*Mathf.Exp(-q*q*.22f));
            }
            // Smoothly bound combined valleys to one quarter of the primary peak.
            return value<0?-.25f*(1-Mathf.Exp(value/.25f)):value;
        }

        static float SideAngle(float q,float left,float right)=>q*Mathf.Lerp(left,right,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.35f,.35f,q)));
        void PrepareSideSection(float t,float radius,float left,float right,float tipAngleScale,float spanScale,float tipPull)
        {
            float flare=.72f+.28f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.8f,t));
            for(int x=0;x<Cols;x++)
            {
                float sinSection=sinTheta[x];
                if(tipAngleScale!=1f||spanScale!=1f) sinSection=Mathf.Sin(thetaColumn[x]*tipAngleScale*spanScale);
                if(tipPull!=0)
                {
                    float s=x/(float)WidthSegments*2-1;
                    sinSection*=1-tipPull*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1f,Mathf.Abs(s)));
                }
                sectionRest[x]=radius*(1+(rimRadius[x]-1)*Mathf.SmoothStep(0,1,t))*sinSection*flare;
            }
            int middle=WidthSegments/2; sectionX[middle]=sectionRest[middle]; sectionZ[middle]=0;
            float fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.8f,t));
            for(int direction=-1;direction<=1;direction+=2) for(int x=middle+direction;x>=0&&x<Cols;x+=direction)
            {
                int previous=x-direction; float dx=sectionRest[x]-sectionRest[previous];
                float q=(x+previous)/(float)WidthSegments-1;
                float angle=SideAngle(q,left*fade,right*fade);
                sectionX[x]=sectionX[previous]+dx*Mathf.Cos(angle);
                sectionZ[x]=sectionZ[previous]-dx*Mathf.Sin(angle);
            }
        }
        static float ClosureAngle(float distance,float bend)=>bend*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2575f,2.575f,distance));
        void Rebuild()
        {
            float curl=Mathf.Lerp(trajectoryCurlLimit,Mathf.Min(CurlAmount,trajectoryCurlLimit),Mathf.SmoothStep(0,1,OpeningProgress));
            float openEnvelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,1f,OpeningProgress));
            float waveOpen=Mathf.SmoothStep(0,1,waveRoom);
            float waveAmount=Mathf.Lerp(.10f,1f,waveOpen);
            float tuckAmount=Mathf.Lerp(.12f,1f,waveOpen);
            float ruffleFactor=surfaceProfile.softFolds?Mathf.Lerp(.08f,1f,waveOpen):.15f+.85f*openEnvelope;
            float ruffleAmount=surfaceProfile.ruffle*ruffleFactor;
            float amplitude=Mathf.Lerp(0.145f,0.075f,PleatSoftness);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || FLOWER_VALIDATION
            amplitude*=diagnosticPleatScale;
#else
            amplitude*=0.8f;
#endif
            for(int x=0;x<Cols;x++)
            {
                // OpeningAngle describes the resting fan. Curl gathers its ribs while
                // the two 3D warps roll the complete sheet; no vertices are clipped.
                float u=x/(float)WidthSegments,s=u*2-1,theta=s*OpeningAngle*Mathf.Deg2Rad*0.5f*(1-0.45f*curl);
                thetaColumn[x]=theta;
                sinTheta[x]=Mathf.Sin(theta); cosTheta[x]=Mathf.Cos(theta);
                rimRadius[x]=1+0.042f*Mathf.Sin(s*6.8f+0.6f)+0.018f*Mathf.Sin(s*16.6f-0.9f)+0.007f*Mathf.Cos(s*32.7f);
                if(surfaceProfile.rimVariation!=1) rimRadius[x]=1+(rimRadius[x]-1)*surfaceProfile.rimVariation;
                float phase=2*Mathf.PI*(u*surfaceProfile.widePleatCount+0.075f*Mathf.Sin(u*8.2f+0.4f));
                widePleat[x]=Mathf.Cos(phase)+Mathf.Lerp(0.16f,0.025f,PleatSoftness)*Mathf.Cos(phase*2+0.3f);
                rimWave[x]=0.085f*Mathf.Sin(s*9.3f+0.2f)+0.026f*Mathf.Sin(s*22.3f);
                if(surfaceProfile.softFolds)
                {
                    float edgeWave=.55f+.45f*waveAmount;
                    rimRadius[x]=1+surfaceProfile.edgeStrength*edgeWave*(.042f*Mathf.Sin(s*6.1f+.6f+edgePhase*.18f)+.014f*Mathf.Sin(s*11.7f-.9f-edgePhase*.25f));
                    rimWave[x]=.09f*Mathf.Sin(s*6.7f+.2f+edgePhase*.2f)+.025f*Mathf.Sin(s*11.1f-edgePhase*.3f);
                    nestEdge[x]=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.95f,Mathf.Abs(s)));
                }
                if(surfaceProfile.ruffle>0)
                {
                    rimRadius[x]*=1f+ruffleAmount*(0.055f*Mathf.Sin(s*8.3f+0.9f)+0.022f*Mathf.Sin(s*15.1f-0.4f));
                    rimRadius[x]*=1f-0.10f*ruffleAmount*Mathf.Pow(Mathf.Abs(s),3f);
                    rimWave[x]+=ruffleAmount*(0.045f*Mathf.Sin(s*6.9f+1.2f)+0.018f*Mathf.Sin(s*12.7f-0.6f));
                }
                leftFold[x]=Mathf.Exp(-Mathf.Pow((s+0.72f)/0.29f,2));
                rightFold[x]=Mathf.Exp(-Mathf.Pow((s-0.80f)/0.23f,2));
            }
            // Compose two cylindrical coordinate warps. Each remains invertible in the
            // supported range; additive competing bends can pinch the parameter surface.
            float crossCurvature=0.04f+0.10f*surfaceProfile.cup+(0.24f+0.22f*surfaceProfile.cup)*curl,radialCurvature=0.06f+0.66f*curl;
            float closing=1-OpeningProgress;
            float leftSide=surfaceProfile.closureSideLeftDegrees*Mathf.Deg2Rad*Mathf.SmoothStep(0,1,closing);
            float rightSide=surfaceProfile.closureSideRightDegrees*Mathf.Deg2Rad*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,1,closing));
            bool sideActive=leftSide>0||rightSide>0;
            float closureRelease=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0f,.7f,OpeningProgress));
            float closure=surfaceProfile.closureBendDegrees*Mathf.Deg2Rad*closureRelease;
            float nestFade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,1f,OpeningProgress));
            float budRightOutGate=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0f,.5f,OpeningProgress));
            // A bud is a young, not-yet-grown petal: narrow the sheet while closed and let
            // it widen out as it opens, so side corners clear their neighbours.
            // Linear recovery with the knot on a cached pose (28/32) keeps midpoint error low.
            float budGrow=Mathf.InverseLerp(0f,.875f,OpeningProgress);
            float budWidth=1-surfaceProfile.budNarrow*(1-budGrow);
            // Narrowing the chord alone leaves the sheet flatter and does not separate the
            // overlapping edges. Pulling the fan angle in during closure narrows the sheet
            // while keeping its cup, and is fully released exactly at full bloom.
            float spanNarrow=surfaceProfile.budSpanNarrow*(1-Mathf.InverseLerp(0f,1f,OpeningProgress));
            if(closure>0)
            {
                closureY[0]=closureZ[0]=0;
                for(int i=1;i<257;i++)
                {
                    const float step=3f/256; float y=(i-.5f)*step;
                    float a=radialCurvature*y,b=a+ClosureAngle(y,closure);
                    closureY[i]=closureY[i-1]+(Mathf.Cos(b)-Mathf.Cos(a))*step;
                    closureZ[i]=closureZ[i-1]+(Mathf.Sin(a)-Mathf.Sin(b))*step;
                }
            }
            for(int y=0;y<Rows;y++)
            {
                float t=y/(float)LengthSegments,t2=t*t,t6=t2*t2*t2;
                float nestTip=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.9f,t));
                // Flatten the transverse cup toward the tip so the sheet lies over its
                // neighbour instead of curling its edge down through it.
                float crossRow=crossCurvature*(1-surfaceProfile.tipRoll*.7f*nestTip);
                float pleatEnvelope=t2,foldEnvelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.55f,1,t));
                float radius=0.025f+2.55f*t;
                // Narrow the fan gradually from mid-length so the 90-degree tip corners
                // become curved shoulders. Centreline (s=0) is untouched, so length holds;
                // a slow ramp keeps lateral positions monotonic so the sheet cannot fold.
                float tipU=Mathf.Clamp01((t-.5f)/.5f),tipRampValue=Mathf.SmoothStep(0,1,tipU);
                float tipAngleScale=1-surfaceProfile.tipRound*.40f*tipRampValue;
                float tipPull=surfaceProfile.tipRound*.05f*tipRampValue;
                float spanScale=1-spanNarrow*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.85f,t));
                if(sideActive) PrepareSideSection(t,radius,leftSide,rightSide,tipAngleScale,spanScale,tipPull);
                for(int x=0;x<Cols;x++)
                {
                    float s=x/(float)WidthSegments*2-1;
                    float tipRimSmooth=Mathf.Lerp(1f,Mathf.Lerp(.25f,1f,waveOpen),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.9f,t)));
                    float rimScale=1+(rimRadius[x]-1)*Mathf.SmoothStep(0,1,t)*tipRimSmooth;
                    float rho=radius*rimScale;
                    // Tip corners recede toward the root and centreline so the neighbouring sheet covers them.
                    float edgeTuckBand=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.8f,Mathf.Abs(s)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.85f,t))*nestFade*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.5f,OpeningProgress));
                    float edgeInset=surfaceProfile.edgeTuck*.45f*edgeTuckBand;
                    rho*=1-edgeInset*.1f;
                    rho*=1-tipPull*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1f,Mathf.Abs(s)));
                    float sinTip=sinTheta[x],cosTip=cosTheta[x];
                    if(tipAngleScale!=1f||spanScale!=1f) { float th=thetaColumn[x]*tipAngleScale*spanScale; sinTip=Mathf.Sin(th); cosTip=Mathf.Cos(th); }
                    float flare=0.72f+0.28f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.05f,0.8f,t));
                    float flatX=rho*sinTip*flare;
                    flatX+=0.075f*t2*Mathf.Sin(t*4.2f+0.2f)*surfaceProfile.surfaceDetail;
                    float flatY=rho*cosTip;
                    float height=amplitude*pleatEnvelope*widePleat[x];
                    height+=0.11f*s*pleatEnvelope+0.055f*t2*Mathf.Sin(s*4.7f+0.4f)+t6*rimWave[x]*surfaceProfile.rimVariation;
                    height*=surfaceProfile.surfaceDetail;
                    float localAngle=foldEnvelope*(0.72f*leftFold[x]*(0.4f+0.6f*curl)-0.90f*rightFold[x]*(0.45f+0.55f*curl));
                    localAngle*=trajectoryFoldScale;
                    float tail=2.55f*Mathf.Max(0,t-0.55f);
                    if(!surfaceProfile.softFolds) height+=tail*Mathf.Sin(localAngle);
                    if(surfaceProfile.softFolds)
                    {
                        float u=(s+1)*.5f;
                        float envelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.5f,t))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,t)));
                        float sides=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.16f,u))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.84f,1,u)));
                        // The inner third of every petal is stacked against its neighbours at all
                        // bloom levels, so wave height there is faded out and only the macro cup remains.
                        float innerFade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.24f,.55f,t));
                        float tipCalm=Mathf.Lerp(.15f,1f,waveOpen);
                        float tipSmooth=Mathf.Lerp(1f,tipCalm,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.85f,t)));
                        float nest=Mathf.Clamp01(surfaceProfile.waveNestEdge*nestEdge[x]+surfaceProfile.waveNestTip*nestTip);
                        float nestGate=1-nest*nestFade;
                        // Macro drape, medium folds and the independent rim have separate scales.
                        float drape=.35f+.65f*surfaceProfile.surfaceDetail;
                        flatX=rho*sinTip*flare+.06f*t2*Mathf.Sin(t*4.2f+.2f+edgePhase*.08f)*drape;
                        // Pull the covered side edges toward the centreline so neighbouring
                        // sheets meet over them instead of crossing through them.
                        flatX*=1-surfaceProfile.edgeTuck*.22f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.9f,Mathf.Abs(s)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.95f,t));
                        height=(.095f*s*t2+.075f*t2*Mathf.Sin(s*3.4f+.4f+edgePhase*.25f)*innerFade*tipSmooth*nestGate)*drape;
                        height+=amplitude*surfaceProfile.foldStrength*envelope*innerFade*sides*SoftFold(u,t)*(.55f+.45f*waveAmount)*tipSmooth*nestGate;
                        height+=t6*surfaceProfile.edgeStrength*rimWave[x]*(.35f+.65f*waveAmount)*tipSmooth*nestGate;
                        float symmetricFold=Mathf.Exp(-Mathf.Pow((s+0.76f)/0.26f,2))-Mathf.Exp(-Mathf.Pow((s-0.76f)/0.26f,2));
                        float symmetricAngle=foldEnvelope*0.78f*symmetricFold*(0.42f+0.58f*curl)*trajectoryFoldScale;
                        height+=tail*Mathf.Sin(symmetricAngle*.85f)*tuckAmount*tipSmooth*nestGate;
                        // At full bloom the sheet faces up, so lowering its right band lifts
                        // that edge visually. In the bud the sheet stands upright and the
                        // transverse cup curls the edge inside the neighbour, so the bud
                        // stage releases that cup on the right half instead.
                        float bloomGate=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.70f,OpeningProgress));
                        float shingleBand=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.30f,.92f,s))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.80f,t));
                        height-=surfaceProfile.shingleLift*shingleBand*.14f*bloomGate;
                        height-=edgeInset;
                    }
                    // Bud only: lift the sheet outward from the centreline to the right rim
                    // so the next petal's left rim stays outside instead of crossing through.
                    float budRightOutOffset=0;
                    if(surfaceProfile.budRightOut!=0f&&budRightOutGate>0f)
                    {
                        float budRightBand=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.9f,.1f,s));
                        float budRightTip=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.45f,t));
                        budRightOutOffset=surfaceProfile.budRightOut*.40f*budRightBand*budRightTip*budRightOutGate;
                    }
                    if(sideActive) flatX+=sectionX[x]-sectionRest[x];
                    // Keep the fixed root tenth untouched; only the free sheet narrows.
                    float budWidthGrow=Mathf.Lerp(1f,budWidth,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.85f,t)));
                    flatX*=budWidthGrow;
                    float radialRadius=1/radialCurvature+height;
                    float py=-1.10f+Mathf.Sin(radialCurvature*flatY)*radialRadius;
                    float radialZ=Mathf.Cos(radialCurvature*flatY)*radialRadius-1/radialCurvature;
                    if(closure>0)
                    {
                        float f=Mathf.Clamp(flatY/3*256,0,255.999f); int a=(int)f;
                        float angle=radialCurvature*flatY+ClosureAngle(flatY,closure);
                        py=-1.10f+Mathf.Sin(radialCurvature*flatY)/radialCurvature+Mathf.Lerp(closureY[a],closureY[a+1],f-a)+height*Mathf.Sin(angle);
                        radialZ=(Mathf.Cos(radialCurvature*flatY)-1)/radialCurvature+Mathf.Lerp(closureZ[a],closureZ[a+1],f-a)+height*Mathf.Cos(angle);
                    }
                    if(sideActive)
                    {
                        float longitudinalAngle=radialCurvature*flatY+ClosureAngle(flatY,closure);
                        py+=sectionZ[x]*Mathf.Sin(longitudinalAngle);
                        radialZ+=sectionZ[x]*Mathf.Cos(longitudinalAngle);
                    }
                    // Every petal is allowed to be asymmetric: keep the left half cupped so
                    // the right neighbour tucks under it, and flatten the right half so this
                    // petal's right edge stays outside the neighbour instead of curling below.
                    float rowCup=crossRow;
                    if(surfaceProfile.softFolds&&surfaceProfile.rightCupRelease>0)
                    {
                        float rightBand=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.10f,.70f,s));
                        float tipBand=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.45f,t));
                        rowCup*=1-.8f*Mathf.Clamp01(surfaceProfile.rightCupRelease*rightBand*tipBand);
                    }
                    float crossRadius=1/rowCup+radialZ;
                    float px=Mathf.Sin(rowCup*flatX)*crossRadius;
                    float pz=Mathf.Cos(rowCup*flatX)*crossRadius-1/rowCup+budRightOutOffset;
                    surface[y*Cols+x]=new Vector3(px,py,pz);
                }
            }
            FoldOverlapEdges();
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
        // The cache sampler supplies the horizontal radial direction in petal space.
        internal Matrix4x4 overlapToFlower=Matrix4x4.identity,overlapFromFlower=Matrix4x4.identity;
        void FoldOverlapEdges()
        {
            if(!surfaceProfile.softFolds||surfaceProfile.budRightOut<=0) return;
            float closing=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.8f,OpeningProgress));
            if(closing<=0) return;
            // Release only the over-curled overlap band. Its tip and lower hinge
            // return to the original sheet, retaining the closed bud silhouette.
            const int band=WidthSegments/5;
            for(int y=LengthSegments/2+1;y<Rows;y++)
            {
                float t=y/(float)LengthSegments;
                float along=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.75f,t))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,1f,t)))*closing;
                for(int side=0;side<2;side++)
                {
                    int hinge=side==0?band:WidthSegments-band;
                    float leftRelease=.25f+.25f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.4f,OpeningProgress));
                    float amount=surfaceProfile.budRightOut*(side==0?leftRelease:.04f)*along;
                    for(int k=1;k<=band;k++)
                    {
                        int x=hinge+(side==0?-k:k);
                        float edge=Mathf.SmoothStep(0,1,Mathf.Clamp01(k/(band*.65f)));
                        int index=y*Cols+x;
                        Vector3 radial=overlapToFlower.MultiplyPoint3x4(surface[index]-surface[WidthSegments/2]);
                        radial.y=0;
                        surface[index]+=overlapFromFlower.MultiplyVector(radial.normalized)*(amount*edge);
                    }
                }
            }
        }
        void OnDestroy() { if(mesh!=null) Destroy(mesh); }
    }
}
