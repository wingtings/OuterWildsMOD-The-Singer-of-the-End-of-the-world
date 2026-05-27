// Water Reflection 水面反射 / 折射 —— 平面反射相机 + 屏幕空间扰动折射
//
// 图形学原理(对应 README #5「水面反射与折射」):
//   * 平面反射:C# 端用一个"镜像相机"把场景按水面翻转渲染到 _ReflectionTex(RenderTexture)。
//     片元按本像素的屏幕坐标采样这张反射图 → 得到镜面倒影(可看到歌者的倒影 = 孤独主题的叙事暗示)。
//   * 折射扰动:用程序化正弦波纹(随时间流动)对采样 UV 做偏移,模拟水面波动下倒影/折射的晃动。
//   * Fresnel:视线越接近掠射角,反射越强(菲涅尔),正对水面时更多透出水色 → 真实水感。
//
// 用法:贴地的水面 Quad 用本材质;C# 的 PlanarReflectionController 负责每帧渲染并 SetTexture("_ReflectionTex")。
Shader "Custom/WaterReflection"
{
    Properties
    {
        _WaterColor    ("水色", Color) = (0.08, 0.14, 0.20, 0.9)
        _ReflectionTex ("反射图(C# 平面反射相机提供)", 2D) = "black" {}
        _Distort       ("折射扰动强度", Range(0, 0.1)) = 0.02
        _WaveScale     ("波纹空间频率", Float) = 6.0
        _WaveSpeed     ("波纹流动速度", Float) = 1.0
        _FresnelPower  ("Fresnel 强度", Float) = 4.0
        _ReflStrength  ("反射强度", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ReflectionTex;
            fixed4 _WaterColor;
            float _Distort, _WaveScale, _WaveSpeed, _FresnelPower, _ReflStrength;

            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNrm : TEXCOORD1;
                float4 screenPos: TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNrm = UnityObjectToWorldNormal(v.normal);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 程序化波纹梯度:两组错向正弦叠加 → 水面起伏的法线扰动
                float2 p = i.worldPos.xz * _WaveScale * 0.05;
                float t = _Time.y * _WaveSpeed;
                float2 grad;
                grad.x = cos(p.x * 1.3 + t) + 0.6 * cos(p.y * 2.1 - t * 1.3);
                grad.y = cos(p.y * 1.1 - t) + 0.6 * cos(p.x * 1.9 + t * 1.1);
                grad *= 0.5;

                float3 N = normalize(i.worldNrm + float3(grad.x, 0, grad.y) * 0.6);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // 反射图按屏幕坐标采样,并用波纹梯度做"折射扰动"
                float2 ruv = i.screenPos.xy / max(i.screenPos.w, 1e-4);
                ruv += grad * _Distort;
                fixed3 refl = tex2D(_ReflectionTex, ruv).rgb;

                // Fresnel:掠射角反射更强
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float k = saturate(_ReflStrength * (fres + 0.15));

                fixed3 col = lerp(_WaterColor.rgb, refl, k);
                float alpha = saturate(_WaterColor.a + fres * 0.3);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
