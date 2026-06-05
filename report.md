# Outer Wilds 世末歌者游戏剧情 MOD

​																			**87-武文韬 && 86-李庥延**

​																						**2026 年 6 月**

---

## 简介

本项目以游戏 *Outer Wilds*（星际拓荒）为载体，借助 OWML + New Horizons 模组框架，制作了中文 Vocaloid 神话曲《世末歌者》的同人剧情，并在其中实现了一套**自定义图形学着色器系统**。项目共编写了 **7 个自定义着色器**（GLSL/HLSL，运行于 Unity 内置渲染管线），覆盖**屏幕空间后处理**（God Rays 神光、体积雾）、**粒子与程序化材质**（体积雨、地面涟漪）、**程序化网格变形**（FFT 声波可视化）、**平面反射/折射**（水面）以及**程序化纹理**（全息投影）等多类图形学技术。所有效果均可通过游戏内配置面板独立开关，并由一套时间线管理器在剧情高潮（True End）时联动演出。模组内容在 Outer Wilds 的球面星球环境下稳定运行，各效果与"末日轮回"的叙事氛围高度契合。

> 项目仓库：`OuterWildsMOD - The Singer of the End of the world`
>
> 仓库地址：https://github.com/wingtings/OuterWildsMOD-The-Singer-of-the-End-of-the-world
>
> 演示视频：Demo.mp4

**分工：**

| 成员   | 工作                                                       |
| ------ | ---------------------------------------------------------- |
| 武文韬 | 可行性调研，着色器编写、框架搭建、剧情与场景搭建、报告撰写 |
| 李庥延 | 着色器编写，模型物理动画烘焙，效果测试                     |

---

## 项目背景与选题

《世末歌者》由 B 站 UP 主 COPY 创作，投稿于 2016 年 8 月 25 日，今年正好是这首歌曲投稿十周年。歌曲的背景故事围绕神明言和与流浪歌手阿绫的赌约展开：在不允许阿绫主动与他人交流的前提下，只要在末日世界里有人愿意与她共同面对死亡，世界便得以延续，否则孤独的轮回与死亡永不停止。我们尝试将该故事映射到 Outer Wilds 的世界观，写了一份很简单的剧情大纲：

| 原作角色 | 声库 | MOD 中映射 |
| -------- | ---- | --------- |
| 神明 | 言和 | 量子月上的赌约石碑/录音石（不出场） |
| 歌者 | 乐正绫 | 站在音乐厅舞台上的 NPC（不能主动说话） |
| 凡人 | 洛天依 | 站在广场上的可对话 NPC |

我们设计的剧情可以在飞船日志中找到线索树（花了点时间绘制了吗每个事件的封面缩略图），游戏的核心玩法是通过在星系中探索，逐渐发现世界的真相，并拯救歌者和天依：

![飞船日志](/figs/log.png)

Outer Wilds 的核心机制与原作天然契合：**22 分钟时间循环** ↔ 末日轮回；**无法直接交流的赌约** ↔ 探索驱动叙事；**超新星爆发** ↔ 滂沱大雨中的世界终结。

我们课程讲授的内容主要为**计算机图形学技术**，而非剧情或模组逻辑。因此本项目把可游玩的剧情作为"载体"，把自行编写的着色器作为重点内容开发。所以我们并未使用 New Horizons 自带的 `hasRain:true` 等内置天气着色器系统，本项目的雨、雾、神光等效果全部由自编着色器与控制器实现。

---

## 整体技术架构

### 运行环境与渲染管线

- **引擎**：Unity 2019.4.39f1（Outer Wilds 使用**内置渲染管线 Built-in RP**，而非 URP/HDRP）。
- **模组框架**：OWML（模组加载器）+ New Horizons（NH，星球/剧情注入）+ Harmony（运行时打补丁）。
- **语言**：C#（模组逻辑与效果控制器）+ ShaderLab（着色器）。

由于是内置管线，屏幕空间后处理（神光、体积雾）必须采用挂在主相机上的 `OnRenderImage` + `Graphics.Blit` 方案，而非 URP 的 `RendererFeature`。这一选择贯穿全项目，是若干技术细节（如深度图获取、Pass 调度）的根本原因。值得强调的是，这并非"退而求其次"——**Outer Wilds 官方自己的大气雾、海市蜃楼等效果也正是用同样的 `OnRenderImage` 图像后处理实现的**（见 §4.2），我们的方案与原作渲染哲学一致。

### Outer Wilds 官方采用的渲染方案剖析

要理解我们"为什么这样写 shader"，必须先理解 *Outer Wilds* 这个游戏本体的渲染方案——因为我们的所有效果都必须嵌进它的相机栈与坐标系里运行。本节根据模板工程中随游戏分发的反编译脚本（`outer-wilds-unity-template/Assets/Assembly-CSharp/`，类名/签名真实可见，仅方法体被剥离）梳理其渲染架构。

**(1) 渲染管线：Unity 内置管线 + 自定义 BRDF。** Outer Wilds 用 Unity 2019.4 的**内置渲染管线**（Built-in RP，前向渲染 Forward + 线性色彩空间 + HDR），**没有**用 URP/HDRP。这带来两个直接后果：① 所有屏幕后处理只能是挂在相机上的图像特效（`OnRenderImage`/`Graphics.Blit` 或 `CommandBuffer`），没有 SRP 的 `ScriptableRendererFeature`；② 物体着色不是 Unity 默认的 Standard PBR，而是官方一套**自定义能量守恒 BRDF**（脚本里可见 `BRDFManager`、`BRDFRegistry`、`BatchedMaterialLookup`），地表岩石、沙、冰、植被等都用 `Outer Wilds/...` 命名空间下的专用 surface shader，并配合 `HeightmapAmbientLightRenderer` 这类组件伪造环境光/反弹光（内置管线无实时 GI）。

