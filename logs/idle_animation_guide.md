# 给 MMD 模型加待机动作并接入 Outer Wilds 的清单

> Author: wingtings（指南整理）
>
> 适用对象：本项目两个 MMD 模型 —— 歌者·阿绫（`Singer.prefab`）、凡人·天依（`Tianyi.prefab`）。
> 目标：让它们在游戏里有循环的**待机/呼吸动作**，而不是僵直站立。
> 配套：`shader_packaging_guide.md`（同一套 AssetBundle 部署管线）。

---

## 〇、为什么不能直接拖个动作文件进去？（先理解原理）

1. **运行时只认 Unity 的 AnimationClip + Animator**。`.vmd`（MMD 动作）、Mixamo 的 `.fbx` 动作，都要先在 Unity 工程里变成 AnimationClip，再由 Animator 播放。
2. **动作要随 prefab 一起打进 AssetBundle**。NH 用 JSON 的 `assetBundle`+`path` 实例化 prefab；只要 prefab 上挂了带默认状态的 `Animator`，**NH 实例化后会自动循环播放，无需任何 C# 代码**。
3. **MMD 骨架是非标准的**（センター/上半身/下半身… 日文骨名）。想用 Mixamo/通用动作就得先把模型配成 **Humanoid**，靠 Unity 的人形抽象层做重定向；想用 MMD 原生 .vmd 则要走 Blender/MMD4Mecanim 转换。

**结论**：动作来源 → 在 Unity 里变成 AnimationClip → 建 AnimatorController（idle 设默认+循环）→ 挂到模型 prefab 的 Animator → 打进 bundle → 部署。

---

## 一、全流程总览

```
┌─ Unity 工程 (outer-wilds-unity-template, 2019.4.39f1) ──────────────────┐
│  1. 模型 FBX 配 Rig（Mixamo 路线=Humanoid）                              │
│  2. 拿到 idle 动作 → 提取成 AnimationClip → Loop Time=true               │
│  3. 建 AnimatorController，idle 设为默认状态                              │
│  4. 模型 prefab 加 Animator（Avatar+Controller，关 Apply Root Motion）   │
│  5. prefab 的 AssetBundle 名保持 singer / tianyi                         │
│  6. Singer ▸ Build AssetBundles → 产出 singer / tianyi (+ .manifest)     │
└────────────────────────────────────────────────────────────────────────┘
        │  复制 bundle
        ▼
TheSingerOfTheEnd/TheSingerOfTheEnd/assets/models/{singer,tianyi}
        │  rebuild mod 自动部署
        ▼
%AppData%/.../Mods/wingtings.TheSingerOfTheEnd/assets/models/...
        │  运行时
        ▼
NH 按 singer_world.json 的 assetBundle+path 实例化 → Animator 自动循环播放
```

> 现有 JSON 引用（无需改动，动作是挂在 prefab 上的）：
> - 歌者：`"assetBundle": "assets/models/singer"`, `"path": "Assets/Model/Singer.prefab"`
> - 天依：`"assetBundle": "assets/models/tianyi"`, `"path": "Assets/Model/Tianyi.prefab"`

---

## 二、选哪条路

| | Mixamo / Quaternius（推荐先试） | MMD 原生 VMD 待机 |
|---|---|---|
| 难度 | 低 | 中（多一步 Blender/插件转换） |
| 前提 | 模型能配成 **Humanoid** | 最好有**原始 PMX** 或保留 MMD 骨名的模型 |
| 风险 | MMD 骨骼配 Humanoid 偶尔要手动改骨映射 | 骨名不匹配则 .vmd 套不上 |
| 效果 | 通用呼吸/待机，够用 | 更角色化、贴合二次元 |

**建议**：先走 Mixamo；只有 Humanoid 怎么都配不好时再回 Blender 走 VMD。

### 资源链接
- **Mixamo**（免费，Adobe 账号）：`https://www.mixamo.com` → 搜 `Breathing Idle` / `Idle` / `Standing Idle` / `Happy Idle`，下 "FBX for Unity"。
- **Universal Animation Library**（Quaternius，itch.io，免费 CC0，120+ 通用人形含 idle）：`https://quaternius.itch.io/universal-animation-library`
- **MMD 待機モーション（.vmd）**：
  - BowlRoll 待機セット：`https://bowlroll.net/file/8900`
  - p-nez.net MOTIONS_：`https://p-nez.net/motions_`
  - tiizu-mmd 모션：`http://tiizu-mmd.com/motion/`
  - BOOTH プリメロ工房（免费 emote 风 VMD）：`https://booth.pm/ja/items/5321972`
  - DeviantArt Idle Animation Pack：`https://www.deviantart.com/deedee524/art/Idle-Animation-Pack-759426476`

