using System.IO;
using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 加载并缓存自定义 shader 的 AssetBundle(assets/shaders/shaders)。
    // 同一个 bundle 不能被 LoadFromFile 加载两次,因此全程只加载一次并缓存。
    public static class AssetLoader
    {
        private const string BundleRelPath = "assets/shaders/shaders";

        private static AssetBundle _bundle;
        private static bool _tried;

        public static Material GodRay { get; private set; }
        public static Material Rain { get; private set; }
        public static Material Ripple { get; private set; }
        public static Material AudioWave { get; private set; }
        public static Material Fog { get; private set; }
        public static Material Water { get; private set; }
        public static Material Hologram { get; private set; }

        private static AssetBundle Bundle
        {
            get
            {
                if (_bundle == null && !_tried)
                {
                    _tried = true;
                    var path = Path.Combine(
                        TheSingerOfTheEnd.Instance.ModHelper.Manifest.ModFolderPath, BundleRelPath);
                    _bundle = AssetBundle.LoadFromFile(path);
                    if (_bundle == null)
                        Log($"加载 shaders bundle 失败: {path}", MessageType.Error);
                    else
                        Log("shaders bundle 加载成功。", MessageType.Success);
                }
                return _bundle;
            }
        }

        // 在 MOD 启动时调用一次,把三个材质取出来缓存。
        public static void Preload()
        {
            if (Bundle == null) return;
            GodRay = Bundle.LoadAsset<Material>("Assets/Materials/GodRayMat.mat");
            Rain = Bundle.LoadAsset<Material>("Assets/Materials/RainMat.mat");
            Ripple = Bundle.LoadAsset<Material>("Assets/Materials/RippleMat.mat");
            // 以下四个材质需在 Unity 工程里新建并打进同一 shaders bundle 后才会非空;
            // 未打包前为 null,对应控制器会自动跳过,不影响其它效果与编译。
            AudioWave = Bundle.LoadAsset<Material>("Assets/Materials/AudioWaveMat.mat");
            Fog = Bundle.LoadAsset<Material>("Assets/Materials/FogMat.mat");
            Water = Bundle.LoadAsset<Material>("Assets/Materials/WaterMat.mat");
            Hologram = Bundle.LoadAsset<Material>("Assets/Materials/HologramMat.mat");
            Log($"材质加载: GodRay={GodRay != null}, Rain={Rain != null}, Ripple={Ripple != null}, " +
                $"AudioWave={AudioWave != null}, Fog={Fog != null}, Water={Water != null}, " +
                $"Hologram={Hologram != null}",
                MessageType.Info);
        }

        private static void Log(string msg, MessageType type)
        {
            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine($"[世末歌者] {msg}", type);
        }
    }
}
