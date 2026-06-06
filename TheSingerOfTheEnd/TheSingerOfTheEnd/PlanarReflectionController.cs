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
        private static GameObject _root;      // 供设置开关即时启停
        private Transform _planet;            // 星球 transform, Update 里不断 push 位置
        private float _planetRadius;          // 水池所在的"地面半径" (Attlerock 地形不规则, 歌者音乐厅区≈30)

        // 歌者音乐厅区前的水洼中心 (Attlerock 局部坐标). 歌者本体位于 (-6.7, 2.2, 29.4) magnitude≈30.
        // 真水洼形状由 TryBuildTerrainMesh() 在 Attlerock 地形碰撞体加载后用射线网格扫描生成,
        // 水面整体仍是 PoolLocal 切平面 (=平面反射数学正确).
        private static readonly Vector3 PoolLocal = new Vector3(-6.7f, 2.2f, 29.4f);

        // —— 地形扫描参数 ——
        private const float ScanRadius   = 25f;   // 围绕 PoolLocal 在切平面上扫描的半径 (m)
        private const float ScanCellSize = 0.5f;  // 网格分辨率 (m), 0.5m 给出 60×60 格
        private const float ScanRayUp    = 12f;   // 射线起点抬到切平面以上多少米
        private const float ScanRayLen   = 30f;   // 射线最大长度
        private const float WaterLift    = -0.3f; // 水面 = 低位地形海拔 + 这个偏移 (负值 = 整体压低水位 → 歌者脚下变浅)
        private const int   MinHitsToBuild = 200; // 命中少于这个数视为地形未加载, 下帧重试
        private const int   ShoreFadeCells = 4;   // 岸边羽化嬽量 (格) - 0=水岸 → 1=足够深不透
        // 球心沿径向反方向(从星心穿过 PoolLocal 的延长线再往下)平移多少米; 0=贴星球曲率, 越大水面越扁。
        // 等效于把水面所在球的半径从 waterAlt 增大到 waterAlt+CurvatureBoost,
        // 球面仍过 PoolLocal 但远端不再"塌"那么厉害 → 岸自然往外推, 歌者脚下水位不变。
        private const float CurvatureBoost = 17.5f;
        private bool _meshBuilt;

        private static readonly int _PlanetCenterID  = Shader.PropertyToID("_PlanetCenter");
        private static readonly int _PlanetRadiusID  = Shader.PropertyToID("_PlanetRadius");
        private static readonly int _ReflectionTexID = Shader.PropertyToID("_ReflectionTex");

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
            // +Y 对齐到 PoolLocal 处的径向方向 → 水面位于该点切平面 (与星球地表局部相切)
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, PoolLocal.normalized);

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = new Mesh { name = "TerrainPuddleMesh_Empty" };  // 占位空网格, Update 内扫描后填充

            var mr = go.AddComponent<MeshRenderer>();
            mr.material = AssetLoader.Water;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var ctrl = go.AddComponent<PlanarReflectionController>();
            ctrl._mat = mr.material;
            ctrl._planet = planet.transform;
            // _PlanetRadius 在 TryBuildTerrainMesh 中扫描得出水面海拔后才设置 (贴合球面)。
            // _EdgeFade 关闭: 不规则水洼形状不需要圆形 UV 羽化,
            // 否则远离 UV 中心的湿格子会被透明掉。
            ctrl._mat.SetFloat("_EdgeFade", 0f);

            _root = go;
            go.SetActive(TheSingerOfTheEnd.Instance.WaterEnabled);

            Log("水面反射控制器已部署, 等待 Attlerock 地形加载后扫描水洼形状...", MessageType.Info);
        }

        // 供设置开关即时启停(关掉时水池隐藏,反射相机随之不再渲染)
        public static void SetActive(bool active)
        {
            if (_root != null) _root.SetActive(active);
        }

        // 每帧把(虚拟)球心位置同步给 shader; 网格未生成时反复尝试 (等地形碰撞体加载)。
        // 虚拟球心 = 星球中心沿"星心→PoolLocal"反方向移 CurvatureBoost 米 → 球面更扁。
        private void Update()
        {
            if (_mat == null || _planet == null) return;
            Vector3 dWorld = _planet.TransformDirection(PoolLocal.normalized);
            _mat.SetVector(_PlanetCenterID, _planet.position - dWorld * CurvatureBoost);
            if (!_meshBuilt) TryBuildTerrainMesh();
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
                // 1024 分辨率 + 24 位深度, 避免小水池里倍果严重偏小
                _rt = new RenderTexture(1024, 1024, 24) { name = "SingerReflectRT" };

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

        // 在 PoolLocal 周围对真实地形碰撞体做射线扫描, 把低洼网格构成水洼 mesh。
        // 调用时若地形未加载(碰撞体未就绪) 命中数过少 → 返回, 下一帧再试。
        // 水面整体仍是 PoolLocal 切平面 (=平面反射数学正确), 仅"形状"由地形决定。
        private void TryBuildTerrainMesh()
        {
            Vector3 nrm = PoolLocal.normalized;
            BuildTangentBasis(nrm, out var tU, out var tV);

            int half = Mathf.CeilToInt(ScanRadius / ScanCellSize);
            int n = half * 2 + 1;

            var altitudes = new float[n, n];
            var hits      = new bool [n, n];
            int hitCount  = 0;

            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i - half) * ScanCellSize;
                float v = (j - half) * ScanCellSize;
                Vector3 cellLocal     = PoolLocal + tU * u + tV * v;
                Vector3 rayStartWorld = _planet.TransformPoint(cellLocal + nrm * ScanRayUp);
                Vector3 rayDirWorld   = _planet.TransformDirection(-nrm);

                // 用 RaycastAll 取所有命中, 选离星心最近的那个 = 真地面;
                // 这样跳过歌者/舞台/建筑的 collider, 不会在道具周围出现"干岛"。
                var allHits = Physics.RaycastAll(rayStartWorld, rayDirWorld, ScanRayLen,
                                                  ~0, QueryTriggerInteraction.Ignore);
                if (allHits.Length > 0)
                {
                    float minAlt = float.MaxValue;
                    for (int k = 0; k < allHits.Length; k++)
                    {
                        float a = (allHits[k].point - _planet.position).magnitude;
                        if (a < minAlt) minAlt = a;
                    }
                    altitudes[i, j] = minAlt;
                    hits[i, j] = true;
                    hitCount++;
                }
            }

            if (hitCount < MinHitsToBuild) return; // 地形碰撞体还没加载好, 等下一帧

            // 取所有命中的 25% 分位数地形高度作"低洼基线", + WaterLift 作水面海拔。
            // 该选择让最低 ~25% 的地形被淹没, 其余高出水面 → 出现自然边界。
            var sorted = new System.Collections.Generic.List<float>(hitCount);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                if (hits[i, j]) sorted.Add(altitudes[i, j]);
            sorted.Sort();
            float p25 = sorted[sorted.Count / 4];
            float waterAlt = p25 + WaterLift;

            // 球心下移 CurvatureBoost 后的新球半径; 球面仍过 PoolLocal (那里水位=waterAlt 不变),
            // 切平面偏移 r 处水位提高 ≈ 0.5 * r² * (1/waterAlt - 1/R)。
            float R        = waterAlt + CurvatureBoost;
            float invDelta = 0.5f * (1f / waterAlt - 1f / R);

            var wet = new bool[n, n];
            int wetCount = 0;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                if (!hits[i, j]) continue;
                float u = (i - half) * ScanCellSize;
                float v = (j - half) * ScanCellSize;
                float waterAltAtCell = waterAlt + (u * u + v * v) * invDelta;
                if (altitudes[i, j] < waterAltAtCell)
                {
                    wet[i, j] = true;
                    wetCount++;
                }
            }

            if (wetCount < 4)
            {
                // 区域过于平坦 → 退一步, 取最低 15% 强制成池
                float fallback = sorted[Mathf.Max(1, sorted.Count * 15 / 100)];
                waterAlt = fallback + WaterLift;
                R        = waterAlt + CurvatureBoost;
                invDelta = 0.5f * (1f / waterAlt - 1f / R);
                wetCount = 0;
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    if (!hits[i, j]) continue;
                    float u = (i - half) * ScanCellSize;
                    float v = (j - half) * ScanCellSize;
                    float waterAltAtCell = waterAlt + (u * u + v * v) * invDelta;
                    if (altitudes[i, j] <= waterAltAtCell)
                    {
                        wet[i, j] = true;
                        wetCount++;
                    }
                }
            }

            // 计算顶点到岸的 Chebyshev 距离 (格子数) → shader 里做平滑岸边透明度。
            // 岸顶点: 有任一相邻格子 “不湿” (dry 或越界) → 距离=0; 其它顶点起始=ShoreFadeCells, 迭代 K 次取邻居 min+1
            var deep = new int[n + 1, n + 1];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                bool isShore = false;
                for (int dj = -1; dj <= 0 && !isShore; dj++)
                for (int di = -1; di <= 0 && !isShore; di++)
                {
                    int ci = i + di, cj = j + dj;
                    if (ci < 0 || ci >= n || cj < 0 || cj >= n || !wet[ci, cj]) isShore = true;
                }
                deep[i, j] = isShore ? 0 : ShoreFadeCells;
            }
            for (int iter = 0; iter < ShoreFadeCells; iter++)
            {
                for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    if (deep[i, j] == 0) continue;
                    int best = deep[i, j];
                    for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || ni > n || nj < 0 || nj > n) continue;
                        if (deep[ni, nj] + 1 < best) best = deep[ni, nj] + 1;
                    }
                    deep[i, j] = best;
                }
            }

            // 对岸距离场做 5-tap 可分离高斯模糊 (1-4-6-4-1 / 16) → 软化 BFS 离散阶梯,
            // 让真实地形岸的 UV 梯度更圆润, shader 端 smoothstep 出来近似高斯渐变。
            var deepF = new float[n + 1, n + 1];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++) deepF[i, j] = deep[i, j];

            var tmp = new float[n + 1, n + 1];
            // 横向
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float s = 0f, w = 0f;
                for (int k = -2; k <= 2; k++)
                {
                    int ii = i + k;
                    if (ii < 0 || ii > n) continue;
                    float kw = (k == 0) ? 6f : (Mathf.Abs(k) == 1 ? 4f : 1f);
                    s += deepF[ii, j] * kw;
                    w += kw;
                }
                tmp[i, j] = s / w;
            }
            // 纵向
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float s = 0f, w = 0f;
                for (int k = -2; k <= 2; k++)
                {
                    int jj = j + k;
                    if (jj < 0 || jj > n) continue;
                    float kw = (k == 0) ? 6f : (Mathf.Abs(k) == 1 ? 4f : 1f);
                    s += tmp[i, jj] * kw;
                    w += kw;
                }
                deepF[i, j] = s / w;
            }

            // mesh-local 空间: +Y = 该点径向 (因为 GO 旋转把 +Y 对齐到 nrm), XZ = 切平面。
            // 顶点 Y 按曲率水位算; shader 会按虚拟球再投影一次, 这里给近似 Y 让 mesh.bounds 不被 frustum 误剔除。

            var verts = new System.Collections.Generic.List<Vector3>();
            var nrms  = new System.Collections.Generic.List<Vector3>();
            var uvs   = new System.Collections.Generic.List<Vector2>();
            var tris  = new System.Collections.Generic.List<int>();
            var vIdx  = new int[n + 1, n + 1];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++) vIdx[i, j] = -1;

            int GetVert(int i, int j)
            {
                if (vIdx[i, j] >= 0) return vIdx[i, j];
                float u = (i - half - 0.5f) * ScanCellSize;
                float v = (j - half - 0.5f) * ScanCellSize;
                float waterAltAtVert = waterAlt + (u * u + v * v) * invDelta;
                float yLocalVert     = waterAltAtVert - PoolLocal.magnitude;
                verts.Add(new Vector3(u, yLocalVert, v));
                nrms .Add(Vector3.up);
                // UV.x = 岸距离 (0=岸 → 1=足够深, 高斯平滑后), shader 用这个调 alpha 羽化。
                // UV.y 未使用 (保留).
                float shore = Mathf.Clamp01(deepF[i, j] / ShoreFadeCells);
                uvs  .Add(new Vector2(shore, 0f));
                return vIdx[i, j] = verts.Count - 1;
            }

            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                if (!wet[i, j]) continue;
                int a = GetVert(i,     j    );
                int b = GetVert(i + 1, j    );
                int c = GetVert(i + 1, j + 1);
                int d = GetVert(i,     j + 1);
                tris.Add(a); tris.Add(d); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(b);
            }

            var m = new Mesh { name = "TerrainPuddleMesh" };
            m.indexFormat = (verts.Count > 65000)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(verts);
            m.SetNormals(nrms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();

            GetComponent<MeshFilter>().mesh = m;
            // 让 shader 把这些顶点投影到 R 半径(虚拟扁球)上 → 水面在 PoolLocal 不变, 远端抬高。
            // 球心已在 Update() 里每帧填到 _planet.position - dWorld*CurvatureBoost。
            _planetRadius = R;
            _mat.SetFloat(_PlanetRadiusID, R);
            // 岸边羽化跨越区间 (UV.x 单位): 0→_EdgeFade 是平滑过渡区。
            // 0.5 = 从岸边 → 中间深度 这段全部用于渐变。
            _mat.SetFloat("_EdgeFade", 0.5f);
            _meshBuilt = true;

            Log($"水洼网格已生成: {wetCount} 格 / {n * n} 扫描点 (命中 {hitCount}), 中心水面海拔 {waterAlt:F2}m, 虚拟球半径 {R:F2}m。", MessageType.Success);
        }

        private static void BuildTangentBasis(Vector3 N, out Vector3 U, out Vector3 V)
        {
            Vector3 a = Mathf.Abs(N.y) < 0.99f ? Vector3.up : Vector3.right;
            U = Vector3.Cross(a, N).normalized;
            V = Vector3.Cross(N, U);
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
