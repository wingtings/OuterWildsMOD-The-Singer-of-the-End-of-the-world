using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 平面反射水面(README #5)。在歌者北极舞台前放一块反射水池(自建网格,法线朝行星径向),
    // 用一台"镜像相机"把场景按水面翻转渲染到 RenderTexture,Custom/WaterReflection 采样得到倒影。
    // 经典的 Unity 内置管线平面反射写法(反射矩阵 + 斜裁剪近平面)。
    // 材质(Custom/WaterReflection)缺失时整体跳过。
    public class PlanarReflectionController : MonoBehaviour
    {
        private Material _mat;
        private Camera _reflCam;
        private RenderTexture _rt;
        private static bool _rendering;       // 防止反射相机递归触发自身

        // 歌者舞台前的反射水池(Attlerock 局部坐标,贴近迁移后的歌者;最终位置需进游戏 P 键微调)
        private static readonly Vector3 PoolLocal = new Vector3(-2.9f, -5.81f, 30.04f);

        public static void Setup(INewHorizons nh)
        {
            if (AssetLoader.Water == null)
            {
                Log("WaterReflection 材质为空,跳过水面反射。", MessageType.Warning);
                return;
            }

            var planet = nh.GetPlanet("Attlerock");
            if (planet == null)
            {
                Log("废岩星(Attlerock)未就绪,跳过水面反射。", MessageType.Warning);
                return;
            }

            var go = new GameObject("SingerReflectPool");
            go.transform.SetParent(planet.transform, false);
            go.transform.localPosition = PoolLocal;
            // 网格法线(+Y)对齐到该点的行星径向方向 → 水面贴着球面平铺
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, PoolLocal.normalized);

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = BuildQuad(16f);

            var mr = go.AddComponent<MeshRenderer>();
            mr.material = AssetLoader.Water;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var ctrl = go.AddComponent<PlanarReflectionController>();
            ctrl._mat = mr.material;

            Log("水面反射已部署(歌者舞台前)。", MessageType.Success);
        }

        // 每当水面将被某相机渲染前调用 → 用该相机的视角渲染镜像反射
        private void OnWillRenderObject()
        {
            if (_mat == null || _rendering) return;
            var cam = Camera.current;
            if (cam == null) return;

            _rendering = true;

            EnsureReflectionCamera(cam);

            Vector3 pos = transform.position;
            Vector3 normal = transform.up;          // 自建网格的世界法线
            float d = -Vector3.Dot(normal, pos);
            Vector4 plane = new Vector4(normal.x, normal.y, normal.z, d);

            Matrix4x4 reflection = CalcReflectionMatrix(plane);
            _reflCam.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;

            // 斜裁剪:把近裁剪面贴到水面,避免渲染到水面以下的几何(标准做法)
            Vector4 clipPlane = CameraSpacePlane(_reflCam, pos, normal, 1f);
            _reflCam.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);

            // 镜像翻转了绕序,需要反转剔除
            GL.invertCulling = true;
            _reflCam.Render();
            GL.invertCulling = false;

            _mat.SetTexture("_ReflectionTex", _rt);

            _rendering = false;
        }

        private void EnsureReflectionCamera(Camera src)
        {
            if (_rt == null)
                _rt = new RenderTexture(512, 512, 16) { name = "SingerReflectRT" };

            if (_reflCam == null)
            {
                var go = new GameObject("SingerReflectionCam");
                go.hideFlags = HideFlags.HideAndDontSave;
                _reflCam = go.AddComponent<Camera>();
                _reflCam.enabled = false;           // 手动 Render
            }

            _reflCam.CopyFrom(src);                 // 复制 fov/裁剪面/清屏等
            _reflCam.targetTexture = _rt;
            _reflCam.cullingMask = src.cullingMask;
        }

        // —— 标准辅助:反射矩阵 + 相机空间平面 ——
        private static Matrix4x4 CalcReflectionMatrix(Vector4 p)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = 1 - 2 * p.x * p.x; m.m01 = -2 * p.x * p.y; m.m02 = -2 * p.x * p.z; m.m03 = -2 * p.x * p.w;
            m.m10 = -2 * p.y * p.x; m.m11 = 1 - 2 * p.y * p.y; m.m12 = -2 * p.y * p.z; m.m13 = -2 * p.y * p.w;
            m.m20 = -2 * p.z * p.x; m.m21 = -2 * p.z * p.y; m.m22 = 1 - 2 * p.z * p.z; m.m23 = -2 * p.z * p.w;
            m.m30 = 0; m.m31 = 0; m.m32 = 0; m.m33 = 1;
            return m;
        }

        private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sign)
        {
            Vector3 offsetPos = pos + normal * 0.05f;
            Matrix4x4 w2c = cam.worldToCameraMatrix;
            Vector3 cpos = w2c.MultiplyPoint(offsetPos);
            Vector3 cnormal = w2c.MultiplyVector(normal).normalized * sign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        // 平铺在局部 XZ 平面、法线朝 +Y 的方形网格
        private static Mesh BuildQuad(float size)
        {
            float h = size * 0.5f;
            var m = new Mesh { name = "ReflectPoolQuad" };
            m.vertices = new[]
            {
                new Vector3(-h, 0, -h), new Vector3(-h, 0, h),
                new Vector3( h, 0,  h), new Vector3( h, 0, -h)
            };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.uv = new[]
            {
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(1, 1), new Vector2(1, 0)
            };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        private void OnDestroy()
        {
            if (_reflCam != null) Destroy(_reflCam.gameObject);
            if (_rt != null) Destroy(_rt);
        }

        private static void Log(string msg, MessageType type) =>
            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine($"[世末歌者] {msg}", type);
    }
}
