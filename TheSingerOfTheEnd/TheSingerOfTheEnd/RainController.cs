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
        private const float RainRadius = 200f;

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
            if (AssetLoader.Rain == null)
            {
                Log("Rain 材质为空,跳过降雨。", MessageType.Warning);
                return;
            }

            var player = Locator.GetPlayerTransform();
            var planet = TheSingerOfTheEnd.Instance.NewHorizons.GetPlanet("Attlerock");
            if (player == null || planet == null)
            {
                Log("玩家或废岩星(Attlerock)未就绪,跳过降雨。", MessageType.Warning);
                return;
            }

            var go = new GameObject("SingerRain");
            go.transform.SetParent(player, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.startLifetime = 1.5f;
            main.startSpeed = 0f;                 // 速度交给 velocityOverLifetime
            main.startSize = 0.15f;
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
            ps.Play();

            SpawnPuddles(planet.transform);
            Log("降雨已部署(跟随玩家,城区内启用)。", MessageType.Success);
        }

        private void Update()
        {
            if (_ps == null || _planet == null) return;
            var player = Locator.GetPlayerTransform();
            if (player == null) return;

            // 以故事区域世界坐标为圆心判定(随星球公转同步移动)
            var storyCenter = _planet.TransformPoint(StoryZoneLocal);
            bool inCity = Vector3.Distance(player.position, storyCenter) < RainRadius;
            var emission = _ps.emission;
            if (emission.enabled != inCity) emission.enabled = inCity;
        }

        // 在城区平整地面铺几块涟漪水洼。父级设为星球,使其随星球自转/公转。
        private static void SpawnPuddles(Transform planet)
        {
            if (AssetLoader.Ripple == null) return;

            // 歌者音乐厅舞台周围地表(贴近迁移后的歌者,略高于表面半径≈30.7 避免 z-fighting;
            // 最终位置需进游戏用 P 键微调贴合地形)
            Vector3[] spots =
            {
                new Vector3(-4f, -6f, 30.1f),
                new Vector3(-8f, -6f, 29.4f),
                new Vector3(-3f, -9f, 29.5f)
            };
            float[] sizes = { 8f, 6f, 7f };

            for (int i = 0; i < spots.Length; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "SingerPuddle_" + i;
                Object.Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(planet, false);
                q.transform.localPosition = spots[i];
                q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // 平铺,法线朝上
                q.transform.localScale = Vector3.one * sizes[i];
                q.GetComponent<MeshRenderer>().material = AssetLoader.Ripple;
            }
        }

        private static void Log(string msg, MessageType type)
        {
            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine($"[世末歌者] {msg}", type);
        }
    }
}
