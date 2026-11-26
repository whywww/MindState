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
        internal bool quietPair12Bypass;
        internal bool quietLayerBypass;
#endif
        float artTime,artYaw,lastValid=float.NegativeInfinity;
        Vector4 artTarget=new Vector4(1,0,0,0);
        Vector4 artFrom=new Vector4(1,0,0,0),artDestination=new Vector4(1,0,0,0);
        Vector4 artVelocity,artLightVelocity;
        Color previewAccentFrom=Color.white,previewAccentTo=Color.white,artAccentCurrent=Color.white,artAccentSmooth=Color.white;
        int accentCommitted;
        float artTransitionTime=6;
        const float ArtTransitionDuration=6,ArtStreamSmoothTime=6f,ArtPreviewLightSmoothing=2.5f,ShapeSmoothing=2.5f,AccentSmoothing=4f,AccentCommitMargin=.45f;
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
            new FlowerArtLook(new Color(1f,.96f,.92f),.82f,1.04f,.95f,.18f,.26f,.07f,.05f,1.12f,.35f,0f,0f,0f),
            new FlowerArtLook(new Color(.52f,.60f,1f),.86f,1.01f,1.08f,.10f,.20f,.03f,0f,1.02f,1.8f,0f,2.5f,.4f),
            new FlowerArtLook(new Color(.98f,.74f,.83f),.82f,.94f,.92f,.02f,.10f,.02f,-.02f,.92f,.3f,-7.5f,0f,0f)};
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
        // history-summary echo and validation can ask for a color without disturbing the live flower.
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
        // Critically damped per-component smoothing. Unlike an exponential lerp it starts from
        // rest, so a state change eases in instead of jumping to its fastest speed on frame one.
        static Vector4 SmoothDampWeights(Vector4 current,Vector4 target,ref Vector4 velocity,float smoothTime,float dt)
        {
            // SmoothDamp divides by deltaTime in its overshoot guard, so a zero step must not run.
            if(dt<=0f) return current;
            return new Vector4(
                Mathf.SmoothDamp(current.x,target.x,ref velocity.x,smoothTime,Mathf.Infinity,dt),
                Mathf.SmoothDamp(current.y,target.y,ref velocity.y,smoothTime,Mathf.Infinity,dt),
                Mathf.SmoothDamp(current.z,target.z,ref velocity.z,smoothTime,Mathf.Infinity,dt),
                Mathf.SmoothDamp(current.w,target.w,ref velocity.w,smoothTime,Mathf.Infinity,dt));
        }
        void ResetArtVelocity(){artVelocity=artLightVelocity=Vector4.zero;}
        void AdvanceArtWeights(float dt)
        {
            if(ArtInput!=FlowerArtInput.Preview)
            {
                // Streaming targets must not restart a timed transition every sample.
                ArtWeights=SmoothDampWeights(ArtWeights,artTarget,ref artVelocity,ArtStreamSmoothTime,dt);
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
            ResetArtVelocity();
            EnterArt(); ArtInput=FlowerArtInput.Preview; artTarget=Vector4.zero; artTarget[i]=1;
        }
        public void WaitForArtBrain() { ResetArtVelocity(); EnterArt(); ArtInput=FlowerArtInput.External; lastValid=float.NegativeInfinity; }
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
            // Captured before any bud offset: the rest pose of the root and the full-bloom centre
            // the scale anchors on. Art mode always enters with the flower at rest.
            restPosition=transform.localPosition; restCaptured=true;
            bloomCenterLocal=transform.InverseTransformPoint(FullBloomBounds.center);
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
            artActive=false; flexWeight=flexPreviousWeight=pair24CurlAmount=pair24CurlPrevious=0; bottomProgress=-1; generated.localRotation=Quaternion.identity; generated.localPosition=Vector3.zero; generated.localScale=Vector3.one; if(restCaptured) transform.localPosition=restPosition;
            HideArtExtras();
            foreach(var l in layers) { l.progress=-1; foreach(var p in l.petals) {
                p.block.SetFloat(FlexWeightId,0); p.block.SetFloat(Pair24CurlId,0); p.block.SetVector(ArtWeightsId,Vector4.zero); p.block.SetVector("_ArtLightWeights",Vector4.zero);
                p.block.SetVector("_StateTint",new Vector4(1,1,0,0)); p.block.SetVector("_StateAccent",(Vector4)Color.white.linear); p.block.SetVector("_StateSurface",Vector4.zero);
                p.renderer.SetPropertyBlock(p.block); } }
            ArtExposureOffset=0;ArtSaturation=1;ArtAccentColor=Color.white;ArtLightGain=ArtLightDrift=1;
            if(naturalLights!=null) UpdateNaturalLights();
        }
        public void ResetArt()
        { artTime=artYaw=0; ResetFlowPhase(); frozen=false; ArtWeights=ArtLightWeights=artFrom=artDestination=new Vector4(1,0,0,0); RawBrainWeights=new Vector4(1,0,0,0); artTransitionTime=ArtTransitionDuration; ArtExposureOffset=0;ArtSaturation=1;ArtAccentColor=Color.white;ArtLightGain=ArtLightDrift=1; ResetBrainBaseline(); SetArtState(FlowerArtState.Clarity); EnsureArtLooks(); accentCommitted=0; artAccentSmooth=artAccentCurrent=artLooks[0].accent; shapeAttention=shapeAttentionTarget=ShapePresets[0].x; shapeRelaxation=shapeRelaxationTarget=ShapePresets[0].y; ResetRibbonThread(); ResetEchoHistory(); }
        public void SetArtPreviewImmediate(FlowerArtState state)
        { SetArtState(state); ArtWeights=ArtLightWeights=artTarget; artFrom=artDestination=artTarget; artTransitionTime=ArtTransitionDuration; artTime=artYaw=0; ResetFlowPhase(); accentCommitted=(int)state; artAccentSmooth=artAccentCurrent=artLooks[(int)state].accent; shapeAttention=shapeAttentionTarget=ShapePresets[(int)state].x; shapeRelaxation=shapeRelaxationTarget=ShapePresets[(int)state].y; ResetRibbonThread(); ResetEchoHistory(); UpdateArt(0); }
        float FlowSpeed(){var periods=appearance!=null?appearance.flowPeriods:new Vector4(12,10,9,18);float speed=0;for(int i=0;i<4;i++)speed+=ArtLightWeights[i]/Mathf.Max(1,periods[i]);return speed;}
        void UpdateArt(float dt)
        {
            if(!frozen)
            {
                artTime+=dt;
                AdvanceArtWeights(dt);
                ArtLightWeights=ArtInput==FlowerArtInput.Preview
                    ?Vector4.Lerp(ArtLightWeights,artTarget,1-Mathf.Exp(-dt/ArtPreviewLightSmoothing))
                    :SmoothDampWeights(ArtLightWeights,artTarget,ref artLightVelocity,ArtStreamSmoothTime,dt);
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
            float gathering=ArtWeights.y;
            float budOpen=artProfiles[1].openness+.03f*(1-Mathf.Cos(artTime*Mathf.PI*2/5))*.5f;
            float other=ArtWeights.x+ArtWeights.z+ArtWeights.w;
            float open=other>1e-6f?(ArtWeights.x*artProfiles[0].openness+ArtWeights.z*artProfiles[2].openness+ArtWeights.w*artProfiles[3].openness)/other:1f;
            // A shared layer phase drives geometry, transform and sparse correction sampling.
            bloomProgress=gathering>=1f?budOpen:Mathf.Lerp(open,budOpen,gathering);
            // The camera stays put, so a closed bloom has to step toward the viewer and grow in place
            // to keep its size; the drop is the old camera lift applied to the whole art cluster.
            float bud=suppress?0:BudFactor(bloomProgress);
            float budScale=1f+BudScaleGain*bud;
            Vector3 rest=new Vector3(0,lift,0);
            generated.localRotation=Quaternion.Euler(pitch,artYaw,sideLean);
            // Lerp(rest,centre,1-scale) keeps the full-bloom centre fixed while the flower scales up.
            generated.localPosition=Vector3.Lerp(rest,bloomCenterLocal,1f-budScale)+viewToCamera*(BudForwardGain*bud);
            generated.localScale=Vector3.one*budScale;
            // The frame leans toward the main bloom, so a tight bud still reads right of centre;
            // slide the whole cluster back toward the middle while it is closed.
            transform.localPosition=restPosition-viewPageUp*(viewDistance*BudDropGain*bud)-viewPageRight*(viewDistance*BudCenterGain*bud);
            ApplyPose(bloomProgress,false);ApplyBottomPatches();
            PrepareArtMotion(); PreparePair24Pose(ArtWeights,suppress);
            Vector4 stateTint=new Vector4(look.brightness,look.saturation,0,0);
            Vector4 stateAccent=(Vector4)look.accent.linear;
            Vector4 stateSurface=new Vector4(look.huePull,look.rim,look.selfGlow,look.gloss);
            for(int k=0;k<layers.Length;k++)
            {
                var l=layers[k];
                for(int j=0;j<l.petals.Length;j++)
                {
                    var p=l.petals[j]; float az=j*Mathf.PI*2/l.petals.Length;
                    float tilt=desiredArtMotion[k][j].x,roll=desiredArtMotion[k][j].y;
                    PathPetalPose(l,p,j,StandardLayerProgress(l.config,bloomProgress),out var position,out var rotation,out var scale);
#if UNITY_EDITOR || FLOWER_VALIDATION
                    if(suppressArtMotion){ tilt=0; roll=0; }
#endif
                    AdjustPair01ArtPose(k,j,suppress?0:ArtWeights.z*WanderingSeamPoseGate,ref rotation,ref scale);
                    AdjustPair55ArtPose(k,j,suppress?0:ArtWeights.z*WanderingSeamPoseGate,ref rotation,ref scale);
                    AdjustPair20ArtPose(k,j,suppress?0:ArtWeights.z*Pair20PoseGate,ref rotation,ref scale);
                    if(!suppress&&k==2&&j==4)AdjustPair24MainPose(ref rotation,ref scale,ref tilt,ref roll);
                    AdjustPair10ArtPose(k,j,suppress?0:ArtWeights.z*Pair10PoseGate,ref rotation,ref scale);
                    AdjustQuietLayerArtPose(k,j,suppress?0:QuietPair12StateGate(ArtWeights.w),ref scale);
                    rotation=rotation*Quaternion.Euler(tilt,0,roll);
                    if(k==2&&j==4)rotation=pair24CounterRotation*rotation;
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
            FinishFlex(); FinishPair24Curl(suppress);
            if(echoPetals!=null) UpdateEcho(dt);
            if(ribbonThread!=null) UpdateRibbons(dt);
        }
        // Shared by the main flower and the history-summary echo; i == 1 (Gathering) is carried by the bud shape.
        void PetalArtMotion(Vector4 weights,float time,float az,int layer,int petal,out float tilt,out float roll,float pair12Weight=-1)
        {
            tilt=0;
            float pair12Gate=layer==1&&petal==2?Pair12MotionGate(pair12Weight<0?weights.z:pair12Weight):0;
            float quietPair12Gate=layer==1&&petal==2?QuietPair12StateGate(weights.w):0;
            float quietLayerGate=layer==1?QuietPair12StateGate(weights.w):0;
#if UNITY_EDITOR || FLOWER_VALIDATION
            if(quietPair12Bypass)quietPair12Gate=0;
            if(quietLayerBypass)quietLayerGate=0;
#endif
            for(int i=0;i<4;i++)
            {
                if(i==1) continue;
                var profile=artProfiles[i]; float period=profile.period;
                if(i==2) period=7f+4*Mathf.Repeat((petal+1)*.618034f+layer*.37f,1);
                float phase=i==2?az+layer*.9f:i==3?-az-layer*.5f:0;
                float motionScale=1;
                if(i==2&&layer==0){if(petal==1)motionScale=Mathf.Lerp(1,OuterPair01BreathScale,WanderingSeamPoseGate);else if(petal==5)motionScale=Mathf.Lerp(1,Pair55BreathScale,WanderingSeamPoseGate);}
                if(i==2&&layer==2&&petal==1)motionScale=Mathf.Lerp(1,Pair20BreathScale,Pair20PoseGate);
                if(i==2&&layer==2&&petal==4)motionScale=Mathf.Lerp(1,Pair24BreathScale,Pair24PoseGate*Pair24StateGate(weights.z));
                if(i==2&&layer==1&&petal==1)motionScale=Mathf.Lerp(1,Pair10BreathScale,Pair10PoseGate);
                if(i==2&&layer==1&&petal==2)motionScale=Mathf.Lerp(1,Pair12BreathScale,pair12Gate);
                if(i==3&&layer==1&&petal==2)motionScale=Mathf.Lerp(1,QuietPair12BreathScale,quietPair12Gate);
                if(i==3&&layer==1)motionScale=Mathf.Lerp(motionScale,QuietLayerBreathScale[petal],quietLayerGate);
                tilt+=weights[i]*profile.amplitude*motionScale*Mathf.Sin(time*Mathf.PI*2/period+phase);
            }
            if(layer==0&&petal==3) tilt+=weights.z*OuterPairTiltBias;
            if(layer==0&&petal==1) tilt+=weights.z*WanderingSeamPoseGate*OuterPair01TiltBias;
            if(layer==0&&petal==5) tilt+=weights.z*WanderingSeamPoseGate*Pair55TiltBias;
            if(layer==2&&petal==1) tilt+=weights.z*Pair20PoseGate*Pair20TiltBias;
            if(layer==2&&petal==4) tilt+=Pair24Weight(weights)*Pair24TiltBias;
            if(layer==1&&petal==1) tilt+=weights.z*Pair10PoseGate*Pair10TiltBias;
            if(layer==1&&petal==2) tilt+=weights.z*pair12Gate*Pair12TiltBias;
            var look=BlendLook(weights);
            // Quiet droops from the outer layer inward; Wandering adds a deterministic per-petal roll.
            tilt+=look.droop*(layer==0?1.2f:.7f);
            roll=look.asym*Mathf.Sin((petal+1)*1.7f+layer*.9f);
            if(layer==0&&petal==1) roll+=weights.z*WanderingSeamPoseGate*OuterPair01RollBias;
            if(layer==0&&petal==5) roll+=weights.z*WanderingSeamPoseGate*Pair55RollBias;
            if(layer==2&&petal==1) roll+=weights.z*Pair20PoseGate*Pair20RollBias;
            if(layer==2&&petal==4) roll+=Pair24Weight(weights)*Pair24RollBias;
            if(layer==1&&petal==1) roll+=weights.z*Pair10PoseGate*Pair10RollBias;
            if(layer==1&&petal==2){tilt+=quietPair12Gate*QuietPair12TiltBias;roll+=quietPair12Gate*QuietPair12RollBias;}
            if(layer==1){tilt+=quietLayerGate*QuietLayerTiltBias[petal];roll+=quietLayerGate*QuietLayerRollBias[petal];}
            // Mid-transition outward arc so P2 swings around the outside of P1 during the
            // Gathering<->Quiet blend; zero at both endpoints.
            if(layer==1&&petal==2)tilt+=Mathf.Sin(Mathf.PI*Mathf.Clamp01(weights.w))*QuietTransitionArc;
        }
        // Keep P3 below P2 through the independent Wandering breathing cycles.
        const float OuterPairTiltBias=7f;
        // Root-preserving P1 calibration, validated against every neighbour over 120 s.
        const float OuterPair01TiltBias=9.725545f,OuterPair01BreathScale=.0168886f,OuterPair01RollBias=.6179104f;
        float WanderingSeamPoseGate=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,.9f,bloomProgress));
        // L0-P5 clears L1-P5 without changing the middle ring or either earlier repair.
        const float Pair55TiltBias=10.29227f,Pair55BreathScale=.1206378f,Pair55RollBias=6.352293f;
        // L2-P1 keeps its attachment and most of its breathing while clearing L2-P0.
        const float Pair20TiltBias=-8.556192f,Pair20BreathScale=.7187908f,Pair20RollBias=-2.445637f;
        float Pair20PoseGate=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,1f,bloomProgress));
        // L2-P4 keeps a stable seam with L2-P0, with its root fixed.
        const float Pair24TiltBias=-6.971874f,Pair24BreathScale=.491653055f,Pair24RollBias=-4.892811f;
        float Pair24PoseGate=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,1f,bloomProgress));
        static float Pair24StateGate(float weight)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(weight));
        float Pair24Weight(Vector4 weights)=>weights.z*Pair24PoseGate*Pair24StateGate(weights.z);
        // Fixed scale through breathing and state changes. Quiet keeps this petal tucked.
        const float Pair24QuietYaw=24,Pair24QuietPower=3;
        float Pair24PlaybackProgress(float progress){
#if UNITY_EDITOR || FLOWER_VALIDATION
            if(suppressArtMotion)return progress;
#endif
            return artActive?Mathf.Clamp01(progress-.22f*ArtWeights.w):progress;
        }
        Vector3 Pair24PoseRoot(){var g=SurfaceFor(layers[2],4);float progress=Pair24PlaybackProgress(bloomProgress);if(progress==bloomProgress)return g.root;float frame=StandardLayerProgress(layers[2].config,progress)*(PoseCount-1);int first=Mathf.Min((int)frame,PoseCount-2);return Vector3.LerpUnclamped(g.poses[first].root,g.poses[first+1].root,frame-first);}
        Quaternion pair24CounterRotation=Quaternion.identity;
        Vector3 pair24Rotation,pair24Scale;
        float pair24RotationWeight;
        static readonly Vector3 Pair24FixedScale=new Vector3(.9f*.711982548f,.8257039f,1.075196f);
        static readonly Vector3 Pair24End=new Vector3(14.63435f,24.1214657f,21.0392628f);
        void PreparePair24Pose(Vector4 weights,bool suppressed){
            pair24CounterRotation=Quaternion.identity;
            if(suppressed){pair24Rotation=Vector3.zero;pair24Scale=Vector3.one;pair24RotationWeight=0;return;}
            float sum=weights.x+weights.y+weights.w,power=3;
            Vector3 rest=Vector3.zero,arc=Vector3.zero;
            if(sum>0){
                float a=weights.x/sum,b=weights.y/sum,c=weights.w/sum;
                rest=new Vector3(9.04858f,-13.8673325f,5.318752f)*a+new Vector3(.0177443027f,-.468444824f,1.0314554f)*b+new Vector3(6.65284348f-3.6351f,-20.8164654f+20.348f,8.960873f-7.9294f)*c;
                arc=new Vector3(-.75f,0,0)*a+new Vector3(0,24,0)*b+new Vector3(0,Pair24QuietYaw,0)*c;
                power=a+3*b+Pair24QuietPower*c;
                pair24CounterRotation=Quaternion.Inverse(Quaternion.Euler((b+c)*Mathf.Sin(Mathf.PI*weights.z),0,-2.688581f*(b+c)*Mathf.Sin(Mathf.PI*weights.z)));
            }
            float q=Mathf.Clamp01(weights.z),w=Mathf.SmoothStep(0,1,Mathf.Pow(q,power));
            pair24RotationWeight=w;pair24Rotation=rest*(1-w)+Pair24End*w+arc*Mathf.Sin(Mathf.PI*w);
            pair24Scale=Pair24FixedScale;
        }
        void AdjustPair24MainPose(ref Quaternion rotation,ref Vector3 scale,ref float tilt,ref float roll){
            float period=7+4*Mathf.Repeat(5*.618034f+2*.37f,1);
            float raw=artProfiles[2].amplitude*Mathf.Sin(artTime*Mathf.PI*2/period+4*Mathf.PI*2/5+1.8f);
            float original=Pair24Weight(ArtWeights),w=pair24RotationWeight;
            tilt=(tilt-original*(Pair24TiltBias+(Pair24BreathScale-1)*raw))*.5f+w*(Pair24TiltBias+(Pair24BreathScale-1)*raw*.5f)+pair24Rotation.x;
            roll=roll-original*Pair24RollBias+w*Pair24RollBias+pair24Rotation.z;
            rotation=Quaternion.AngleAxis(-16.3266544f*w+pair24Rotation.y,Vector3.up)*rotation;
            scale=Vector3.Scale(PetalScale(layers[2].config,layers[2].petals[4],4,1),pair24Scale);
        }
        // L1-P1 clears L1-P0 while keeping its attachment fixed.
        const float Pair10TiltBias=3.68836641f,Pair10BreathScale=0.3280386f,Pair10RollBias=10.9942226f;
        float Pair10PoseGate=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,1f,bloomProgress));
        // P2 stays inside the independently baked P3 neighbour-tilt interval.
        const float Pair12TiltBias=1.5f,Pair12BreathScale=0.5f;
        float Pair12PoseGate=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,1f,bloomProgress));
        float Pair12MotionGate(float weight)=>Pair12PoseGate*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,1f,weight));
        // Quiet keeps L1-P2 clear of L1-P1 in the closed droop pose, root fixed.
        const float QuietPair12TiltBias=0f,QuietPair12BreathScale=1f,QuietPair12RollBias=0f;
        const float QuietTransitionArc=6f;
        static float QuietPair12StateGate(float weight)=>Mathf.SmoothStep(0,1,weight);
        // Quiet calibration for all six L1 petals (P2 included, kept on the outside of P1).
        // Elements are mutable only so the editor search can probe candidates; the committed
        // values are the fix. Gate ramps smoothly with the Quiet weight for fluid transitions.
        internal static readonly float[] QuietLayerTiltBias=new float[]{2,2,0,2,0,0};
        internal static readonly float[] QuietLayerBreathScale=new float[]{0.5f,0,0.75f,0,0.25f,0.25f};
        internal static readonly float[] QuietLayerRollBias=new float[]{2,6,2,4,2,0};
        // Slight root-fixed narrowing so the ring can sit tighter with less outward splay.
        internal const float QuietLayerWidthScale=.9f;
        static void AdjustQuietLayerArtPose(int layer,int petal,float weight,ref Vector3 scale){
            if(layer!=1||weight<=0)return;
            scale=Vector3.Scale(scale,new Vector3(Mathf.Lerp(1,QuietLayerWidthScale,weight),1,1));
        }
        static void AdjustPair10ArtPose(int layer,int petal,float weight,ref Quaternion rotation,ref Vector3 scale){
            if(layer!=1||petal!=1||weight<=0)return;
            rotation=Quaternion.AngleAxis(20.2531967f*weight,Vector3.up)*rotation;
            scale=Vector3.Scale(scale,Vector3.Lerp(Vector3.one,new Vector3(0.731844366f,0.955875039f,0.9241903f),weight));
        }
        static void AdjustPair20ArtPose(int layer,int petal,float weight,ref Quaternion rotation,ref Vector3 scale){
            if(layer!=2||petal!=1||weight<=0)return;
            rotation=Quaternion.AngleAxis(-6.325974f*weight,Vector3.up)*rotation;
            scale=Vector3.Scale(scale,Vector3.Lerp(Vector3.one,new Vector3(.7496356f,.8110957f,1.09828f),weight));
        }
        static void AdjustPair55ArtPose(int layer,int petal,float weight,ref Quaternion rotation,ref Vector3 scale){
            if(layer!=0||petal!=5||weight<=0)return;
            rotation=Quaternion.AngleAxis(-18.17691f*weight,Vector3.up)*rotation;
            scale=Vector3.Scale(scale,Vector3.Lerp(Vector3.one,new Vector3(.8766819f,1.129184f,1.090139f),weight));
        }
        static void AdjustPair01ArtPose(int layer,int petal,float weight,ref Quaternion rotation,ref Vector3 scale){
            if(layer!=0||petal!=1||weight<=0)return;
            rotation=Quaternion.AngleAxis(-14.81485f*weight,Vector3.up)*rotation;
            scale=Vector3.Scale(scale,Vector3.Lerp(Vector3.one,new Vector3(.8745521f,1.250042f,.9350485f),weight));
        }
    }
}