**(2) "代理(Proxy)"系统——本作渲染最核心的设计。** 太阳系尺度极大（数万米），若把所有星球放在真实坐标上，远处坐标的单精度浮点会严重抖动。Outer Wilds 的解法是**浮动原点 + 代理渲染**：玩家所在的活动区域始终被保持在世界原点附近，其余天体相对玩家平移；你在天上看到的远处星球**并不是那颗高精度的真实星球**，而是由 `DistantProxyManager` 管理、按比例缩放摆位的低精度**代理副本**（脚本里成排的 `ProxyPlanet`、`ProxyBody`、`ProxyGiantsDeep`、`ProxyBrittleHollow`、`SunProxy`……）。它们由一台**独立的远景相机**渲染，再与近景相机合成。换言之，游戏画面是**多相机分层合成**（近景 / 远景代理 / 太阳 / 头盔 HUD / 地图模式等多台 `OWCamera`）。

**(3) 跨天文距离的阴影：自定义 Proxy Shadow。** Unity 标准的 shadow cascade 覆盖不了"一颗星球给另一颗星球投影"这种尺度，于是官方实现了一整套代理阴影系统（`ProxyShadowCaster`、`ProxyShadowCascade`、`ProxyShadowLight`、`ProxyShadowCasterGroup`…）来在代理空间里生成远距离阴影。

**(4) 大气雾即图像后处理。** 你环绕每颗星球看到的大气透视，是 `PlanetaryFogRenderer`（`[ImageEffectAllowedInSceneView]`、`[RequireComponent(typeof(OWCamera))]`）这一**逐相机图像特效**做的——基于深度与高度的雾。**这正是我们体积雾(§4.4)与神光(§4.3)走主相机 `OnRenderImage` 的合法性依据**：我们只是在官方已有的图像特效栈上再叠加自己的 Pass。

**(5) 扇区(Sector)流式与 LOD。** 内容按 `Sector` 分区，根据玩家所在扇区启停（`SectorRendererLODGroup`、`SectorProxy`），做剔除与性能管理；星球地表与水/沙等用可细分网格渲染（`TessellatedSphereRenderer`、`TessellatedPlaneRenderer`），量子卫星/黑洞等特殊体则用相机+模板缓冲(Stencil)与专用 shader（`EyeProxyQuantumMoon`、`ProxyQuantumMoon`、`BlackHoleVolume`）。

> **对我们的约束与启示。** 因为是内置管线，我们能、也只能用 `OnRenderImage`/`Graphics.Blit` 做后处理；因为是浮动原点 + 球面星球，我们的雨/涟漪/声波环必须挂在**玩家或星球本地空间**、按**球面径向**而非世界 `-Y` 取向（这解释了 §4.1/§4.2/§4.5 中所有"对齐到径向"的代码）；因为远景是代理副本，后处理的深度判定（如神光的天空阈值）要兼容代理几何。理解官方方案后，我们的每一个工程取舍都不再是"试出来的"，而是被渲染架构唯一确定的。

### 模组如何注入游戏本体：OWML + Harmony + New Horizons

本项目**不修改游戏的任何原始文件**，所有内容都在游戏进程启动后于运行时**增量注入**。这条注入链由三层组成：

**(1) OWML —— 把我们的 DLL 变成游戏里的一个 MonoBehaviour。** Outer Wilds Mod Manager 会在游戏目录装一个启动补丁（doorstop / BepInEx 式 patcher）。游戏一启动，该补丁先拉起 OWML（Outer Wilds Mod Loader），OWML 扫描 `OWML/Mods/` 下每个模组的 `manifest.json`，加载其 DLL，把其中继承自 `ModBehaviour`（本质是 `MonoBehaviour`）的主类实例化进 Unity 场景，并按 Unity 生命周期调用 `Awake()/Start()`。于是我们的 `TheSingerOfTheEnd` 类**就是游戏进程里的一个真实游戏对象**，能直接访问 `UnityEngine` 与游戏的 `Assembly-CSharp`（`Locator`、`PlayerData`、`DialogueConditionManager` 等都是这么调到的）。`manifest.json` 里：

```json
{
  "filename": "TheSingerOfTheEnd.dll",
  "uniqueName": "wingtings.TheSingerOfTheEnd",
  "owmlVersion": "2.15.1",
  "dependencies": ["xen.NewHorizons"]   // 声明依赖 New Horizons，OWML 保证其先加载
}
```

**(2) Harmony —— 运行时给游戏方法打补丁。** `new Harmony(...).PatchAll(...)`（`TheSingerOfTheEnd.cs:83`）会扫描程序集里带 `[HarmonyPatch]` 的类，用 IL 织入的方式给游戏原方法挂 prefix/postfix/transpiler 钩子，**无需改动游戏文件**即可改变其运行时行为。这是 OW 模组能"改本体逻辑"的通用机制。

