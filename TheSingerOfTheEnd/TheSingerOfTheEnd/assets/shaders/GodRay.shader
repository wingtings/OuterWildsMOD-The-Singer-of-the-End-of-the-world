// God Rays / 丁达尔光束 —— 屏幕空间体积光散射(后处理)
//
// 图形学原理:Crepuscular Rays 用"径向模糊光散射"近似。
//   1) Occlusion(遮挡)pass:把"光源/天空"画亮,其余几何体画黑 → 得到一张只剩光的遮罩图。
//   2) RadialBlur(径向模糊)pass:从每个像素朝"光源屏幕坐标"方向步进采样并按 decay 衰减累加,
//      让光遮罩沿径向"拉出"成光束。这是 Kodeco/GPU Gems 的经典做法。
//   3) Composite(合成)pass:把光束图叠加(加色)回原始场景。
//
// 这个 shader 自身不决定顺序,由 C# 端用 Graphics.Blit 依次调用三个 Pass(见 logs 的打包指南)。
// OW 用的是 Unity 内置渲染管线,因此走 OnRenderImage 全屏后处理,而不是 URP RendererFeature。
Shader "Custom/GodRays"
{
    Properties
    {
        _MainTex     ("Source (由 Blit 提供)", 2D) = "white" {}
        _SceneTex    ("Original Scene (合成 pass 用)", 2D) = "black" {}
        _LightPos    ("光源屏幕坐标 (0..1, xy)", Vector) = (0.5, 0.5, 0, 0)
        _RayColor    ("光束染色", Color) = (1.0, 0.95, 0.8, 1.0)
        _Intensity   ("合成强度", Float) = 1.0
        _Density     ("采样步距密度", Float) = 0.9
        _Weight      ("单次采样权重", Float) = 0.6
        _Decay       ("衰减系数", Float) = 0.95
        _Exposure    ("曝光", Float) = 1.0
        _DepthThreshold ("天空判定阈值 (Linear01)", Float) = 0.99
        _Samples     ("采样次数", Int) = 64
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // ---------- Pass 0 : Occlusion(从深度图提取"天空=光源")----------
        Pass
        {
            Name "Occlusion"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;   // 需 C# 端开启 camera.depthTextureMode |= Depth
            float _DepthThreshold;
            fixed4 _RayColor;

            fixed4 frag (v2f_img i) : SV_Target
            {
                // Linear01Depth: 0=近, 1=远(天空盒)。远处即视为光源所在。
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth01  = Linear01Depth(rawDepth);

                fixed4 scene = tex2D(_MainTex, i.uv);
                // 是天空 → 保留场景颜色(亮天)作为光;是实体几何 → 涂黑(遮挡)
                float isSky = step(_DepthThreshold, depth01);
                return scene * isSky * _RayColor;
            }
            ENDCG
        }

        // ---------- Pass 1 : Radial Blur(把光遮罩沿径向拉成光束)----------
        Pass
        {
            Name "RadialBlur"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _LightPos;
            float _Density, _Weight, _Decay, _Exposure;
            int _Samples;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                // 朝光源方向的步进向量;_Density 控制光束长度
                float2 deltaUV = (uv - _LightPos.xy) * (_Density / _Samples);

                fixed4 color = tex2D(_MainTex, uv);
                float illuminationDecay = 1.0;

                // 沿径向逐步采样,越往光源走衰减越多 → 形成由亮到暗的放射光束
                [loop]
                for (int s = 0; s < _Samples; s++)
                {
                    uv -= deltaUV;
                    fixed4 samp = tex2D(_MainTex, uv);
                    samp *= illuminationDecay * _Weight;
                    color += samp;
                    illuminationDecay *= _Decay;
                }
                return color * _Exposure;
            }
            ENDCG
        }

        // ---------- Pass 2 : Composite(光束加色叠回原场景)----------
        Pass
        {
            Name "Composite"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;    // 上一阶段的光束图
            sampler2D _SceneTex;   // C# 端传入的原始场景
            float _Intensity;

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 rays  = tex2D(_MainTex, i.uv);
                fixed4 scene = tex2D(_SceneTex, i.uv);
                // 加色混合:光束只增亮、不压暗,符合"光线穿透"的视觉
                return scene + rays * _Intensity;
            }
            ENDCG
        }
    }
    Fallback Off
}