---

## 三、Mixamo 路线（推荐，无需写 C#）

1. **模型 FBX → Rig 页**：Animation Type = `Humanoid`，Avatar Definition = `Create From This Model` → **Apply** → 点 **Configure** 检查骨映射（MMD 模型重点看 Chest/Spine/手指有没有红/配错，手动拖对；少数中间骨可留空）。
2. **下 Mixamo 待机**：选 idle，Format = `FBX for Unity`，Skin = `Without Skin`，下载。
3. **导入该 idle FBX → Rig 页**：Animation Type = `Humanoid`（靠人形抽象层重定向，骨架不同也能套）。
4. **提取动画片段**：展开 idle FBX，选里面的 AnimationClip，`Ctrl+D` 复制到外面；在它的导入设置里：
   - 勾 **Loop Time = true**（循环）
   - Root Transform Position(Y) / Position(XZ) / Rotation 全部勾 **Bake Into Pose**（防止待机时人物漂移/转圈）
5. **建 AnimatorController**：右键 `Create ▸ Animator Controller`，把 idle 片段拖进去作为默认状态（橙色）。
6. **模型 prefab 加 `Animator` 组件**：Avatar = 模型自己的 Avatar，Controller = 刚建的；**关掉 Apply Root Motion**。
7. **打包部署**：prefab 的 AssetBundle 名保持 `singer` / `tianyi` → `Singer ▸ Build AssetBundles` → 复制 `singer`/`tianyi`(+`.manifest`) 进 `assets/models/` → rebuild mod / 手动复制到部署目录 → 重进循环。

---

## 四、VMD 路线（备选，贴合 MMD 角色）

你本来就在用 Blender，所以：
1. Blender 装 **`mmd_tools`** 插件。
2. 导入**原始 PMX** 模型（不是已转的 FBX —— VMD 要靠 MMD 骨名匹配）。
3. 导入下载的待机 `.vmd`。
4. **Bake Action**（烘焙成关键帧动作）。
5. 导出**带动画的 FBX**。
6. 回到 Unity 第 4~7 步（此时 Rig 用 `Generic` 也行，骨架本就匹配）。

> 备选工具：Unity 插件 **MMD4Mecanim**（2019 兼容），直接喂 PMX + VMD 生成 AnimationClip。

---

## 五、常见坑

- **待机时人物缓慢平移/转圈** → 第 4 步 Root Transform 没勾 Bake Into Pose，或 Animator 没关 Apply Root Motion。
- **手指/上半身姿势怪** → Humanoid 骨映射没配对，回 Configure 手动改；MMD 模型常见 Chest/UpperChest 错位。
- **进游戏不播放** → 检查 Animator 是否真的挂在 prefab 上、Controller 有默认状态、且 Animator 和 prefab 一起进了 bundle（重新 Build 确认 .manifest 里包含 controller/clip）。
- **动作和 NpcBehavior 冲突？** → 不冲突。`NpcBehavior`（转向玩家）改的是根物体朝向，Animator 播的是骨骼姿势，二者并存。
- **打两个 bundle 各自独立** → 阿绫的动作不会影响天依，各自 prefab 各配各的。

---

## 六、逐模型检查清单

### 歌者·阿绫（singer）
- [ ] 模型 FBX Rig = Humanoid，Configure 骨映射无红
- [ ] idle 片段已提取，Loop Time = true，Root 全部 Bake Into Pose
- [ ] AnimatorController 建好，idle 为默认状态
- [ ] `Singer.prefab` 挂 Animator（Avatar+Controller，关 Root Motion）
- [ ] prefab AssetBundle 名 = `singer`
- [ ] Build → 复制 `singer`(+`.manifest`) 到 `assets/models/`
- [ ] 部署 → 进游戏确认循环播放、不漂移

### 凡人·天依（tianyi）
- [ ] 模型 FBX Rig = Humanoid，Configure 骨映射无红
- [ ] idle 片段已提取，Loop Time = true，Root 全部 Bake Into Pose
- [ ] AnimatorController 建好，idle 为默认状态
- [ ] `Tianyi.prefab` 挂 Animator（Avatar+Controller，关 Root Motion）
- [ ] prefab AssetBundle 名 = `tianyi`
- [ ] Build → 复制 `tianyi`(+`.manifest`) 到 `assets/models/`
- [ ] 部署 → 进游戏确认循环播放、不漂移

---

## 七、答辩 / 版权提醒

Mixamo、MMD 配布动作大多要求**署名作者**。在 report / credits 里列出动作来源（作者名 + 链接），避免成绩项目的版权问题。