**(3) New Horizons —— 用 JSON 声明式地造星球与剧情。** NH 本身也是一个 OWML 模组，对外暴露 `INewHorizons` API。我们在 `Start()` 里 `TryGetModApi<INewHorizons>("xen.NewHorizons")` 拿到它，再调 `NewHorizons.LoadConfigs(this)`（`TheSingerOfTheEnd.cs:68`），NH 便会读取本模组目录下的 `planets/`、`systems/`、`translations/` 等配置，在运行时**构造 GameObject**：生成/改写星球地形（heightMap/程序化）、按 prefab 路径或 AssetBundle 实例化道具与角色、接线对话树、注入飞船日志与信号、甚至替换标题界面。**整个内容层是声明式的——不写引擎代码就能造世界**；而真正要写引擎代码（我们的 7 个图形效果控制器）的部分，则在 NH 把场景搭好、星系加载完成后，由我们监听 `GetStarSystemLoadedEvent()` 再挂上去。

> 一句话概括三层关系：**OWML 让我们的代码进游戏跑，Harmony 让我们改游戏已有行为，New Horizons 让我们声明式地往游戏里加世界**；我们的图形学代码则站在这三层之上，作为普通 Unity 组件挂到玩家相机/身体上。

### Unity 模板工程（outer-wilds-unity-template）的原理与作用

着色器、材质、模型这类**资产**无法用 `dotnet build` 编译进 DLL，必须先在 Unity 编辑器里打成 **AssetBundle**。但游戏运行在 Unity 2019.4 的特定版本、且资产里常需引用游戏自带的脚本组件——为此官方提供了 `outer-wilds-unity-template` 这个**与游戏同版本的 Unity 工程**。它的原理是：

- **内置游戏脚本的"桩"副本**：工程的 `Assets/Assembly-CSharp/` 里放着游戏全部脚本的反编译版本（**只有类型与签名、方法体被剥空**，本项目里可见 `OWRenderer.cs`、`PlanetaryFogRenderer.cs` 都只剩空类）。它们不是用来运行的，而是让 Unity 编辑器**认识游戏的组件类型**：当我们做 prefab 时可以挂 `OWItem` 等游戏组件，Unity 会按类型/GUID 把引用序列化进 AssetBundle；到了真实游戏里，这些引用再解析回游戏真正的实现类。
- **同版本编辑器保证二进制兼容**：AssetBundle 与 Unity 版本强绑定，用同版本工程打包才能被游戏 `AssetBundle.LoadFromFile` 正确加载。
- **既用于自定义 shader/材质，也用于角色与舞台模型**：本项目的 7 个 `.shader`、各自的 `Material`、以及歌者/天依的 MMD 模型与自制舞台（§五），全部在此工程内制作并打成 bundle。工程里那一大批 `Assets/Shaders/Toon_*`（UnityChanToonShaderVer2）即为角色卡通渲染所引入（§6.2）。

### 我们的完整工作流

把上面三层串起来，本项目实际的迭代工作流如下（四条管线并行、最后在游戏里合一）：

```
①叙事/设计  原曲剧情 → 映射到 OW 机制（22分钟循环=末日轮回，赌约=探索叙事）

②内容管线(声明式)   编辑 planets/*.json·dialogue/*.xml·shiplog/*.xml
                    → dotnet build 自动部署 → 进游戏用调试键校验
                    （F7 识别 prop 路径 / P 键 Debug Raycast 摆放坐标 / 重置剧情开关）

③图形/资产管线(Unity)   写 .shader → 建 Material → 指定 bundle 名 "shaders"
                       → BuildAssetBundles → 拷贝到 mod 的 assets/shaders/
                       （MMD 模型同理：FBX→Rig→UTS2 材质→prefab→models bundle）

④代码管线(C#)   写 ModBehaviour / 各效果 Controller
               → dotnet build（编译依赖 OWML + OuterWildsGameLibs 引用程序集）
               → csproj 的 DeployToOWML 目标 robocopy /MIR 镜像到 OWML Mods 目录

⑤运行验证   Mod Manager 启用本模组 → 进"世末"星系 → MOD 设置面板逐项开关 → 迭代
```

其中**自动部署**是效率关键：`TheSingerOfTheEnd.csproj` 末尾的 `DeployToOWML` 目标在每次 `Build` 后把输出目录**整目录镜像**（`robocopy /MIR`）到 `%APPDATA%\OuterWildsModManager\OWML\Mods\wingtings.TheSingerOfTheEnd`，避免"改了 JSON 但部署版还是旧的"。C# 代码与声明式内容（JSON/XML）改完即 `dotnet build` 上车；唯独**改了 `.shader` 必须回 Unity 工程重打 bundle**（材质是烘进 bundle 二进制的），这是本工作流里最重的一环，因此能用 C# uniform 调的参数尽量不动 shader 源码（如神光 `ForceRays` 模式即是纯 C# 复用既有 uniform、免重建 bundle 的设计）。

### 着色器资产管线（AssetBundle）

着色器无法以源码形式直接交给运行中的游戏，必须在 Unity 工程中：

```
.shader 源码  →  创建 Material  →  统一 bundle 名 "shaders"
            →  BuildAssetBundles  →  拷贝到 MOD 的 assets/shaders/shaders
            →  运行时 AssetBundle.LoadFromFile 加载  →  取出 Material
```

`AssetLoader.cs` 负责"只加载一次并缓存"全部材质，缺失的材质返回 `null`，对应控制器会自动跳过，从而保证编译与其它效果不受影响：

```csharp
public static void Preload()
{
    if (Bundle == null) return;
    GodRay = Bundle.LoadAsset<Material>("Assets/Materials/GodRayMat.mat");
    Rain   = Bundle.LoadAsset<Material>("Assets/Materials/RainMat.mat");
    Ripple = Bundle.LoadAsset<Material>("Assets/Materials/RippleMat.mat");
    Fog    = Bundle.LoadAsset<Material>("Assets/Materials/FogMat.mat");
    // AudioWave / Water / Hologram 同理，缺失则为 null，控制器自动跳过
}
```

