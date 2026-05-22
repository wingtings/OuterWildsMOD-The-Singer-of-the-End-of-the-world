using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Reflection;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    public class TheSingerOfTheEnd : ModBehaviour
    {
        public static TheSingerOfTheEnd Instance;
        public INewHorizons NewHorizons;

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
                ModHelper.Console.WriteLine("[世末歌者] ERROR: New Horizons API not found! Make sure New Horizons is installed.", MessageType.Error);
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

            try
            {
                new Harmony("wingtings.TheSingerOfTheEnd").PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                ModHelper.Console.WriteLine($"[世末歌者] Harmony patch warning: {ex.Message}", MessageType.Warning);
            }

            NewHorizons.GetStarSystemLoadedEvent().AddListener(OnStarSystemLoaded);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

            LogSettings();

            ModHelper.Console.WriteLine("[世末歌者] MOD loaded successfully!", MessageType.Success);
        }

        public override void Configure(IModConfig config)
        {
            if (IsDebug())
            {
                ModHelper.Console.WriteLine("[世末歌者] Settings changed, re-reading config...", MessageType.Info);
                LogSettings();
            }
        }

        private void LogSettings()
        {
            var fogDensity = ModHelper.Config.GetSettingsValue<float>("Fog Density (雾浓度)");
            var rainEnabled = ModHelper.Config.GetSettingsValue<bool>("Rain Enabled (启用雨)");
            var debugMode = IsDebug();

            ModHelper.Console.WriteLine($"[世末歌者] Settings: Fog={fogDensity:F2}, Rain={rainEnabled}, Debug={debugMode}", MessageType.Info);
        }

        public bool IsDebug()
        {
            return ModHelper.Config.GetSettingsValue<bool>("Debug Mode (调试模式)");
        }

        private void OnStarSystemLoaded(string systemName)
        {
            ModHelper.Console.WriteLine($"[世末歌者] Star system loaded: {systemName}", MessageType.Success);

            if (systemName != "outing_system") return;

            ModHelper.Console.WriteLine("[世末歌者] 鸥停星系 loaded! Setting up...", MessageType.Success);

            var singerCity = NewHorizons.GetPlanet("世末之城");
            if (singerCity != null)
                ModHelper.Console.WriteLine("[世末歌者] 世末之城 found.", MessageType.Success);
            else
                ModHelper.Console.WriteLine("[世末歌者] WARNING: 世末之城 not found!", MessageType.Warning);

            var godRealm = NewHorizons.GetPlanet("神谕之境");
            if (godRealm != null)
                ModHelper.Console.WriteLine("[世末歌者] 神谕之境 found.", MessageType.Success);
            else
                ModHelper.Console.WriteLine("[世末歌者] WARNING: 神谕之境 not found!", MessageType.Warning);
        }

        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (IsDebug())
                ModHelper.Console.WriteLine($"[世末歌者] Scene changed: {previousScene} -> {newScene}", MessageType.Info);

            if (newScene != OWScene.SolarSystem) return;

            ModHelper.Console.WriteLine("[世末歌者] Entered solar system scene.", MessageType.Success);
        }

        public void OnDestroy()
        {
            LoadManager.OnCompleteSceneLoad -= OnCompleteSceneLoad;
        }
    }
}
