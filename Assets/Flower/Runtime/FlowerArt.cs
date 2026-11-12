using System;
using UnityEngine;
namespace MindState
{
    public enum FlowerArtState { Clarity, Gathering, Wandering, Quiet }
    public enum FlowerArtInput { Preview, External }
    [Serializable] public sealed class FlowerArtProfile
    {
        public float amplitude,period,openness;
        public FlowerArtProfile(float a,float p,float o=1f) { amplitude=a; period=p; openness=o; }
    }
    [Serializable] public struct FlowerArtLook
    {
        public Color accent;
        public float huePull,brightness,saturation,gloss,rim,selfGlow,exposure,lightGain,lightDrift,droop,asym,yawSpeed;
        public FlowerArtLook(Color a,float hue,float bright,float sat,float gl,float r,float sg,float ex,float lg,float ld,float dr,float roll,float yaw)
        { accent=a; huePull=hue; brightness=bright; saturation=sat; gloss=gl; rim=r; selfGlow=sg; exposure=ex; lightGain=lg; lightDrift=ld; droop=dr; asym=roll; yawSpeed=yaw; }
    }
    public sealed partial class FlowerAssembler
    {
        public FlowerArtProfile[] artProfiles={
            new FlowerArtProfile(.35f,12,.88f),
            new FlowerArtProfile(0,5,0f), // Gathering uses the original cached path.
            new FlowerArtProfile(3.2f,9,1f),
            new FlowerArtProfile(1.4f,14,.64f)};
        public FlowerArtLook[] artLooks;
        public FlowerArtInput ArtInput { get; private set; }
        public Vector4 ArtWeights { get; private set; }=new Vector4(1,0,0,0);
        public Vector4 ArtLightWeights { get; private set; }=new Vector4(1,0,0,0);
        public Vector4 RawBrainWeights { get; private set; }=new Vector4(1,0,0,0);
        public Vector4 DisplayWeights=>artTarget;
        public float BaselineAttention=>medianAttention;
        public float BaselineRelaxation=>medianRelaxation;
        public bool BrainBaselineFrozen { get; set; }
        public float ArtExposureOffset { get; private set; }
        public float ArtSaturation { get; private set; }=1;
        public Color ArtAccentColor { get; private set; }=Color.white;
        public float ArtLightGain { get; private set; }=1;
        public float ArtLightDrift { get; private set; }=1;
        public bool ArtActive=>artActive;
        public float ArtTime=>artTime;
        public string ArtStatus=>!artActive?"开合预览":ArtInput==FlowerArtInput.Preview?"单态预览":Time.realtimeSinceStartup-lastValid>5?"正在重新感知":"外部脑电";
        bool artActive;
#if UNITY_EDITOR || FLOWER_VALIDATION
        internal bool suppressArtMotion;
#endif
        float artTime,artYaw,lastValid=float.NegativeInfinity;
        Vector4 artTarget=new Vector4(1,0,0,0);
        Vector4 artFrom=new Vector4(1,0,0,0),artDestination=new Vector4(1,0,0,0);
        float artTransitionTime=9;
        const float ArtTransitionDuration=9,ArtStreamSmoothing=3;
        // Display-side mapping: 180 s median baseline, 0.5 s sampling, light sharpening.
        const int BaselineCapacity=360;
        const float BaselineInterval=.5f,BaselineAbsoluteGain=2.2f,BaselineRelativeGain=.8f,BaselineDeadband=.015f;
        const float WeightSharpness=2.2f,TieThreshold=.03f,TieLean=.05f;
        readonly float[] baselineAttention=new float[BaselineCapacity];
        readonly float[] baselineRelaxation=new float[BaselineCapacity];
        readonly float[] baselineScratchA=new float[BaselineCapacity];
        readonly float[] baselineScratchR=new float[BaselineCapacity];
        int baselineCount,baselineHead,lastDominant;
        float lastBaselineSample=float.NegativeInfinity,medianAttention=.5f,medianRelaxation=.5f;
        // All modes share the standard opening path and global correction clock.
        float StandardLayerProgress(FlowerLayer c,float global)=>Mathf.Clamp01((global*BloomDuration-c.delay)/Mathf.Max(1,c.duration));
        static FlowerArtLook[] CreateDefaultLooks()=>new[]{
            new FlowerArtLook(new Color(.92f,.97f,1f),.08f,1f,1f,.05f,.26f,.06f,.04f,1.06f,.6f,0f,0f,3.5f),
            new FlowerArtLook(new Color(1f,.86f,.72f),.12f,1f,1f,.02f,.1f,.04f,.02f,.92f,.4f,0f,0f,0f),
            new FlowerArtLook(new Color(.86f,.8f,1f),.12f,.98f,.98f,0f,.14f,.05f,0f,1f,1.6f,0f,2f,0f),
            new FlowerArtLook(new Color(.78f,.75f,.82f),.16f,.86f,.78f,-.05f,.02f,.015f,-.08f,.82f,.5f,-6f,0f,0f)};
        public void EnsureArtLooks()
        {
            if(artLooks!=null&&artLooks.Length==4&&artLooks[0].lightGain>.01f&&artLooks[0].brightness>.01f) return;
            artLooks=CreateDefaultLooks();
        }
        [ContextMenu("Reset four-state looks")]
        void ResetArtLooks(){artLooks=CreateDefaultLooks();}
        FlowerArtLook BlendLook(Vector4 weights)
        {
            EnsureArtLooks();
            var look=new FlowerArtLook();
            look.accent=Color.black;
            for(int i=0;i<4;i++)
            {
                var s=artLooks[i];float w=weights[i];
                look.accent+=s.accent*w;
                look.huePull+=s.huePull*w;look.brightness+=s.brightness*w;look.saturation+=s.saturation*w;
                look.gloss+=s.gloss*w;look.rim+=s.rim*w;look.selfGlow+=s.selfGlow*w;look.exposure+=s.exposure*w;
                look.lightGain+=s.lightGain*w;look.lightDrift+=s.lightDrift*w;
                look.droop+=s.droop*w;look.asym+=s.asym*w;look.yawSpeed+=s.yawSpeed*w;
            }
            return look;
        }
        void AdvanceArtWeights(float dt)
        {
            if(ArtInput!=FlowerArtInput.Preview)
            {
                // Streaming targets must not restart a timed transition every sample.
                ArtWeights=Vector4.Lerp(ArtWeights,artTarget,1-Mathf.Exp(-dt/ArtStreamSmoothing));
                artDestination=ArtWeights; artTransitionTime=ArtTransitionDuration;
                return;
            }
            if(artDestination!=artTarget)
            { artFrom=ArtWeights; artDestination=artTarget; artTransitionTime=0; }
            artTransitionTime=Mathf.Min(ArtTransitionDuration,artTransitionTime+dt);
            ArtWeights=Vector4.Lerp(artFrom,artDestination,Mathf.SmoothStep(0,1,artTransitionTime/ArtTransitionDuration));
        }
        static readonly int ArtWeightsId=Shader.PropertyToID("_ArtWeights"),ArtClockId=Shader.PropertyToID("_ArtClock"),ArtPhaseId=Shader.PropertyToID("_ArtPhase");
        static bool ArtDiagnosticsRequested()
        { foreach(var a in Environment.GetCommandLineArgs()) if(a=="-verifyProject"||a=="-bakeFlower") return true; return false; }
        public void SetArtState(FlowerArtState state)
        {
            int i=(int)state; if(i<0||i>3) return;
            EnterArt(); ArtInput=FlowerArtInput.Preview; artTarget=Vector4.zero; artTarget[i]=1;
        }
        public void WaitForArtBrain() { EnterArt(); ArtInput=FlowerArtInput.External; lastValid=float.NegativeInfinity; }
        public void SetBrainMetrics(float attention01,float relaxation01,bool signalValid=true)
        {
            EnterArt(); ArtInput=FlowerArtInput.External;
            if(!signalValid||!Finite(attention01)||!Finite(relaxation01)) return;
            float a=Mathf.Clamp01(attention01),r=Mathf.Clamp01(relaxation01);
            RawBrainWeights=BrainArtWeights(a,r);
            PushBaseline(a,r);
            artTarget=MapDisplayWeights(a,r);
            lastValid=Time.realtimeSinceStartup;
        }
        public static Vector4 BrainArtWeights(float a,float r)
        { a=Mathf.Clamp01(a); r=Mathf.Clamp01(r); return new Vector4(a*r,a*(1-r),(1-a)*r,(1-a)*(1-r)); }
        public void ResetBrainBaseline()
        {
            baselineCount=baselineHead=lastDominant=0;
            lastBaselineSample=float.NegativeInfinity;
            medianAttention=medianRelaxation=.5f;
        }
        void PushBaseline(float a,float r)
        {
            if(BrainBaselineFrozen) return;
            float now=Time.realtimeSinceStartup;
            if(now-lastBaselineSample<BaselineInterval) return;
            lastBaselineSample=now;
            baselineAttention[baselineHead]=a;
            baselineRelaxation[baselineHead]=r;
            baselineHead=(baselineHead+1)%BaselineCapacity;
            if(baselineCount<BaselineCapacity) baselineCount++;
            if(baselineCount<10) return;
            Array.Copy(baselineAttention,baselineScratchA,baselineCount);
            Array.Copy(baselineRelaxation,baselineScratchR,baselineCount);
            Array.Sort(baselineScratchA,0,baselineCount);
            Array.Sort(baselineScratchR,0,baselineCount);
            medianAttention=baselineScratchA[baselineCount/2];
            medianRelaxation=baselineScratchR[baselineCount/2];
        }
        static float Deadband(float x)=>x>BaselineDeadband?x-BaselineDeadband:x<-BaselineDeadband?x+BaselineDeadband:0f;
        static Vector4 Sharpen(Vector4 w)
        {
            float x=Mathf.Pow(Mathf.Max(0,w.x),WeightSharpness),y=Mathf.Pow(Mathf.Max(0,w.y),WeightSharpness);
            float z=Mathf.Pow(Mathf.Max(0,w.z),WeightSharpness),u=Mathf.Pow(Mathf.Max(0,w.w),WeightSharpness);
            float sum=x+y+z+u;
            return sum>1e-6f?new Vector4(x/sum,y/sum,z/sum,u/sum):new Vector4(1,0,0,0);
        }
        Vector4 MapDisplayWeights(float a,float r)
        {
            float nA=.5f+.5f*(float)Math.Tanh(BaselineAbsoluteGain*(a-.5f)+BaselineRelativeGain*Deadband(a-medianAttention));
            float nR=.5f+.5f*(float)Math.Tanh(BaselineAbsoluteGain*(r-.5f)+BaselineRelativeGain*Deadband(r-medianRelaxation));
            var w=BrainArtWeights(nA,nR);
            float max=w.x,min=w.x;int dominant=0;
            for(int i=1;i<4;i++){max=Mathf.Max(max,w[i]);min=Mathf.Min(min,w[i]);if(w[i]>w[dominant])dominant=i;}
            w=Sharpen(w);
            if(max-min<TieThreshold)
            {
                var lean=Vector4.zero;lean[lastDominant]=1f;
                w=Vector4.Lerp(w,lean,TieLean);
            }
            float sum=w.x+w.y+w.z+w.w;
            w=sum>1e-6f?w/sum:new Vector4(1,0,0,0);
            lastDominant=dominant;
            return w;
        }
        void EnterArt()
        {
            if(artActive) return;
            autoPlay=false; bloomProgress=1; ApplyPose(); bottomProgress=-1; artActive=true;
        }
        void LeaveArt()
        {
            if(!artActive) return;
            artActive=false; bottomProgress=-1; generated.localRotation=Quaternion.identity; generated.localScale=Vector3.one;
            foreach(var l in layers) { l.progress=-1; foreach(var p in l.petals) {
                p.block.SetVector(ArtWeightsId,Vector4.zero); p.block.SetVector("_ArtLightWeights",Vector4.zero);
                p.block.SetVector("_StateTint",new Vector4(1,1,0,0)); p.block.SetVector("_StateAccent",(Vector4)Color.white.linear); p.block.SetVector("_StateSurface",Vector4.zero);
                p.renderer.SetPropertyBlock(p.block); } }
            ArtExposureOffset=0;ArtSaturation=1;ArtAccentColor=Color.white;ArtLightGain=ArtLightDrift=1;
            if(naturalLights!=null) UpdateNaturalLights();
        }
        public void ResetArt()
        { artTime=artYaw=0; ResetFlowPhase(); frozen=false; ArtWeights=ArtLightWeights=artFrom=artDestination=new Vector4(1,0,0,0); RawBrainWeights=new Vector4(1,0,0,0); artTransitionTime=ArtTransitionDuration; ArtExposureOffset=0;ArtSaturation=1;ArtAccentColor=Color.white;ArtLightGain=ArtLightDrift=1; ResetBrainBaseline(); SetArtState(FlowerArtState.Clarity); }
        public void SetArtPreviewImmediate(FlowerArtState state)
        { SetArtState(state); ArtWeights=ArtLightWeights=artTarget; artFrom=artDestination=artTarget; artTransitionTime=ArtTransitionDuration; artTime=artYaw=0; ResetFlowPhase(); UpdateArt(0); }
        float FlowSpeed(){var periods=appearance!=null?appearance.flowPeriods:new Vector4(12,10,9,18);float speed=0;for(int i=0;i<4;i++)speed+=ArtLightWeights[i]/Mathf.Max(1,periods[i]);return speed;}
        void UpdateArt(float dt)
        {
            if(!frozen)
            {
                artTime+=dt;
                AdvanceArtWeights(dt);
                ArtLightWeights=Vector4.Lerp(ArtLightWeights,artTarget,1-Mathf.Exp(-dt/ArtStreamSmoothing));
                artYaw=Mathf.Repeat(artYaw+BlendLook(ArtWeights).yawSpeed*dt,360);
                ArtFlowPhase=Mathf.Repeat(ArtFlowPhase+dt*FlowSpeed(),1);
            }
            var look=BlendLook(ArtWeights);
            var lightLook=BlendLook(ArtLightWeights);
            ArtExposureOffset=look.exposure;ArtSaturation=look.saturation;
            ArtLightGain=lightLook.lightGain;ArtLightDrift=lightLook.lightDrift;ArtAccentColor=lightLook.accent;
            UpdateNaturalLights();
            generated.localRotation=Quaternion.Euler(0,artYaw,0);
            generated.localScale=Vector3.one;
            float gathering=ArtWeights.y;
            float bud=.04f+.03f*(1-Mathf.Cos(artTime*Mathf.PI*2/5));
            float other=ArtWeights.x+ArtWeights.z+ArtWeights.w;
            float open=other>1e-6f?(ArtWeights.x*artProfiles[0].openness+ArtWeights.z*artProfiles[2].openness+ArtWeights.w*artProfiles[3].openness)/other:1f;
            // A shared layer phase drives geometry, transform and sparse correction sampling.
            bloomProgress=gathering>=1f?bud:Mathf.Lerp(open,bud,gathering);
            ApplyPose(bloomProgress,false);ApplyBottomPatches();
            Vector4 stateTint=new Vector4(look.brightness,look.saturation,0,0);
            Vector4 stateAccent=(Vector4)look.accent.linear;
            Vector4 stateSurface=new Vector4(look.huePull,look.rim,look.selfGlow,look.gloss);
            float quietDroop=look.droop;
            float wanderRoll=look.asym;
            for(int k=0;k<layers.Length;k++)
            {
                var l=layers[k];
                for(int j=0;j<l.petals.Length;j++)
                {
                    var p=l.petals[j]; float az=j*Mathf.PI*2/l.petals.Length,tilt=0,roll=0;
                    for(int i=0;i<4;i++)
                    {
                        if(i==1) continue;
                        var profile=artProfiles[i]; float period=profile.period;
                        if(i==2) period=6.5f+4*Mathf.Repeat((j+1)*.618034f+k*.37f,1);
                        float phase=i==2?az+k*.9f:i==3?-az-k*.5f:0;
                        float motion=profile.amplitude*Mathf.Sin(artTime*Mathf.PI*2/period+phase);
                        tilt+=ArtWeights[i]*motion;
                    }
                    // Quiet droops from the outer layer inward; Wandering adds a deterministic per-petal roll.
                    tilt+=quietDroop*(k==0?1.2f:.7f);
                    roll+=wanderRoll*Mathf.Sin((j+1)*1.7f+k*.9f);
                    PathPetalPose(l,p,j,StandardLayerProgress(l.config,bloomProgress),out var position,out var rotation,out var scale);
#if UNITY_EDITOR || FLOWER_VALIDATION
                    if(suppressArtMotion){ tilt=0; roll=0; }
#endif
                    rotation=rotation*Quaternion.Euler(tilt,0,roll);
                    ApplyPetalPose(l,p,j,position,rotation,scale);
                    if(appearance!=null) {
                        float v=Mathf.Repeat((j+1)*.618034f+k*.37f+appearance.seed*.013f,1);
                        if(!frozen) p.flowPhase=Mathf.Repeat(p.flowPhase+dt*FlowSpeed()/(1+(v*2-1)*appearance.periodVariation),1);
                        p.block.SetVector("_FlowSettings",new Vector4(p.flowPhase,appearance.bandWidth,v*appearance.phaseSpread,0));
                    }
                    p.block.SetVector("_StateTint",stateTint);p.block.SetVector("_StateAccent",stateAccent);p.block.SetVector("_StateSurface",stateSurface);
                    p.block.SetVector("_ArtLightWeights",ArtLightWeights); p.block.SetVector(ArtWeightsId,ArtWeights); p.block.SetFloat(ArtClockId,artTime); p.block.SetFloat(ArtPhaseId,az+k*.5f);
                    p.renderer.SetPropertyBlock(p.block);
                }
            }
        }
    }
}