### 模组加载与效果挂载流程（技术路径）

```
TheSingerOfTheEnd.Start()
  ├─ 获取 New Horizons API
  ├─ NewHorizons.LoadConfigs()           // 注入星球/剧情/对话/日志
  ├─ AssetLoader.Preload()               // 加载着色器 bundle
  ├─ Harmony.PatchAll()
  └─ 监听 StarSystemLoaded
        └─ OnStarSystemLoaded("SolarSystem")
              ├─ 挂载 EndingJudge（结局判定）
              └─ SetupGraphics() 协程
                    ├─ 等待玩家相机就绪
                    ├─ 主相机挂 GodRayController / VolumetricFogController
                    └─ 等玩家身体就绪后部署：
                         RainController（体积雨+涟漪）
                         AudioVisualizerController（声波）
                         PlanarReflectionController（水面）
                         HologramController（全息）
                         NpcBehavior / TimelineManager
```

七个效果各有一个布尔开关，统一从 `default-config.json` 读取，并支持游戏内**实时开关**（`Configure()` → `ApplyShaderTogglesLive()`）：

```csharp
public bool GodRayEnabled    { get; private set; }
public bool RainEnabled      { get; private set; }
public bool RippleEnabled    { get; private set; }
public bool AudioWaveEnabled { get; private set; }
public bool FogEnabled       { get; private set; }
public bool WaterEnabled     { get; private set; }
public bool HologramEnabled  { get; private set; }
```

###  项目文件树

```
TheSingerOfTheEnd/                          # 解决方案根目录
└── TheSingerOfTheEnd/                      # MOD 工程目录
    ├── planets/                            # 星球内容（NH 注入原版星球）
    │   ├── singer_world.json               # 废岩星(Attlerock)·世末之城
    │   ├── god_realm.json                  # 量子月(Quantum Moon)·神谕之境
    │   ├── audio/                          # 雨声 / 伴奏 / 人声（OGG）
    │   ├── text/                           # 挪麦可翻译文字 XML（日记 / 赌约石碑）
    │   ├── dialogue/                       # 对话树 XML（天依 / 扩音装置）
    │   └── shiplog/                        # 飞船日志 XML + 缩略图
    ├── systems/SolarSystem.json            # 向原版星系注入配色 + Entry 坐标
    ├── translations/english.json           # UI / 日志 / 对话翻译键
    ├── assets/
    │   ├── models/                         # 角色与舞台模型 bundle
	│   │   ├── singer                      # 歌者·阿绫 模型
	│   │   ├── tianyi                      # 凡人·天依 模型
	│   │   └── stage                       # 自制舞台模型（含 MeshCollider）
    │   └── shaders/                        # ★ 自定义着色器源码 + 编译产物
    │       ├── GodRay.shader               # 神光 / 丁达尔（屏幕空间后处理）
    │       ├── VolumetricRain.shader       # 体积雨粒子
    │       ├── RainRipple.shader           # 地面涟漪水洼
    │       ├── VolumetricFog.shader        # 体积雾（Ray Marching + Beer-Lambert）
    │       ├── AudioWave.shader            # 声波可视化（FFT 顶点位移）
    │       ├── WaterReflection.shader      # 水面反射 / 折射（平面反射）
    │       ├── Hologram.shader             # 全息投影（扫描线 + Fresnel + Glitch）
    │       └── shaders / shaders.manifest  # 已编译 AssetBundle
    ├── TheSingerOfTheEnd.cs                # ★ MOD 主入口（加载配置 / 挂载效果）
    ├── AssetLoader.cs                      # ★ 着色器 bundle 加载与材质缓存
    ├── GodRayController.cs                 # ★ 神光后处理控制器（3-Pass Blit）
    ├── RainController.cs                   # ★ 体积雨粒子 + 涟漪部署
    ├── VolumetricFogController.cs          # ★ 体积雾后处理（四角射线重建）
    ├── AudioVisualizerController.cs        # ★ FFT 频谱 → 环形网格
    ├── PlanarReflectionController.cs       # ★ 镜像相机平面反射
    ├── HologramController.cs               # ★ 全息面板部署
    ├── NpcBehavior.cs                      # 歌者 / 天依 NPC 行为
    ├── TimelineManager.cs                  # ★ 22 分钟时间线 + True End 演出
    ├── EndingJudge.cs                      # 结局判定
    ├── manifest.json / addon-manifest.json / default-config.json
    ├── title-screen.json                  # 自定义标题界面（歌者立于旋转舞台）
    └── TheSingerOfTheEnd.csproj
```

> ★ 标注的为本项目自行编写的图形学相关核心代码。

---

## 三、自定义图形学效果：原理与实现

### 体积雨 Volumetric Rain（粒子系统 + 运动模糊着色器）

**图形学原理：** 用 Unity 粒子系统提交大量 billboard 雨滴（GPU Instancing 批量渲染），着色器在顶点阶段沿屏幕竖直方向拉伸四边形以模拟高速下落的**运动模糊拖尾**；片元阶段用到中心轴的距离做软衰减得到"雨丝"截面，并叠加 **Fresnel** 边缘高光模拟水的折射感。

