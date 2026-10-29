Shader "MindState/Radial Pearl Silk"
{
    Properties
    {
        _Color ("Pearl white", Color) = (0.88,0.89,0.94,1)
        _RootColor ("Root lavender", Color) = (0.73,0.65,0.79,1)
        _PinkColor ("Petal blush", Color) = (0.95,0.78,0.85,1)
        _BumpMap ("Radial fine pleat normal", 2D) = "bump" {}
        _DetailMask ("R polish / G blush / B thinness / A relief", 2D) = "gray" {}
        _FineCount ("Primary fine pleats", Range(20,40)) = 32
        _BumpStrength ("Soft micro relief", Range(0,1)) = 0.30
        _Smoothness ("Silk polish", Range(0,1)) = 0.30
        _SmoothnessVariation ("Local polish variation", Range(0,0.1)) = 0.06
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
        sampler2D _BumpMap,_DetailMask;
        fixed4 _Color,_RootColor,_PinkColor;
        half _BumpStrength,_Smoothness,_SmoothnessVariation,_FineCount,_GrayMode,_Breath;
        struct Input { float2 uv_BumpMap; float3 viewDir; float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void surf(Input IN,inout SurfaceOutputStandardSpecular o)
        {
            float2 uv=IN.uv_BumpMap;
            float2 detailUV=float2(uv.x*(_FineCount/32.0),uv.y);
            half4 detail=tex2D(_DetailMask,detailUV);
            half footprint=fwidth(detailUV.x)*96;
            half fade=smoothstep(0.06,0.28,uv.y)*(1-0.35*smoothstep(0.85,1,uv.y));
            fade*=1-smoothstep(0.32,0.85,footprint);
            half3 n=UnpackNormal(tex2D(_BumpMap,detailUV));
            n.xy*=_BumpStrength*fade*lerp(0.8,1.1,detail.a);
            o.Normal=normalize(n);
            half f=1-saturate(dot(normalize(IN.viewDir),o.Normal));
            half sheen=f*f;
            half3 base=lerp(_RootColor.rgb,_Color.rgb,smoothstep(0,0.8,uv.y));
            half blush=(0.02+0.055*(1-uv.y))*detail.g;
            base=lerp(base,_PinkColor.rgb,blush);
            base=lerp(base,half3(0.77,0.75,0.89),sheen*0.09);
            o.Albedo=base;
            o.Specular=half3(0.029,0.028,0.033);
            o.Smoothness=clamp(_Smoothness+(detail.r*2-1)*_SmoothnessVariation,0.18,0.44);
            o.Occlusion=1;
            half3 wn=WorldNormalVector(IN,o.Normal);
            half3 ld=normalize(UnityWorldSpaceLightDir(IN.worldPos));
            half back=saturate(dot(-wn,ld)); back*=back;
            half thin=lerp(0.035,0.14,detail.b);
            // A bounded ambient lift plus light-dependent backscatter, not alpha transparency.
            o.Emission=base*0.08+half3(0.38,0.35,0.49)*sheen*(0.035+0.003*_Breath)
                       +base*_LightColor0.rgb*back*thin;
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
