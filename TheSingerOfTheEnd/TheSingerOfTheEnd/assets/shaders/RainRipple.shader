// Rain Ripple v2 —— 贴在地面积水上的半透明水面材质
//
// 设计要点(相比 v1 重写, 不再"一坨屎"):
//   * 旋钮全部物理化: 波长(米)、寿命(秒)、最大半径(米)、平均同时活滴数,
//     美术不用再去对着无量纲的 _RippleScale/_RippleFalloff 凑参数。
//   * 涟漪 = N 个解析"扩张环": 用 cos(k*(d-r)) * 高斯径向窗 * 寿命包络,
//     d 是当前像素到滴心的距离(米), r=phase*MaxRadius 是当前环半径。
//     视觉上就是清晰一圈圈向外扩, 不再是全 mesh sin 起伏。
//   * 解析梯度法线: 对环波形直接求 d/dr, 得到精确切平面坡度,
//     再投到世界, 配合星球径向法线做球面切平面扰动。
//   * Drop 调度: 固定 12 槽, 每槽寿命到了才换位置(避免"环跳"); 用 keepProb
//     裁剪激活个数, 平均 = _RippleDensity, 上限 = 12。
//   * 反射与涟漪解耦的关键:
//       - Fresnel 用"几何法线 Ng"(决定整体反射强度), 不被毫米级波纹改写;
//       - 反射 UV 扰动用屏幕空间法线偏移 / 距离, 物理量级一致;
//       - 高光 _SpecStrength 单独可调, 避免与反射图里的太阳"算两次";
//       - Cubemap 兜底 (_ReflectionCube + _ReflectionBlend), puddle 朝向偏离
//         平面反射相机时可滑回环境反射, 避免穿帮。
//   * 水洼形状: 仍走 mesh UV ([0,1]² 中心向外做圆形羽化), C# 端不规则水洼
//     可设 _EdgeFade=0 关闭(原 PlanarReflectionController 已这么做)。
//
// C# 端集成(保持兼容, 同 v1):
//   material.SetVector ("_PlanetCenter",  planet.position);
//   material.SetFloat  ("_PlanetRadius",  groundRadius);
//   material.SetTexture("_ReflectionTex", reflectionRT);
Shader "Custom/RainRipple"
{
    Properties
    {
        // —— 水色 ——
        _WaterColor       ("水色(垂直看)",       Color)        = (0.15, 0.20, 0.28, 0.60)
        _DeepColor        ("深水色(掠射看)",     Color)        = (0.05, 0.08, 0.12, 1.00)

        // —— 反射 ——
        _ReflectionTex    ("平面反射图",          2D)           = "black" {}
        _ReflectionCube   ("环境反射 Cubemap",    Cube)         = "" {}
        _ReflectionStrength("反射强度",            Range(0, 2))  = 1.0
        _ReflectionBlend  ("反射混合(0立方,1平面)", Range(0, 1)) = 1.0
        _FresnelF0        ("Fresnel F0",          Range(0, 0.2))= 0.02

        // —— 高光 ——
        _SpecColor2       ("高光颜色",            Color)        = (1, 1, 1, 1)
        _Shininess        ("高光锐度",            Range(1, 256))= 128
        _SpecStrength     ("高光强度",            Range(0, 2))  = 0.6

        // —— 涟漪(全部物理单位) ——
        _Wavelength       ("波长(米)",            Float)        = 0.35
        _RippleLifetime   ("单滴寿命(秒)",        Float)        = 1.6
        _RippleRadius     ("最大扩散半径(米)",    Float)        = 0.8
        _RippleDensity    ("平均同时活滴数(<=12)", Range(0, 12)) = 6.0
        _RippleBumpiness  ("法线扰动强度",        Range(0, 4))  = 1.0

        // —— 水洼几何 ——
        _PuddleRadius     ("水洼半径(米, UV 0.5→1)",Float)      = 1.0
        _EdgeFade         ("UV 圆形羽化(0=关闭)", Range(0, 0.5))= 0.15

        // —— 雨滴打地模式(各向同性, 不靠光照对齐) ——
        // 0 = 标准水面(默认, 兼容 PlanarReflectionController)
        // 1 = 纯涟漪环(透明地面 + 白色环, 任意视角都看得见)
        _RippleOnly       ("纯涟漪模式(0水面/1雨滴打地)", Range(0,1)) = 0
        _RippleVisibility ("涟漪亮度(纯涟漪模式)", Range(0, 20))    = 5.0

        // —— 星球曲面(C# 写) ——
        _PlanetCenter     ("星球中心(世界)",      Vector)       = (0,0,0,0)
        _PlanetRadius     ("星球半径",            Float)        = 0
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

            sampler2D   _ReflectionTex;
            samplerCUBE _ReflectionCube;
            fixed4 _WaterColor, _DeepColor, _SpecColor2;
            float  _ReflectionStrength, _ReflectionBlend, _FresnelF0;
            float  _Shininess, _SpecStrength;
            float  _Wavelength, _RippleLifetime, _RippleRadius, _RippleDensity, _RippleBumpiness;
            float  _PuddleRadius, _EdgeFade;
            float  _RippleOnly, _RippleVisibility;
            float4 _PlanetCenter;
            float  _PlanetRadius;

            // —— 哈希(避免 sin 在 Mac/Mobile 上的精度抖) ——
            float hash11(float n)
            {
                n = frac(n * 0.1031);
                n *= n + 33.33;
                n *= n + n;
                return frac(n);
            }
            float2 hash21(float n)
            {
                float3 p3 = frac(float3(n, n + 1.0, n + 2.0) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // 给定外法线 N, 构造一组切线基 (U, V)
            void buildTangentBasis(float3 N, out float3 U, out float3 V)
            {
                float3 a = abs(N.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
                U = normalize(cross(a, N));
                V = cross(N, U);
            }

            // 多滴累加: 输入水洼局部坐标 p (米), 输出高度与切平面梯度 (米/米 = 无量纲)
            #define NUM_DROPS 12
            void heightAndGrad(float2 p, out float h, out float2 grad)
            {
                h    = 0;
                grad = float2(0, 0);

                float t        = _Time.y;
                float life     = max(_RippleLifetime, 0.05);
                float wl       = max(_Wavelength, 0.01);
                float k        = 6.2831853 / wl;
                float sigma    = wl * 0.6;                  // 高斯径向窗宽 ~ 一波长
                float invSig2  = 1.0 / (sigma * sigma);
                float keepProb = saturate(_RippleDensity / (float)NUM_DROPS);
                float maxR     = max(_RippleRadius, 0.05);
                float spread   = max(_PuddleRadius * 0.85 - maxR * 0.5, _PuddleRadius * 0.3);

                [unroll]
                for (int idx = 0; idx < NUM_DROPS; idx++)
                {
                    float fi    = (float)idx;
                    // 错相位让滴出生时间分散
                    float tShift = t / life + fi * 0.137;
                    float slot   = floor(tShift);
                    float phase  = frac(tShift);
                    float seed   = slot * 31.41 + fi * 7.13;

                    if (hash11(seed) > keepProb) continue;

                    // 寿命包络: 起爆 8% 内冲到峰值, 70% 之后线性衰减; 寿命外一定为 0
                    float envT = smoothstep(0.0, 0.08, phase) * smoothstep(1.0, 0.7, phase);
                    if (envT <= 1e-4) continue;

                    // 滴心: 在 puddle 内的圆盘上随机
                    float2 r01 = hash21(seed * 2.3);
                    float ang  = r01.x * 6.2831853;
                    float rad  = sqrt(r01.y) * spread;
                    float2 c   = float2(cos(ang), sin(ang)) * rad;

                    float2 r2 = p - c;
                    float  d  = length(r2) + 1e-4;

                    float ring = phase * maxR;             // 当前环的半径(米)
                    float x    = d - ring;                 // 离环锋面的距离

                    // 环越扩越细: sigma 随 phase 收紧, 加上面积守恒衰减 1/sqrt(ring)
                    float sigEff   = sigma * (1.0 - 0.4 * phase);
                    float invSE2   = 1.0 / max(sigEff * sigEff, 1e-4);
                    float envS     = exp(-x * x * invSE2 * 0.5);
                    float areaAtt  = 1.0 / sqrt(max(ring, wl * 0.5) / max(wl, 0.01));

                    float c1 = cos(x * k);
                    float s1 = sin(x * k);
                    float h1 = c1 * envS * envT * areaAtt;
                    // dh/dd = envT*envS*areaAtt * ( -k*sin(xk) - (x/sigEff^2)*cos(xk) )
                    float dhd = envT * envS * areaAtt * (-k * s1 - x * invSE2 * c1);

                    h    += h1;
                    grad += (r2 / d) * dhd;
                }

                h    *= _RippleBumpiness;
                grad *= _RippleBumpiness;
            }

            v2f vert (appdata v)
            {
                v2f o;

                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = UnityObjectToWorldNormal(v.normal);

                // 星球弯曲: 顶点强制对齐到 _PlanetRadius 的球面
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

            fixed4 frag (v2f i) : SV_Target
            {
                // —— 1. 水洼局部坐标(米) ——
                float2 p = (i.uv - 0.5) * 2.0 * max(_PuddleRadius, 0.01);

                // 边缘羽化(同时让 grad 在外边逐渐归零, 避免环冲出 puddle)
                float rUV  = length(i.uv - 0.5) * 2.0;     // 0 中心 → 1 角
                float edge = (_EdgeFade > 0.0001)
                             ? (1.0 - smoothstep(1.0 - _EdgeFade, 1.0, rUV))
                             : 1.0;

                // —— 2. 涟漪高度 + 切平面梯度 ——
                float  h;
                float2 grad;
                heightAndGrad(p, h, grad);
                grad *= edge;

                // —— 3. 法线: 几何 vs 扰动, 各司其职 ——
                float3 Ng    = normalize(i.worldNrm);
                float3 bump  = grad.x * i.tanU + grad.y * i.tanV;
                float3 N     = normalize(Ng - bump);

                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 H = normalize(L + V);

                // —— 4. Fresnel 用几何法线(整体反射占比, 不被波纹改写) ——
                float NgV     = saturate(dot(Ng, V));
                float fresnel = _FresnelF0 + (1.0 - _FresnelF0) * pow(1.0 - NgV, 5.0);

                // —— 5. 反射 UV: 屏幕空间法线偏移 / 距离 ——
                float2 sUV   = i.screenPos.xy / max(i.screenPos.w, 1e-4);
                float3 Nview = mul((float3x3)UNITY_MATRIX_V, bump);  // 偏移向量, 不是单位法线
                float  dist  = length(_WorldSpaceCameraPos - i.worldPos);
                float  k1    = 0.06 / max(dist, 1.0);                // 远小近大
                sUV         += Nview.xy * k1;

                fixed3 reflPlanar = tex2D(_ReflectionTex, sUV).rgb;
                fixed3 reflCube   = texCUBE(_ReflectionCube, reflect(-V, N)).rgb;
                fixed3 refl       = lerp(reflCube, reflPlanar, _ReflectionBlend) * _ReflectionStrength;

                // —— 6. 高光 ——
                float spec = pow(saturate(dot(N, H)), _Shininess) * _SpecStrength;

                // —— 7. 颜色合成 ——
                fixed3 baseCol = lerp(_DeepColor.rgb, _WaterColor.rgb, NgV);
                fixed3 col     = lerp(baseCol, refl, fresnel) + spec * _SpecColor2.rgb;

                float alpha = saturate(_WaterColor.a + fresnel * 0.4 + spec) * edge;

                // —— 8. 纯涟漪模式: 用波高度 |h| 直接驱动透明度+亮度 ——
                // 这一通道不依赖光线/相机/法线对齐, 任意视角都能看到环;
                // h 是 heightAndGrad 累积的波场高度(米), |h| 在涟漪环锋面峰值最大,
                // 平地处 ≈ 0; 因此 alpha 在涟漪锋面亮起, 其他地方完全透明.
                if (_RippleOnly > 0.5)
                {
                    float ring = saturate(abs(h) * _RippleVisibility) * edge;
                    col   = _SpecColor2.rgb;        // 涟漪颜色 (默认白)
                    alpha = ring;
                }
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
