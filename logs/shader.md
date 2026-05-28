# Shader 落地保姆级指南（Unity 2019 零基础）

> Author: 整理 by Claude（2026-05-28）
>
> 适用对象：本项目 `TheSingerOfTheEnd/TheSingerOfTheEnd/assets/shaders/` 下的 **7 个**自定义 shader。
> 目标：在**完全不会用 Unity** 的前提下，一步一步把它们做成游戏里真正能跑、答辩能演示的效果。
>
> 看完你需要做的只有三件事：**① 在 Unity 里建 7 个材质 → ② 打成一个叫 `shaders` 的包 → ③ 把包拷回项目**。
> C# 代码这边**已经全部接好了**（控制器、加载器、挂载时机都写完且编译通过），你不用碰一行代码。

---

## 〇、先理解 3 件事（5 分钟，能省你一天）

1. **游戏运行时不认 `.shader` 源码，只认"编译好的材质"。** 编译只发生在 Unity 编辑器里。所以必须在 Unity 里把 shader 包成 **AssetBundle**（一种打包好的资源文件），游戏再从这个包里加载。
2. **真正被打进包的是"材质(Material)"，不是 shader。** 材质引用了 shader，打包材质时 shader 会被自动带上。所以你的核心操作是"建材质"。
3. **OW 用的是 Unity 内置渲染管线（Built-in RP），不是 URP/HDRP。** 本项目所有 shader 都是按内置管线写的，你**不要**去装 URP，也不要动渲染管线设置，否则会变粉。

一句话流程：

```
Unity 工程里：建 7 个材质 → 都标上 bundle 名 "shaders" → 点菜单打包
        ↓ 产出一个无扩展名的文件 shaders
拷到：TheSingerOfTheEnd/TheSingerOfTheEnd/assets/shaders/shaders   ← 覆盖现有的同名文件
        ↓ dotnet build（会自动部署到 MOD 目录）
进游戏：C# 自动 LoadFromFile 加载这个包，取出材质挂到相机/粒子/水面/面板
```

> ⚠️ 路径要点：本项目的 C# (`AssetLoader.cs`) 从 **`assets/shaders/shaders`** 读包。
> （旧版 `shader_packaging_guide.md` 里写的是 `assets/models/shaders`，**已过时，以本文为准**。）

---

## 一、7 个 shader → 7 个材质 对照表（最重要，先抄下来）

C# 是用**写死的资产路径**去包里找材质的。所以材质的**文件名、所在文件夹、shader 选择必须和下表一模一样**，错一个字母就会加载成 null（那个效果就不出现）。

| # | 材质文件（路径必须一致） | 材质里要选的 Shader | 对应 C# 控制器 | 进游戏在哪看 |
|---|--------------------------|---------------------|----------------|--------------|
| 1 | `Assets/Materials/GodRayMat.mat`   | `Custom/GodRays`        | GodRayController（相机后处理） | 面朝太阳时屏幕出现放射光束；True End 雨停穿云爆发 |
| 2 | `Assets/Materials/RainMat.mat`     | `Custom/VolumetricRain` | RainController（粒子）        | 在歌者周围 200m 内下雨（跟随玩家） |
| 3 | `Assets/Materials/RippleMat.mat`   | `Custom/RainRipple`     | RainController（水洼 Quad）   | 歌者舞台周围 3 块地面水洼涟漪 |
| 4 | `Assets/Materials/AudioWaveMat.mat`| `Custom/AudioWave`      | AudioVisualizerController（环） | 歌者脚下的声波环，靠近且有歌声时起伏 |
| 5 | `Assets/Materials/FogMat.mat`      | `Custom/VolumetricFog`  | VolumetricFogController（相机后处理） | 全场景体积雾/大气透视 |
| 6 | `Assets/Materials/WaterMat.mat`    | `Custom/WaterReflection`| PlanarReflectionController（水池） | 歌者舞台前一块反射水池，能照出倒影 |
| 7 | `Assets/Materials/HologramMat.mat` | `Custom/Hologram`       | HologramController（面板）    | 神谕之境(量子月)石碑区上空的悬浮发光面板 |

> 现在项目里那个旧的 `assets/shaders/shaders` 包**只含前 3 个**（GodRay/Rain/Ripple）。
> 这次要重打一个含**全部 7 个**的新包。代码对缺失材质有保护：没打进去的就 null、对应效果自动跳过，不影响其它效果，也不报错。

---

## 二、准备：打开 Unity 工程

1. 装 **Unity Hub**（官网下）。
2. Unity Hub ▸ Installs ▸ Add ▸ 选 **2019.4.39f1**（必须是这个版本，OW 就是它；版本不对打出来的包进游戏会报版本错）。
   - 装的时候勾上 **Windows Build Support (Mono/IL2CPP)**（打 Win64 包要用）。
