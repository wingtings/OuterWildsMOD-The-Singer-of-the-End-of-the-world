// Volumetric Fog 体积雾 —— 屏幕空间 Ray Marching + Beer-Lambert 大气散射(后处理)
//
// 图形学原理(对应 README #2「体积雾 / 大气散射」):
//   * 用深度图重建每个像素的世界射线:C# 端把相机四角的世界空间射线打进 _FrustumCornersWS,
//     片元用 uv 双线性插值得到本像素射线,再用 Linear01Depth 把它缩放到场景命中点。
//   * 沿这条射线从相机步进(Ray Marching),逐步采样雾密度 σ:
//       σ = _FogDensity · 高度衰减 · 3D 噪声(随时间流动)
//   * Beer-Lambert 定律累计透过率:每步 a = 1 - exp(-σ·Δt),透过率 T *= (1-a),
//       雾色按 (在此步新增的不透明度 × 当前透过率) 累加 → 物理上正确的"参与介质"积分近似。
//   * 合成:scene·T + 累计雾色。远处/天空因步进距离长而更"雾",符合大气透视。
//
// OW 内置渲染管线:走 OnRenderImage 全屏后处理;C# 端需开启 camera.depthTextureMode |= Depth。
Shader "Custom/VolumetricFog"
{
    Properties
    {
        _MainTex      ("Scene (由 Blit 提供)", 2D) = "white" {}
        _FogColor     ("雾色", Color) = (0.62, 0.66, 0.74, 1.0)
        _FogDensity   ("雾密度 σ0", Float) = 0.012
        _HeightFalloff("高度衰减(世界Y)", Float) = 0.0
        _FogBaseY     ("雾基准高度", Float) = 0.0
        _NoiseScale   ("3D 噪声缩放", Float) = 0.012
        _NoiseSpeed   ("噪声流动速度", Float) = 0.25
        _MaxDistance  ("最大步进距离", Float) = 600.0
        _Steps        ("步进次数", Int) = 24
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _CameraDepthTexture;

            float4x4 _FrustumCornersWS;   // 行0=BL 行1=BR 行2=TL 行3=TR (世界空间, 相机→远平面角射线)
            float4   _CameraWS;           // 相机世界坐标
            fixed4   _FogColor;
            float _FogDensity, _HeightFalloff, _FogBaseY, _NoiseScale, _NoiseSpeed, _MaxDistance;
            int   _Steps;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // —— 便宜的 3D 值噪声(hash + 三线性插值)——
            float hash31(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            float noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(i + float3(0,0,0));
                float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0));
                float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1));
                float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1));
                float n111 = hash31(i + float3(1,1,1));
                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 duv = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) duv.y = 1.0 - duv.y;
                #endif

                // 双线性插值四角射线得到本像素的世界空间射线(相机→远平面)
                float3 bl = _FrustumCornersWS[0].xyz;
                float3 br = _FrustumCornersWS[1].xyz;
                float3 tl = _FrustumCornersWS[2].xyz;
                float3 tr = _FrustumCornersWS[3].xyz;
                float3 ray = lerp(lerp(bl, br, i.uv.x), lerp(tl, tr, i.uv.x), i.uv.y);

                float lin01 = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv));

                float3 ro = _CameraWS.xyz;
                float3 hit = ro + ray * lin01;          // 场景命中点(天空时≈远平面)
                float3 dir = hit - ro;
                float dist = min(length(dir), _MaxDistance);
                dir = (dist > 1e-4) ? dir / length(dir) : float3(0,0,1);

                int steps = max(_Steps, 1);
                float stepLen = dist / steps;
                float t = _Time.y * _NoiseSpeed;

                float T = 1.0;               // 透过率
                float3 fog = 0.0;            // 累计雾色

                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 p = ro + dir * (stepLen * (s + 0.5));

                    float h = exp(-max(0.0, (p.y - _FogBaseY)) * _HeightFalloff);
                    float n = 0.55 + 0.45 * noise3(p * _NoiseScale + t);
                    float sigma = _FogDensity * h * n;

                    float a = 1.0 - exp(-sigma * stepLen);   // Beer-Lambert(本步不透明度)
                    fog += T * a * _FogColor.rgb;
                    T   *= (1.0 - a);
                    if (T < 0.01) break;                     // 已基本不透明,提前结束
                }

                fixed3 scene = tex2D(_MainTex, i.uv).rgb;
                return fixed4(scene * T + fog, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
