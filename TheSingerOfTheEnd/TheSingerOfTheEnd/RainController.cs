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

            // —— 地面涟漪水洼(材质存在才建,按「地面涟漪」开关启停)——
            SpawnPuddles(planet.transform);
            SetRipplesActive(TheSingerOfTheEnd.Instance.RippleEnabled);
        }

        private void Update()
        {
            if (_ps == null || _planet == null) return;
            var player = Locator.GetPlayerTransform();
            if (player == null) return;

            // 大气层门控:距废岩星星心 < 大气层半径才下雨(在大气层以内才有降雨)。
            bool inAtmosphere =
                Vector3.Distance(player.position, _planet.position)
                    < TheSingerOfTheEnd.AttlerockAtmosphereRadius;
            var emission = _ps.emission;
            if (emission.enabled != inAtmosphere) emission.enabled = inAtmosphere;
        }

        // 已生成的涟漪水洼(供开关即时启停)
        private static readonly System.Collections.Generic.List<GameObject> _puddles =
            new System.Collections.Generic.List<GameObject>();

        // 供「地面涟漪」开关即时启停
        public static void SetRipplesActive(bool active)
        {
            foreach (var p in _puddles)
                if (p != null && p.activeSelf != active) p.SetActive(active);
        }

        // 在城区平整地面铺几块涟漪水洼。父级设为星球,使其随星球自转/公转。
        private static void SpawnPuddles(Transform planet)
        {
            _puddles.Clear();                       // 新场景重建,清掉上一循环的旧引用
            if (AssetLoader.Ripple == null) return;

            // 歌者音乐厅舞台周围地表。BUG 修复:旧实现用固定 Euler(90,0,0) 让 Quad 法线朝局部 -Y,
            // 但球面上"上方"是径向(指向星心外),两者不一致 → 涟漪平面竖起来"夹住"歌者。
            // 改为把每块 Quad 摆进该点的切平面(法线 = 局部径向),并投影到舞台所在半径略上方。
            float stageR = StoryZoneLocal.magnitude;        // 舞台地面半径 ≈ 30.7

            // 各水洼相对舞台中心(StoryZoneLocal)的方向偏移,贴着舞台四周铺开
            Vector3[] offsets =
            {
                new Vector3( 2.5f, 0f,  0.5f),
                new Vector3(-2.5f, 0f, -1.0f),
                new Vector3( 0.5f, 0f,  3.0f)
            };
            float[] sizes = { 8f, 6f, 7f };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 spot = StoryZoneLocal + offsets[i];
                Vector3 radial = spot.normalized;           // 该点球面外法线(局部)
                Vector3 local  = radial * (stageR + 0.1f);  // 投影回舞台半径,略抬高避免 z-fighting

                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "SingerPuddle_" + i;
                Object.Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(planet, false);
                q.transform.localPosition = local;
                // Quad 法线(+Z)对齐径向 → 平铺在切平面(地面)上。shader 已加 Cull Off,正反面都可见。
                q.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, radial);
                q.transform.localScale = Vector3.one * sizes[i];
                q.GetComponent<MeshRenderer>().material = AssetLoader.Ripple;
                _puddles.Add(q);
            }
        }

        private static void Log(string msg, MessageType type)
        {
            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine($"[世末歌者] {msg}", type);
        }
    }
}
