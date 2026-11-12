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
            new FlowerArtProfile(1.5f,8,.90f),
            new FlowerArtProfile(0,5,.13f), // Gathering keeps the narrow bud silhouette.
            new FlowerArtProfile(4f,9,1f),
            new FlowerArtProfile(1.4f,14,.35f)};
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
        Color previewAccentFrom=Color.white,previewAccentTo=Color.white,artAccentCurrent=Color.white,artAccentSmooth=Color.white;
        int accentCommitted;
        float artTransitionTime=6;
        const float ArtTransitionDuration=6,ArtStreamSmoothing=2.5f,ShapeSmoothing=2.5f,AccentSmoothing=1.8f,AccentCommitMargin=.45f;
        // Raw attention/relaxation drive the silk thread's route. They are smoothed separately from
        // the four-state commitment so slider moves keep reshaping the path even while the picture
        // stays committed to one state.
        public float ShapeAttention=>shapeAttention;
        public float ShapeRelaxation=>shapeRelaxation;
        float shapeAttention=.5f,shapeRelaxation=.5f,shapeAttentionTarget=.5f,shapeRelaxationTarget=.5f;
        static readonly Vector2[] ShapePresets={new Vector2(.8f,.8f),new Vector2(.8f,.2f),new Vector2(.2f,.8f),new Vector2(.2f,.2f)};
        // Display-side mapping: 180 s median baseline plus a committed softmax choice.
        const int BaselineCapacity=360;
        const float BaselineInterval=.5f,BaselineAbsoluteGain=3f,BaselineRelativeGain=.8f,BaselineDeadband=.015f;
        const float SoftmaxSharpness=16f,DominantSwitchMargin=.02f,TieThreshold=.03f,TieLean=.05f;
        readonly float[] baselineAttention=new float[BaselineCapacity];
        readonly float[] baselineRelaxation=new float[BaselineCapacity];
        readonly float[] baselineScratchA=new float[BaselineCapacity];
        readonly float[] baselineScratchR=new float[BaselineCapacity];
        int baselineCount,baselineHead,lastDominant;
        float lastBaselineSample=float.NegativeInfinity,medianAttention=.5f,medianRelaxation=.5f;
        // All modes share the standard opening path and global correction clock.
        float StandardLayerProgress(FlowerLayer c,float global)=>Mathf.Clamp01((global*BloomDuration-c.delay)/Mathf.Max(1,c.duration));
        static FlowerArtLook[] CreateDefaultLooks()=>new[]{
            new FlowerArtLook(new Color(.62f,.91f,.99f),.86f,.98f,1.08f,.25f,.40f,0f,.04f,1.12f,.5f,0f,0f,2f),
            new FlowerArtLook(new Color(.98f,.68f,.32f),.82f,.98f,.98f,.12f,.18f,.06f,.02f,1.05f,.35f,0f,0f,0f),
            new FlowerArtLook(new Color(.52f,.60f,1f),.86f,1.01f,1.08f,.10f,.20f,.03f,0f,1.02f,1.8f,0f,2.5f,.4f),
            new FlowerArtLook(new Color(.58f,.42f,.62f),.82f,.82f,.80f,-.04f,.04f,.01f,-.10f,.74f,.3f,-7.5f,0f,0f)};
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
            for(int i=0;i<4;i++)
            {
                var s=artLooks[i];float w=weights[i];
                look.huePull+=s.huePull*w;look.brightness+=s.brightness*w;look.saturation+=s.saturation*w;
                look.gloss+=s.gloss*w;look.rim+=s.rim*w;look.selfGlow+=s.selfGlow*w;look.exposure+=s.exposure*w;
                look.lightGain+=s.lightGain*w;look.lightDrift+=s.lightDrift*w;
                look.droop+=s.droop*w;look.asym+=s.asym*w;look.yawSpeed+=s.yawSpeed*w;
            }
            return look;
        }
        // The picture picks one color family; the runner-up only shades it. Averaging all four
        // accents during a mixed state is what used to turn the flower into gray-pink.
        static void LeadingPair(Vector4 weights,out int top,out int second,out float margin)
        {
            top=0;
            for(int i=1;i<4;i++) if(weights[i]>weights[top]) top=i;
            second=top==0?1:0;
            for(int i=0;i<4;i++) if(i!=top&&weights[i]>weights[second]) second=i;
            margin=weights[top]-weights[second];
        }
        // Commits the color family only once a leader is clearly ahead; never mutates state so the
        // delayed echo and validation can ask for a color without disturbing the live flower.
        void CommitAccent(Vector4 weights)
        {
            LeadingPair(weights,out int top,out _,out float margin);
            if(margin>=AccentCommitMargin) accentCommitted=top;
        }
        int LeadingState(Vector4 weights)
        {
            LeadingPair(weights,out int top,out _,out _);
            return top;
        }
        // A tie keeps the committed color; the switch happens at a clear lead and the shared
        // filter below turns that step into a drift.
        Color CommittedAccent()
        {
            EnsureArtLooks();
            return artLooks[accentCommitted].accent;
        }
        internal Color AccentFor(Vector4 weights)=>CommittedAccent();
        // One step of the shared color filter; exposed so validation can prove per-frame continuity.
        internal static Color SmoothAccentStep(Color current,Color target,float dt)
            =>Color.Lerp(current,target,1-Mathf.Exp(-Mathf.Max(0f,dt)/AccentSmoothing));
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
            EnsureArtLooks();
            // Preview crossfades straight between two accents instead of passing through a mix.
            previewAccentFrom=artActive?artAccentCurrent:artLooks[i].accent;
            previewAccentTo=artLooks[i].accent;
            accentCommitted=i;
            shapeAttentionTarget=ShapePresets[i].x;shapeRelaxationTarget=ShapePresets[i].y;
            EnterArt(); ArtInput=FlowerArtInput.Preview; artTarget=Vector4.zero; artTarget[i]=1;
        }
        public void WaitForArtBrain() { EnterArt(); ArtInput=FlowerArtInput.External; lastValid=float.NegativeInfinity; }
        public void SetBrainMetrics(float attention01,float relaxation01,bool signalValid=true)
        {
            EnterArt(); ArtInput=FlowerArtInput.External;
            if(!signalValid||!Finite(attention01)||!Finite(relaxation01)) return;
            float a=Mathf.Clamp01(attention01),r=Mathf.Clamp01(relaxation01);
            RawBrainWeights=BrainArtWeights(a,r);
            shapeAttentionTarget=a;shapeRelaxationTarget=r;
            PushBaseline(a,r);
            artTarget=MapDisplayWeights(a,r);
            CommitAccent(artTarget);
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
        Vector4 MapDisplayWeights(float a,float r)
        {
            float nA=.5f+.5f*(float)Math.Tanh(BaselineAbsoluteGain*(a-.5f)+BaselineRelativeGain*Deadband(a-medianAttention));
            float nR=.5f+.5f*(float)Math.Tanh(BaselineAbsoluteGain*(r-.5f)+BaselineRelativeGain*Deadband(r-medianRelaxation));
            var w=BrainArtWeights(nA,nR);
            float max=w.x,min=w.x;int dominant=0;
            for(int i=1;i<4;i++){max=Mathf.Max(max,w[i]);min=Mathf.Min(min,w[i]);if(w[i]>w[dominant])dominant=i;}
            // A challenger must lead by a small margin before it takes over.
            if(dominant!=lastDominant&&w[dominant]-w[lastDominant]<DominantSwitchMargin) dominant=lastDominant;
            // Softmax over the four quadrants commits the picture to one state instead of averaging them.
            var e=new Vector4(
                Mathf.Exp(SoftmaxSharpness*w.x+(dominant==0?.6f:0)),
                Mathf.Exp(SoftmaxSharpness*w.y+(dominant==1?.6f:0)),
                Mathf.Exp(SoftmaxSharpness*w.z+(dominant==2?.6f:0)),
                Mathf.Exp(SoftmaxSharpness*w.w+(dominant==3?.6f:0)));
            float sum=e.x+e.y+e.z+e.w;
            var result=sum>1e-6f?e/sum:new Vector4(1,0,0,0);
            if(max-min<TieThreshold)
            {
                var lean=Vector4.zero;lean[lastDominant]=1f;
                result=Vector4.Lerp(result,lean,TieLean);
                sum=result.x+result.y+result.z+result.w;
                result=sum>1e-6f?result/sum:new Vector4(1,0,0,0);
            }
            lastDominant=dominant;
            return result;
        }
        void EnterArt()
        {
            if(artActive) return;
            autoPlay=false; bloomProgress=1; ApplyPose(); bottomProgress=-1; artActive=true;
            EnsureArtLooks();
            LeadingPair(ArtWeights,out int top,out _,out _);
            accentCommitted=top;
            artAccentSmooth=artAccentCurrent=artLooks[top].accent;
            ShowArtExtras();
        }
        void LeaveArt()
        {
            if(!artActive) return;
            artActive=false; bottomProgress=-1; generated.localRotation=Quaternion.identity; generated.localPosition=Vector3.zero; generated.localScale=Vector3.one;
            HideArtExtras();
            foreach(var l in layers) { l.progress=-1; foreach(var p in l.petals) {
                p.block.SetVector(ArtWeightsId,Vector4.zero); p.block.SetVector("_ArtLightWeights",Vector4.zero);
                p.block.SetVector("_StateTint",new Vector4(1,1,0,0)); p.block.SetVector("_StateAccent",(Vector4)Color.white.linear); p.block.SetVector("_StateSurface",Vector4.zero);
                p.renderer.SetPropertyBlock(p.block); } }
            ArtExposureOffset=0;ArtSaturation=1;ArtAccentColor=Color.white;ArtLightGain=ArtLightDrift=1;
            if(naturalLights!=null) UpdateNaturalLights();
        }
        public void ResetArt()
        { artTime=artYaw=0; ResetFlowPhase(); frozen=false; ArtWeights=ArtLightWeights=artFrom=artDestination=new Vector4(1,0,0,0); RawBrainWeights=new Vector4(1,0,0,0); artTransitionTime=ArtTransitionDuration; ArtExposureOffset=0;ArtSaturation=1;ArtAccentColor=Color.white;ArtLightGain=ArtLightDrift=1; ResetBrainBaseline(); SetArtState(FlowerArtState.Clarity); EnsureArtLooks(); accentCommitted=0; artAccentSmooth=artAccentCurrent=artLooks[0].accent; shapeAttention=shapeAttentionTarget=ShapePresets[0].x; shapeRelaxation=shapeRelaxationTarget=ShapePresets[0].y; ResetRibbonThread(); }
        public void SetArtPreviewImmediate(FlowerArtState state)
        { SetArtState(state); ArtWeights=ArtLightWeights=artTarget; artFrom=artDestination=artTarget; artTransitionTime=ArtTransitionDuration; artTime=artYaw=0; ResetFlowPhase(); accentCommitted=(int)state; artAccentSmooth=artAccentCurrent=artLooks[(int)state].accent; shapeAttention=shapeAttentionTarget=ShapePresets[(int)state].x; shapeRelaxation=shapeRelaxationTarget=ShapePresets[(int)state].y; ResetRibbonThread(); UpdateArt(0); }
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
                float shapeStep=1f-Mathf.Exp(-dt/ShapeSmoothing);
                shapeAttention=Mathf.Lerp(shapeAttention,shapeAttentionTarget,shapeStep);
                shapeRelaxation=Mathf.Lerp(shapeRelaxation,shapeRelaxationTarget,shapeStep);
            }
            var look=BlendLook(ArtWeights);
            var lightLook=BlendLook(ArtLightWeights);
            Color accentTarget;
            if(ArtInput==FlowerArtInput.Preview)
            {
                float accentTime=ArtTransitionDuration>0?Mathf.Clamp01(artTransitionTime/ArtTransitionDuration):1f;
                accentTarget=Color.Lerp(previewAccentFrom,previewAccentTo,Mathf.SmoothStep(0,1,accentTime));
            }
            else
            {
                CommitAccent(ArtWeights);
                accentTarget=CommittedAccent();
            }
            // A shared first-order filter bounds how fast the color can move, so violent input
            // swings read as a drift instead of a hard cut. Preview already crossfades over six
            // seconds, so it maps straight through.
            if(!frozen) artAccentSmooth=ArtInput==FlowerArtInput.Preview?accentTarget:SmoothAccentStep(artAccentSmooth,accentTarget,dt);
            look.accent=artAccentSmooth;
            lightLook.accent=artAccentSmooth;
            artAccentCurrent=artAccentSmooth;
            ArtExposureOffset=look.exposure;ArtSaturation=look.saturation;
            ArtLightGain=lightLook.lightGain;ArtLightDrift=lightLook.lightDrift;ArtAccentColor=lightLook.accent;
            UpdateNaturalLights();
            bool suppress=false;