3. Unity Hub ▸ Projects ▸ Add ▸ 选本仓库里的 **`outer-wilds-unity-template`** 文件夹 ▸ 用 2019.4.39f1 打开。
4. 第一次打开会 import 很久（几分钟到十几分钟），耐心等右下角进度条走完。

打开后认识 4 个面板（后面一直用）：
- **Project**（左下）：工程里的所有文件。右键能 `Create`。
- **Inspector**（右侧）：选中任何东西，这里显示它的属性。
- **Hierarchy**（左上）：当前场景里的物体（本任务基本不用）。
- **Console**（菜单 `Window ▸ General ▸ Console`）：看报错。**shader 报红会在这里和 Inspector 里显示。**

---

## 三、把 7 个 shader 放进 Unity 工程

1. 在 **Project** 面板里找到 `Assets` 文件夹，右键 ▸ `Create ▸ Folder`，建一个 **`Shaders`**（如果已有就用现成的）。
2. 用 Windows 资源管理器，把项目里这 7 个文件：

   ```
   TheSingerOfTheEnd/TheSingerOfTheEnd/assets/shaders/
       GodRay.shader  VolumetricRain.shader  RainRipple.shader
       AudioWave.shader  VolumetricFog.shader  WaterReflection.shader  Hologram.shader
   ```

   直接拖到 Unity 的 `Assets/Shaders` 文件夹里（拖进 Project 面板那块区域即可）。
3. 回到 Unity，等它自动编译（左下角转圈）。
   - **这一步会暴露语法错误**：去 `Console` 看有没有红字。本项目的 7 个 shader 都是验证过能编译的；如果报错，多半是拖文件时漏了或重名。
   - 选中某个 `.shader`，Inspector 里若显示 "Compiled / 没有报错" 即 OK。

> 小贴士：如果你只想最省事，也可以**只拖 `Hologram.shader` 一个进来**（因为旧包已含前 3 个的，但前 3 个的材质你这次也要重建以便打进同一个新包）。稳妥起见，**7 个都拖进来**最不容易乱。

---

## 四、建 7 个材质（核心步骤）

1. 在 `Assets` 下右键 ▸ `Create ▸ Folder`，建 **`Materials`**（名字就叫 Materials）。
2. 进入 `Assets/Materials`，右键 ▸ `Create ▸ Material`。新建出来的材质会让你立刻改名 —— 改成 **`GodRayMat`**（不要带 `.mat`，Unity 自动加）。
3. 选中刚建的 `GodRayMat`，看 **Inspector 最上方**有个 **Shader** 下拉框（默认是 `Standard`）。点开它 ▸ 找到 **`Custom`** 分组 ▸ 选 **`GodRays`**。
4. 重复以上，按**第一节的表**把 7 个材质全部建好、改名、选对 shader：

   ```
   GodRayMat    → Custom/GodRays
   RainMat      → Custom/VolumetricRain
   RippleMat    → Custom/RainRipple
   AudioWaveMat → Custom/AudioWave
   FogMat       → Custom/VolumetricFog
   WaterMat     → Custom/WaterReflection
   HologramMat  → Custom/Hologram
   ```

   > ❗名字大小写要完全一致：`GodRayMat`、`AudioWaveMat`、`HologramMat`……，且都放在 `Assets/Materials/` 里。
   > 这是 C# 写死的路径（`Assets/Materials/GodRayMat.mat` 等），错一个字母那个效果就 null。

### 4.1 各材质的参数与贴图（选中材质后在 Inspector 调）

大部分参数**保持默认也能跑**，下面是建议值和"是否需要贴图"：

- **GodRayMat (`Custom/GodRays`)**：纯后处理，**不用贴图**。强度等参数 C# 运行时会接管（`Intensity`），默认即可。
- **RainMat (`Custom/VolumetricRain`)**：建议给 `_MainTex` 放一张**雨滴贴图**（一条上下渐隐的白色竖条，或一个软白圆点 PNG）。没有也能跑（默认白）。`_Color` 的 alpha 要 > 0。
- **RippleMat (`Custom/RainRipple`)**：地面水洼，**不用贴图**，靠程序化涟漪 + Fresnel。默认即可。
- **AudioWaveMat (`Custom/AudioWave`)**：**不用贴图**。C# 每帧 `SetFloatArray("_Spectrum")` 喂频谱。`_Brightness`/`_Displacement` 可调大让起伏更明显。
- **FogMat (`Custom/VolumetricFog`)**：**不用贴图**。`_FogDensity` 是基准雾浓度（C# 会读它当 base，再乘 DensityScale）；默认约 0.012，想雾更浓可调到 0.02。
- **WaterMat (`Custom/WaterReflection`)**：**不用贴图**（反射图 `_ReflectionTex` 由 C# 运行时塞反射相机的渲染结果）。可给法线扰动参数调水面波纹。
- **HologramMat (`Custom/Hologram`)**：`_MainTex` **可选**（留空=纯发光面板；放一张要展示的图/字符/歌者剪影就会显示在面板上）。
  推荐参数：`_HoloColor` 青蓝、`_ScanStrength` 0.5、`_GlitchStrength` 0.03~0.05、`_Alpha` 0.6~0.7。

