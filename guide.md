# 安装与游玩指南

> 模组名：**The Singer Of The End**（`wingtings.TheSingerOfTheEnd`）
>依赖：**New Horizons**（`xen.NewHorizons`）— 必装，否则星球与剧情不会加载。
>仓库地址: https://github.com/wingtings/OuterWildsMOD-The-Singer-of-the-End-of-the-world

---

## 0. 准备环境

| 必需项 | 说明 |
| ------ | ---- |
| **Outer Wilds 正版本体** | Steam / Epic 均可。**不需要** DLC（Echoes of the Eye）。 |
| **Outer Wilds Mod Manager** | 官方模组管理器，用来一键安装/启用模组。下载：https://outerwildsmods.com/ |
| **磁盘空间** | 模组本体约 `358 MB`（含模型与着色器 bundle）。 |

> 本模组在 **Windows + PC 版 Outer Wilds** 上开发与测试。主机版 / 云游戏无法安装模组。

---

## 方式一：拿 `.zip` 解压

### 步骤

1. **安装并打开 Outer Wilds Mod Manager**
   首次启动时它会自动检测游戏路径（Steam/Epic）。若没检测到，手动指向 `OuterWilds.exe` 所在目录。

2. **安装依赖：New Horizons**
   在 Mod Manager 的 “Get Mods” 列表里搜索 **New Horizons**，点击安装。本模组的全部星球、剧情、对话都依赖它。

3. **安装本模组 The Singer Of The End**

   - **使用本仓库 Release 产物 `wingtings.TheSingerOfTheEnd.zip`**：
     
     1. 打开 Mod Manager，点击右上角菜单 → **Show Mods Folder**（或 “Open OWML / Mods 目录”）。
     2. 该目录通常为：
        ```
        %APPDATA%\OuterWildsModManager\OWML\Mods
        ```
     3. 在其中**新建文件夹** `wingtings.TheSingerOfTheEnd`，把 zip **解压后的全部文件**（`TheSingerOfTheEnd.dll`、`manifest.json`、`planets/`、`assets/` 等）放进去。
     4. 回到 Mod Manager，按 F5 / 重新打开，列表里应出现 *The Singer Of The End*。

4. **确认两个模组都已勾选启用**
   在 Mod Manager 的 “Installed Mods” 里，确保 **New Horizons** 和 **The Singer Of The End** 前的开关都是开启状态。

5. **点击 “Start Game” 启动游戏**
   必须通过 Mod Manager 的按钮启动（它会注入 OWML 加载器）。直接从 Steam 启动不会加载模组。

6. **进入游戏**
   标题界面会变成歌者立于旋转舞台、播放《世末歌者》伴奏的定制菜单 —— 看到它就说明模组加载成功了。

   ![定制标题界面](/figs/ui.png)

---

## 方式二：从源码编译

### 环境要求

| 工具 | 版本 | 用途 |
| ---- | ---- | ---- |
| **.NET SDK** | 6.0+ | 编译 C# 模组（工程目标框架 `net48`，SDK 用于 `dotnet build`） |
| **Outer Wilds Mod Manager** | 最新 | 安装 OWML + New Horizons，并启动游戏 |
| Visual Studio 2022/2026 或 VS Code | 可选 | 编辑代码 |
| Unity 2019.4.39f1 | **仅在改模型/着色器时需要** | 重新打包 AssetBundle（普通编译用不到） |

### 编译步骤

1. **先用「方式一」装好 Mod Manager + New Horizons**（编译产物要部署到它的 Mods 目录，且运行时依赖 NH）。

2. **克隆本仓库**，进入模组工程目录：

   ```powershell
   cd "TheSingerOfTheEnd\TheSingerOfTheEnd"
   ```

3. **编译**：

   ```powershell
   dotnet build -c Release
   ```

   - 依赖（`OWML`、`OuterWildsGameLibs`）会通过 NuGet 自动还原。
   - 本工程在 `.csproj` 里配置了 **DeployToOWML** 构建后任务：编译成功后会自动把产物镜像到
     `%APPDATA%\OuterWildsModManager\OWML\Mods\wingtings.TheSingerOfTheEnd`，
     所以**编译完即已部署**，无需手动复制。

