using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 声波可视化(README #4):以歌者为中心的环形网格,顶点位移由实时 FFT 频谱驱动。
    // FFT 取自 AudioListener.GetSpectrumData —— 它捕获玩家"听到的所有声音",
    // 因此玩家靠近歌者时,环面会随歌声(伴奏/人声信号)起伏,无需直接拿到歌者的 AudioSource。
    // 材质(Custom/AudioWave)缺失时整体跳过,不影响其它效果。
    public class AudioVisualizerController : MonoBehaviour
    {
        private const int Bins = 32;          // 与 shader 中 _Spectrum[32] 对应
        private const int FftSize = 256;      // 必须是 2 的幂
        private const float Gain = 9f;

        private readonly float[] _samples = new float[FftSize];
        private readonly float[] _bins = new float[Bins];
        private Material _mat;
        private static GameObject _root;   // 供设置开关即时启停

        // 歌者所在的音乐厅舞台(Attlerock 局部坐标,与 singer_world.json 迁移后的歌者位置对齐)
        private static readonly Vector3 StageLocal = new Vector3(-5.52638f, -7.194386f, 29.36535f);

        public static void Setup(INewHorizons nh)
        {
            if (AssetLoader.AudioWave == null)
            {
                Log("AudioWave 材质为空,跳过声波可视化。", MessageType.Warning);
                return;
            }

            var planet = nh.GetPlanet("Attlerock");
            if (planet == null)
            {
                Log("废岩星(Attlerock)未就绪,跳过声波可视化。", MessageType.Warning);
                return;
            }

            // 中心对齐到歌者的实际位置(歌者可能被重新摆放);找不到再回退到常量。
            Vector3 stageLocal = StageLocal;
            var singer = FindDeep(planet.transform, "歌者(阿绫)");
            if (singer != null)
                stageLocal = planet.transform.InverseTransformPoint(singer.position);

            var go = new GameObject("SingerAudioWave");
            go.transform.SetParent(planet.transform, false);
            go.transform.localPosition = stageLocal;
            // 让网格的局部 +Y(法线)对齐到该点的行星径向方向
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, stageLocal.normalized);

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = BuildRing(64, 8, 1.5f, 11f);

            var mr = go.AddComponent<MeshRenderer>();
            mr.material = AssetLoader.AudioWave;       // 取实例,避免改到共享材质
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var ctrl = go.AddComponent<AudioVisualizerController>();
            ctrl._mat = mr.material;

            _root = go;
            go.SetActive(TheSingerOfTheEnd.Instance.AudioWaveEnabled);

            Log("声波可视化已部署(歌者音乐厅舞台)。", MessageType.Success);
        }

        // 供设置开关即时启停(关掉时整块声波网格隐藏)
        public static void SetActive(bool active)
        {
            if (_root != null) _root.SetActive(active);
        }

        private void Update()
        {
            if (_mat == null) return;

            AudioListener.GetSpectrumData(_samples, 0, FFTWindow.BlackmanHarris);

            int per = FftSize / Bins;
            for (int b = 0; b < Bins; b++)
            {
                float sum = 0f;
                for (int k = 0; k < per; k++) sum += _samples[b * per + k];
                // sqrt 提升弱信号可见度,clamp 到 [0,1]
                _bins[b] = Mathf.Clamp01(Mathf.Sqrt(sum / per) * Gain);
            }

            _mat.SetFloatArray("_Spectrum", _bins);
        }

        // 生成一块平铺在局部 XZ 平面、法线朝 +Y 的环形(annulus)网格。
        // uv.x = 绕环角度(→频段索引),uv.y = 从内圈到外圈(→涟漪传播)。
        private static Mesh BuildRing(int angular, int radial, float innerR, float outerR)
        {
            int vCols = angular + 1;
            int vRows = radial + 1;
            var verts = new Vector3[vCols * vRows];
            var norms = new Vector3[vCols * vRows];
            var uvs = new Vector2[vCols * vRows];

            for (int r = 0; r < vRows; r++)
            {
                float tr = r / (float)radial;
                float rad = Mathf.Lerp(innerR, outerR, tr);
                for (int a = 0; a < vCols; a++)
                {
                    float ta = a / (float)angular;
                    float ang = ta * Mathf.PI * 2f;
                    int idx = r * vCols + a;
                    verts[idx] = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                    norms[idx] = Vector3.up;
                    uvs[idx] = new Vector2(ta, tr);
                }
            }

            var tris = new int[angular * radial * 6];
            int t = 0;
            for (int r = 0; r < radial; r++)
            {
                for (int a = 0; a < angular; a++)
                {
                    int i0 = r * vCols + a;
                    int i1 = i0 + 1;
                    int i2 = i0 + vCols;
                    int i3 = i2 + 1;
                    tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                    tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
                }
            }

            var m = new Mesh { name = "AudioWaveRing" };
            m.vertices = verts;
            m.normals = norms;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        // 深度优先按名查找子物体(歌者由 NH 的 rename 命名为 "歌者(阿绫)")
        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void Log(string msg, MessageType type) =>
            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine($"[世末歌者] {msg}", type);
    }
}
