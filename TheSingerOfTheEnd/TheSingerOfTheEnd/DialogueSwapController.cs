using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 真结局达成后,把天依与歌者的对话树整体替换为"真结局版"内容
    // (读取 planets/dialogue/*_trueend.xml,运行时换掉对话树文本)。
    //
    // 设计与 StageController / SingerModelController 完全一致——按"每循环"的 AMPLIFIER_REPAIRED 走:
    //   - Setup() 在 SetupGraphics 末尾调用,定位两棵对话树并缓存;
    //     若本循环已修复扩音器(达成真结局),进场直接替换(处理真结局循环内的场景重载)。
    //   - SwapToTrueEnd() 由 TimelineManager.PlayTrueEnd() 调用,真结局达成瞬间替换。
    // 不用持久条件:真结局演出(雨停/圣光/舞台/歌者模型/天依传送)本就每循环重播,
    //   对话替换随之走循环,才不会与场景其它状态(仍在下雨的新循环)冲突。
    //
    // 替换手段:CharacterDialogueTree.SetTextXml(new TextAsset(xml)) + LoadXml() 重新解析节点;
    //   显示名(_characterName)单独改写——LoadXml 不读取 <NameField>。
    public class DialogueSwapController : MonoBehaviour
    {
        public static DialogueSwapController Instance { get; private set; }

        private const string RepairedCondition = "AMPLIFIER_REPAIRED";

        // 替换后的对话文件(相对 MOD 根目录)与显示名。
        private const string TianyiXmlRel = "planets/dialogue/tianyi_dialogue_trueend.xml";
        private const string SingerXmlRel = "planets/dialogue/singer_dialogue_trueend.xml";
        private const string TianyiAfterName = "天依";
        private const string SingerAfterName = "歌者";

        // 两棵对话树在废岩星(Attlerock)局部坐标中的触发位置(取自 singer_world.json 的 dialogue 数组)。
        // 按位置就近定位,避免依赖显示名机制;进场时(尚未修复)位置一定是原始位置。
        private static readonly Vector3 TianyiDialogueLocal = new Vector3(41.15849f, 28.24348f, -53.61192f);
        private static readonly Vector3 SingerDialogueLocal = new Vector3(-6.719388f, 2.247597f, 29.43108f);

        private CharacterDialogueTree _tianyi;
        private CharacterDialogueTree _singer;
        private bool _swapped;

        private static FieldInfo _nameField;
        private static MethodInfo _loadXml;
        // New Horizons 的对话翻译注册入口(反射调用):
        //   HandleUnityCreatedDialogue(CharacterDialogueTree) —— 对"非 NH 管线创建/替换"的对话做翻译注册+重建,正是本场景;
        //   AddTranslation(string xml, string characterName) —— 回退方案,直接把页面/选项/角色名写入翻译表。
        // 不注册翻译,游戏找不到翻译键,会把『节点名+原文』当文本显示(即 Start / FirstVoice 等英文)。
        private static MethodInfo _nhHandleUnityCreatedDialogue;
        private static MethodInfo _nhAddTranslation;

        public static void Setup(INewHorizons nh)
        {
            if (Instance != null) return;
            var go = new GameObject("SingerDialogueSwap");
            Instance = go.AddComponent<DialogueSwapController>();
            Instance.Init(nh);
        }

        private void Awake() => Instance = this;

        private void Init(INewHorizons nh)
        {
            CacheReflection();

            var planet = nh.GetPlanet("Attlerock");
            if (planet == null)
            {
                Log("WARNING: 找不到废岩星(Attlerock),真结局对话替换未生效。", MessageType.Warning);
                return;
            }

            _tianyi = FindNearestDialogue(planet.transform.TransformPoint(TianyiDialogueLocal), 4f);
            _singer = FindNearestDialogue(planet.transform.TransformPoint(SingerDialogueLocal), 4f);

            if (_tianyi == null) Log("WARNING: 未定位到天依对话树,真结局对话替换可能失效。", MessageType.Warning);
            if (_singer == null) Log("WARNING: 未定位到歌者对话树,真结局对话替换可能失效。", MessageType.Warning);

            // 本循环已达成真结局(扩音器已修复)→ 进场直接替换。一般进场为 false(每循环重置),
            // 除非在已达成真结局的循环中重新加载了场景。
            bool repaired = DialogueConditionManager.SharedInstance?
                .GetConditionState(RepairedCondition) ?? false;
            if (repaired)
            {
                SwapToTrueEnd();
                Log("真结局对话:本循环已达成真结局,进场直接替换。", MessageType.Info);
            }
        }

        // 由 TimelineManager.PlayTrueEnd() 调用:真结局达成,替换两棵对话树。
        public void SwapToTrueEnd()
        {
            if (_swapped) return;
            _swapped = true;
            SwapOne(_tianyi, TianyiXmlRel, TianyiAfterName, "天依");
            SwapOne(_singer, SingerXmlRel, SingerAfterName, "歌者");
        }

        private void SwapOne(CharacterDialogueTree tree, string relPath, string newName, string label)
        {
            if (tree == null) return;
            try
            {
                string path = Path.Combine(
                    TheSingerOfTheEnd.Instance.ModHelper.Manifest.ModFolderPath, relPath);
                if (!File.Exists(path))
                {
                    Log($"WARNING: 找不到{label}真结局对话文件: {path}", MessageType.Warning);
                    return;
                }

                string xml = File.ReadAllText(path);
                string assetName = Path.GetFileNameWithoutExtension(relPath);
                tree.SetTextXml(new TextAsset(xml) { name = assetName }); // 换掉文本资产(带名字,与 NH 一致)

                // 关键修复:把新对话注册进 New Horizons 翻译表,否则游戏显示『节点名+原文』(Start/FirstVoice 等英文)。
                if (_nhHandleUnityCreatedDialogue != null)
                {
                    // 首选 NH 官方入口:它会 AddTranslation + 重新套用文本 + 下一帧修正。
                    _nhHandleUnityCreatedDialogue.Invoke(null, new object[] { tree });
                }
                else
                {
                    // 回退:直接注册翻译并重新解析节点。
                    _nhAddTranslation?.Invoke(null, new object[] { xml, null });
                    _loadXml?.Invoke(tree, null);
                }

                _nameField?.SetValue(tree, newName); // 同步显示名(其键已随 NameField 一并注册翻译)
                Log($"{label}对话已替换为真结局版。", MessageType.Success);
            }
            catch (Exception ex)
            {
                Log($"替换{label}对话失败: {ex.Message}", MessageType.Error);
            }
        }

        // 找到离 pos 最近的对话触发器(maxDist 内),与 NpcBehavior 一致。
        private static CharacterDialogueTree FindNearestDialogue(Vector3 pos, float maxDist)
        {
            CharacterDialogueTree best = null;
            float bestSq = maxDist * maxDist;
            foreach (var d in FindObjectsOfType<CharacterDialogueTree>())
            {
                float sq = (d.transform.position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return best;
        }

        private static void CacheReflection()
        {
            if (_nameField != null) return;
            var t = typeof(CharacterDialogueTree);
            _nameField = t.GetField("_characterName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            _loadXml = t.GetMethod("LoadXml",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // 在已加载的程序集中找到 New Horizons 的 DialogueBuilder(本 MOD 不直接引用 NH.dll)。
            var nhType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType("NewHorizons.Builder.Props.DialogueBuilder"); } catch { return null; } })
                .FirstOrDefault(x => x != null);
            if (nhType != null)
            {
                _nhHandleUnityCreatedDialogue = nhType.GetMethod("HandleUnityCreatedDialogue",
                    BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(CharacterDialogueTree) }, null);
                _nhAddTranslation = nhType.GetMethod("AddTranslation",
                    BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string) }, null);
            }
            if (_nhHandleUnityCreatedDialogue == null && _nhAddTranslation == null)
                Log("WARNING: 未找到 NH 对话翻译注册入口,真结局对话可能出现节点名英文(Start/FirstVoice)。", MessageType.Warning);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static void Log(string msg, MessageType type) =>
            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine($"[世末歌者] {msg}", type);
    }
}
