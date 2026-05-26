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

        // True End 演出三阶段时长（秒）
        private const float PhaseRainFade   = 3f;
        private const float PhaseRayBurst   = 5f;
        private const float PhaseRaySettle  = 5f;

        private GodRayController _godRay;
        private bool _trueEndPlaying;
        private float _trueEndTimer;
        private bool _shadersEnabled;

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

        private void Start()
        {
            _shadersEnabled = TheSingerOfTheEnd.Instance.ShadersEnabled;
        }

        // 由 TheSingerOfTheEnd.Configure 调用，立即同步设置值
        public void OnSettingsChanged()
        {
            _shadersEnabled = TheSingerOfTheEnd.Instance.ShadersEnabled;
        }

        private void Update()
        {
            if (_trueEndPlaying)
            {
                UpdateTrueEnd();
                return;
            }

            if (!_shadersEnabled) return;

            // 归一化循环进度 t ∈ [0,1]：0=循环开始，1=超新星
            float remaining = TimeLoop.GetSecondsRemaining();
            float t = Mathf.Clamp01(1f - remaining / LoopDuration);

            ApplyNormalTimeline(t);
        }

        // 随时间线性提升 God Ray 强度（0.5 → 0.85）、后半段增大雨量
        private void ApplyNormalTimeline(float t)
        {
            EnsureGodRay();
            if (_godRay != null)
                _godRay.Intensity = Mathf.Lerp(0.5f, 0.85f, t);

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
            _intensityAtTrueEnd = _godRay != null ? _godRay.Intensity : 0.85f;

            Log("True End 时间线启动：雨停 → 光束爆发 → 平静", MessageType.Success);
        }

        private void UpdateTrueEnd()
        {
            _trueEndTimer += Time.deltaTime;
            EnsureGodRay();

            float t1 = Mathf.Clamp01(_trueEndTimer / PhaseRainFade);

            // 阶段一（0~3 s）：雨粒子逐渐停止
            if (_trueEndTimer <= PhaseRainFade)
            {
                RainController.Instance?.SetEmissionRate(Mathf.Lerp(6000f, 0f, t1));
                return;
            }

            // 阶段二（3~8 s）：God Ray 强度爆发至 1.5
            float t2 = Mathf.Clamp01((_trueEndTimer - PhaseRainFade) / PhaseRayBurst);
            if (_trueEndTimer <= PhaseRainFade + PhaseRayBurst)
            {
                if (_godRay != null && _shadersEnabled)
                    _godRay.Intensity = Mathf.Lerp(_intensityAtTrueEnd, 1.5f, t2);
                return;
            }

            // 阶段三（8~13 s）：God Ray 缓降至 1.0
            float t3 = Mathf.Clamp01(
                (_trueEndTimer - PhaseRainFade - PhaseRayBurst) / PhaseRaySettle);
            if (_godRay != null && _shadersEnabled)
                _godRay.Intensity = Mathf.Lerp(1.5f, 1.0f, t3);
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
