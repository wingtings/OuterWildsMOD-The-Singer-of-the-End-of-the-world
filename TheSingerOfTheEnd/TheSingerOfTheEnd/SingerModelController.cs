using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 控制歌者两套模型的切换(singer_world.json 里的两个 detail):
    //   - "歌者(弹吉他)" (assets/models/singer_with_guitar):真结局达成前显示,歌者仍在弹奏。
    //   - "歌者(阿绫)"   (assets/models/singer):真结局达成后显示,世界得赎,歌者放下吉他。
    // 设计与 StageController 一致:
    //   - Setup() 由 TheSingerOfTheEnd.SetupGraphics 末尾调用,缓存两套模型并按当前状态设初始显隐;
    //   - SwitchToNormal() 由 TimelineManager.PlayTrueEnd() 调用,真结局达成时切到正常模型;
    //   - 用 RepairedCondition(每循环重置)判断:与雨停/圣光/舞台/天依的真结局演出同步走循环,
    //     而非持久条件——真结局演出本就每循环重播,模型切换也应随循环走。
    // 两套模型在 JSON 中放在同一坐标,切换即整体 SetActive,不改其它系统对 "歌者(阿绫)" 的引用。
    public class SingerModelController : MonoBehaviour
    {
        public static SingerModelController Instance { get; private set; }

        private const string NormalName = "歌者(阿绫)";
        private const string GuitarName = "歌者(弹吉他)";
        private const string StoolName  = "凳子";   // 弹吉他歌者坐的凳子,与弹吉他模型同生命周期
        private const string RepairedCondition = "AMPLIFIER_REPAIRED";

        private Transform _normal;
        private Transform _guitar;
        private Transform _stool;
        private bool _switched;

        public static void Setup(INewHorizons nh)
        {
            if (Instance != null) return;
            var go = new GameObject("SingerModelController");
            Instance = go.AddComponent<SingerModelController>();
            Instance.Init(nh);
        }

        private void Awake() => Instance = this;

        private void Init(INewHorizons nh)
        {
            var planet = nh.GetPlanet("Attlerock");
            if (planet == null)
            {
                Log("WARNING: 找不到废岩星(Attlerock),歌者模型切换未生效。", MessageType.Warning);
                return;
            }

            _normal = FindDeep(planet.transform, NormalName);
            _guitar = FindDeep(planet.transform, GuitarName);
            _stool  = FindDeep(planet.transform, StoolName);

            if (_normal == null)
                Log("WARNING: 未找到正常歌者模型(歌者(阿绫))。", MessageType.Warning);
            if (_guitar == null)
                Log("WARNING: 未找到弹吉他歌者模型(歌者(弹吉他)),将始终显示正常模型。", MessageType.Warning);
            if (_stool == null)
                Log("WARNING: 未找到凳子模型(凳子)。", MessageType.Warning);

            // 弹吉他模型缺失时无可切换:保持正常模型显示,凳子一并隐藏,避免出现"什么都没有"。
            if (_guitar == null)
            {
                _normal?.gameObject.SetActive(true);
                _stool?.gameObject.SetActive(false);
                _switched = true;
                return;
            }

            // 本循环是否已修复扩音器(达成真结局)。一般进场为 false(每循环重置),
            // 除非在已达成真结局的循环中重新加载场景。
            bool repaired = DialogueConditionManager.SharedInstance?
                .GetConditionState(RepairedCondition) ?? false;

            if (repaired)
            {
                SwitchToNormal();
                Log("歌者模型:本循环已达成真结局,进场直接显示正常模型。", MessageType.Info);
            }
            else
            {
                _guitar.gameObject.SetActive(true);
                _normal?.gameObject.SetActive(false);
                _stool?.gameObject.SetActive(true);   // 真结局前:弹吉他歌者坐的凳子一并显示
                Log("歌者模型:真结局达成前显示弹吉他模型(含凳子)。", MessageType.Info);
            }
        }

        // 由 TimelineManager.PlayTrueEnd() 调用:真结局达成,切换为正常模型,凳子消失。
        public void SwitchToNormal()
        {
            if (_switched) return;
            _switched = true;

            _normal?.gameObject.SetActive(true);
            _guitar?.gameObject.SetActive(false);
            _stool?.gameObject.SetActive(false);   // 真结局达成:凳子随弹吉他模型一起消失
            Log("歌者模型切换为正常模型(真结局),凳子已隐藏。", MessageType.Success);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 深度优先按名查找(可命中 inactive 子物体),与 StageController/NpcBehavior 一致。
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
