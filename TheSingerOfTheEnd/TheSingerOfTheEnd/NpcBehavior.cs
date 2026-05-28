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

        // 歌者(阿绫)在废岩星(Attlerock)局部坐标中的位置(取自 singer_world.json)。
        // 当 _singerTransform 缺失时作为回退参照。
        private static readonly Vector3 SingerLocalPos = new Vector3(-5.52638f, -7.194386f, 29.36535f);

        // True End 时天依停在距歌者多远(米)——歌者身边
        private const float StandDistance = 1.8f;
        private const float BobAmplitude  = 0.05f;  // 歌者演唱时的轻微上下摆动幅度(米)

        // Setup 时缓存:星球根与歌者 Transform。传送计算全部在星球局部空间进行,
        // 这样落点贴合球面,且会随星球自转/公转一起运动。
        private static Transform _planetRoot;
        private static Transform _singerTransform;

        private Transform _playerBody;
        private Animator _anim;
        private float _bobPhase;
        private bool _teleported;          // 本循环内天依是否已传送到歌者身边

        // 防漂移:记录初始局部位置作为摆动基准(绝对设值,不累加)。
        private Vector3 _baseLocalPos;
        private Vector3 _bobUpLocal = Vector3.up;

        // 天依的对话触发器(传送时一起移动,使对话交互位置同步)
        private CharacterDialogueTree _tianyiDialogue;
        private Vector3 _tianyiStartPos;

        // 由 TheSingerOfTheEnd.SetupGraphics 末尾调用
        public static void Setup(INewHorizons nh)
        {
            var planet = nh.GetPlanet("Attlerock");
            if (planet == null) return;

            _planetRoot     = planet.transform;
            _singerTransform = FindDeep(planet.transform, "歌者(阿绫)");

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

            // 记录摆动基准(防止累加漂移)。摆动方向取该点径向(局部空间星心在原点)。
            _baseLocalPos = transform.localPosition;
            _bobUpLocal = _baseLocalPos.sqrMagnitude > 1e-4f
                ? _baseLocalPos.normalized : Vector3.up;

            // 缓存天依对话触发器与初始位置,供 True End 传送时同步移动。
            if (Role == NpcRole.Tianyi)
            {
                _tianyiStartPos = transform.position;
                _tianyiDialogue = FindNearestDialogue(_tianyiStartPos, 8f);
            }
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

            if (repaired)
            {
                // True End：歌声传遍全城，天依传送到歌者身边（每个循环只触发一次）。
                if (!_teleported)
                {
                    TeleportToSinger();
                    _teleported = true;
                }
                return; // 传送后保持站姿、面向歌者
            }

            // 未修复：玩家靠近时看向玩家，否则维持仰望天空（朝向不变）。
            if (dist < ActivateRadius)
                RotateToward(_playerBody.position);
        }

        // True End 触发后，把天依瞬间传送到歌者身边并转身面向歌者，
        // 同时把她的对话触发器移到新位置（对话交互位置同步移动）。
        // 全程在星球局部空间计算：落点投影回歌者所在球面半径，朝向用该点外法线找平。
        private void TeleportToSinger()
        {
            if (_planetRoot == null) return;

            // 歌者局部坐标：优先用实际物体位置，缺失时回退到 JSON 常量。
            Vector3 singerLocal = _singerTransform != null
                ? _planetRoot.InverseTransformPoint(_singerTransform.position)
                : SingerLocalPos;

            Vector3 myLocal = _planetRoot.InverseTransformPoint(transform.position);

            // 球面外法线（局部空间下星心即原点）。
            Vector3 upLocal = singerLocal.normalized;

            // 站位方向：从歌者指向天依原处的切向，让她停在“观众侧”朝向歌者。
            Vector3 tangent = Vector3.ProjectOnPlane(myLocal - singerLocal, upLocal);
            if (tangent.sqrMagnitude < 1e-4f)
                tangent = Vector3.ProjectOnPlane(Vector3.forward, upLocal);
            tangent = tangent.normalized;

            // 落点：歌者身边 StandDistance 米，再投影回歌者所在球面半径。
            Vector3 targetLocal = singerLocal + tangent * StandDistance;
            targetLocal = targetLocal.normalized * singerLocal.magnitude;

            transform.position = _planetRoot.TransformPoint(targetLocal);

            // 朝向：站立（up 朝外），水平面内面向歌者。
            Vector3 worldUp     = _planetRoot.TransformDirection(targetLocal.normalized);
            Vector3 singerWorld = _planetRoot.TransformPoint(singerLocal);
            Vector3 face        = Vector3.ProjectOnPlane(singerWorld - transform.position, worldUp);
            if (face.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(face.normalized, worldUp);

            // 对话交互位置同步移动到天依新位置。
            if (_tianyiDialogue == null)
                _tianyiDialogue = FindNearestDialogue(_tianyiStartPos, 8f);
            if (_tianyiDialogue != null)
            {
                _tianyiDialogue.transform.position = transform.position;
                _tianyiDialogue.transform.rotation = transform.rotation;
            }

            // 摆动基准也更新到新位置,避免传送后回弹。
            _baseLocalPos = transform.localPosition;

            TheSingerOfTheEnd.Instance?.ModHelper?.Console?.WriteLine(
                "[世末歌者] 天依已传送到歌者身边（True End），对话位置已同步。", OWML.Common.MessageType.Success);
        }

        private void UpdateSinger(float dist)
        {
            // 轻微上下摆动（演唱手势）。绝对设值(基准 + 偏移),不累加 → 不会随时间漂移。
            _bobPhase += Time.deltaTime * 1.1f;
            transform.localPosition = _baseLocalPos + _bobUpLocal * (Mathf.Sin(_bobPhase) * BobAmplitude);

            if (dist < ActivateRadius)
                RotateToward(_playerBody.position);
        }

        // 找到离 pos 最近的对话触发器(在 maxDist 内),用于让天依的对话随她传送。
        private static CharacterDialogueTree FindNearestDialogue(Vector3 pos, float maxDist)
        {
            CharacterDialogueTree best = null;
            float bestSq = maxDist * maxDist;
            foreach (var d in Object.FindObjectsOfType<CharacterDialogueTree>())
            {
                float sq = (d.transform.position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return best;
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
