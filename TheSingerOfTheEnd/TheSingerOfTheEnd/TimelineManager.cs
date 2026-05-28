using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 22 分钟游戏循环时间线管理器。
    // 职责：随剩余时间渐变 God Ray 强度和降雨密度；True End 触发后播放"雨停→光束爆发"演出。
    // 由 TheSingerOfTheEnd.SetupGraphics 协程创建，EndingJudge 调用 PlayTrueEnd()。
    public class TimelineManager : MonoBehaviour
    {
        public static TimelineManager Instance { get; private set; }

        private const float LoopDuration = 1320f; // OW 标准循环长度 22 分钟

        // True End 演出阶段时长（秒）。圣光短暂爆发后自动淡出 → 恢复正常(不再常亮)。
        private const float PhaseRainFade = 3f;   // 雨停 + 雾散 + 圣光升起
        private const float PhaseRayHold  = 2f;   // 圣光保持峰值
        private const float PhaseRayFade  = 3f;   // 圣光淡出 → 关闭
        private const float RayPeak       = 0.9f;

        private GodRayController _godRay;
        private VolumetricFogController _fog;
        private bool _trueEndPlaying;
        private bool _fogDisabled;
        private float _trueEndTimer;

        // 在 True End 开始时快照当前强度，用于平滑插值
        private float _intensityAtTrueEnd;

        public static void Setup()
        {
            if (Instance != null) return;
            var go = new GameObject("SingerTimeline");
            Instance = go.AddComponent<TimelineManager>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (_trueEndPlaying)
            {
                UpdateTrueEnd();
                return;
            }

            // 各子效果由其控制器是否存在(null 检查)决定开关:
            // 对应 shader 关掉时控制器不创建/被禁用,这里的赋值自然落空。

            // 归一化循环进度 t ∈ [0,1]：0=循环开始，1=超新星
            float remaining = TimeLoop.GetSecondsRemaining();
            float t = Mathf.Clamp01(1f - remaining / LoopDuration);

            ApplyNormalTimeline(t);
        }

        // 平时只随时间增大雨量;圣光不在平时出现(仅 True End 演出期间由 ForceRays 控制)。
        private void ApplyNormalTimeline(float t)
        {
            // 后半段（t > 0.5）雨量逐渐增大（4000 → 6000 粒/秒）
            if (t > 0.5f && RainController.Instance != null)
            {
                float rainT = (t - 0.5f) / 0.5f;
                RainController.Instance.SetEmissionRate(Mathf.Lerp(4000f, 6000f, rainT));
            }
        }

        // ── True End 演出 ──────────────────────────────────────────────
        // 由 EndingJudge.TriggerTrueEnding() 调用
        public void PlayTrueEnd()
        {
            if (_trueEndPlaying) return;
            _trueEndPlaying = true;
            _trueEndTimer = 0f;

            EnsureGodRay();

            // 强制神光从 0 升起:从固定屏幕位置射出合成阳光,胜利后无论玩家朝哪都能看到("阳光穿透乌云")。
            if (_godRay != null)
            {
                _godRay.ForcedLightPos = new Vector2(0.5f, 0.72f);
                _godRay.Intensity = 0f;
                _godRay.ForceRays = true;
            }

            if (_fog == null) _fog = FindObjectOfType<VolumetricFogController>();

            Log("True End 时间线启动：雨停 + 雾散 → 圣光短暂爆发 → 自动恢复", MessageType.Success);
        }

        private void UpdateTrueEnd()
        {
            _trueEndTimer += Time.deltaTime;
            EnsureGodRay();

            // 阶段一（0~3 s）：雨停 + 雾散，圣光从 0 升到峰值（云层裂开、阳光穿透）
            if (_trueEndTimer <= PhaseRainFade)
            {
                float k = Mathf.Clamp01(_trueEndTimer / PhaseRainFade);
                RainController.Instance?.SetEmissionRate(Mathf.Lerp(6000f, 0f, k));
                if (_fog != null) _fog.DensityScale = Mathf.Lerp(1f, 0f, k);
                if (_godRay != null) _godRay.Intensity = Mathf.Lerp(0f, RayPeak, k);
                return;
            }

            // 雾完全散去后彻底关闭,避免其 OnRenderImage 盖住圣光。
            if (!_fogDisabled && _fog != null)
            {
                _fog.DensityScale = 0f;
                _fog.enabled = false;
                _fogDisabled = true;
            }

            // 阶段二（3~5 s）：圣光保持峰值
            if (_trueEndTimer <= PhaseRainFade + PhaseRayHold)
            {
                if (_godRay != null) _godRay.Intensity = RayPeak;
                return;
            }

            // 阶段三（5~8 s）：圣光淡出至 0
            float fadeStart = PhaseRainFade + PhaseRayHold;
            if (_trueEndTimer <= fadeStart + PhaseRayFade)
            {
                float k = Mathf.Clamp01((_trueEndTimer - fadeStart) / PhaseRayFade);
                if (_godRay != null) _godRay.Intensity = Mathf.Lerp(RayPeak, 0f, k);
                return;
            }

            // 结束：关闭强制模式,圣光恢复正常(不再常亮)。
            if (_godRay != null && _godRay.ForceRays)
            {
                _godRay.Intensity = 0f;
                _godRay.ForceRays = false;
            }
        }

        private void EnsureGodRay()
        {
            if (_godRay == null)
                _godRay = FindObjectOfType<GodRayController>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static void Log(string msg, MessageType type) =>
            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine(
                $"[世末歌者] {msg}", type);
    }
}
