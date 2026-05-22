Shader "Custom/VolumetricRain"
{
    Properties
    {
        _MainTex ("Rain Drop Texture", 2D) = "white" {}
        _Color ("Rain Color", Color) = (0.7, 0.7, 0.8, 0.3)
        _Speed ("Fall Speed", Float) = 10.0
        _StretchFactor ("Motion Blur Stretch", Float) = 2.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float alpha : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _Color;
            float _Speed;
            float _StretchFactor;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 velocity = float3(0, -_Speed, 0);

                // 沿速度方向拉伸顶点 → 运动模糊效果
                float stretch = (v.uv.y - 0.5) * _StretchFactor;
                worldPos += normalize(velocity) * stretch;

                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = v.uv;
                o.alpha = 1.0 - abs(v.uv.y - 0.5) * 2.0;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                col.a *= i.alpha;
                return col;
            }
            ENDCG
        }
    }
}