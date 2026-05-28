using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 全息投影面板(README #6)。在神谕之境(Quantum Moon)的石碑区上空放一块竖直发光面板,
    // 用 Custom/Hologram 渲染(扫描线 + Fresnel 边缘光 + Glitch),作为"神明信息展示"。
    // 面板绕行星径向缓慢自转 + 沿径向轻微浮动,强化"悬浮投影体"观感。
    // 材质(Custom/Hologram)缺失时整体跳过,不影响其它效果与编译。
    public class HologramController : MonoBehaviour
    {
        private const float SpinSpeed = 18f;   // 自转角速度(度/秒)
        private const float BobAmp    = 0.15f; // 浮动幅度(米)
        private const float BobSpeed  = 1.3f;

        // 神谕之境石碑区上空(Quantum Moon 局部坐标,表面半径≈72;此处抬到≈75 形成"悬浮")
        private static readonly Vector3 PanelLocal = new Vector3(6f, 74f, 13f);

        private float _bobPhase;
        private Vector3 _baseLocalPos;
        private Vector3 _upLocal;

        public static void Setup(INewHorizons nh)
        {
            if (AssetLoader.Hologram == null)
            {
                Log("Hologram 材质为空,跳过全息投影。", MessageType.Warning);
                return;
            }

            var planet = nh.GetPlanet("Quantum Moon");
            if (planet == null)
            {
                Log("量子月(神谕之境)未就绪,跳过全息投影。", MessageType.Warning);
                return;
            }

            var go = new GameObject("OracleHologram");
            go.transform.SetParent(planet.transform, false);
            go.transform.localPosition = PanelLocal;

            // 让面板竖直:局部 +Y(法线上方)对齐行星径向;+Z(面板正面)朝某一切向。
            Vector3 up = PanelLocal.normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.ProjectOnPlane(Vector3.right, up);
            tangent.Normalize();
            go.transform.localRotation = Quaternion.LookRotation(tangent, up);

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = BuildPanel(3.2f, 4.2f);

            var mr = go.AddComponent<MeshRenderer>();
            mr.material = AssetLoader.Hologram;     // 直接用共享材质实例即可(单个面板)
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var ctrl = go.AddComponent<HologramController>();
            ctrl._baseLocalPos = PanelLocal;
            ctrl._upLocal = up;

            Log("全息投影已部署(神谕之境石碑区)。", MessageType.Success);
        }

        private void Update()
        {
            // 绕行星径向(局部 +Y)缓慢自转
            transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.Self);

            // 沿径向轻微上下浮动
            _bobPhase += Time.deltaTime * BobSpeed;
            transform.localPosition = _baseLocalPos + _upLocal * (Mathf.Sin(_bobPhase) * BobAmp);
        }

        // 竖直面板:XY 平面、法线 +Z、以原点为中心。双面由 shader 的 Cull Off 保证。
        private static Mesh BuildPanel(float width, float height)
        {
            float hw = width * 0.5f, hh = height * 0.5f;
            var m = new Mesh { name = "HologramPanel" };
            m.vertices = new[]
            {
                new Vector3(-hw, -hh, 0f), new Vector3(-hw, hh, 0f),
                new Vector3( hw,  hh, 0f), new Vector3( hw, -hh, 0f)
            };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            m.uv = new[]
            {
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(1, 1), new Vector2(1, 0)
            };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        private static void Log(string msg, MessageType type) =>
            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine($"[世末歌者] {msg}", type);
    }
}
