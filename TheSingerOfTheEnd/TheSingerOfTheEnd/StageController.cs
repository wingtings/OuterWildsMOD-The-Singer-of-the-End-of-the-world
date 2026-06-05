using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 控制舞台(自制模型,singer_world.json 里 rename="舞台" 的 detail)的显隐。
    // 设计:真结局达成前舞台不出现,达成真结局时舞台浮现(配合圣光爆发)。
    //   - Setup() 由 TheSingerOfTheEnd.SetupGraphics 末尾调用,缓存舞台并隐藏;
    //   - Reveal() 由 TimelineManager.PlayTrueEnd() 调用,触发缩放浮现动画;
    //   - 若玩家此前已达成过真结局(持久条件),进场直接显示(世界已得赎)。
    // 用 SetActive(false) 整体隐藏而非只关 Renderer,避免玩家撞到看不见的舞台/站到其碰撞体上。
    public class StageController : MonoBehaviour
    {
        public static StageController Instance { get; private set; }

        private const string StageName = "舞台";
        // 本循环内的"扩音器已修复"条件(每个循环会重置)。与 EndingJudge/NpcBehavior 用的同一个。
        // 不用持久条件 AMPLIFIER_EVER_REPAIRED:真结局演出(雨停/圣光)本就每循环重播,舞台也应随循环走。
        private const string RepairedCondition = "AMPLIFIER_REPAIRED";

        // 浮现动画时长(秒)。与 TimelineManager 的雨停/圣光阶段(~3s)同步,一起完成"世界得赎"演出。
        private const float RevealDuration = 3.5f;

        private Transform _stage;
        private Vector3 _finalScale;
        private bool _revealed;
        private bool _animating;
        private float _animTimer;

        public static void Setup(INewHorizons nh)
        {
            if (Instance != null) return;
            var go = new GameObject("SingerStageController");
            Instance = go.AddComponent<StageController>();
            Instance.Init(nh);
        }

        private void Awake() => Instance = this;

        private void Init(INewHorizons nh)
        {
            var planet = nh.GetPlanet("Attlerock");
            if (planet == null)
            {
                Log("WARNING: 找不到废岩星(Attlerock),舞台显隐未生效。", MessageType.Warning);
                return;
            }

            _stage = FindDeep(planet.transform, StageName);
            if (_stage == null)
            {
                Log("WARNING: 场景内未找到舞台对象(舞台),显隐未生效。", MessageType.Warning);
                return;
            }

            _finalScale = _stage.localScale;

            // 本循环是否已修复扩音器(达成真结局):是 → 舞台保持显示;否 → 隐藏待显。
            // 一般进场时为 false(每循环重置),除非在已达成真结局的循环中重新加载场景。
            bool repaired = DialogueConditionManager.SharedInstance?
                .GetConditionState(RepairedCondition) ?? false;

            if (repaired)
            {
                _revealed = true;
                Log("舞台:本循环已达成真结局,进场直接显示。", MessageType.Info);
            }
            else
            {
                _stage.gameObject.SetActive(false);
                Log("舞台:真结局达成前隐藏。", MessageType.Info);
            }
        }

        // 由 TimelineManager.PlayTrueEnd() 调用:真结局达成,舞台浮现。
        public void Reveal()
        {
            if (_stage == null || _revealed) return;
            _revealed = true;

            _stage.gameObject.SetActive(true);
            _stage.localScale = Vector3.zero;
            _animTimer = 0f;
            _animating = true;
            Log("舞台显现(真结局)。", MessageType.Success);
        }

        private void Update()
        {
            if (!_animating || _stage == null) return;

            _animTimer += Time.deltaTime;
            float k = Mathf.Clamp01(_animTimer / RevealDuration);
            // 缓出曲线(1-(1-k)^2):由小到大平滑浮现,末尾减速更稳。
            float e = 1f - (1f - k) * (1f - k);
            _stage.localScale = Vector3.Lerp(Vector3.zero, _finalScale, e);

            if (k >= 1f)
            {
                _stage.localScale = _finalScale;
                _animating = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 深度优先按名查找(可命中 inactive 子物体),与 NpcBehavior 一致。
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
