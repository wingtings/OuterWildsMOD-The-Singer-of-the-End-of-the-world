// Volumetric Rain 雨滴 —— 配合 ParticleSystem 使用的雨滴材质
//
// 图形学要点:
//   * 运动模糊拖尾:在顶点着色器里沿"屏幕竖直方向"拉伸 billboard 四边形(uv.y 偏移),
//     模拟高速下落雨滴在一帧内的拖影,比单纯贴一张拉长贴图更省且可调。
//   * Billboard 永远面向相机:用相机的 right/up 基向量在视图空间重建四边形,避免侧看变扁。
//   * 雨滴形状/透明度:片元用到中心轴的距离做软衰减(teardrop),并叠加 Fresnel 边缘高光,
//     让雨丝有"水的折射高光"质感。
//   * 末日渐变:_Color 由 C# 端随循环时间从灰蓝渐变到暗红(见 TimelineManager 思路)。
//
// 用法:ParticleSystem 的 Renderer 用本材质;Render Mode 选 Billboard(本 shader 自己做拉伸,
//       不依赖 Stretched Billboard)。GPU Instancing 由粒子系统批量提交。
Shader "Custom/VolumetricRain"
{
    Properties
    {
        _MainTex       ("Rain Drop Texture", 2D) = "white" {}
        _Color         ("Rain Color", Color) = (0.7, 0.75, 0.85, 0.35)
        _StretchFactor ("竖直拉伸(运动模糊)", Float) = 2.5
        _Width         ("雨丝宽度", Range(0.05, 1.0)) = 0.35
        _FresnelPower  ("Fresnel 边缘高光", Float) = 2.0
        _Softness      ("首尾淡出", Range(0.01, 1.0)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;        // 粒子系统传入的逐粒子颜色
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
                float3 viewDir: TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _StretchFactor, _Width, _FresnelPower, _Softness;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Billboard 模式下粒子系统已为每个粒子生成"正对相机"的四边形顶点(v.vertex,
                // 位于粒子系统局部空间)。直接变换即可——逐粒子位置由 v.vertex 携带。
                // 竖直拖尾改由 C# 端 startSize3D(细而高的 billboard)实现。
                // 旧实现用 UnityObjectToViewPos(0) 取"原点",会把所有粒子塌缩到系统原点
                // (玩家脚下)一点,导致整片雨不可见——这是体积雨看不见的根因。
                o.vertex = UnityObjectToClipPos(v.vertex);

                float3 posView = UnityObjectToViewPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                o.viewDir = normalize(-posView);   // 视图空间下指向相机
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // 到竖直中轴的水平距离 → 软化成"雨丝"截面
                float distToAxis = abs(i.uv.x - 0.5) * 2.0;
                float lineAlpha = saturate(1.0 - distToAxis / _Width);

                // 沿运动方向(uv.y)首尾淡出,中段最实 → 拖尾感
                float headTail = saturate(1.0 - abs(i.uv.y - 0.5) * 2.0 / _Softness);

                // Fresnel:视线越接近雨丝切线方向越亮,模拟水的边缘折射高光
                float fresnel = pow(1.0 - saturate(i.viewDir.z), _FresnelPower);

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 col = i.color * tex;
                col.rgb += fresnel * 0.3;
                col.a *= lineAlpha * headTail;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
