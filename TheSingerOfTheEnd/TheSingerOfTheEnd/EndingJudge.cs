using OWML.Common;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 检测"扩音装置已修复"条件,触发 True End 演出。
    // 由 TheSingerOfTheEnd.OnStarSystemLoaded 在鸥停星系加载后挂载到场景。
    public class EndingJudge : MonoBehaviour
    {
        private const string RepairedCondition = "AMPLIFIER_REPAIRED";
        private const string EndingFact = "TRUE_ENDING_FACT";

        // 两个扩音器插槽(singer_world.json 的 itemSockets 写入)。两枚零件都插上 → 扩音器修复。
        private const string SocketA = "AMP_SOCKET_A";
        private const string SocketB = "AMP_SOCKET_B";

        private bool _ended;
        private float _checkTimer;

        private void Update()
        {
            if (_ended) return;

            // 每 0.5 秒检查一次,避免每帧开销
            _checkTimer += Time.deltaTime;
            if (_checkTimer < 0.5f) return;
            _checkTimer = 0f;

            var conditions = DialogueConditionManager.SharedInstance;
            if (conditions == null) return;

            // 两个插槽都插入了对应零件 → 视为扩音器修复(交互式修复路径)。
            if (!conditions.GetConditionState(RepairedCondition)
                && conditions.GetConditionState(SocketA)
                && conditions.GetConditionState(SocketB))
            {
                conditions.SetConditionState(RepairedCondition, true);
                try { PlayerData.SetPersistentCondition("AMPLIFIER_EVER_REPAIRED", true); } catch { }
                Locator.GetShipLogManager()?.RevealFact("AMPLIFIER_FIXED");
            }

            if (conditions.GetConditionState(RepairedCondition))
                TriggerTrueEnding();
        }

        private void TriggerTrueEnding()
        {
            _ended = true;

            var console = TheSingerOfTheEnd.Instance?.ModHelper?.Console;
            console?.WriteLine("[世末歌者] True Ending triggered: amplifier repaired.", MessageType.Success);

            // 揭示结局对应的 Ship Log 事实
            var shipLog = Locator.GetShipLogManager();
            shipLog?.RevealFact(EndingFact);

            // 启动时间线演出（雨停 + God Ray 爆发）
            TimelineManager.Instance?.PlayTrueEnd();

            // 屏幕通知
            if (NotificationManager.SharedInstance != null)
            {
                var data = new NotificationData(
                    NotificationTarget.Player,
                    "雨停了。云层裂开，阳光穿透乌云。轮回终结，世界得赎。",
                    15f);
                NotificationManager.SharedInstance.PostNotification(data);
            }
        }
    }
}
