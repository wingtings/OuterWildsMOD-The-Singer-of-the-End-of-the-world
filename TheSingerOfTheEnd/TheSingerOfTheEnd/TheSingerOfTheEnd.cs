using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    public class TheSingerOfTheEnd : ModBehaviour
    {
        public static TheSingerOfTheEnd Instance;
        public INewHorizons NewHorizons;

        // 废岩星(Attlerock)大气层半径(到星心的距离)。体积雾与体积雨只在此半径内生效,
        // 实现"大气层以内才有雾气和下雨"。应与 singer_world.json 的 Atmosphere.size / Base.surfaceSize 协调。
        public const float AttlerockAtmosphereRadius = 150f;

        // 七个 shader 各自的开关(供各 Controller 读取，避免每帧查 Config)
        public bool GodRayEnabled    { get; private set; }
        public bool RainEnabled      { get; private set; }   // 体积雨粒子
        public bool RippleEnabled    { get; private set; }   // 地面涟漪水洼
        public bool AudioWaveEnabled { get; private set; }
        public bool FogEnabled       { get; private set; }
        public bool WaterEnabled     { get; private set; }
        public bool HologramEnabled  { get; private set; }

        // 配置项键名(与 default-config.json 完全一致)
        private const string K_GodRay   = "God Ray 神光";
        private const string K_Rain     = "Volumetric Rain 体积雨";
        private const string K_Ripple   = "Rain Ripple 地面涟漪";
        private const string K_Audio    = "Audio Wave 声波可视化";
        private const string K_Fog      = "Volumetric Fog 体积雾";
        private const string K_Water    = "Water Reflection 水面反射";
        private const string K_Hologram = "Hologram 全息投影";

        private void ReadShaderToggles(IModConfig config)
        {
            GodRayEnabled    = config.GetSettingsValue<bool>(K_GodRay);
            RainEnabled      = config.GetSettingsValue<bool>(K_Rain);
            RippleEnabled    = config.GetSettingsValue<bool>(K_Ripple);
            AudioWaveEnabled = config.GetSettingsValue<bool>(K_Audio);
            FogEnabled       = config.GetSettingsValue<bool>(K_Fog);
            WaterEnabled     = config.GetSettingsValue<bool>(K_Water);
            HologramEnabled  = config.GetSettingsValue<bool>(K_Hologram);
        }

        public void Awake()
        {
            Instance = this;
        }

        public void Start()
        {
            ModHelper.Console.WriteLine($"[世末歌者] MOD is loading...", MessageType.Info);

            NewHorizons = ModHelper.Interaction.TryGetModApi<INewHorizons>("xen.NewHorizons");
            if (NewHorizons == null)
            {
                ModHelper.Console.WriteLine("[世末歌者] ERROR: New Horizons API not found!", MessageType.Error);
                return;
            }
            ModHelper.Console.WriteLine("[世末歌者] New Horizons API acquired.", MessageType.Success);

            try
            {
                NewHorizons.LoadConfigs(this);
                ModHelper.Console.WriteLine("[世末歌者] Configs loaded successfully.", MessageType.Success);
            }
            catch (Exception ex)
            {
                ModHelper.Console.WriteLine($"[世末歌者] ERROR loading configs: {ex.Message}", MessageType.Error);
                ModHelper.Console.WriteLine(ex.StackTrace, MessageType.Error);
                return;
            }

            // 预加载自定义 shader 的 AssetBundle 并取出材质（只加载一次，内部已缓存）
            AssetLoader.Preload();

            try
            {
                new Harmony("wingtings.TheSingerOfTheEnd").PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                ModHelper.Console.WriteLine($"[世末歌者] Harmony patch warning: {ex.Message}", MessageType.Warning);
            }

            ReadShaderToggles(ModHelper.Config);

            NewHorizons.GetStarSystemLoadedEvent().AddListener(OnStarSystemLoaded);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

            LogSettings();
            ModHelper.Console.WriteLine("[世末歌者] MOD loaded successfully!", MessageType.Success);
        }

        public override void Configure(IModConfig config)
        {
            ReadShaderToggles(config);
            ApplyShaderTogglesLive();

            // 一次性剧情重置触发器
            if (config.GetSettingsValue<bool>("Reset Story Progress (重置剧情进度)"))
            {
                ResetStoryProgress();
                // 执行后立即复原，避免每次启动都触发
                config.SetSettingsValue("Reset Story Progress (重置剧情进度)", false);
            }

            if (IsDebug())
            {
                ModHelper.Console.WriteLine("[世末歌者] Settings changed, re-reading config...", MessageType.Info);
                LogSettings();
            }
        }

        // 设置变更时即时同步到当前场景里已存在的效果。
        // 关闭立即生效;若把某个之前关着的效果重新打开,部分可能需重进循环/场景(下次 SetupGraphics 重建)。
        private void ApplyShaderTogglesLive()
        {
            var godRay = FindObjectOfType<GodRayController>();
            if (godRay != null) godRay.enabled = GodRayEnabled;

            var fog = FindObjectOfType<VolumetricFogController>();
            if (fog != null) fog.enabled = FogEnabled;

            if (RainController.Instance != null)
            {
                if (RainEnabled) RainController.Instance.EnablePS();
                else             RainController.Instance.DisablePS();
            }
            RainController.SetRipplesActive(RippleEnabled);

            AudioVisualizerController.SetActive(AudioWaveEnabled);
            PlanarReflectionController.SetActive(WaterEnabled);
            HologramController.SetActive(HologramEnabled);
        }

        // 清除本 MOD 所有对话条件（含持久条件），方便测试时重置剧情
        private void ResetStoryProgress()
        {
            string[] conditions =
            {
                "TALKED_TO_TIANYI", "AMPLIFIER_REPAIRED", "TIANYI_HEARD_SONG",
                "AMPLIFIER_EVER_REPAIRED", "HAS_TOWER_PART", "HAS_GODREALM_PART",
                "AMP_SOCKET_A", "AMP_SOCKET_B", "TALKED_TO_SINGER", "TIANYI_REUNITED"
            };

            var mgr = DialogueConditionManager.SharedInstance;
            foreach (var c in conditions)
            {
                mgr?.SetConditionState(c, false);
                try { PlayerData.SetPersistentCondition(c, false); } catch { }
            }

            ModHelper.Console.WriteLine(
                "[世末歌者] 剧情进度已重置（对话条件清除）。飞船日志事实需在游戏菜单手动清除。",
                MessageType.Success);
        }

        private void LogSettings()
        {
            ModHelper.Console.WriteLine(
                $"[世末歌者] Shader 开关: GodRay={GodRayEnabled}, Rain={RainEnabled}, Ripple={RippleEnabled}, " +
                $"Audio={AudioWaveEnabled}, Fog={FogEnabled}, Water={WaterEnabled}, Hologram={HologramEnabled}, " +
                $"Debug={IsDebug()}", MessageType.Info);
        }

        public bool IsDebug()
            => ModHelper.Config.GetSettingsValue<bool>("Debug Mode (调试模式)");

        private void OnStarSystemLoaded(string systemName)
        {
            ModHelper.Console.WriteLine($"[世末歌者] Star system loaded: {systemName}", MessageType.Success);
            if (systemName != "SolarSystem") return;

            ModHelper.Console.WriteLine("[世末歌者] SolarSystem loaded! Setting up...", MessageType.Success);

            var singerCity = NewHorizons.GetPlanet("Attlerock");
            if (singerCity != null)
                ModHelper.Console.WriteLine("[世末歌者] 废岩星(世末之城/Attlerock) found.", MessageType.Success);
            else
                ModHelper.Console.WriteLine("[世末歌者] WARNING: Attlerock not found!", MessageType.Warning);

            var godRealm = NewHorizons.GetPlanet("Quantum Moon");
            if (godRealm != null)
                ModHelper.Console.WriteLine("[世末歌者] 量子月(神谕之境) found.", MessageType.Success);
            else
                ModHelper.Console.WriteLine("[世末歌者] WARNING: Quantum Moon not found!", MessageType.Warning);

            // 每个循环挂载结局判定器（随场景重建）
            var judge = new GameObject("SingerEndingJudge");
            judge.AddComponent<EndingJudge>();
            ModHelper.Console.WriteLine("[世末歌者] EndingJudge attached.", MessageType.Success);

            // 等玩家相机/身体就绪后挂载图形效果与 NPC 行为
            StartCoroutine(SetupGraphics());
        }

        // 玩家相机要到场景加载后才存在，用协程等待再挂载后处理。
        private IEnumerator SetupGraphics()
        {
            // 等待玩家相机（最多 30 秒）
            OWCamera owCam = null;
            float timeout = 0f;
            while ((owCam = Locator.GetPlayerCamera()) == null && timeout < 30f)
            {
                timeout += Time.deltaTime;
                yield return null;
            }
            if (owCam == null)
            {
                ModHelper.Console.WriteLine("[世末歌者] 玩家相机超时，图形效果未挂载。", MessageType.Warning);
                yield break;
            }

            // 相机后处理(God Rays / 体积雾)挂在主相机 GameObject 上。
            // 始终挂载,用各自开关控制 enabled;材质缺失时控制器内部直接透传。
            var camGo = owCam.mainCamera.gameObject;
            if (camGo.GetComponent<GodRayController>() == null)
            {
                var sun  = NewHorizons.GetPlanet("Sun");
                var ctrl = camGo.AddComponent<GodRayController>();
                ctrl.Init(AssetLoader.GodRay, sun != null ? sun.transform : null);
                ctrl.enabled = GodRayEnabled;
                ModHelper.Console.WriteLine("[世末歌者] GodRayController attached.", MessageType.Success);
            }
            if (AssetLoader.Fog != null && camGo.GetComponent<VolumetricFogController>() == null)
            {
                var fog = camGo.AddComponent<VolumetricFogController>();
                // 体积雾限定在废岩星(Attlerock)附近,避免在太空生效
                fog.Init(AssetLoader.Fog, NewHorizons.GetPlanet("Attlerock")?.transform);
                fog.enabled = FogEnabled;
                ModHelper.Console.WriteLine("[世末歌者] VolumetricFogController attached.", MessageType.Success);
            }

            // 等玩家身体就绪后部署降雨 + 涟漪 + 声波 + 水面 + 全息(各控制器内部按开关 SetActive,材质缺失自动跳过)
            while (Locator.GetPlayerTransform() == null) yield return null;

            RainController.Setup();
            AudioVisualizerController.Setup(NewHorizons);
            PlanarReflectionController.Setup(NewHorizons);
            HologramController.Setup(NewHorizons);

            NpcBehavior.Setup(NewHorizons);
            TimelineManager.Setup();

            ModHelper.Console.WriteLine("[世末歌者] SetupGraphics complete.", MessageType.Success);
        }

        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (IsDebug())
                ModHelper.Console.WriteLine(
                    $"[世末歌者] Scene changed: {previousScene} -> {newScene}", MessageType.Info);

            if (newScene != OWScene.SolarSystem) return;
            ModHelper.Console.WriteLine("[世末歌者] Entered solar system scene.", MessageType.Success);
        }

        public void OnDestroy()
        {
            LoadManager.OnCompleteSceneLoad -= OnCompleteSceneLoad;
        }
    }
}