4. **通过 Mod Manager 启动游戏**，确认模组已启用即可游玩。

> 🔧 若你修改了 `assets/` 下的着色器或模型，需要在 Unity 模板工程（`outer-wilds-unity-template`）里重新打包对应的 AssetBundle 再编译。打包流程见 `logs/shader_packaging_guide.md`。

---

## 玩法速览

> 完整剧情设定见 [`README.md`](/README.md) 与 `logs/design.md`。

- 游戏沿用 Outer Wilds 的 **22 分钟时间循环**机制，对应《世末歌者》的“末日轮回”。
- 故事发生在两颗**原版星球**上（由 New Horizons 注入内容）：
  - **废岩星 / Attlerock（世末之城）**：广场上的凡人·天依、音乐厅舞台上的歌者·阿绫、损坏的扩音装置。
  - **量子卫星 / Quantum Moon（神域之境）**：神明的赌约石碑与录音石。
- **核心谜题**：找到散落的扩音装置零件 → 修复音乐厅的扩音装置 → 歌声传遍废墟 → 天依走向歌者 → **True End（十指相扣）**。
- 若 22 分钟耗尽仍未修复扩音装置，则触发 **Normal End（无尽轮回）**，世界重置。
- 用**信号探测器**追踪“歌者之声”信号可定位音乐厅；石碑可用 **Nomai 翻译器**阅读。

![True End](/figs/true_end.png)

---

## 配置项（游戏内可调）

在 Mod Manager 中选中本模组 → 齿轮 / “Settings”，可实时调整：

| 设置 | 默认 | 说明 |
| ---- | ---- | ---- |
| God Ray 神光 | 开 | 屏幕空间体积光后处理 |
| Volumetric Rain 体积雨 | 开 | 自定义雨滴粒子着色器 |
| Rain Ripple 地面涟漪 | 开 | 地面积水涟漪 |
| Audio Wave 声波可视化 | 开 | FFT 频谱驱动的网格变形 |
| Volumetric Fog 体积雾 | 开 | Ray Marching + Beer-Lambert |
| Water Reflection 水面反射 | 开 | 平面反射 / 折射 |
| Hologram 全息投影 | 开 | 程序化全息材质 |
| Fog Density / Sun Intensity / Ambient Light | — | 雾浓度 / 日照强度 / 环境光滑条 |
| Signal Detection Range | 800 | 信号探测范围（米） |
| Debug Mode | 关 | 调试模式 |
| Reset Story Progress | 关 | 勾选后下次进存档重置剧情进度 |

> 七个图形学效果都能**独立开关**，方便对比演示。性能吃紧时可先关掉体积雾 / 水面反射。

---

## 常见问题（FAQ）

**Q：进游戏后星球/剧情没出现？**
A：99% 是 **New Horizons 没装或没启用**。回到 Mod Manager 确认 NH 与本模组都已勾选，且是通过 Mod Manager 的 “Start Game” 启动的（不要直接从 Steam 启动）。

**Q：模组列表里看不到 The Singer Of The End？**
A：检查解压位置 —— 文件必须直接位于 `…\OWML\Mods\wingtings.TheSingerOfTheEnd\` 下（`manifest.json` 应在该文件夹根部，而不是再套一层子目录）。

**Q：角色模型变成粉红色 / 材质丢失？**
A：说明卡通着色器变体在打包时被剥离。请使用仓库 Release 的完整 zip，或按 `logs/shader_packaging_guide.md` 重新打包 AssetBundle。

**Q：`dotnet build` 报找不到 OWML / OuterWildsGameLibs？**
A：确保已联网让 NuGet 还原；这两个包是公开的 NuGet 依赖（见 `.csproj`）。

**Q：能在没有 DLC 的情况下玩吗？**
A：可以。本模组只用基础版资源，不依赖 Echoes of the Eye。

---

## 卸载

在 Outer Wilds Mod Manager 的 “Installed Mods” 列表中，找到 *The Singer Of The End*，点击删除（或直接删掉 `…\OWML\Mods\wingtings.TheSingerOfTheEnd\` 文件夹）即可。NH 可保留给其他模组使用。

---

> 🎵 *“在世界的尽头，总要有人愿意听完最后一首歌。”*
