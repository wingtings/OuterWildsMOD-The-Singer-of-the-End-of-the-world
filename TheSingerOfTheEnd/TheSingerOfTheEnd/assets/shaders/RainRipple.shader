// Rain Ripple 积水涟漪 —— 贴在地面积水/水洼上的半透明水面材质
//
// 图形学要点:
//   * 程序化涟漪: 多个雨滴位置上发出 sin(d*freq - t*speed) * exp(-d*falloff) 的扩散环。
//   * 解析梯度法线: 对 sin*exp 直接求 d/dr, 得到精确法线, 不再用 ddx/ddy(噪声大且与屏幕分辨率耦合)。
//   * 星球曲面适配: 顶点着色器把平面四边形按 _PlanetCenter / _PlanetRadius 投影到球面,
//     一块小 Plane 即可贴合任意半径的星球; C# 端每帧 SetVector 即可。
//   * 切线基: 法线方向取"星球外法线", 涟漪在球面切平面里展开, 不再假设 XZ 平面。
//   * Fresnel 用 Schlick(F0=0.02) 近似水面菲涅尔, 配合反射图给出湿光感。
//   * 边缘羽化: 用 UV 距中心做圆形 mask, 避免方形水洼硬边。
//
// C# 端集成示例:
//   material.SetVector("_PlanetCenter", planetTransform.position);
//   material.SetFloat ("_PlanetRadius", planetRadius);   // groundSize 即可
//   material.SetTexture("_ReflectionTex", reflectionRT); // PlanarReflectionController 输出
//
// 提示: 水洼 Mesh 顶点越多, 球面弯曲越平滑; 推荐用细分过的 Plane(>= 20x20 顶点)。
Shader "Custom/RainRipple"
{
    Properties
    {
        _WaterColor    ("水色",            Color)        = (0.15, 0.20, 0.28, 0.60)
        _DeepColor     ("深水色(掠射融合)", Color)        = (0.05, 0.08, 0.12, 1.00)
        _ReflectionTex ("平面反射图(可选)", 2D)            = "black" {}
        _RippleStrength("涟漪强度",         Range(0,2))   = 1.0
        _RippleScale   ("涟漪频率",         Float)        = 12.0
        _RippleSpeed   ("扩散速度",         Float)        = 3.0
        _RippleFalloff ("距离衰减",         Float)        = 2.0
        _DropLifetime  ("单滴寿命(秒)",     Float)        = 2.0
        _DropDensity   ("雨滴密度",         Range(0,1))   = 1.0
        _SpecColor2    ("高光颜色",         Color)        = (1,1,1,1)
        _Shininess     ("高光锐度",         Range(1,256)) = 96
        _FresnelF0     ("Fresnel F0",       Range(0,0.2)) = 0.02
        _EdgeFade      ("边缘羽化(UV)",     Range(0,0.5)) = 0.15

        // —— 星球曲面 —— C# 运行时填充; _PlanetRadius<=0 时退化为平面行为
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
                float3 tanU      : TEXCOORD4; // 切平面基 U
                float3 tanV      : TEXCOORD5; // 切平面基 V
            };

            sampler2D _ReflectionTex;
            fixed4 _WaterColor, _DeepColor, _SpecColor2;
            float  _RippleStrength, _RippleScale, _RippleSpeed, _RippleFalloff;
            float  _DropLifetime, _DropDensity, _Shininess, _FresnelF0, _EdgeFade;
            float4 _PlanetCenter;
            float  _PlanetRadius;

            // —— 哈希 ——
            float  hash11(float n) { return frac(sin(n) * 43758.5453); }
            float2 hash21(float n) { return frac(sin(float2(n, n + 1.7)) * float2(43758.5453, 22578.1459)); }

            // 给定外法线 N, 构造一组切线基 (U,V)
            void buildTangentBasis(float3 N, out float3 U, out float3 V)
            {
                float3 a = abs(N.y) < 0.99 ? float3(0,1,0) : float3(1,0,0);
                U = normalize(cross(a, N));
                V = cross(N, U);
            }

            // 单滴: 距中心 d, 寿命相位 phase∈[0,1) 时的高度 & 沿径向导数
            // h(d) = sin(d*k - phase*2π*speed) * exp(-d*falloff) * envelope(phase)
            void dropHeight(float d, float phase, out float h, out float dh)
            {
                float k     = _RippleScale;
                float decay = exp(-d * _RippleFalloff);
                float t     = phase * 6.2831853 * _RippleSpeed;
                float ang   = d * k - t;
                float s     = sin(ang);
                float c     = cos(ang);
                // 寿命包络: 起爆瞬间冲一下, 之后线性衰减为 0
                float env   = saturate(1.0 - phase) * smoothstep(0.0, 0.05, phase);
                h  = s * decay * env;
                // dh/dd = (k*cos - falloff*sin) * exp(-d*falloff) * env
                dh = (k * c - _RippleFalloff * s) * decay * env;
            }

            // 多滴累加: 输出高度 h 与切平面梯度 (gu, gv)
            // 雨滴用 hash 在 [-1,1]^2 切线坐标内随机, 按时间分片循环复用 (省 uniform)。
            void heightAndGrad(float2 tc, out float h, out float2 grad)
            {
                h        = 0;
                grad     = float2(0,0);
                float t  = _Time.y;
                for (int i = 0; i < 8; i++)
                {
                    float life  = max(_DropLifetime, 0.01);
                    // 每滴一个"时段索引": 寿命到了就换随机点(避免相位永远一致)
                    float slot  = floor(t / life + i * 0.137);
                    float phase = frac(t / life + i * 0.137);
                    if (hash11(slot * 11.7 + i) > _DropDensity) continue;

                    float2 c   = hash21(slot * 31.1 + i * 7.3) * 2.0 - 1.0;
                    float2 r2  = tc - c;
                    float  d   = length(r2) + 1e-4;
                    float  hi, dhi;
                    dropHeight(d, phase, hi, dhi);
                    h    += hi;
                    grad += (r2 / d) * dhi;
                }
                h    *= 0.25 * _RippleStrength;
                grad *= 0.25 * _RippleStrength;
            }

            v2f vert (appdata v)
            {
                v2f o;

                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = UnityObjectToWorldNormal(v.normal);

                // 星球弯曲: 把顶点强制对齐到 _PlanetRadius 的球面上
                // 注意: 这里假设 Plane 大致已经摆在星球表面附近; C# 端把它的 transform.position
                // 放在地表想要的中心点即可, 顶点会自动按方向收敛到球面。
                if (_PlanetRadius > 0.001)
                {
                    float3 nOut = normalize(wp - _PlanetCenter.xyz);
                    wp = _PlanetCenter.xyz + nOut * _PlanetRadius;
                    wn = nOut; // 外法线即径向
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
                // 切平面坐标: 把 worldPos 投到 (tanU, tanV) 上, 再缩放到涟漪参数空间。
                // 用 worldPos 而非 UV, 保证同一星球上不同水洼的频率一致(物理尺度统一)。
                float scale = 0.05;
                float2 tc;
                tc.x = dot(i.worldPos, i.tanU) * scale;
                tc.y = dot(i.worldPos, i.tanV) * scale;

                float  h;
                float2 grad;
                heightAndGrad(tc, h, grad);

                // 切平面梯度 → 世界扰动法线
                float3 N = normalize(i.worldNrm - (grad.x * i.tanU + grad.y * i.tanV) * 4.0);

                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 H = normalize(L + V);

                // Blinn-Phong 高光: 涟漪法线扰动让高光闪烁碎散
                float spec = pow(saturate(dot(N, H)), _Shininess);

                // Schlick Fresnel: 视角越平水越亮
                float NdotV   = saturate(dot(N, V));
                float fresnel = _FresnelF0 + (1.0 - _FresnelF0) * pow(1.0 - NdotV, 5.0);

                // 倒影: 用扰动后的屏幕 UV 采样平面反射图
                float2 reflUV = i.screenPos.xy / i.screenPos.w + grad * 0.05;
                fixed3 refl   = tex2D(_ReflectionTex, reflUV).rgb;

                // 颜色混合: 浅色(垂直看) ↔ 深色(掠射看), 叠加倒影与高光
                fixed3 baseCol = lerp(_DeepColor.rgb, _WaterColor.rgb, NdotV);
                fixed3 col     = baseCol + refl * fresnel + spec * _SpecColor2.rgb;

                // 圆形边缘羽化: UV 中心向外做平滑淡出, 避免方形水洼硬边
                float2 d2c = i.uv - 0.5;
                float  rUV = length(d2c) * 2.0;            // 0(中心) → 1(角)
                float  edge = 1.0 - smoothstep(1.0 - _EdgeFade, 1.0, rUV);

                float alpha = saturate(_WaterColor.a + fresnel * 0.4 + spec) * edge;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