**关键修复（值得记录的踩坑）：** 早期实现用 `UnityObjectToViewPos(0)`（系统原点）重建 billboard，导致所有粒子塌缩到玩家脚下一点、整片雨不可见；修正为按粒子自身顶点 `UnityObjectToClipPos(v.vertex)` 渲染，竖直拖尾改由 C# 端的细高 `startSize3D` 实现。

```hlsl
// 片元：雨丝截面 + 拖尾淡出 + Fresnel 高光
float distToAxis = abs(i.uv.x - 0.5) * 2.0;
float lineAlpha  = saturate(1.0 - distToAxis / _Width);   // 到竖直中轴 → 雨丝
float headTail   = saturate(1.0 - abs(i.uv.y - 0.5) * 2.0 / _Softness); // 首尾淡出
float fresnel    = pow(1.0 - saturate(i.viewDir.z), _FresnelPower);     // 边缘折射高光
col.rgb += fresnel * 0.3;
col.a   *= lineAlpha * headTail;
```

**实现要点（球面星球的特殊处理）：** Outer Wilds 的星球是球体，没有统一的"下方"。`RainController` 把粒子系统挂在玩家身上、在**玩家本地空间**沿 `-Y` 下落，从而无论玩家在球面何处雨都朝脚下落；并用"距星心 < 大气层半径(150m)"做门控，离开大气层（太空 / 量子月）自动停雨：

```csharp
main.simulationSpace = ParticleSystemSimulationSpace.Local;
vol.y = new ParticleSystem.MinMaxCurve(-30f);   // 本地空间向脚下落
// Update 中：
bool inAtmosphere = Vector3.Distance(player.position, _planet.position)
                      < TheSingerOfTheEnd.AttlerockAtmosphereRadius;
```

**效果展示。**

![体积雨 - 开启](/figs/rain_on.png)
![体积雨 - 关闭](图片占位：rain_off.png)

<p style="text-align:center;"><strong>图 4.1：开启 / 关闭体积雨着色器的对比</strong></p>
---

### 地面涟漪 Rain Ripple（程序化高度场 + 屏幕空间偏导法线）

**图形学原理：** 在地面贴片上用若干个"扩散环"叠加出高度场 `h(x,z,t)`，每个环是一条沿半径传播、随距离指数衰减的正弦波，模拟雨滴落点向外扩散的水纹：

```
ripple(p, c, t) = sin(d·freq − t·speed) · exp(−d·falloff),   d = |p − c|
```

再用屏幕空间偏导 `ddx/ddy` 求高度场梯度得到**扰动法线**（无需法线贴图），配合 Blinn-Phong 高光与 Fresnel 得到"湿/反光"的水洼质感。

```hlsl
float heightField(float2 p) {
    float t = _Time.y, h = 0;
    h += ripple(p, float2(0.25,0.30), t);
    h += ripple(p, float2(0.70,0.65), t+1.7);   // 多个错相位落点同时扩散
    h += ripple(p, float2(0.45,0.85), t+3.1);
    h += ripple(p, float2(0.85,0.15), t+4.6);
    return h * 0.25;
}
// 片元：屏幕空间偏导 → 扰动法线 → Blinn-Phong + Fresnel
float dhx = ddx(h), dhy = ddy(h);
float3 N  = normalize(i.worldNrm + float3(dhx, 0, dhy) * 8.0);
float spec    = pow(saturate(dot(N, H)), _Shininess);
float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
```

**实现要点：** 着色器加 `Cull Off`，C# 端把水洼 Quad 的法线对齐到球面径向（`Quaternion.FromToRotation(Vector3.forward, radial)`）并投影到舞台所在半径，修复了早期"涟漪平面竖起来夹住歌者"的方向 bug。

**效果展示：**

![地面涟漪](图片占位：ripple.png)

<p style="text-align:center;"><strong>图 4.2：歌者舞台周围的程序化涟漪水洼</strong></p>
---

### God Rays 神光 / 丁达尔效应（屏幕空间体积光散射，三 Pass 后处理）

**图形学原理：** 采用经典的 GPU Gems / Kodeco "径向模糊光散射"近似，由 C# 端用 `Graphics.Blit` 依次调度三个 Pass：

1. **Occlusion（遮挡）**：以太阳屏幕坐标为中心取一块**有界圆盘亮源**，只有天空像素（`Linear01Depth ≥ 阈值`）算亮源、实体几何算黑，从而切出被遮挡的光隙。关键改进：亮源上限被钳到 `_RayColor(≤1)`，避免把 HDR 太阳原样喂入造成整屏过曝。
2. **RadialBlur（径向模糊）**：从每个像素朝光源方向步进采样、按 `decay` 衰减累加，把亮源沿径向"拉"成光束。
3. **Composite（合成）**：用 **Screen 滤色混合** `1−(1−a)(1−b)` 叠回原场景，数学上不会超过 1，是防止"圣光糊成死白"的第二道保险。

```hlsl
// Pass 0 Occlusion：天空判定 + 太阳圆盘
float isSky = step(_DepthThreshold, Linear01Depth(rawDepth));
float2 d = (i.uv - _LightPos.xy); d.x *= _ScreenParams.x/_ScreenParams.y;
float disk = saturate(1.0 - length(d)/_SourceRadius); disk *= disk;
return _RayColor * (isSky * disk * step(0.0, _LightPos.z));

// Pass 1 RadialBlur：沿径向累加衰减
for (int s = 0; s < _Samples; s++) {
    uv -= deltaUV;
    color += tex2D(_MainTex, uv) * (illuminationDecay * _Weight);
    illuminationDecay *= _Decay;
}
```

