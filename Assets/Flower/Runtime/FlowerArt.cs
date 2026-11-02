using System;
using UnityEngine;
namespace MindState
{
    public enum FlowerArtState { Clarity, Gathering, Wandering, Quiet }
    public enum FlowerArtInput { Preview, Simulation, External }
    [Serializable] public sealed class FlowerArtProfile
    {
        public Vector3 tilt;
        public float amplitude,period;
        public FlowerArtProfile(Vector3 t,float a,float p) { tilt=t; amplitude=a; period=p; }
    }
    public sealed partial class FlowerAssembler
    {
        public FlowerArtProfile[] artProfiles={
            new FlowerArtProfile(new Vector3(70,64,37),.2f,1),
            new FlowerArtProfile(Vector3.zero,0,5), // Gathering uses the original cached path.
            new FlowerArtProfile(new Vector3(82,76,52),3,9),
            new FlowerArtProfile(new Vector3(65,59,34),1.5f,14)};
        public FlowerArtInput ArtInput { get; private set; }
        public Vector4 ArtWeights { get; private set; }=new Vector4(1,0,0,0);
        public bool ArtActive=>artActive;
        public float ArtTime=>artTime;
        public string ArtStatus=>!artActive?"开合预览":ArtInput==FlowerArtInput.Preview?"单态预览":ArtInput==FlowerArtInput.Simulation?"模拟脑电":Time.realtimeSinceStartup-lastValid>5?"正在重新感知":"外部脑电";
        bool artActive;
        float artTime,artYaw,lastValid=float.NegativeInfinity;
        Vector4 artTarget=new Vector4(1,0,0,0);
        static readonly int ArtWeightsId=Shader.PropertyToID("_ArtWeights"),ArtClockId=Shader.PropertyToID("_ArtClock"),ArtPhaseId=Shader.PropertyToID("_ArtPhase");
        static bool ArtDiagnosticsRequested()
        { foreach(var a in Environment.GetCommandLineArgs()) if(a=="-flowerAvoidance"||a=="-verifyFlower"||a=="-verifyPetal"||a=="-silkAB"||a=="-verifyArt") return true; return false; }
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
            autoPlay=false; bloomProgress=1; ApplyPose(); artActive=true;
        }
        void LeaveArt()
        {
            if(!artActive) return;
            artActive=false; generated.localRotation=Quaternion.identity; generated.localScale=Vector3.one;
            foreach(var l in layers) { l.progress=-1; foreach(var p in l.petals) { p.block.SetVector(ArtWeightsId,Vector4.zero); p.renderer.SetPropertyBlock(p.block); } }
        }
        public void ResetArt()
        { artTime=artYaw=0; frozen=false; ArtWeights=new Vector4(1,0,0,0); SetArtState(FlowerArtState.Clarity); }
        public void SetArtPreviewImmediate(FlowerArtState state)
        { SetArtState(state); ArtWeights=artTarget; artTime=artYaw=0; UpdateArt(0); }
        void UpdateArt(float dt)
        {
            if(!frozen)
            {
                artTime+=dt;
                if(ArtInput==FlowerArtInput.Simulation) artTarget=BrainArtWeights(.5f+.48f*Mathf.Sin(artTime*.19f),.5f+.48f*Mathf.Sin(artTime*.11f));
                if(ArtInput==FlowerArtInput.External&&Time.realtimeSinceStartup-lastValid>5) artTarget=new Vector4(0,0,0,1);
                ArtWeights=Vector4.Lerp(ArtWeights,artTarget,1-Mathf.Exp(-dt/4));
                artYaw=Mathf.Repeat(artYaw+4*ArtWeights.x*dt,360);
            }
            generated.localRotation=Quaternion.Euler(0,artYaw,0);
            generated.localScale=Vector3.one;
            float gathering=ArtWeights.y;
            float bud=.02f*(1-Mathf.Cos(artTime*Mathf.PI*2/5));
            bloomProgress=1-gathering*(1-bud);
            ApplyPose(bloomProgress,false);
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
                        tilt+=ArtWeights[i]*(profile.tilt[k]+motion);
                    }
                    PathPetalPose(l,p,j,l.progress,out var pathPosition,out var pathRotation,out var pathScale);
                    Vector3 position=pathPosition,scale=pathScale; Quaternion rotation=pathRotation;
                    if(gathering<1)
                    {
                        float other=ArtWeights.x+ArtWeights.z+ArtWeights.w;
                        tilt/=Mathf.Max(other,1e-6f);
                        float authored=p.authoredTilt*(1-.75f*ArtWeights.x/Mathf.Max(other,1e-6f));
                        PetalFrame(l.config,p.azimuth,tilt+p.tilt+authored,p.roll,p.heightOffset,j,l.petals.Length,1,out var artPosition,out var artRotation);
                        position=Vector3.Lerp(artPosition,pathPosition,gathering);
                        rotation=Quaternion.Slerp(artRotation,pathRotation,gathering);
                        scale=Vector3.Lerp(PetalScale(l.config,p,j,1),pathScale,gathering);
                    }
                    ApplyPetalPose(l,p,j,position,rotation,scale);
                    p.block.SetVector(ArtWeightsId,ArtWeights); p.block.SetFloat(ArtClockId,artTime); p.block.SetFloat(ArtPhaseId,az+k*.5f);
                    p.renderer.SetPropertyBlock(p.block);
                }
            }
        }
    }
}
