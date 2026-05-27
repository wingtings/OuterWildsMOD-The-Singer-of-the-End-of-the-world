// Audio Wave 声波可视化 —— 以歌者为中心的环状声波网格,由实时 FFT 频谱驱动顶点位移
//
// 图形学要点(对应 README #4「声波可视化」):
//   * C# 端用 AudioListener.GetSpectrumData 做实时 FFT,把 32 个频段的幅值写进 _Spectrum[] 数组 uniform。
//   * 顶点着色器:按顶点角向坐标(uv.x)取对应频段幅值,沿法线方向位移 → 频谱"长"成起伏的声波环。
//     叠加一个随 uv.y 向外传播的正弦相位(_RippleSpeed),形成"以歌者为中心向外扩散"的涟漪。
//   * 片元着色器:频率(uv.x)→ HSV 色相,幅值 → 亮度(README 的颜色映射);加色混合发光。
//
// 用法:C# 生成一块环形(annulus)网格,法线朝"行星径向上",赋本材质;每帧 SetFloatArray("_Spectrum", ...)。
Shader "Custom/AudioWave"
{
    Properties
    {
        _BaseColor   ("基色调制", Color) = (0.5, 0.7, 1.0, 0.9)
        _Displacement("位移高度(随幅值)", Float) = 4.0
        _RippleSpeed ("涟漪向外传播速度", Float) = 2.5
        _RippleFreq  ("涟漪空间频率", Float) = 10.0
        _Brightness  ("发光强度", Float) = 2.5
        _Floor       ("本底起伏(无声时)", Range(0,1)) = 0.05
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Blend SrcAlpha One          // 加色发光
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            // 32 段频谱,由 C# 端 material.SetFloatArray("_Spectrum", ...) 写入
            uniform float _Spectrum[32];

            fixed4 _BaseColor;
            float _Displacement, _RippleSpeed, _RippleFreq, _Brightness, _Floor;

            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float  amp : TEXCOORD1;
            };

            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                // uv.x ∈ [0,1] 绕环一圈 → 频段索引; uv.y ∈ [0,1] 从内圈到外圈
                int bin = (int)floor(saturate(v.uv.x) * 31.0);
                float amp = max(_Spectrum[bin], _Floor);

                // 向外传播的涟漪相位(沿 uv.y),让环面像水波一样起伏扩散
                float ripple = sin(v.uv.y * _RippleFreq - _Time.y * _RippleSpeed) * 0.5 + 0.5;
                float disp = amp * _Displacement * (0.35 + 0.65 * v.uv.y) * ripple;

                float3 p = v.vertex.xyz + normalize(v.normal) * disp;
                o.pos = UnityObjectToClipPos(float4(p, 1.0));
                o.uv  = v.uv;
                o.amp = amp;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 频率 → 色相(低频偏蓝 0.62 → 高频偏品红 0.85),幅值 → 亮度
                float hue = lerp(0.62, 0.85, i.uv.x);
                float3 col = hsv2rgb(float3(hue, 0.8, 1.0)) * _BaseColor.rgb;

                float glow = saturate(i.amp * _Brightness);
                return fixed4(col * _Brightness * i.amp, glow * _BaseColor.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
