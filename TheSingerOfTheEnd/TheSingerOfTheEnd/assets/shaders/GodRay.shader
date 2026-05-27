// God Rays / 丁达尔光束 —— 屏幕空间体积光散射(后处理)
//
// 图形学原理:Crepuscular Rays 用"径向模糊光散射"近似(GPU Gems / Kodeco 经典做法)。
//   1) Occlusion(遮挡)pass:在"太阳屏幕位置"附近取一块圆形亮源(只有天空像素算亮,
//      实体几何算黑),得到一张以太阳为中心、被场景遮挡切割出空隙的光遮罩。
//      —— 关键修复:亮源被限制成"太阳附近的圆盘"且强度上限 = _RayColor(≤1),
//         不再把 HDR 的太阳本体原样喂进去,从根本上避免"过曝/整屏发白"。
//   2) RadialBlur(径向模糊)pass:从每个像素朝光源方向步进采样、按 decay 衰减累加,
//      把光遮罩沿径向"拉出"成光束。
//   3) Composite(合成)pass:用 Screen(滤色)混合叠回原场景。Screen 混合数学上不会超过 1,
//      因此再亮也不会糊成纯白 —— 这是"圣光过亮"的第二道保险。
//
// 顺序由 C# 端用 Graphics.Blit 依次调用三个 Pass(见 logs 的打包指南)。
// OW 用内置渲染管线,所以走 OnRenderImage 全屏后处理,而非 URP RendererFeature。
//
// 关于"只有面朝太阳才有光":屏幕空间方法本质上需要光源在(或接近)画面内才能拉出光束。
//   C# 端已把太阳屏幕坐标"夹"到屏幕边缘并按偏离程度淡出,使太阳偏在侧方时光束仍从边缘射入,
//   大幅拓宽可见角度;但当太阳完全位于身后时无光源可拉,这在物理上也是正确的(背对太阳没有丁达尔光)。
//   若要"任意角度都笼罩歌者的光柱",应改用世界空间的体积光锥(另一种效果)。
Shader "Custom/GodRays"
{
    Properties
    {
        _MainTex     ("Source (由 Blit 提供)", 2D) = "white" {}
        _SceneTex    ("Original Scene (合成 pass 用)", 2D) = "black" {}
        _LightPos    ("光源屏幕坐标 (0..1, xy; z>0=前方)", Vector) = (0.5, 0.5, 1, 0)
        _RayColor    ("光束染色(同时是亮源上限)", Color) = (1.0, 0.92, 0.78, 1.0)
        _Intensity   ("合成强度", Float) = 0.35
        _SourceRadius("亮源圆盘半径(屏幕比例)", Float) = 0.22
        _Density     ("采样步距密度(光束长度)", Float) = 0.7
        _Weight      ("单次采样权重", Float) = 0.45
        _Decay       ("衰减系数", Float) = 0.95
        _Exposure    ("曝光(总强度收束)", Float) = 0.22
        _DepthThreshold ("天空判定阈值 (Linear01)", Float) = 0.99
        _Samples     ("采样次数", Int) = 48
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // ---------- Pass 0 : Occlusion(以太阳为中心取一块有界亮源)----------
        Pass
        {
            Name "Occlusion"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;   // 需 C# 端开启 camera.depthTextureMode |= Depth
            float4 _MainTex_TexelSize;
            float _DepthThreshold, _SourceRadius;
            fixed4 _RayColor;
            float4 _LightPos;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 depthUV = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0)
                    depthUV.y = 1.0 - depthUV.y;
                #endif

                // Linear01Depth: 0=近, 1=远(天空盒)。只有天空像素可以成为光源。
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, depthUV);
                float depth01  = Linear01Depth(rawDepth);
                float isSky    = step(_DepthThreshold, depth01);

                // 到太阳屏幕坐标的距离(纵横比校正),圆盘内才算亮源 → 光束自太阳辐射而出
                float2 d = (i.uv - _LightPos.xy);
                d.x *= _ScreenParams.x / _ScreenParams.y;
                float disk = saturate(1.0 - length(d) / max(_SourceRadius, 1e-3));
                disk *= disk;                       // 二次衰减,中心更聚拢、更自然

                float forward = step(0.0, _LightPos.z);   // 太阳在前方才生成亮源

                // 亮源上限就是 _RayColor(≈1),实体几何(isSky=0)在此处切出阴影空隙
                return _RayColor * (isSky * disk * forward);
            }
            ENDCG
        }

        // ---------- Pass 1 : Radial Blur(把有界亮源沿径向拉成光束)----------
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
                float validDensity = _LightPos.z > 0 ? _Density : 0.0;
                float2 deltaUV = (uv - _LightPos.xy) * (validDensity / _Samples);

                fixed4 color = tex2D(_MainTex, uv);
                float illuminationDecay = 1.0;

                // 沿径向逐步采样,越往光源走衰减越多 → 形成由亮到暗的放射光束
                [loop]
                for (int s = 0; s < _Samples; s++)
                {
                    uv -= deltaUV;
                    fixed4 samp = tex2D(_MainTex, uv);
                    color += samp * (illuminationDecay * _Weight);
                    illuminationDecay *= _Decay;
                }
                // _Exposure 收束总能量,避免多次采样累加导致过曝
                return color * _Exposure;
            }
            ENDCG
        }

        // ---------- Pass 2 : Composite(Screen 滤色混合叠回原场景,不会过曝)----------
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
                fixed3 scene = tex2D(_SceneTex, i.uv).rgb;

                // 先把光束限制在 [0,1],再用 Screen 混合:result = 1-(1-a)(1-b)
                // Screen 永远不会超过 1,因此无论光束多强都不会糊成死白,只会自然提亮。
                fixed3 rayCol = saturate(rays.rgb * _Intensity);
                fixed3 outCol = 1.0 - (1.0 - scene) * (1.0 - rayCol);
                return fixed4(outCol, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
