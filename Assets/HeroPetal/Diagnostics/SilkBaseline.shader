Shader "MindState/Diagnostics/Silk Baseline"
{
    Properties
    {
        _Color ("Pearl white", Color) = (0.87,0.88,0.94,1)
        _RootColor ("Root lavender", Color) = (0.60,0.48,0.70,1)
        _BumpMap ("Single radial filament normal", 2D) = "bump" {}
        _FineCount ("Radial fine pleats", Range(20,40)) = 32
        _BumpStrength ("Soft relief", Range(0,1)) = 0.18
        _Smoothness ("Silk polish", Range(0,1)) = 0.38
        _GrayMode ("Gray geometry preview", Float) = 0
        _Breath ("Breathing", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back ZWrite On
        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _BumpMap;
        fixed4 _Color,_RootColor;
        half _BumpStrength,_Smoothness,_FineCount,_GrayMode,_Breath;
        struct Input { float2 uv_BumpMap; float3 viewDir; float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void surf(Input IN,inout SurfaceOutputStandardSpecular o)
        {
            float2 uv=IN.uv_BumpMap;
            half fade=smoothstep(0.06,0.32,uv.y)*(1-0.45*smoothstep(0.85,1,uv.y));
            half3 n=UnpackNormal(tex2D(_BumpMap,float2(uv.x*_FineCount,uv.y)));
            n.xy*=_BumpStrength*fade; o.Normal=normalize(n);
            half grazing=pow(1-saturate(dot(normalize(IN.viewDir),o.Normal)),3);
            half3 base=lerp(_RootColor.rgb,_Color.rgb,smoothstep(0,0.75,uv.y));
            base=lerp(base,half3(0.70,0.68,0.85),grazing*0.14);
            o.Albedo=base; o.Specular=half3(0.045,0.043,0.052); o.Smoothness=_Smoothness; o.Occlusion=1;
            half3 wn=WorldNormalVector(IN,o.Normal),ld=normalize(UnityWorldSpaceLightDir(IN.worldPos));
            half transmission=pow(saturate(dot(-wn,ld)),2),rim=smoothstep(0.5,1,uv.y);
            o.Emission=base*0.14+half3(0.35,0.30,0.48)*grazing*(0.055+0.004*_Breath)
                       +half3(0.49,0.40,0.65)*transmission*(0.05+rim*0.09);
            if(_GrayMode>0.5)
            {
                o.Normal=half3(0,0,1); o.Albedo=half3(0.52,0.52,0.52);
                o.Specular=0.02; o.Smoothness=0.23; o.Emission=0.018;
            }
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Standard (Specular setup)"
}

