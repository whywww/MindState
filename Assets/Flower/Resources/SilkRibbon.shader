Shader "MindState/Silk Ribbon"
{
    Properties
    {
        _Color ("Silk light", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 0.5
        _Width ("Width", Float) = 0.03
        _Core ("Core sharpness", Range(1,32)) = 3
        _MaskCenter ("Mask center", Vector) = (0,0,0,0)
        _MaskRadius ("Mask radius", Float) = 0
        _TrailPower ("Trail falloff", Range(0.5,4)) = 2.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color,_MaskCenter;
            float _Intensity,_Width,_Core,_MaskRadius,_TrailPower;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                float3 tangent=normalize(mul((float3x3)unity_ObjectToWorld,v.normal));
                float3 view=normalize(_WorldSpaceCameraPos-world);
                float3 side=cross(tangent,view);
                side=dot(side,side)>1e-6?normalize(side):float3(1,0,0);
                world+=side*(v.uv.y*2-1)*_Width;
                o.pos=mul(UNITY_MATRIX_VP,float4(world,1));
                o.uv=v.uv;
                o.world=world;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                // The thread may never light up the bloom's own silhouette.
                float radial=length(i.world.xz-_MaskCenter.xz);
                float keep=_MaskRadius>.0001?smoothstep(_MaskRadius,_MaskRadius*1.08,radial):1;
                float d=saturate(1-abs(i.uv.y*2-1));
                float core=pow(d,_Core);
                float halo=pow(d,2);
                // uv.x runs from the fading tail (0) to the bright head (1); the head gets a small
                // extra boost so the stroke reads as a pen tip moving along the route.
                float trail=pow(saturate(i.uv.x),_TrailPower);
                float tip=smoothstep(.88,1.,i.uv.x);
                float alpha=(core*.70+halo*.40)*_Intensity*keep*trail*(.78+.65*tip);
                return float4(_Color.rgb*alpha,0);
            }
            ENDCG
        }
    }
    Fallback Off
}
