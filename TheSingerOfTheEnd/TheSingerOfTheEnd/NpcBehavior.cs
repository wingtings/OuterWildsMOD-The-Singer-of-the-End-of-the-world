using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 挂载在场景内的 NPC GameObject 上。
    // Setup() 在 SetupGraphics 协程末尾调用，此时玩家已就绪。
    // 角色名取自 singer_world.json 里的 "rename" 字段。
    public class NpcBehavior : MonoBehaviour
    {
        public enum NpcRole { Tianyi, Singer }

        public NpcRole Role;
        public float TurnSpeed = 60f;       // 转向角速度(度/秒)
        public float ActivateRadius = 14f;  // 玩家进入此距离才启动面向逻辑

        // 音乐厅舞台在废岩星局部坐标中的位置（与歌者占位对齐），修复后天依转向此处
        private static readonly Vector3 HallLocalPos = new Vector3(0f, 177f, -33f);

        private Transform _playerBody;
        private Animator _anim;
        private float _bobPhase;

        // 由 TheSingerOfTheEnd.SetupGraphics 末尾调用
        public static void Setup(INewHorizons nh)
        {
            var planet = nh.GetPlanet("Brittle Hollow");
            if (planet == null) return;

            TryAttach(planet.transform, "蓝发女孩(天依占位)", NpcRole.Tianyi);
            TryAttach(planet.transform, "歌者(阿绫)",         NpcRole.Singer);
        }

        private static void TryAttach(Transform planetRoot, string npcName, NpcRole role)
        {
            var t = FindDeep(planetRoot, npcName);
            if (t == null) return;
            var b = t.gameObject.AddComponent<NpcBehavior>();
            b.Role = role;
        }

        private void Start()
        {
            _playerBody = Locator.GetPlayerTransform();

            // 禁用根运动，让我们能自由旋转 pivot 而不与 Animator 冲突
            _anim = GetComponentInChildren<Animator>();
            if (_anim != null) _anim.applyRootMotion = false;
        }

        private void Update()
        {
            if (_playerBody == null)
            {
                _playerBody = Locator.GetPlayerTransform();
                if (_playerBody == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerBody.position);

            if (Role == NpcRole.Tianyi)
                UpdateTianyi(dist);
            else
                UpdateSinger(dist);
        }

        private void UpdateTianyi(float dist)
        {
            bool repaired = DialogueConditionManager.SharedInstance?
                .GetConditionState("AMPLIFIER_REPAIRED") ?? false;

            Vector3 faceTarget;

            if (repaired)
            {
                // 面向音乐厅方向（在星球局部空间求世界坐标）
                var hallWorld = transform.parent != null
                    ? transform.parent.TransformPoint(HallLocalPos)
                    : transform.position + transform.forward * 5f;
                faceTarget = hallWorld;
            }
            else if (dist < ActivateRadius)
            {
                faceTarget = _playerBody.position;
            }
            else
            {
                // 默认仰望天空：当前朝向不变
                return;
            }

            RotateToward(faceTarget);
        }

        private void UpdateSinger(float dist)
        {
            // 始终微幅上下摆动（演唱手势）
            _bobPhase += Time.deltaTime * 1.1f;
            var lp = transform.localPosition;
            lp.y += Mathf.Sin(_bobPhase) * 0.003f;
            transform.localPosition = lp;

            if (dist < ActivateRadius)
                RotateToward(_playerBody.position);
        }

        // 绕 transform.up（星球法线方向）旋转以面向目标，不倾斜
        private void RotateToward(Vector3 worldTarget)
        {
            var dir = Vector3.ProjectOnPlane(
                worldTarget - transform.position, transform.up);
            if (dir.sqrMagnitude < 0.001f) return;

            var goal = Quaternion.LookRotation(dir.normalized, transform.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, goal, TurnSpeed * Time.deltaTime);
        }

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
    }
}