**实现要点（叙事驱动的设计）：** 按剧情需要，神光**只在 True End 演出期间**出现。`GodRayController` 提供 `ForceRays` 强制模式：把天空阈值 `_DepthThreshold` 降为 0、在固定屏幕坐标合成一个"人造太阳盘"，于是胜利时无论玩家朝哪都能看到"阳光穿透乌云"的光柱，而不受太阳实际朝向限制。

```csharp
private void OnRenderImage(RenderTexture src, RenderTexture dst) {
    if (!ForceRays || Intensity <= 0.001f) { Graphics.Blit(src, dst); return; }
    Graphics.Blit(src, occ,  _mat, 0);   // Occlusion
    Graphics.Blit(occ, blur, _mat, 1);   // RadialBlur
    Graphics.Blit(blur, occ, _mat, 1);   // 再来一次 → 光束更长
    _mat.SetTexture("_SceneTex", src);
    Graphics.Blit(occ, dst,  _mat, 2);   // Composite (Screen)
}
```

**效果展示。**

![神光 True End](图片占位：godray.png)

<p style="text-align:center;"><strong>图 4.3：True End 时"阳光穿透乌云"的神光演出</strong></p>
---

### 体积雾 Volumetric Fog（Ray Marching + Beer-Lambert 大气散射）

**图形学原理：** 屏幕空间后处理，对每个像素：

- **重建世界射线**：C# 端用 `Camera.CalculateFrustumCorners` 把相机四角的世界空间射线打进 `_FrustumCornersWS`，片元用 uv 双线性插值得到本像素射线，再用 `Linear01Depth` 缩放到场景命中点。
- **沿射线步进采样雾密度** `σ = σ₀ · 高度衰减 · 3D噪声(随时间流动)`。
- **Beer-Lambert 透过率积分**：每步不透明度 `a = 1 − exp(−σ·Δt)`，透过率 `T *= (1−a)`，雾色按 `T·a` 累加，是物理上正确的参与介质（Participating Media）积分近似。

```hlsl
float3 ray = lerp(lerp(bl, br, uv.x), lerp(tl, tr, uv.x), uv.y); // 双线性插值四角射线
float3 hit = ro + ray * Linear01Depth(...);                      // 场景命中点
for (int s = 0; s < steps; s++) {
    float3 p = ro + dir * (stepLen * (s + 0.5));
    float n     = 0.55 + 0.45 * noise3(p * _NoiseScale + t);     // 3D 值噪声流动
    float sigma = _FogDensity * h * n;
    float a = 1.0 - exp(-sigma * stepLen);                       // Beer-Lambert
    fog += T * a * _FogColor.rgb;  T *= (1.0 - a);
    if (T < 0.01) break;                                         // 提前终止优化
}
return fixed4(scene * T + fog, 1.0);
```

**实现要点：** 3D 值噪声用 hash + 三线性插值在着色器内程序化生成（无需 3D 纹理）。`VolumetricFogController` 把雾限制在废岩星大气层内并随距离渐隐；同时为避免雾的 `OnRenderImage` 盖住神光，True End 时由时间线先把雾密度淡出再关闭该控制器。

**效果展示：**

![体积雾 - 开启](/figs/fog_on.png)
![体积雾 - 开启2](/figs/fog_on2.png)

<p style="text-align:center;"><strong>图 4.4：开启 / 关闭体积雾的大气透视对比</strong></p>
---

### 声波可视化 Audio Wave（实时 FFT + 程序化网格顶点位移）

**图形学原理：** C# 端用 `AudioListener.GetSpectrumData`（FFTWindow.BlackmanHarris）做实时频谱分析，把 256 点频谱归并为 32 段写入着色器 uniform 数组 `_Spectrum[32]`。顶点着色器按角向坐标取对应频段幅值、沿法线位移，叠加沿 uv.y 向外传播的正弦相位，形成"以歌者为中心向外扩散的声波环"；片元按 **频率→HSV 色相、幅值→亮度** 上色并加色发光。

```csharp
// C# 端：FFT → 32 段 → 上传
AudioListener.GetSpectrumData(_samples, 0, FFTWindow.BlackmanHarris);
for (int b = 0; b < Bins; b++) {
    float sum = 0f; for (int k = 0; k < per; k++) sum += _samples[b*per + k];
    _bins[b] = Mathf.Clamp01(Mathf.Sqrt(sum/per) * Gain);   // sqrt 提升弱信号
}
_mat.SetFloatArray("_Spectrum", _bins);
```

```hlsl
// 顶点：按频段幅值沿法线位移 + 向外传播涟漪
int bin   = (int)floor(saturate(v.uv.x) * 31.0);
float amp = max(_Spectrum[bin], _Floor);
float ripple = sin(v.uv.y * _RippleFreq - _Time.y * _RippleSpeed) * 0.5 + 0.5;
float disp   = amp * _Displacement * (0.35 + 0.65 * v.uv.y) * ripple;
float3 p = v.vertex.xyz + normalize(v.normal) * disp;
```

**实现要点：** 环形（annulus）网格由 C# 程序化生成（`BuildRing`），uv.x = 绕环角度（→频段），uv.y = 内圈到外圈（→涟漪传播），并对齐到歌者所在的球面径向。

**效果展示：**

![声波可视化](图片占位：audiowave.png)

<p style="text-align:center;"><strong>图 4.5：随歌声起伏的声波环</strong></p>
---

### 水面反射与折射 Water Reflection（平面反射相机 + 屏幕空间扰动）

