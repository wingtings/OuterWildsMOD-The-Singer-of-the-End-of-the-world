Shader "Custom/GodRays"
{
    Properties
    {
        _MainTex ("Screen Texture", 2D) = "white" {}
        _LightPos ("Light Screen Position", Vector) = (0.5, 0.5, 0, 0)
        _Intensity ("Ray Intensity", Float) = 0.5
        _Decay ("Decay", Float) = 0.95
        _Samples ("Sample Count", Int) = 64
    }

    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _LightPos;
            float _Intensity;
            float _Decay;
            int _Samples;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 deltaUV = (uv - _LightPos.xy) / _Samples;

                fixed4 color = tex2D(_MainTex, uv);
                float illuminationDecay = 1.0;

                for (int s = 0; s < _Samples; s++)
                {
                    uv -= deltaUV;
                    fixed4 sample = tex2D(_MainTex, uv);
                    sample *= illuminationDecay * _Intensity;
                    color += sample;
                    illuminationDecay *= _Decay;
                }

                return color;
            }
            ENDCG
        }
    }
}