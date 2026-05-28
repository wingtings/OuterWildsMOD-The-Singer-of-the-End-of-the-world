// Rain Ripple 积水涟漪 —— 贴在地面积水/水洼上的半透明水面材质
//
// 图形学要点(对应 README 的"涟漪法线:多层正弦波叠加的法线扰动,随时间扩散衰减"):
//   * 程序化涟漪:用若干个"扩散环"叠加成高度场 h(x,z,t)。每个环是一条沿半径传播、
//     随距离指数衰减的正弦波 sin(d*freq - t*speed)*exp(-d) → 雨滴落点向外扩散的水纹。
//   * 法线扰动:对高度场求屏幕空间偏导 ddx/ddy 得到扰动法线,不需要法线贴图。
//   * Fresnel 水面:掠射角更亮(菲涅尔),配合扰动法线的高光,得到"湿/反光"的水洼质感。
//   * 倒影(可选):_ReflectionTex 由 C# 端的平面反射相机渲染传入;无则退化为纯水色+高光。
//
// 用法:做一块贴地的薄四边形(Quad)当水洼,Renderer 用本材质;C# 端可随雨量调 _RippleStrength。
Shader "Custom/RainRipple"
{
    Properties
    {
        _WaterColor    ("水色", Color) = (0.15, 0.2, 0.28, 0.6)
        _ReflectionTex ("平面反射图(可选)", 2D) = "black" {}
        _RippleStrength("涟漪强度(法线扰动)", Range(0,2)) = 1.0
        _RippleScale   ("涟漪频率", Float) = 12.0
        _RippleSpeed   ("扩散速度", Float) = 3.0
        _RippleFalloff ("距离衰减", Float) = 2.0
        _SpecColor2    ("高光颜色", Color) = (1,1,1,1)
        _Shininess     ("高光锐度", Range(1,128)) = 48
        _FresnelPower  ("Fresnel 强度", Float) = 4.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off          // 地面贴片双面可见,避免因正反面判定而整片消失

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNrm : TEXCOORD1;
                float4 screenPos: TEXCOORD2;
            };

            sampler2D _ReflectionTex;
            fixed4 _WaterColor, _SpecColor2;
            float _RippleStrength, _RippleScale, _RippleSpeed, _RippleFalloff, _Shininess, _FresnelPower;

            // 单个扩散环:中心 c,在水面参数坐标 p 处的瞬时高度
            float ripple(float2 p, float2 c, float t)
            {
                float d = distance(p, c);
                // sin 沿半径传播;exp 让远处的波纹变弱(能量扩散)
                float wave = sin(d * _RippleScale - t * _RippleSpeed);
                return wave * exp(-d * _RippleFalloff);
            }

            // 多层叠加的高度场:几个错相位的落点同时扩散
            float heightField(float2 p)
            {
                float t = _Time.y;
                float h = 0;
                h += ripple(p, float2(0.25, 0.30), t);
                h += ripple(p, float2(0.70, 0.65), t + 1.7);
                h += ripple(p, float2(0.45, 0.85), t + 3.1);
                h += ripple(p, float2(0.85, 0.15), t + 4.6);
                return h * 0.25;
            }

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
                // 用世界 XZ 作为水面参数坐标,缩放到 0..1 量级
                float2 p = i.worldPos.xz * 0.05;
                float h = heightField(p) * _RippleStrength;

                // 屏幕空间偏导近似高度场梯度 → 扰动法线
                float dhx = ddx(h);
                float dhy = ddy(h);
                float3 N = normalize(i.worldNrm + float3(dhx, 0, dhy) * 8.0);

                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);

                // Blinn-Phong 高光(被涟漪法线扰动后会闪烁,像水面碎光)
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), _Shininess);

                // Fresnel:视线越平,水面越亮越反光
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // 倒影(若 C# 提供反射图);扰动法线让倒影随波纹晃动
                float2 reflUV = i.screenPos.xy / i.screenPos.w + float3(dhx, dhy, 0).xy * 4.0;
                fixed3 refl = tex2D(_ReflectionTex, reflUV).rgb;

                fixed3 col = _WaterColor.rgb + refl * fresnel + spec * _SpecColor2.rgb;
                float alpha = saturate(_WaterColor.a + fresnel * 0.4 + spec);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