**图形学原理。** 内置管线经典平面反射：用一台**镜像相机**把场景按水面平面翻转渲染到 RenderTexture，着色器按屏幕坐标采样这张反射图得到倒影；再用程序化正弦波纹梯度对采样 UV 做偏移，模拟水面波动下的折射晃动；Fresnel 控制反射/透射混合比（掠射角反射强）。

```csharp
// C# 端：反射矩阵 + 斜裁剪近平面（标准平面反射）
Matrix4x4 reflection = CalcReflectionMatrix(plane);
_reflCam.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;
Vector4 clipPlane = CameraSpacePlane(_reflCam, pos, normal, 1f);
_reflCam.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);
GL.invertCulling = true;  _reflCam.Render();  GL.invertCulling = false; // 镜像翻转绕序
_mat.SetTexture("_ReflectionTex", _rt);
```

```hlsl
// 片元：波纹梯度扰动反射 UV + Fresnel 混合
float2 ruv = i.screenPos.xy / i.screenPos.w + grad * _Distort;
fixed3 refl = tex2D(_ReflectionTex, ruv).rgb;
float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
fixed3 col = lerp(_WaterColor.rgb, refl, saturate(_ReflStrength*(fres+0.15)));
```

**实现要点。** 用静态 `_rendering` 标志防止反射相机递归触发自身；反射相机 `CopyFrom` 主相机参数并仅在 `OnWillRenderObject` 时手动 `Render`。叙事上，反射中可看到歌者的倒影，呼应"孤独"主题。

**效果展示。**

![水面反射](图片占位：water.png)

<p style="text-align:center;"><strong>图 4.6：歌者舞台前反射水池中的倒影</strong></p>
---

### 全息投影 Hologram（程序化扫描线 + Fresnel 边缘光 + Glitch 抖动）

**图形学原理。** 纯程序化、无需贴图的全息材质：

- **扫描线**：基于**世界坐标 Y 轴**的正弦函数随时间滚动（用世界 Y 而非 UV，使扫描带在物体旋转时仍稳定贴在世界空间），模拟 CRT/全息扫描带。
- **边缘发光（Fresnel）**：视线越接近表面切线越亮，模拟全息体光场在边缘汇聚。
- **故障抖动（Glitch）**：顶点阶段按"行块 + 时间块"取 hash 噪声，周期性地把该行 uv.x 水平错位，产生偶发撕裂。

```hlsl
// 顶点 Glitch：按行块 + 时间块触发水平错位
float block = floor(v.uv.y * 12.0), tBlock = floor(_Time.y * _GlitchSpeed);
float g = (hash11(block + tBlock) - 0.5) * 2.0;
float trigger = step(0.80, hash11(tBlock*1.7 + block*0.37));
uv.x += g * _GlitchStrength * trigger;

// 片元：世界 Y 扫描线 + Fresnel 边缘辉光
float scan = sin(i.worldPos.y * _ScanCount - _Time.y * _ScanSpeed) * 0.5 + 0.5;
float fres = pow(1.0 - saturate(dot(N, V)), _RimPower);
col = _HoloColor.rgb * tex.rgb * lerp(1.0, scan, _ScanStrength) + _RimColor.rgb * fres;
```

**效果展示。**

![全息投影](/figs/hologram.png)

<p style="text-align:center;"><strong>图 4.7：神谕之境的全息信息面板</strong></p>
---

## 效果联动：时间线与结局演出

`TimelineManager` 把上述效果与 22 分钟循环、剧情结局绑定，体现图形效果服务于叙事：

- **平时**：循环后半段（t > 0.5）雨量从 4000 渐增到 6000 粒/秒，烘托末日临近。
- **True End 演出**（由 `EndingJudge` 触发，分三阶段，共约 8 秒）：
  1. **雨停雾散（0~3s）**：雨量 6000→0，雾密度 1→0，神光强度 0→峰值 0.9；
  2. **圣光保持（3~5s）**：神光维持峰值（阳光穿透乌云）；
  3. **圣光淡出（5~8s）**：神光 0.9→0，关闭强制模式恢复常态。

```csharp
// 阶段一：雨停 + 雾散 + 圣光升起
float k = Mathf.Clamp01(_trueEndTimer / PhaseRainFade);
RainController.Instance?.SetEmissionRate(Mathf.Lerp(6000f, 0f, k));
if (_fog != null)    _fog.DensityScale = Mathf.Lerp(1f, 0f, k);
if (_godRay != null) _godRay.Intensity = Mathf.Lerp(0f, RayPeak, k);
```

> **渲染顺序的工程细节**：体积雾与神光都走主相机 `OnRenderImage`，雾若后执行会盖住神光，故时间线在雾完全散去后显式 `_fog.enabled = false`，确保圣光不被覆盖。

![True End 演出](图片占位：trueend.png)

> *图 4.1：雨停 → 阳光穿透 → 十指相扣的 True End 演出（预留截图）*

---

## 角色资产与卡通渲染（Technical Art）

除上述七个屏幕/场景着色器外，本项目还包含一条**角色美术管线**，把二次元歌姬模型以契合的画风带入游戏。

### MMD 模型与舞台

- **MMD 模型**：歌者·阿绫与凡人·天依分别在 Unity 模板工程中导入 MMD 模型、配 Rig、打包为独立 AssetBundle（`assets/models/singer`、`assets/models/tianyi`），由 New Horizons 通过 `assetBundle + path` 实例化到废岩星的两个平台（替换了早期的索拉努姆占位）。
- **舞台模型**：`assets/models/stage`（含 `MeshCollider`），歌者立于其上，呼应"音乐厅舞台上的歌者"设定。
- **自定义标题界面**（`title-screen.json`）：关闭随机 NH 星球与默认篝火星球，让歌者立于缓慢旋转的舞台之上，并以《世末歌者》伴奏作为菜单 BGM——把图形与叙事氛围从进入游戏前就建立起来。

