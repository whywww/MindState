using System;
using UnityEngine;

namespace MindState
{
    [Serializable]
    public sealed class FlowerPetalOffset
    {
        public float height,tilt;
        public float lengthScale=1;
        public void Constrain()
        {
            height=Mathf.Clamp(Finite(height)?height:0,-.1f,.1f);
            tilt=Mathf.Clamp(Finite(tilt)?tilt:0,-8,8);
            lengthScale=Mathf.Clamp(Finite(lengthScale)?lengthScale:1,.88f,1.12f);
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    }

    [Serializable]
    public sealed class FlowerLayer
    {
        public string name;
        [Range(1,10)] public int count;
        [Min(0.1f)] public float size;
        public float azimuth,rootRadius,rootHeight;
        [Range(0,90)] public float closedTilt,openTilt;
        [Range(90,150)] public float openingAngle;
        [Range(0,1)] public float curl,softness;
        [Min(0)] public float delay;
        [Min(1)] public float duration=12;
        public Color pearl,root,pink;
        [Range(.4f,1)] public float width=1;
        [Range(0,1)] public float maximumCurl=1;
        [Range(0,1)] public float edgeFoldScale=1;
        public PetalSurfaceProfile surfaceProfile=PetalSurfaceProfile.Legacy;
        [Range(0,1)] public float microNormalScale=1;
        [Tooltip("Small longitudinal adjustment only; petals retain their radial, inward-facing orientation.")]
        [Range(-12,12)] public float roll;
        public float[] bloomRoll=Array.Empty<float>();
        public bool stagedOffsets;
        public float OffsetWeight(float progress)=>stagedOffsets?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,1,progress)):1;
        public float RollAt(float progress)
        {
            if(bloomRoll==null||bloomRoll.Length<2) return roll;
            float frame=Mathf.Clamp01(progress)*(bloomRoll.Length-1); int a=Mathf.Min((int)frame,bloomRoll.Length-2);
            return Mathf.Lerp(bloomRoll[a],bloomRoll[a+1],Mathf.SmoothStep(0,1,frame-a));
        }
        // Tangential clearance between adjacent petal tips at the current pose. Each
        // layer's own travel and delay turn it into an outer-to-inner wave cue.
        public float WaveRoom(float progress)
        {
            progress=Mathf.Clamp01(progress);
            float closed=TipGap(0),open=TipGap(1);
            return open>closed+1e-4f?Mathf.Clamp01(Mathf.InverseLerp(closed,open,TipGap(progress))):progress;
        }
        float TipGap(float progress)
        {
            float tilt=Mathf.Lerp(closedTilt,openTilt,Mathf.SmoothStep(0,1,progress))*Mathf.Deg2Rad;
            float reach=rootRadius+2.55f*size*Mathf.Sin(tilt);
            float centers=2f*reach*Mathf.Sin(Mathf.PI/Mathf.Max(1,count));
            float petalWidth=5.15f*size*width*Mathf.Sin(openingAngle*Mathf.Deg2Rad*.5f);
            return centers-petalWidth;
        }
        public float tangentOffset,depthOffset;
        [Tooltip("Small, static alternating root-height offsets; radial facing is unchanged.")]
        [Range(0,.12f)] public float rootStagger;
        [Range(0,3)] public float azimuthVariation=3,tiltVariation=3;
        [Range(0,4)] public float rollVariation=4;
        [Range(0,.04f)] public float widthVariation=.04f;
        [Range(0,.05f)] public float lengthVariation=.05f;
        [Tooltip("Optional static per-petal height, pitch and longitudinal scale. Missing entries preserve the original layout.")]
        public FlowerPetalOffset[] petalOffsets=Array.Empty<FlowerPetalOffset>();
        public FlowerPetalOffset OffsetAt(int index)=>petalOffsets!=null&&index>=0&&index<petalOffsets.Length?petalOffsets[index]:null;