#if UNITY_EDITOR || FLOWER_VALIDATION
            suppress=suppressArtMotion;
#endif
            // The whole flower leans for Wandering and nods for Quiet; Clarity and Gathering stay upright.
            float pitch=suppress?0:4f*ArtWeights.w;
            float sideLean=suppress?0:5f*ArtWeights.z;
            float lift=suppress?0:(.01f*ArtWeights.z-.05f*ArtWeights.w);
            generated.localRotation=Quaternion.Euler(pitch,artYaw,sideLean);
            generated.localPosition=new Vector3(0,lift,0);
            generated.localScale=Vector3.one;
            float gathering=ArtWeights.y;
            float bud=artProfiles[1].openness+.03f*(1-Mathf.Cos(artTime*Mathf.PI*2/5))*.5f;
            float other=ArtWeights.x+ArtWeights.z+ArtWeights.w;
            float open=other>1e-6f?(ArtWeights.x*artProfiles[0].openness+ArtWeights.z*artProfiles[2].openness+ArtWeights.w*artProfiles[3].openness)/other:1f;
            // A shared layer phase drives geometry, transform and sparse correction sampling.
            bloomProgress=gathering>=1f?bud:Mathf.Lerp(open,bud,gathering);
            ApplyPose(bloomProgress,false);ApplyBottomPatches();
            Vector4 stateTint=new Vector4(look.brightness,look.saturation,0,0);
            Vector4 stateAccent=(Vector4)look.accent.linear;
            Vector4 stateSurface=new Vector4(look.huePull,look.rim,look.selfGlow,look.gloss);
            for(int k=0;k<layers.Length;k++)
            {
                var l=layers[k];
                for(int j=0;j<l.petals.Length;j++)
                {
                    var p=l.petals[j]; float az=j*Mathf.PI*2/l.petals.Length;
                    PetalArtMotion(ArtWeights,artTime,az,k,j,out float tilt,out float roll);
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
            if(echoPetals!=null) UpdateEcho(dt);
            if(ribbonThread!=null) UpdateRibbons(dt);
        }
        // Shared by the main flower and the delayed echo; i == 1 (Gathering) is carried by the bud shape.
        void PetalArtMotion(Vector4 weights,float time,float az,int layer,int petal,out float tilt,out float roll)
        {
            tilt=0;
            for(int i=0;i<4;i++)
            {
                if(i==1) continue;
                var profile=artProfiles[i]; float period=profile.period;
                if(i==2) period=7f+4*Mathf.Repeat((petal+1)*.618034f+layer*.37f,1);
                float phase=i==2?az+layer*.9f:i==3?-az-layer*.5f:0;
                tilt+=weights[i]*profile.amplitude*Mathf.Sin(time*Mathf.PI*2/period+phase);
            }
            var look=BlendLook(weights);
            // Quiet droops from the outer layer inward; Wandering adds a deterministic per-petal roll.
            tilt+=look.droop*(layer==0?1.2f:.7f);
            roll=look.asym*Mathf.Sin((petal+1)*1.7f+layer*.9f);
        }
    }
}
