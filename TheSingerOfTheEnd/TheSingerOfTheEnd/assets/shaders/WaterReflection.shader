// Water Reflection 水面反射 / 折射 —— 平面反射相机 + 屏幕空间扰动折射 + 球面弯曲
//
// 图形学原理(对应 README #5「水面反射与折射」):
//   * 平面反射:C# 端用一个"镜像相机"按水面切平面翻转渲染到 _ReflectionTex(RenderTexture)。
//     片元按本像素的屏幕坐标采样这张反射图 → 得到镜面倒影(可看到歌者的倒影 = 孤独主题的叙事暗示)。
//   * 折射扰动:用程序化波纹(随时间流动)对采样 UV 做偏移,模拟水面波动下倒影的晃动。
//   * Schlick Fresnel:水面 F0≈0.02,视线越接近掠射角反射越强。
//
// 星球曲面适配:
//   顶点着色器若收到有效 _PlanetRadius,会把顶点投影到该半径的球面上,
//   使一块铺得较大的水池自然贴合星球地表(法线 = 径向);否则退化为普通平面。
//   配合 C# 端把网格做大并细分(20+ 顶点)效果最佳。
//
// 用法:贴地的水面 Mesh 用本材质;C# 的 PlanarReflectionController 负责:
//   1) 每帧渲染镜像相机并 SetTexture("_ReflectionTex");
//   2) SetVector("_PlanetCenter", planet.position);
//   3) SetFloat("_PlanetRadius", radius)。
Shader "Custom/WaterReflection"
{
    Properties
    {
        _WaterColor    ("水色(浅·正视)",    Color)        = (0.10, 0.16, 0.22, 0.85)
        _DeepColor     ("深水色(掠射融合)", Color)        = (0.02, 0.05, 0.10, 1.00)
        _ReflectionTex ("反射图(C# 平面反射相机提供)", 2D) = "black" {}
        _Distort       ("折射扰动强度",     Range(0, 0.1))= 0.02
        _WaveScale     ("波纹空间频率",     Float)        = 6.0
        _WaveSpeed     ("波纹流动速度",     Float)        = 1.0
        _BumpStrength  ("法线扰动幅度",     Range(0, 2))  = 0.6
        _FresnelF0     ("Fresnel F0",       Range(0,0.2)) = 0.02
        _ReflStrength  ("反射强度",         Range(0, 1))  = 0.85
        _SpecColor2    ("高光颜色",         Color)        = (1,1,1,1)
        _Shininess     ("高光锐度",         Range(1,256)) = 96
        _EdgeFade      ("边缘羽化(UV)",     Range(0,0.5)) = 0.18

        // —— 星球曲面 —— C# 运行时填充; _PlanetRadius<=0 时退化为平面
        _PlanetCenter  ("星球中心(世界)",   Vector)       = (0,0,0,0)
        _PlanetRadius  ("星球半径",         Float)        = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos       : SV_POSITION;
                float3 worldPos  : TEXCOORD0;
                float3 worldNrm  : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float2 uv        : TEXCOORD3;
                float3 tanU      : TEXCOORD4;
                float3 tanV      : TEXCOORD5;
            };

            sampler2D _ReflectionTex;
            fixed4 _WaterColor, _DeepColor, _SpecColor2;
            float  _Distort, _WaveScale, _WaveSpeed, _BumpStrength;
            float  _FresnelF0, _ReflStrength, _Shininess, _EdgeFade;
            float4 _PlanetCenter;
            float  _PlanetRadius;

            void buildTangentBasis(float3 N, out float3 U, out float3 V)
            {
                float3 a = abs(N.y) < 0.99 ? float3(0,1,0) : float3(1,0,0);
                U = normalize(cross(a, N));
                V = cross(N, U);
            }

            v2f vert (appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = UnityObjectToWorldNormal(v.normal);

                // 球面弯曲: 顶点强制投影到 _PlanetRadius 的球面上
                if (_PlanetRadius > 0.001)
                {
                    float3 nOut = normalize(wp - _PlanetCenter.xyz);
                    wp = _PlanetCenter.xyz + nOut * _PlanetRadius;
                    wn = nOut;
                }

                o.worldPos  = wp;
                o.worldNrm  = normalize(wn);
                buildTangentBasis(o.worldNrm, o.tanU, o.tanV);
                o.pos       = UnityWorldToClipPos(float4(wp, 1));
                o.screenPos = ComputeScreenPos(o.pos);
                o.uv        = v.uv;
                return o;
            }

            // 切平面波纹: 在 (tanU, tanV) 投影坐标里做两组错向正弦, 输出梯度 (gu, gv)
            float2 wavesGrad(float u, float vv, float t)
            {
                float2 g;
                g.x = cos(u * 1.3 + t)        + 0.6 * cos(vv * 2.1 - t * 1.3);
                g.y = cos(vv * 1.1 - t)       + 0.6 * cos(u  * 1.9 + t * 1.1);
                return g * 0.5;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1) 切平面坐标
                float scale = _WaveScale * 0.05;
                float u  = dot(i.worldPos, i.tanU) * scale;
                float vv = dot(i.worldPos, i.tanV) * scale;
                float t  = _Time.y * _WaveSpeed;

                float2 grad = wavesGrad(u, vv, t);

                // 2) 切平面梯度 → 世界扰动法线
                float3 N = normalize(i.worldNrm - (grad.x * i.tanU + grad.y * i.tanV) * _BumpStrength);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 H = normalize(L + V);

                // 3) 反射图: 屏幕 UV + 折射扰动
                float2 ruv = i.screenPos.xy / max(i.screenPos.w, 1e-4);
                ruv += grad * _Distort;
                fixed3 refl = tex2D(_ReflectionTex, ruv).rgb;

                // 4) Schlick Fresnel
                float NdotV = saturate(dot(N, V));
                float fres  = _FresnelF0 + (1.0 - _FresnelF0) * pow(1.0 - NdotV, 5.0);

                // 5) 高光: 太阳/光源的镜面闪烁
                float spec = pow(saturate(dot(N, H)), _Shininess);

                // 6) 颜色混合: 浅色(正视) ↔ 深色(掠射), 叠加倒影 + 高光
                fixed3 baseCol = lerp(_DeepColor.rgb, _WaterColor.rgb, NdotV);
                float  k       = saturate(_ReflStrength * (fres + 0.15));
                fixed3 col     = lerp(baseCol, refl, k) + spec * _SpecColor2.rgb;

                // 7) 岸边羽化: UV.x 由 C# 写入"距岸距离" (0=岸边, 1=足够深)。
                //    _EdgeFade 是淡入区间宽度: alpha 在 [0, _EdgeFade] 区间从 0 平滑升到 1。
                float edge = smoothstep(0.0, max(_EdgeFade, 1e-3), i.uv.x);

                float alpha = saturate(_WaterColor.a + fres * 0.3 + spec) * edge;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
