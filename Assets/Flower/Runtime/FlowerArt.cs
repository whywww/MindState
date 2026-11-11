using System;
using UnityEngine;
namespace MindState
{
    public enum FlowerArtState { Clarity, Gathering, Wandering, Quiet }
    public enum FlowerArtInput { Preview, Simulation, External }
    [Serializable] public sealed class FlowerArtProfile
    {
        public float amplitude,period,openness;
        public FlowerArtProfile(float a,float p,float o=1f) { amplitude=a; period=p; openness=o; }
    }
    public sealed partial class FlowerAssembler
    {
        public FlowerArtProfile[] artProfiles={
            new FlowerArtProfile(.2f,1,.88f),
            new FlowerArtProfile(0,5,0f), // Gathering uses the original cached path.
            new FlowerArtProfile(2.4f,9,1f),
            new FlowerArtProfile(1.2f,14,.70f)};
        public FlowerArtInput ArtInput { get; private set; }
        public Vector4 ArtWeights { get; private set; }=new Vector4(1,0,0,0);
        public Vector4 ArtLightWeights { get; private set; }=new Vector4(1,0,0,0);
        public bool ArtActive=>artActive;
        public float ArtTime=>artTime;
        public string ArtStatus=>!artActive?"开合预览":ArtInput==FlowerArtInput.Preview?"单态预览":ArtInput==FlowerArtInput.Simulation?"模拟脑电":Time.realtimeSinceStartup-lastValid>5?"正在重新感知":"外部脑电";
        bool artActive;
#if UNITY_EDITOR || FLOWER_VALIDATION
        internal bool suppressArtMotion;
#endif
        float artTime,artYaw,lastValid=float.NegativeInfinity;
        Vector4 artTarget=new Vector4(1,0,0,0);
        Vector4 artFrom=new Vector4(1,0,0,0),artDestination=new Vector4(1,0,0,0);
        float artTransitionTime=18;
        const float ArtTransitionDuration=18;
        // All modes share the standard opening path and global correction clock.
        float StandardLayerProgress(FlowerLayer c,float global)=>Mathf.Clamp01((global*BloomDuration-c.delay)/Mathf.Max(1,c.duration));
        void AdvanceArtWeights(float dt)
        {
            if(ArtInput!=FlowerArtInput.Preview)
            {
                // Streaming targets must not restart a timed transition every sample.
                ArtWeights=Vector4.Lerp(ArtWeights,artTarget,1-Mathf.Exp(-dt/6f));
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
        public void SimulateArtBrain() { EnterArt(); ArtInput=FlowerArtInput.Simulation; }
        public void WaitForArtBrain() { EnterArt(); ArtInput=FlowerArtInput.External; lastValid=float.NegativeInfinity; }
        public void SetBrainMetrics(float attention01,float relaxation01,bool signalValid=true)
        {
            EnterArt(); ArtInput=FlowerArtInput.External;
            if(!signalValid||!Finite(attention01)||!Finite(relaxation01)) return;
            artTarget=BrainArtWeights(attention01,relaxation01); lastValid=Time.realtimeSinceStartup;
        }
        public static Vector4 BrainArtWeights(float a,float r)
        { a=Mathf.Clamp01(a); r=Mathf.Clamp01(r); return new Vector4(a*r,a*(1-r),(1-a)*r,(1-a)*(1-r)); }
        void EnterArt()
        {
            if(artActive) return;
            autoPlay=false; bloomProgress=1; ApplyPose(); bottomProgress=-1; artActive=true;
        }
        void LeaveArt()
        {
            if(!artActive) return;
            artActive=false; bottomProgress=-1; generated.localRotation=Quaternion.identity; generated.localScale=Vector3.one;
            foreach(var l in layers) { l.progress=-1; foreach(var p in l.petals) { p.block.SetVector(ArtWeightsId,Vector4.zero); p.block.SetVector("_ArtLightWeights",Vector4.zero); p.renderer.SetPropertyBlock(p.block); } }
        }
        public void ResetArt()
        { artTime=artYaw=0; ResetFlowPhase(); frozen=false; ArtWeights=ArtLightWeights=artFrom=artDestination=new Vector4(1,0,0,0); artTransitionTime=ArtTransitionDuration; SetArtState(FlowerArtState.Clarity); }
        public void SetArtPreviewImmediate(FlowerArtState state)
        { SetArtState(state); ArtWeights=ArtLightWeights=artTarget; artFrom=artDestination=artTarget; artTransitionTime=ArtTransitionDuration; artTime=artYaw=0; ResetFlowPhase(); UpdateArt(0); }
        float FlowSpeed(){var periods=appearance!=null?appearance.flowPeriods:new Vector4(12,10,9,18);float speed=0;for(int i=0;i<4;i++)speed+=ArtLightWeights[i]/Mathf.Max(1,periods[i]);return speed;}
        void UpdateArt(float dt)
        {
            if(!frozen)
            {
                artTime+=dt;
                if(ArtInput==FlowerArtInput.Simulation) artTarget=BrainArtWeights(.5f+.48f*Mathf.Sin(artTime*.19f),.5f+.48f*Mathf.Sin(artTime*.11f));
                if(ArtInput==FlowerArtInput.External&&Time.realtimeSinceStartup-lastValid>5) artTarget=new Vector4(0,0,0,1);
                AdvanceArtWeights(dt);
                ArtLightWeights=Vector4.Lerp(ArtLightWeights,artTarget,1-Mathf.Exp(-dt/3f));
                artYaw=Mathf.Repeat(artYaw+4*ArtWeights.x*dt,360);
                ArtFlowPhase=Mathf.Repeat(ArtFlowPhase+dt*FlowSpeed(),1);
            }
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
            for(int k=0;k<layers.Length;k++)
            {
                var l=layers[k];
                for(int j=0;j<l.petals.Length;j++)
                {
                    var p=l.petals[j]; float az=j*Mathf.PI*2/l.petals.Length,tilt=0;
                    for(int i=0;i<4;i++)
                    {
                        if(i==1) continue;
                        var profile=artProfiles[i]; float period=profile.period;
                        if(i==2) period=7+4*Mathf.Repeat((j+1)*.618034f+k*.37f,1);
                        float phase=i==1?-k*.3f/period*Mathf.PI*2:i==2?az+k*.9f:i==3?-az-k*.5f:0;
                        float motion=profile.amplitude*Mathf.Sin(artTime*Mathf.PI*2/period+phase);
                        tilt+=ArtWeights[i]*motion;
                    }
                    PathPetalPose(l,p,j,StandardLayerProgress(l.config,bloomProgress),out var position,out var rotation,out var scale);
                    // Dynamic pitch is additive; authored standard shape remains intact.
#if UNITY_EDITOR || FLOWER_VALIDATION
                    if(suppressArtMotion) tilt=0;
#endif
                    rotation=rotation*Quaternion.Euler(tilt,0,0);
                    ApplyPetalPose(l,p,j,position,rotation,scale);
                    if(appearance!=null) {
                        float v=Mathf.Repeat((j+1)*.618034f+k*.37f+appearance.seed*.013f,1);
                        if(!frozen) p.flowPhase=Mathf.Repeat(p.flowPhase+dt*FlowSpeed()/(1+(v*2-1)*appearance.periodVariation),1);
                        p.block.SetVector("_FlowSettings",new Vector4(p.flowPhase,appearance.bandWidth,v*appearance.phaseSpread,0));
                    }
                    p.block.SetVector("_ArtLightWeights",ArtLightWeights); p.block.SetVector(ArtWeightsId,ArtWeights); p.block.SetFloat(ArtClockId,artTime); p.block.SetFloat(ArtPhaseId,az+k*.5f);
                    p.renderer.SetPropertyBlock(p.block);
                }
            }
        }
    }
}