### 卡通渲染（UTS2，进行中）

MMD 模型默认的 Standard PBR 材质在二次元角色上"发灰发塑料"。本项目在 Unity 2019.4 + 内置渲染管线的硬约束下，调研并选用 **UnityChanToonShaderVer2（UTS2，v2.0.9）** 作为卡通渲染方案（URP 专用的 UTS3 最低要 2020.3，被排除）：

- **色阶（Cel/Ramp）**：把连续的 `N·L` 光照在阈值处硬切为「亮部 / 一阶暗部 / 二阶暗部」，暗部往冷/偏紫挪以保持"通透"；
- **Rim Light**：基于 `1−N·V` 的边缘光，在阴雨 + 神光逆光场景中把角色从背景剥离；
- **MatCap**：直接接管 MMD 自带的 `.sph/.spa` 球面贴图，恢复头发/眼睛/金属件的高光质感；
- **Outline**：反向壳法描边（沿法线外推 + 翻面），故对 FBX 导入坚持 `Normals = Import` 以保住自定义法线。

为处理模型 30+ 个材质，编写了批量转换 Editor 脚本（`Assets/Editor/MmdToUTS2.cs`），按材质名（皮肤 / 脸 / 眼睛 / 头发 / 衣物 / 自发光件）自动套用不同的卡通预设，再逐个微调。配套还有去除冗余材质属性 / shader 关键字、防变体剥离（`Always Included Shaders`）等打包脚本，保证卡通 shader 进 AssetBundle 后不"粉红"。

> 详细选型论证与逐材质方案见 Github 仓库 `logs/5.30_Unity.md`、`logs/5.28_TA.md`。

### 待机动画与 FFT 自发光（规划中）

- **待机动画**：为两个 MMD 模型接入循环待机动作（Mixamo Humanoid 重定向 或 MMD 原生 VMD 两条路线），随 prefab 上的 `Animator` 打进同一 bundle，由 NH 实例化后自动循环，无需额外 C#。方案见 `logs/idle_animation_guide.md`。
- **FFT 自发光联动**：计划把声波可视化（§3.5）的实时 FFT 频谱复用到歌者衣物/耳机的自发光材质——低频驱动"心跳"、整体能量驱动"波形"，配合 Bloom 让角色随歌声呼吸发光，把声波环、神光、角色串成一台完整演出。

## 运行与使用方法

1. 安装 Outer Wilds Mod Manager 与 New Horizons 依赖；
2. 将本 MOD 放入 OWML Mods 目录（`csproj.user` 配置了 `dotnet build` 自动部署）；
3. 在 Mod Manager 中启用 *The Singer Of The End*，进入游戏选择 *世末* 星系；
4. 在游戏内 MOD 设置面板中可**逐项开关**七个着色器效果，并支持调试 / 重置剧情进度。

各效果的开关键名：

| 配置项 | 效果 |
| ------ | ---- |
| God Ray 神光 | 屏幕空间体积光（仅 True End 出现） |
| Volumetric Rain 体积雨 | 粒子雨 + 运动模糊 |
| Rain Ripple 地面涟漪 | 程序化水洼 |
| Volumetric Fog 体积雾 | Ray Marching 大气散射 |
| Audio Wave 声波可视化 | FFT 驱动声波环 |
| Water Reflection 水面反射 | 平面反射水池 |
| Hologram 全息投影 | 程序化全息面板 |

---

## 总结与展望

本项目在 Outer Wilds 内置渲染管线下，从零实现了覆盖后处理、粒子、程序化材质、网格变形、平面反射、程序化纹理等多个图形学分支的 **7 个自定义着色器**，并以一套配置/控制器/时间线系统把它们组织成服务于《世末歌者》叙事的整体。在此之上还搭建了一条角色美术管线：双 MMD 角色、自制舞台、自定义标题界面，以及正在落地的 UTS2 卡通渲染。过程中解决了球面星球的雨向、屏幕空间深度获取、平面反射递归、后处理渲染顺序、AssetBundle 变体剥离等一系列工程问题。

**进行中 / 可改进方向**：
- 完成角色 **UTS2 卡通渲染**进游戏验证（防粉红、法线/描边、脸部色阶微调）；
- 为歌者/天依接入**待机动画**；

___

## 附录：参考资料 & 借物表

- God Rays Shader Breakdown — https://cyanilux.com/tutorials/god-rays-shader-breakdown/
- Volumetric Light Scattering (Kodeco)
- Unity URP Volumetric Fog — https://www.vertexfragment.com/ramblings/urp-volumetric-fog/
- OWML / New Horizons 官方文档 — https://owml.outerwildsmods.com/ , https://nh.outerwildsmods.com/
- UnityChanToonShaderVer2 (UTS2) v2.0.9 — https://github.com/unity3d-jp/UnityChanToonShaderVer2_Project
- lilToon（卡通渲染备选方案）— https://github.com/lilxyzw/lilToon
- Vesper's Assorted Outer Wilds Shaders（着色器集成参考）

借物表：

- 乐正绫-世末歌者配布版：猫妖zhi泪 https://space.bilibili.com/3135576
- 洛天依V4汉族篇：唯孤君

- 吉他：Sega/Ricetans90°
- 小舞台：林依