> 如果你给 `RainMat`/`HologramMat` 用了**自己的贴图 PNG**，记得那张贴图也要拖进 Assets，并在第五节里**一起标上 `shaders` 包名**，否则进游戏贴图丢失。

---

## 五、给 7 个材质指定同一个 AssetBundle 名

这一步决定它们被打进**同一个包**，C# 只加载一个文件。

1. 在 `Assets/Materials` 里**框选全部 7 个材质**（按住 Ctrl 一个个点，或框选）。
2. 看 **Inspector 最底部**，有一行 **`AssetBundle`**，左边一个下拉（默认 `None`），右边一个下拉（variant，不用管）。
3. 点左边下拉 ▸ `New...` ▸ 输入 **`shaders`**（全小写）▸ 回车。
4. 确认 7 个材质底部都显示 `AssetBundle: shaders`。（如果有自定义贴图，也选中它，同样标 `shaders`。）

> 截图位置示意（Inspector 最底部）：`AssetBundle: [ shaders ▾ ]   [ None ▾ ]`

---

## 六、防止 shader 被"剥离"变粉（关键坑）

Unity 打包时会"剥离"它认为没用到的 shader 变体，导致进游戏一片**粉红/品红**。两道保险：

1. 菜单 `Edit ▸ Project Settings ▸ Graphics` ▸ 拉到最下 **`Always Included Shaders`** ▸ 把 **Size 加几个**，把我们的 7 个 `Custom/...` shader 逐个拖/选进去。
   （选 shader 时在弹窗里搜 `Custom/GodRays`、`Custom/Hologram` 等。）
2. 保证**打包就在这个装了这些 shader 的工程里做**（就是 outer-wilds-unity-template），别跨工程。

> 这一步对**后处理类**（GodRay/Fog）尤其重要——它们没有可见网格，最容易被误剥离。

---

## 七、写一个打包脚本并构建

Unity 没有现成的"打包按钮"，要加一个菜单项。

1. 在 `Assets` 下建文件夹 **`Editor`**（名字必须叫 Editor）。
2. 在 `Assets/Editor` 右键 ▸ `Create ▸ C# Script` ▸ 命名 **`BuildBundles`**。双击打开，把内容整体替换为：

   ```csharp
   using UnityEditor;
   using System.IO;

   public static class BuildBundles
   {
       [MenuItem("Singer/Build AssetBundles")]
       public static void Build()
       {
           string outDir = "AssetBundles";              // 产出到工程根目录下的 AssetBundles/
           Directory.CreateDirectory(outDir);
           BuildPipeline.BuildAssetBundles(
               outDir,
               BuildAssetBundleOptions.None,
               BuildTarget.StandaloneWindows64);        // OW 是 Win64，必须选这个
           UnityEngine.Debug.Log("AssetBundles built to: " + Path.GetFullPath(outDir));
       }
   }
   ```

3. 回 Unity 等编译完，顶部菜单栏会多出一个 **`Singer`** ▸ 点 **`Build AssetBundles`**。
4. 等右下角进度条走完。在工程根目录（`outer-wilds-unity-template/AssetBundles/`）会出现：

   ```
   AssetBundles/
       shaders            ← 我们要的包（无扩展名）★
       shaders.manifest   ← 编辑器元数据（运行时不需要，但拷过去也无妨）
       AssetBundles       ← 主清单（不需要）
       AssetBundles.manifest
   ```

> 找不到 `Singer` 菜单？说明脚本没编译过——回 Console 看红字，常见是把脚本放错（必须在 `Assets/Editor/` 里）。

---

## 八、把新包拷回项目，覆盖旧包

把第七步产出的 **`shaders`** 文件（还有 `shaders.manifest`）复制到：

```
TheSingerOfTheEnd/TheSingerOfTheEnd/assets/shaders/shaders          ← 覆盖现有同名文件
TheSingerOfTheEnd/TheSingerOfTheEnd/assets/shaders/shaders.manifest ← 一并覆盖
```

