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

        // 供各 Controller 读取，避免每帧查 Config
        public bool ShadersEnabled { get; private set; }

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

            ShadersEnabled = ModHelper.Config.GetSettingsValue<bool>("Shader Effects Enabled (启用Shader效果)");

            NewHorizons.GetStarSystemLoadedEvent().AddListener(OnStarSystemLoaded);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

            LogSettings();
            ModHelper.Console.WriteLine("[世末歌者] MOD loaded successfully!", MessageType.Success);
        }

        public override void Configure(IModConfig config)
        {
            ShadersEnabled = config.GetSettingsValue<bool>("Shader Effects Enabled (启用Shader效果)");

            // 立即同步到已存在的控制器
            var godRay = FindObjectOfType<GodRayController>();
            if (godRay != null) godRay.enabled = ShadersEnabled;

            var fog = FindObjectOfType<VolumetricFogController>();
            if (fog != null) fog.enabled = ShadersEnabled;

            if (RainController.Instance != null)
            {
                if (ShadersEnabled) RainController.Instance.EnablePS();
                else                RainController.Instance.DisablePS();
            }

            TimelineManager.Instance?.OnSettingsChanged();

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

        // 清除本 MOD 所有对话条件（含持久条件），方便测试时重置剧情
        private void ResetStoryProgress()
        {
            string[] conditions =
            {
                "TALKED_TO_TIANYI", "AMPLIFIER_REPAIRED", "TIANYI_HEARD_SONG",
                "AMPLIFIER_EVER_REPAIRED"
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
            var fogDensity  = ModHelper.Config.GetSettingsValue<float>("Fog Density (雾浓度)");
            var rainEnabled = ModHelper.Config.GetSettingsValue<bool>("Rain Enabled (启用雨)");
            var debugMode   = IsDebug();

            ModHelper.Console.WriteLine(
                $"[世末歌者] Settings: Fog={fogDensity:F2}, Rain={rainEnabled}, " +
                $"Shaders={ShadersEnabled}, Debug={debugMode}", MessageType.Info);
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

            if (ShadersEnabled)
            {
                // 相机后处理(God Rays / 体积雾)挂在主相机 GameObject 上
                var camGo = owCam.mainCamera.gameObject;
                if (camGo.GetComponent<GodRayController>() == null)
                {
                    var sun  = NewHorizons.GetPlanet("Sun");
                    var ctrl = camGo.AddComponent<GodRayController>();
                    ctrl.Init(AssetLoader.GodRay, sun != null ? sun.transform : null);
                    ModHelper.Console.WriteLine("[世末歌者] GodRayController attached.", MessageType.Success);
                }
                if (AssetLoader.Fog != null && camGo.GetComponent<VolumetricFogController>() == null)
                {
                    var fog = camGo.AddComponent<VolumetricFogController>();
                    fog.Init(AssetLoader.Fog);
                    ModHelper.Console.WriteLine("[世末歌者] VolumetricFogController attached.", MessageType.Success);
                }
            }
            else
            {
                ModHelper.Console.WriteLine("[世末歌者] Shader effects disabled, skipping GodRay.", MessageType.Info);
            }

            // 等玩家身体就绪后部署降雨 + 涟漪 + NPC 行为 + 时间线
            while (Locator.GetPlayerTransform() == null) yield return null;

            if (ShadersEnabled)
            {
                RainController.Setup();
                AudioVisualizerController.Setup(NewHorizons);   // 声波可视化(材质缺失时自动跳过)
                PlanarReflectionController.Setup(NewHorizons);  // 水面反射(材质缺失时自动跳过)
                HologramController.Setup(NewHorizons);          // 全息投影(材质缺失时自动跳过)
            }

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
