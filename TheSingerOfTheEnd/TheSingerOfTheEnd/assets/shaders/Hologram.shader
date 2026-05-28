// Hologram 全息投影 —— 神谕之境的信息展示面板(对应 README #6「全息投影着色器」)
//
// 图形学要点:
//   * 扫描线(Scanlines):基于"世界坐标 Y 轴"的正弦函数(README 明确要求),随时间滚动 →
//     经典 CRT/全息的水平扫描带。用世界 Y 而非 UV,使扫描带在物体旋转时仍稳定贴在世界空间。
//   * 边缘发光(Fresnel):视线方向越接近表面切线,菲涅尔项越大 → 面板边缘/掠射处更亮,
//     模拟全息体的"光场只在边缘汇聚"的观感。
//   * 故障抖动(Glitch):在顶点着色器里按"行块(uv.y 分块)+ 时间块"取 hash 噪声,
//     周期性地把该行的 uv.x 水平错位 → 偶发的水平撕裂/抖动。
//   * 半透明叠加:Blend SrcAlpha OneMinusSrcAlpha + ZWrite Off,叠在场景之上;
//     发光色 _HoloColor/_RimColor 让它在偏暗背景上"自发光"。
//
// 用法:C# (HologramController) 生成一块竖直面板网格,赋本材质;_MainTex 可选放要展示的图(留空=纯发光)。
Shader "Custom/Hologram"
{
    Properties
    {
        _MainTex          ("内容贴图(可选)", 2D) = "white" {}
        _HoloColor        ("全息主色", Color) = (0.30, 0.90, 1.00, 1.0)
        _RimColor         ("边缘辉光色", Color) = (0.65, 1.00, 1.00, 1.0)
        _RimPower         ("Fresnel 边缘锐度", Range(0.5, 8)) = 3.0
        _ScanCount        ("扫描线密度(世界Y)", Float) = 18.0
        _ScanSpeed        ("扫描线滚动速度", Float) = 2.0
        _ScanStrength     ("扫描线明暗强度", Range(0,1)) = 0.55
        _GlitchStrength   ("故障抖动强度", Range(0,0.3)) = 0.04
        _GlitchSpeed      ("故障频率", Float) = 8.0
        _Alpha            ("整体不透明度", Range(0,1)) = 0.65
        _Brightness       ("发光强度", Float) = 1.6
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _HoloColor, _RimColor;
            float _RimPower, _ScanCount, _ScanSpeed, _ScanStrength;
            float _GlitchStrength, _GlitchSpeed, _Alpha, _Brightness;

            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f
            {
                float4 pos         : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir     : TEXCOORD3;
            };

            // 轻量 hash 噪声(给 glitch 用,无需贴图)
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            v2f vert (appdata v)
            {
                v2f o;

                // —— Glitch:按行块(uv.y 切 12 块)+ 时间块取噪声,偶发地水平抖动该行 ——
                float block   = floor(v.uv.y * 12.0);
                float tBlock  = floor(_Time.y * _GlitchSpeed);
                float g       = (hash11(block + tBlock) - 0.5) * 2.0;            // [-1,1]
                float trigger = step(0.80, hash11(tBlock * 1.7 + block * 0.37)); // 仅部分时间触发
                float2 uv = v.uv;
                uv.x += g * _GlitchStrength * trigger;

                o.uv          = TRANSFORM_TEX(uv, _MainTex);
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.worldPos    = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir     = normalize(_WorldSpaceCameraPos - o.worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                // 扫描线:基于世界 Y 的正弦,随时间滚动(README #6)
                float scan     = sin(i.worldPos.y * _ScanCount - _Time.y * _ScanSpeed) * 0.5 + 0.5;
                float scanMask = lerp(1.0, scan, _ScanStrength);

                // Fresnel 边缘光:视线越接近切线越亮
                float ndv  = saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                float fres = pow(1.0 - ndv, _RimPower);

                float3 col = _HoloColor.rgb * tex.rgb * scanMask;   // 主体(受扫描线调制)
                col += _RimColor.rgb * fres;                        // 边缘辉光
                col *= _Brightness;

                // alpha:基底×内容×扫描 + 边缘项 → 边缘与亮扫描带更实,暗带更透
                float a = _Alpha * tex.a * scanMask + fres * _RimColor.a;
                return fixed4(col, saturate(a));
            }
            ENDCG
        }
    }
    Fallback Off
}
