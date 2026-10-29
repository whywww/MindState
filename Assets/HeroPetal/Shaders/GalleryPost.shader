Shader "Hidden/MindState/GalleryPost"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BloomTex;
        float4 _MainTex_TexelSize;
        float2 _Direction;
        float _Bloom, _Exposure;
        half4 threshold(v2f_img i) : SV_Target
        {
            half3 c=tex2D(_MainTex,i.uv).rgb;
            half b=max(c.r,max(c.g,c.b));
            return half4(c * max(0,b-1.05)/max(b,0.001),1);
        }
        half4 blur(v2f_img i) : SV_Target
        {
            float2 d=_MainTex_TexelSize.xy*_Direction;
            half3 c=tex2D(_MainTex,i.uv).rgb*0.227027;
            c+=(tex2D(_MainTex,i.uv+d*1.384615).rgb+tex2D(_MainTex,i.uv-d*1.384615).rgb)*0.316216;
            c+=(tex2D(_MainTex,i.uv+d*3.230769).rgb+tex2D(_MainTex,i.uv-d*3.230769).rgb)*0.070270;
            return half4(c,1);
        }
        half4 composite(v2f_img i) : SV_Target
        {
            half3 c=(tex2D(_MainTex,i.uv).rgb+tex2D(_BloomTex,i.uv).rgb*_Bloom)*_Exposure;
            // ACES fitted filmic response; applied in linear space.
            c=saturate((c*(2.51*c+0.03))/(c*(2.43*c+0.59)+0.14));
            float2 q=(i.uv-0.5)*float2(1.0,0.8);
            c*=1-dot(q,q)*0.28;
            return half4(c,1);
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment threshold
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            ENDCG }
    }
}