> 现项目里这个 `shaders` 旧包只有 3 个材质，**直接覆盖**即可。
> 项目 csproj 里的 `<None Include="assets\**\*.*">` 会把 `assets/` 下所有文件（含无扩展名的包）复制进部署目录，**已实测**无扩展名文件能正确部署，无需改 csproj。

---

## 九、编译部署并进游戏验证

1. 在 `TheSingerOfTheEnd/TheSingerOfTheEnd/` 下执行：

   ```bash
   dotnet build
   ```

   （本项目 `csproj.user` 把输出路径设成了 OWML 的 Mods 目录，`dotnet build` 会**自动部署**。）
2. 用 **Outer Wilds Mod Manager** 启动游戏，确认本 MOD 已启用。
3. 进游戏后开 OWML 控制台 / 看日志，应出现：

   ```
   [世末歌者] shaders bundle 加载成功。
   [世末歌者] 材质加载: GodRay=True, Rain=True, Ripple=True, AudioWave=True, Fog=True, Water=True, Hologram=True
   ```

   - 哪个是 `False`，就是那个材质没打进包——回第四/五节检查它的**文件名/路径/包名**。
4. 按 **F7** 打开 Unity Explorer（若装了）实地看物体；按上表去对应地点验收每个效果：
   - 面朝太阳 → 看 **God Ray**；
   - 走到歌者舞台 → 看 **雨 / 水洼 / 声波环 / 反射水池**；
   - 飞去神谕之境(量子月)石碑区 → 看 **全息面板**。

---

## 十、排错速查表

| 现象 | 原因 | 解决 |
|------|------|------|
| 物体一片**粉红** | shader 没打进包 / 被剥离 / 管线不对 | 确认材质标了 `shaders` 包名；把 shader 加进 `Always Included Shaders`（第六节）；确认是内置管线写法（本项目都是） |
| 日志里某材质 `=False` | 路径/文件名/包名不一致 | 材质必须在 `Assets/Materials/`、名字与第一节表**完全一致**、标了 `shaders` 包 |
| `加载 shaders bundle 失败` | 包没拷对位置 / 没 build | 确认拷到了 `assets/shaders/shaders`；`dotnet build` 过一次 |
| 包**版本错** | Unity 版本/平台不对 | **必须**用 2019.4.39f1 + 平台选 `StandaloneWindows64` 重打 |
| God Ray / Fog **没效果** | 后处理类最易被剥离 | 务必加进 `Always Included Shaders`；面朝太阳看 God Ray |
| 雨**看不见** | 透明/朝向 | `RainMat` 的 `_Color` alpha > 0；粒子 Render Mode 已由 C# 设为 Billboard，无需改 |
| 全息面板**位置不对/陷地里** | 神谕之境地表半径与预设不符 | 改 `HologramController.cs` 的 `PanelLocal`（默认 `{6,74,13}`），进游戏用 P 键读坐标微调 |
| 声波环/水池/水洼**飘在空中或陷地** | 舞台地表高度需微调 | 它们已对齐到迁移后的歌者坐标；进游戏用 P 键读地表坐标，回去改各控制器里的 `StageLocal/PoolLocal/spots` |

---

## 十一、各 shader 的图形学考核点（答辩用，一句话版）

- **GodRays**：屏幕空间径向模糊 + 3 Pass（深度提天空遮挡 → 径向模糊 → Screen 合成），后处理。
- **VolumetricRain**：粒子系统 + 顶点按速度方向拉伸 Billboard（运动模糊）+ Fresnel 折射。
- **RainRipple**：多层正弦波叠加的程序化法线扰动，随时间扩散；Fresnel + Blinn-Phong。
- **AudioWave**：`AudioListener.GetSpectrumData` 实时 FFT → 顶点位移；频率→HSV 色相，幅值→亮度。
- **VolumetricFog**：Ray Marching + Beer-Lambert（`T=exp(-∫σdt)`）+ 由深度重建世界位置。
- **WaterReflection**：平面反射（反射矩阵 + 斜裁剪近平面的镜像相机渲染到 RenderTexture）。
- **Hologram**：世界 Y 扫描线 + Fresnel 边缘辉光 + 顶点级 Glitch 错位 + 半透明叠加。

---

## 十二、做完这次之后（与 TA 建议衔接）

TA（`logs/5.28_TA.md`）指出**投入产出比最高**的下一步不是这 7 个屏幕特效，而是**角色卡通渲染**：现在歌者 MMD 模型还是 Standard PBR，发灰发塑料。建议用 **UnityChanToonShader 2 (UTS2)** 替换（专为内置管线写、官方推荐 2019.4，与 OW 完美吻合）。那是另一条流水线（也要打进同一个 bundle），见 `logs/5.28.md` 的"下一步"。本指南先把这 7 个 shader 收口。
