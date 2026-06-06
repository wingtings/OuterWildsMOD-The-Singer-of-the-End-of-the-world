using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 体积雨:跟随玩家的粒子系统,用 Custom/VolumetricRain 材质渲染。
    // 距离门控:以废岩星北极故事区域为圆心 200 m 内才下雨,避免在太空或量子月出现降雨。
    // 还负责在城区地面铺几块涟漪水洼(Custom/RainRipple)。
    public class RainController : MonoBehaviour
    {
        public static RainController Instance { get; private set; }

        private ParticleSystem _ps;
        private Transform _planet;          // Attlerock(废岩星) Transform

        // 故事区域在废岩星(Attlerock)局部坐标中的中心(迁移后的歌者音乐厅舞台)
        private static readonly Vector3 StoryZoneLocal = new Vector3(-5.52638f, -7.194386f, 29.36535f);

        // 供 TimelineManager 控制发射速率
        public void SetEmissionRate(float rate)
        {
            if (_ps == null) return;
            var em = _ps.emission;
            em.rateOverTime = Mathf.Max(0f, rate);
        }

        // 供 Configure() 的 shader 开关使用
        public void EnablePS()  { if (_ps != null && !_ps.isPlaying) _ps.Play(); }
        public void DisablePS() { if (_ps != null &&  _ps.isPlaying) _ps.Stop(); }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Setup()
        {
            var player = Locator.GetPlayerTransform();
            var planet = TheSingerOfTheEnd.Instance.NewHorizons.GetPlanet("Attlerock");
            if (player == null || planet == null)
            {
                Log("玩家或废岩星(Attlerock)未就绪,跳过降雨/涟漪。", MessageType.Warning);
                return;
            }

            // —— 体积雨粒子(材质存在才建,按「体积雨」开关启停)——
            if (AssetLoader.Rain != null)
            {
                var go = new GameObject("SingerRain");
                go.transform.SetParent(player, false);

                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop();

                var main = ps.main;
                main.startLifetime = 1.5f;
                main.startSpeed = 0f;                 // 速度交给 velocityOverLifetime
                // 细而高的 billboard → 竖直雨丝(配合 shader 用 v.vertex 渲染逐粒子位置)
                main.startSize3D = true;
                main.startSizeX = 0.06f;              // 雨丝宽度
                main.startSizeY = 1.1f;               // 雨丝长度(竖直拖尾)
                main.startSizeZ = 0.06f;
                main.maxParticles = 8000;
                main.gravityModifier = 0f;            // 不用世界重力(球面星球方向不一致)
                main.simulationSpace = ParticleSystemSimulationSpace.Local;

                var emission = ps.emission;
                emission.rateOverTime = 4000f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.position = new Vector3(0f, 15f, 0f);   // 玩家头顶
                shape.scale = new Vector3(40f, 0.5f, 40f);

                // 在玩家本地空间里向"脚下"(-Y)落 → 不受星球朝向影响,永远朝地面下落
                var vol = ps.velocityOverLifetime;
                vol.enabled = true;
                vol.space = ParticleSystemSimulationSpace.Local;
                vol.y = new ParticleSystem.MinMaxCurve(-30f);

                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.renderMode = ParticleSystemRenderMode.Billboard;  // shader 自己做竖直拉伸
                rend.material = AssetLoader.Rain;

                var ctrl = go.AddComponent<RainController>();
                ctrl._ps = ps;
                ctrl._planet = planet.transform;
                Instance = ctrl;

                if (TheSingerOfTheEnd.Instance.RainEnabled) ps.Play();
                else                                        ps.Stop();
                Log("体积雨已部署(跟随玩家,城区内启用)。", MessageType.Success);
            }
            else
            {
                Log("Rain 材质为空,跳过体积雨。", MessageType.Warning);
            }

            // —— 地面涟漪水洼:不在 Setup 里建,放到 Update 里持续扫描地形
            // (Attlerock 是 NewHorizons 流式加载, collider 通常 30~60s 后才到位,
            //  和 PlanarReflectionController 同款思路: _puddlesBuilt 标志 + 每帧重试一次)
            _planetForPuddles = planet.transform;
        }

        private void Update()
        {
            // 涟漪水洼: 等地形加载好再一次性扫描+生成 (Attlerock 是流式)
            if (!_puddlesBuilt && _planetForPuddles != null) TryBuildPuddles();

            // 错峰激活: 在 [0, RippleStaggerSpread] 秒内逐渐把各块点亮,
            // 让雨打地面不是 "一下全出现" 而是点点连续落下。
            // _puddleActivateAt[i] 是该块应该被点亮的绝对 Time.time, < 0 表示已点亮。
            if (_puddlesBuilt && _staggerActive)
            {
                float now = Time.time;
                bool anyPending = false;
                for (int i = 0; i < _puddles.Count; i++)
                {
                    float at = _puddleActivateAt[i];
                    if (at < 0f) continue;
                    if (now >= at)
                    {
                        var p = _puddles[i];
                        if (p != null && TheSingerOfTheEnd.Instance.RippleEnabled && !p.activeSelf)
                            p.SetActive(true);
                        _puddleActivateAt[i] = -1f;
                    }
                    else anyPending = true;
                }
                if (!anyPending) _staggerActive = false;
            }

            if (_planet == null) return;

            // 把当前星球中心位置喂给每块水洼的 shader (星球在公转/自转, 位置每帧变)
            Vector3 pc = _planet.position;
            for (int i = 0; i < _puddles.Count; i++)
            {
                var p = _puddles[i];
                if (p == null || !p.activeInHierarchy) continue;
                var mat = p.GetComponent<MeshRenderer>().material;
                mat.SetVector(_PlanetCenterID, pc);
            }

            if (_ps == null) return;
            var player = Locator.GetPlayerTransform();
            if (player == null) return;

            // 大气层门控:距废岩星星心 < 大气层半径才下雨(在大气层以内才有降雨)。
            bool inAtmosphere =
                Vector3.Distance(player.position, _planet.position)
                    < TheSingerOfTheEnd.AttlerockAtmosphereRadius;
            var emission = _ps.emission;
            if (emission.enabled != inAtmosphere) emission.enabled = inAtmosphere;
        }

        // 已生成的涟漪水洼 + 每块对应的球面半径(供 shader 弯曲贴星球)
        private static readonly System.Collections.Generic.List<GameObject> _puddles =
            new System.Collections.Generic.List<GameObject>();
        private static readonly System.Collections.Generic.List<float> _puddleRadii =
            new System.Collections.Generic.List<float>();
        // 每块水洼"目标点局部方向"(星球本地坐标的单位向量), 用于地面射线扫描。
        private static readonly System.Collections.Generic.List<Vector3> _puddleSpotLocal =
            new System.Collections.Generic.List<Vector3>();
        // 一次性扫描+生成完成 (Attlerock 是 NewHorizons 流式加载, 等地形 collider 到位再建)
        private static bool _puddlesBuilt;
        private static bool _staggerActive;
        private static Transform _planetForPuddles;
        private static readonly System.Collections.Generic.List<float> _puddleActivateAt =
            new System.Collections.Generic.List<float>();
        private static readonly int _PlanetCenterID = Shader.PropertyToID("_PlanetCenter");
        private static readonly int _PlanetRadiusID = Shader.PropertyToID("_PlanetRadius");

        // 射线扫描参数 (与 PlanarReflectionController 同款思路: 平行射线 + 地形加载完一次性建)
        private const float PuddleRayUp  = 12f;  // 切平面以上多少米 (与 PlanarReflectionController.ScanRayUp 一致)
        private const float PuddleRayLen = 30f;  // 射线最大长度 (与 PlanarReflectionController.ScanRayLen 一致)
        private const float PuddleLift   = 0.05f; // 命中点上抬避免 z-fighting
        private const int   MinHitsToBuild = 30; // 命中数低于这个视为地形未加载, 下帧重试

        // —— 涟漪扫描参数 (Fibonacci 球面采样, 覆盖整个 Attlerock) ——
        // Attlerock 表面积 ≈ 4π·30² ≈ 11300 m², 1500 个采样点 → 平均每点 ~7.5 m²
        // (相邻点距 ≈ 2.7 m), 配合 2 m 贴片大致能铺满又不太挤。
        // —— 涟漪扫描参数 (Fibonacci 球面采样, 覆盖整个 Attlerock) ——
        // Attlerock 形状不规则, 命中率 ≈16%, 4000 采样点 → 实际 ≈640 块涟漪,
        // 表面覆盖率 ≈23%, 视野内几乎总在下雨。
        private const int   FiboSampleCount  = 4000; // 球面采样总数 (上限决定密度)
        private const float RippleSkipRadius = 8f;   // 距 PoolLocal 这个距离内不放涟漪 (避免与平面反射水面 z-fighting)
        private const float RipplePuddleSize = 2f;   // 单块涟漪贴片基础边长 (m)
        private const int   RipplePuddleCap  = 4000; // 实际生成数上限
        // 随机扰动: 让 Fibonacci 网格看起来更自然 (deterministic hash, 每次生成结果一致)
        private const float RippleJitterRadius = 0.3f; // 切平面内位置抖动半径 (m); 过大会导致块重叠浪费
        private const float RippleSizeJitter   = 0.3f; // 大小相对抖动, 实际尺寸 ∈ [1-x, 1+x] × Size
        // 错峰激活: 所有块不同时出现, 在 [0, RippleStaggerSpread] 秒内随机调度 → 似雨滴连续落下
        private const float RippleStaggerSpread = 3.0f;
        // 平面反射水池中心(局部坐标), 与 PlanarReflectionController.PoolLocal 一致, 用于避让
        private static readonly Vector3 PoolLocalForRipple = new Vector3(-6.7f, 2.2f, 29.4f);

        // —— 调试开关 ——
        // true = 把水洼换成纯红不透明方块(Standard 着色器), 玩家现场可以直接看到粒粒位置:
        //   看得见红方块 → 位置/贴地都对, 问题在 shader/material 路径
        //   看不见红方块 → 被埋在地里 / 朝向反了 / 根本没生成
        // 验证完改回 false 重新 build 即可。
        public const bool DebugVisible = false;

        // 供「地面涟漪」开关即时启停 (只对已生成的水洼生效)
        public static void SetRipplesActive(bool active)
        {
            for (int i = 0; i < _puddles.Count; i++)
            {
                var p = _puddles[i];
                if (p == null) continue;
                if (p.activeSelf != active) p.SetActive(active);
            }
            // 重新开启时不走错峰 (玩家中途拨开关 → 全亮)
            _staggerActive = false;
            for (int i = 0; i < _puddleActivateAt.Count; i++)
                _puddleActivateAt[i] = -1f;
        }

        // Fibonacci 球面均匀采样 + 每点用自己径向射线 → 覆盖整个星球。
        // 每条射线起点都在该点正上方 (本地径向 * (estR + 12)), 方向 = -该点径向,
        // 即"垂直地表往下打", 不存在切平面方案在远端擦边的问题。
        // 整个采样命中数 ≥ MinHitsToBuild 才生成水洼, 否则 _puddlesBuilt 保持 false 下帧重试。
        // Attlerock 是 NewHorizons 流式星球, 地形 collider 通常 30~60s 后才到位。
        private void TryBuildPuddles()
        {
            if (AssetLoader.Ripple == null) { _puddlesBuilt = true; return; }

            // —— 诊断: shader bundle 是否是含新 _RippleOnly property 的新版 ——
            if (!_diagLogged)
            {
                bool hasRippleOnly = AssetLoader.Ripple.HasProperty("_RippleOnly");
                if (hasRippleOnly)
                    Log("[诊断] RippleMat 包含 _RippleOnly 属性 → shader bundle 是新版。", MessageType.Success);
                else
                    Log("[诊断] RippleMat 没有 _RippleOnly 属性 → shader bundle 是旧版! 必须在 Unity 打开 outer-wilds-unity-template, 跑 Tools → Singer MOD → Build Shader Bundle 重新打包。", MessageType.Error);
                _diagLogged = true;
            }

            var planet = _planetForPuddles;

            // 估算星球半径: StoryZoneLocal 就在地表附近, 用它的模作上限即可
            float estR = StoryZoneLocal.magnitude + 4f;

            // 收集候选: 命中点 (local) + 该处地表法线 (local) + 索引 (用于 jitter)
            var candidates = new System.Collections.Generic.List<(int k, Vector3 posLocal, Vector3 normLocal)>();
            int hitCount = 0;

            // Fibonacci 球面: 黄金角螺旋, N 个点近似均匀分布, 无极点堆积
            const float Phi = 2.39996322972865332f; // 黄金角 = π(3-√5)
            for (int k = 0; k < FiboSampleCount; k++)
            {
                // y 从 +1 线性递减到 -1, 配合螺旋角分布
                float y       = 1f - (k + 0.5f) * (2f / FiboSampleCount);
                float radiusY = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float theta   = Phi * k;
                Vector3 dirLocal = new Vector3(Mathf.Cos(theta) * radiusY, y, Mathf.Sin(theta) * radiusY);

                // 避让平面反射水池区域
                Vector3 spotApprox = dirLocal * estR;
                if ((spotApprox - PoolLocalForRipple).magnitude < RippleSkipRadius) continue;

                Vector3 rayStartWorld = planet.TransformPoint(dirLocal * (estR + PuddleRayUp));
                Vector3 rayDirWorld   = planet.TransformDirection(-dirLocal);

                var allHits = Physics.RaycastAll(rayStartWorld, rayDirWorld,
                                                 PuddleRayUp + PuddleRayLen,
                                                 ~0, QueryTriggerInteraction.Ignore);
                if (allHits.Length == 0) continue;

                // 选离星心最近的命中 = 真地面 (跳过歌者/舞台/建筑 collider)
                int bestK = -1; float minAlt = float.MaxValue;
                for (int h = 0; h < allHits.Length; h++)
                {
                    float a = (allHits[h].point - planet.position).magnitude;
                    if (a < minAlt) { minAlt = a; bestK = h; }
                }
                var hit = allHits[bestK];
                Vector3 posLocal  = planet.InverseTransformPoint(hit.point);
                Vector3 normLocal = planet.InverseTransformDirection(hit.normal);
                candidates.Add((k, posLocal, normLocal));
                hitCount++;
            }

            if (hitCount < MinHitsToBuild) return;  // 地形 collider 还没就绪, 下帧再试

            // 命中够 → 一次性建好所有水洼
            int spawned = 0;
            for (int idx = 0; idx < candidates.Count && spawned < RipplePuddleCap; idx++)
            {
                var c = candidates[idx];
                // 用真实地表法线对齐 plane, 在斜坡/台阶上才不会一半埋地一半飘空;
                // 命中点本身已是地表真位置, 再沿地表法线抬 PuddleLift 避免 z-fighting。
                Vector3 nLocal = c.normLocal.sqrMagnitude > 1e-6f ? c.normLocal.normalized
                                                                  : c.posLocal.normalized;

                // 基于 c.k 的 deterministic hash → 三个 [0,1) 噪声: 位置u, 位置v, 大小
                float h1 = Frac(Mathf.Sin(c.k * 12.9898f + 78.233f) * 43758.5453f);
                float h2 = Frac(Mathf.Sin(c.k * 39.346f  + 11.135f) * 24634.6345f);
                float h3 = Frac(Mathf.Sin(c.k * 93.989f  + 47.336f) * 18972.4751f);

                // 在地表切平面内偏移 ±RippleJitterRadius, 让分布看起来随机而非螺旋网格
                Vector3 up0 = Mathf.Abs(nLocal.y) < 0.99f ? Vector3.up : Vector3.right;
                Vector3 tU  = Vector3.Cross(up0, nLocal).normalized;
                Vector3 tV  = Vector3.Cross(nLocal, tU);
                Vector3 jitterLocal = tU * ((h1 - 0.5f) * 2f * RippleJitterRadius)
                                    + tV * ((h2 - 0.5f) * 2f * RippleJitterRadius);

                // 大小抖动: [1-Range, 1+Range] × RipplePuddleSize
                float sizeMul = 1f + (h3 - 0.5f) * 2f * RippleSizeJitter;

                var q = GameObject.CreatePrimitive(PrimitiveType.Plane);
                q.name = $"SingerPuddle_{c.k}";
                Object.Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(planet, false);
                q.transform.localPosition = c.posLocal + jitterLocal + nLocal * PuddleLift;
                q.transform.localRotation = Quaternion.FromToRotation(Vector3.up, nLocal);
                q.transform.localScale    = Vector3.one * (RipplePuddleSize * sizeMul * 0.1f);

                var mr = q.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows    = false;

                if (DebugVisible)
                {
                    var dbgMat = mr.material;
                    dbgMat.color = new Color(1f, 0.2f, 0.2f, 1f);
                }
                else
                {
                    mr.sharedMaterial = AssetLoader.Ripple;
                    var mat = mr.material;
                    mat.SetFloat(_PlanetRadiusID, 0f);  // 已贴到真地面海拔, 不需要 shader 再球面投影
                    mat.SetFloat(Shader.PropertyToID("_RippleOnly"),       1f);
                    // 亮度减半: visibility 保持 (控制 alpha 即可见度), 只把 SpecColor2 减半 (控制颜色强度)
                    // 上次把两者都减半 → 实际亮度变成 1/4, 涟漪被雨幕吃掉看不见。
                    mat.SetFloat(Shader.PropertyToID("_RippleVisibility"), 6f);
                    mat.SetColor(Shader.PropertyToID("_SpecColor2"),       new Color(0.55f, 0.6f, 0.7f, 1f));
                    mat.SetFloat(Shader.PropertyToID("_PuddleRadius"),   2.0f);
                    mat.SetFloat(Shader.PropertyToID("_RippleBumpiness"),1.5f);
                    mat.SetFloat(Shader.PropertyToID("_RippleDensity"),  10f);
                    mat.SetFloat(Shader.PropertyToID("_EdgeFade"),       0f);
                    mat.SetFloat(Shader.PropertyToID("_Wavelength"),     0.30f);
                    mat.SetFloat(Shader.PropertyToID("_RippleRadius"),   1.0f);
                    float lifeJitter = (c.k % 7) / 7f * 0.4f - 0.2f;
                    mat.SetFloat(Shader.PropertyToID("_RippleLifetime"), 1.2f + lifeJitter);
                }

                q.SetActive(false);
                _puddles.Add(q);
                _puddleRadii.Add(c.posLocal.magnitude);
                _puddleSpotLocal.Add(nLocal);
                // 错峰激活时间: 0~RippleStaggerSpread 秒内随机。为了让不依赖 SetRipplesActive 也不出错,
                // 如果 RippleEnabled=false 也记下时间, 到了点只设标志不真的开。
                float at = Time.time + h1 * RippleStaggerSpread;
                _puddleActivateAt.Add(TheSingerOfTheEnd.Instance.RippleEnabled ? at : -1f);
                if (!TheSingerOfTheEnd.Instance.RippleEnabled)
                {
                    // 开关为关时也保持 false; SetRipplesActive 会一次性启用
                }
                spawned++;
            }

            _puddlesBuilt = true;
            _staggerActive = TheSingerOfTheEnd.Instance.RippleEnabled;
            Log($"涟漪扫描完成 (全星球): 采样 {FiboSampleCount} 点, 命中 {hitCount} → 生成 {spawned} 块水洼 (错峰激活 {RippleStaggerSpread}s)。", MessageType.Success);
        }

        private static bool _diagLogged;

        private static float Frac(float x) { return x - Mathf.Floor(x); }

        private static void Log(string msg, MessageType type)
        {
            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine($"[世末歌者] {msg}", type);
        }
    }
}
