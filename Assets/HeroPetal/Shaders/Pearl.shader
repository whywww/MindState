Shader "MindState/Radial Pearl Silk"
{
    Properties
    {
        _TargetAppearance ("Target palette enabled", Float) = 0
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
        _ArtWeights ("Art weights", Vector) = (0,0,0,0)
        _ArtClock ("Art clock", Float) = 0
        _ArtPhase ("Art phase", Float) = 0
        _StateTint ("State tint (brightness, saturation)", Vector) = (1,1,0,0)
        _StateAccent ("State accent", Color) = (1,1,1,1)
        _StateSurface ("State surface (hue, rim, glow, gloss)", Vector) = (0,0,0,0)
        _Breath ("Breathing", Float) = 0
        _RootTrim ("Petal root cut", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back ZWrite On
        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _BumpMap,_DetailMask,_FlowPalette; float4 _FlowSettings;
        fixed4 _Color,_RootColor,_PinkColor;
        half _BumpStrength,_Smoothness,_SmoothnessVariation,_FineCount,_GrayMode,_Breath;
        float4 _RootTrim; float4x4 _RootTrimWorldToFlower;
        float _TargetAppearance; float4 _RoseLinear,_PeachLinear,_LavenderLinear,_PearlLinear,_CyanLinear,_AppearancePlacement,_AppearanceSurface,_ArtLightWeights;
        float4 _ArtWeights; float _ArtClock,_ArtPhase;
        float4 _StateTint,_StateSurface; half4 _StateAccent;
        struct Input { float2 uv_BumpMap; float3 viewDir; float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void surf(Input IN,inout SurfaceOutputStandardSpecular o)
        {
            if(_RootTrim.x>.5) {
                float3 root=mul(_RootTrimWorldToFlower,float4(IN.worldPos,1)).xyz;
                float radius=length(root.xz);
                if(IN.uv_BumpMap.y<=.25 || radius<_RootTrim.z*1.15) clip(root.y-(_RootTrim.y+.004));
                if(IN.uv_BumpMap.y<=.25 && root.y<_RootTrim.w) clip(_RootTrim.z-radius);
            }
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
            float outward=pow(.5+.5*cos(uv.y*6.283-_ArtClock*.65),4);
            float inward=pow(.5+.5*cos(uv.y*6.283+_ArtClock*1.256-_ArtPhase*.2),4);
            float drifting=.5+.5*sin(uv.x*3+uv.y*2-_ArtClock*.45+_ArtPhase);
            float quiet=.5+.5*sin(_ArtClock*.448-_ArtPhase);
            float sheenMask=dot(_ArtWeights,float4(outward,inward,drifting,quiet*.5));
            if(_TargetAppearance>.5)
            {
                // Natural root-to-rim gradient with a soft pearl edge and subtle fresnel iridescence.
                float3 rootCol=lerp(_RoseLinear.rgb,_PeachLinear.rgb,saturate(.40+.35*detail.g+.15*(1-uv.x))*.65);
                float3 bodyCol=lerp(_RoseLinear.rgb,_PeachLinear.rgb,.18+.38*_AppearancePlacement.x);
                float3 col=lerp(rootCol,bodyCol,smoothstep(.05,.50,uv.y));
                col=lerp(col,_PearlLinear.rgb,smoothstep(.66,.99,uv.y)*(.30+.14*_AppearancePlacement.y));
                col*=lerp(.94,1.06,detail.r);
                col=lerp(col,_LavenderLinear.rgb,.08*saturate(_AppearancePlacement.z*.5));
                // Keep the pearl luminance while pulling gently toward the state accent.
                float lum=dot(col,float3(.2126,.7152,.0722));
                float accentLum=max(dot(_StateAccent.rgb,float3(.2126,.7152,.0722)),.001);
                col=lerp(col,_StateAccent.rgb*(lum/accentLum),saturate(_StateSurface.x));
                float gray=dot(col,float3(.2126,.7152,.0722));
                col=lerp(gray.xxx,col,saturate(_StateTint.y))*_StateTint.x;
                o.Albedo=col*1.04;
                o.Smoothness=clamp(_AppearanceSurface.x+.06+(detail.r*2-1)*.035+_StateSurface.w,.22,.55);
                o.Specular=half3(.040,.038,.044);
                float fres=pow(1-saturate(dot(normalize(IN.viewDir),o.Normal)),3);
                float irid=fres*(.30+.70*detail.b)*_AppearanceSurface.z;
                float3 iridCol=lerp(_CyanLinear.rgb,_LavenderLinear.rgb,.35+.35*sin(uv.x*6.283+_ArtClock*.10));
                o.Emission=col*.055+iridCol*irid*.12+col*_LightColor0.rgb*back*_AppearanceSurface.y*.35
                           +_StateAccent.rgb*fres*_StateSurface.y+col*_StateSurface.z;
                o.Smoothness=clamp(o.Smoothness+.04*sheenMask,.22,.55);
                o.Emission+=o.Albedo*.025*sheenMask;
            }
            else
            {
                o.Smoothness=clamp(o.Smoothness+.04*sheenMask,.18,.44);
                o.Emission+=base*.025*sheenMask;
            }
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