        public void Constrain()
        {
            count=Mathf.Clamp(count,1,10); size=Mathf.Max(.1f,Clean(size,1));
            width=Mathf.Clamp(Clean(width,1),.4f,1); maximumCurl=Mathf.Clamp01(Clean(maximumCurl,1));
            edgeFoldScale=Mathf.Clamp01(Clean(edgeFoldScale,1));
            surfaceProfile=surfaceProfile.Constrained(); microNormalScale=Mathf.Clamp01(Clean(microNormalScale,1));
            curl=Mathf.Min(Mathf.Clamp01(Clean(curl,.2f)),maximumCurl); softness=Mathf.Clamp01(Clean(softness,.7f));
            openingAngle=Mathf.Clamp(Clean(openingAngle,125),90,150);
            closedTilt=Mathf.Clamp(Clean(closedTilt,50),0,90); openTilt=Mathf.Clamp(Clean(openTilt,60),0,90);
            roll=Mathf.Clamp(Clean(roll,0),-12,12);
            if(bloomRoll!=null) for(int i=0;i<bloomRoll.Length;i++) bloomRoll[i]=Mathf.Clamp(Clean(bloomRoll[i],roll),-12,12);
            azimuth=Clean(azimuth,0); rootRadius=Mathf.Max(0,Clean(rootRadius,.1f)); rootHeight=Clean(rootHeight,0);
            tangentOffset=Mathf.Clamp(Clean(tangentOffset,0),-.03f,.03f); depthOffset=Mathf.Clamp(Clean(depthOffset,0),-.03f,.03f);
            rootStagger=Mathf.Clamp(Clean(rootStagger,0),0,.12f);
            azimuthVariation=Mathf.Clamp(Clean(azimuthVariation,3),0,3); tiltVariation=Mathf.Clamp(Clean(tiltVariation,3),0,3);
            rollVariation=Mathf.Clamp(Clean(rollVariation,4),0,4); widthVariation=Mathf.Clamp(Clean(widthVariation,.04f),0,.04f);
            lengthVariation=Mathf.Clamp(Clean(lengthVariation,.05f),0,.05f);
            delay=Mathf.Max(0,Clean(delay,0)); duration=Mathf.Max(1,Clean(duration,12));
            if(petalOffsets!=null) foreach(var offset in petalOffsets) offset?.Constrain();
        }
        static float Clean(float v,float fallback)=>float.IsNaN(v)||float.IsInfinity(v)?fallback:v;
    }

    [CreateAssetMenu(menuName="MindState/Flower Preset")]
    public sealed class FlowerPreset : ScriptableObject
    {
        public int randomSeed=731;
        [Range(0,1)] public float colorVariation=0.035f;
        public FlowerLayer[] layers=Defaults();
        // Deserialize against defaults, not the previous asset's optional fields.
        // FromJsonOverwrite alone retains authored offsets when an older JSON omits them.
        public void ApplyLayoutJson(string json)
        {
            var fresh=CreateInstance<FlowerPreset>();
            try
            {
                JsonUtility.FromJsonOverwrite(json,fresh);
                randomSeed=fresh.randomSeed; colorVariation=fresh.colorVariation; layers=fresh.layers;
            }
            finally { if(Application.isPlaying) Destroy(fresh); else DestroyImmediate(fresh); }
        }
        public int PetalCount { get { int n=0; foreach(var l in layers) n+=l.count; return n; } }
        public float Duration { get { float t=1; foreach(var l in layers) t=Mathf.Max(t,l.delay+l.duration); return t; } }
        public static FlowerLayer[] Defaults()=>new[] {
            new FlowerLayer { name="Outer / 珍珠外裙",count=6,size=1,azimuth=0,rootRadius=.10f,rootHeight=0,closedTilt=54,openTilt=68,openingAngle=125,curl=.16f,softness=.82f,delay=0,pearl=new Color(.90f,.88f,.95f),root=new Color(.71f,.61f,.77f),pink=new Color(.96f,.81f,.87f) },
            new FlowerLayer { name="Middle / 淡粉花冠",count=5,size=.8f,azimuth=30,rootRadius=.08f,rootHeight=.12f,closedTilt=52,openTilt=50,openingAngle=118,curl=.30f,softness=.76f,delay=1.8f,pearl=new Color(.91f,.85f,.93f),root=new Color(.76f,.59f,.71f),pink=new Color(.98f,.77f,.84f) },
            new FlowerLayer { name="Inner / 浅紫花杯",count=3,size=.56f,azimuth=12,rootRadius=.06f,rootHeight=.25f,closedTilt=50,openTilt=40,openingAngle=110,curl=.38f,softness=.70f,delay=3.6f,pearl=new Color(.87f,.83f,.95f),root=new Color(.76f,.61f,.78f),pink=new Color(.96f,.79f,.86f) }
        };
    }
}

