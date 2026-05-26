# Outer Wilds 世末歌者同人剧情 MOD 开发

> Author：wingtings
>
> 2026 USTC 计算机图形学期末大作业

## 世末歌者原作背景

### 核心设定（据 COP 官方剧情梗概）

《世末歌者》由 COP 创作，2016年8月25日投稿，乐正绫演唱，是中文 Vocaloid 神话曲（1000万+播放）。

> 言和希望流浪歌手阿绫和她打赌，在不允许阿绫主动和他人交流的前提下，只要在任何一个她创造的不久于末日的世界里，有谁愿意和阿绫一起面对死亡，言和就会实现她的愿望，并让那个世界持续下去，否则孤独的轮回和死亡永远不会停止。 ——COP

| 角色 | 对应声库 | 身份 | MOD中映射 |
| ---- | -------- | ---- | --------- |
| 神明 | 言和 | 世界的缔造者与毁灭者，发起赌约 | 石碑/录音石（不出场） |
| 歌者 | 乐正绫 | 流浪歌手，渴望成名→渴望被靠近 | 站在音乐厅舞台上的NPC |
| 凡人 | 洛天依 | 碌碌无为的女孩，沉浸在自己的悲伤中 | 站在广场上的可对话NPC |

### 赌约规则

- 歌者**不可主动与凡人交流**——唯一被允许的事是歌唱
- 若有人**自愿**与歌者共同面对死亡 → 世界得赎，轮回终止
- 否则 → 孤独的轮回和死亡永远不会停止
- **赌约只约束歌者一人**——第三者（玩家）不受限，但不可代替凡人做出选择

### 与 Outer Wilds 的天然契合点

- **22分钟循环** ↔ 末日轮回（每次循环末世界毁灭）
- **无法直接交流** ↔ 歌者不能主动说话的赌约限制
- **探索驱动叙事** ↔ 玩家通过探索理解歌者的故事
- **超新星爆炸** ↔ 滂沱大雨中的世界终结
- **知识是唯一进度** ↔ 每次循环积累的是对故事的理解

---

## 技术准备

### 一些链接指路

引擎：Unity 3D  下载 OuterWilds MOD Manager 以加载进游戏内

IDE: VS2026/Vscode/etc

OuterWilds MOD官方网站：https://outerwildsmods.com/

Github 游戏组织：https://github.com/ow-mods

游戏资产获取：https://github.com/ow-mods/outer-wilds-unity-wiki/wiki （需要加入 Discord 找管理员要资源）

新建 MOD 流程模版：

- https://owml.outerwildsmods.com/
- https://github.com/ow-mods/ow-mod-template

![](/figs/modder.png)

新建地图需要阅读：

- https://nh.outerwildsmods.com/
- https://github.com/ow-mods/outer-wilds-unity-wiki/wiki

![](/figs/newhorizons.png)

### 项目结构与配置

#### 环境搭建步骤

1. **安装 .NET SDK**（6.0 或更高版本）
2. **安装 Outer Wilds Mod Manager**（从 https://outerwildsmods.com/ 下载）
3. **安装 OWML Mod Template**：（教程位置：https://owml.outerwildsmods.com/guides/getting-started/）

   ```bash
   dotnet new --install Bwc9876.OuterWildsModTemplate
   ```
4. **创建项目**：

   ```bash
   dotnet new sln --name TheSingerOfTheEnd
   dotnet new OuterWildsMod -n TheSingerOfTheEnd --AuthorName wingtings --usesNH true
   dotnet sln add TheSingerOfTheEnd/
   ```

#### 项目文件树

使用 VS2026 从 `ow-mod-template` 建立之后（带 New Horizons 支持），项目文件树如下：

```
TheSingerOfTheEnd/
├── .github/
│   └── workflows/
│       └── release.yml              # 自动发布 workflow
├── planets/                          # 星球配置（JSON）
│   ├── outing_sun.json              # 鸥停之星（恒星）
│   ├── singer_world.json            # 世末之城（主场景，歌者+凡人都在此）
│   └── god_realm.json               # 神谕之境（赌约石碑）
├── systems/                          # 星系配置
│   └── outing_system.json            # 鸥停星系定义
├── assets/                           # 自定义资产
│   ├── models/                       # MMD → FBX 转换后的角色模型
│   │   ├── ling_singer.fbx          # 乐正绫（歌者）
│   │   ├── tianyi_mortal.fbx        # 洛天依（凡人）
│   │   └── yanhe_god.fbx            # 言和（神明）
│   ├── textures/                     # 贴图资源
│   ├── audio/                        # BGM 和音效
│   │   ├── shimo_singer_bgm.ogg    # 世末歌者主旋律
│   │   └── rain_ambient.ogg        # 雨声环境音
│   └── shaders/                      # 自定义着色器
│       ├── RainDropShader.shader    # 雨滴粒子着色器
│       ├── VolumetricFog.shader     # 体积雾
│       └── GodRay.shader            # 神明降临光束
├── dialogue/                         # 对话树配置
│   └── translator_text.xml          # Nomai文字翻译文本
├── TheSingerOfTheEnd.cs             # MOD 主入口
├── TheSingerOfTheEnd.csproj         # C# 项目文件
├── manifest.json                     # MOD 元数据
└── default-config.json              # 用户配置项
```

#### manifest.json 配置

```json
{
  "$schema": "https://raw.githubusercontent.com/ow-mods/owml/master/schemas/manifest_schema.json",
  "filename": "TheSingerOfTheEnd.dll",
  "author": "wingtings",
  "name": "The Singer Of The End",
  "uniqueName": "wingtings.TheSingerOfTheEnd",
  "version": "0.1.0",
  "owmlVersion": "2.15.1",
  "dependencies": ["xen.NewHorizons"]
}

```

#### 星球配置示例 (planets/singer_world.json)

```json
{
  "name": "鸥停",
  "$schema": "https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json",
  "starSystem": "shimo",
  "Base": {
    "groundSize": 300,
    "surfaceSize": 301,
    "surfaceGravity": 12
  },
  "Orbit": {
    "semiMajorAxis": 2800,
    "primaryBody": "shimo_sun",
    "isTidallyLocked": false,
    "siderealPeriod": 400
  },
  "Atmosphere": {
    "size": 500,
    "fogTint": { "r": 80, "g": 90, "b": 100, "a": 255 },
    "fogSize": 450,
    "fogDensity": 0.6,
    "hasRain": true,
    "cloudTint": { "r": 60, "g": 65, "b": 75, "a": 200 }
  },
  "Props": {
    "reveal": [
      {
        "revealOn": "SINGER",
        "position": { "x": 0, "y": 50, "z": 0 }
      }
    ]
  }
}
```

---

## 剧情设计

> 完整剧本见 `logs/design.md`

### 一句话概括

> 玩家在 22 分钟的循环中探访废岩星（故事发生于此）与量子月（赌约起源地），拼凑歌者与凡人的故事，修复扩音装置让歌声传遍废墟，使凡人终于走向歌者。

### 场景（借用原版两颗星球，通过 NH 注入故事内容）

| 原版星球 | 故事地点 | 内容 | 图形学效果 |
| -------- | -------- | ---- | ---------- |
| 废岩星（Brittle Hollow）| 北极遗迹广场 | 歌者日记石碑 + 天依NPC + 篝火废墟 | 体积雨 + 涟漪着色器 |
| 废岩星（Brittle Hollow）| 废弃音乐厅遗迹 | 歌者NPC + 损坏的扩音装置 | God Ray（面向原版太阳）|
| 废岩星（Brittle Hollow）| 北极边缘高塔 | 挪麦重力炮地标 + 高塔观测台 | — |
| 量子月（Quantum Moon）| 赌约碑区 | 赌约三条规则 + 第三者条款 + 录音石 | — |

> **设计转折**（见 `logs/5.26_migration.md`）：原版废岩星字面上是"正在坍塌进黑洞的古文明遗迹"，与《世末歌者》"困于末日轮回的废城"高度契合；量子月的量子跳跃特性与"神明之境"的不确定性叙事一致，且追踪量子月本身就是原版游戏的核心谜题之一。

### 核心谜题

修复扩音装置：找到零件 A（广场）+ 零件 B（高塔）→ 插入音乐厅的扩音装置 → 歌声传遍城市 → 天依听到歌声 → True End

### 结局

| 结局 | 触发条件 | 描述 |
| ---- | -------- | ---- |
| True End: 十指相扣 | 扩音装置修复，天依走向歌者 | 雨停，阳光穿透乌云，两人十指相扣，循环终结 |
| Normal End: 无尽轮回 | 22分钟耗尽，未修复扩音装置 | 末日照常降临，一切重置 |

---

## 图形学效果设计

> 以下为本项目需要实现的计算机图形学技术点，作为期末大作业的核心内容。

### 1. 体积雨 (Volumetric Rain)

**技术要点：** 粒子系统 + 自定义着色器

```
技术栈：Unity Particle System + Custom Shader (HLSL)
难度：★★★☆☆
```

**实现方案：**

- 使用 GPU Instancing 渲染大量雨滴粒子（目标：10000+ 粒子）
- 雨滴着色器实现运动模糊拖尾（基于速度方向拉伸 Billboard 四边形）
- 雨滴与地面碰撞时生成涟漪效果（法线贴图动画）
- 末日阶段雨量/颜色渐变（从灰色细雨 → 红色暴雨，暗示能量风暴）

**着色器核心算法：**

- 顶点着色器：根据速度向量拉伸顶点位置，模拟运动模糊
- 片段着色器：使用 Fresnel 效果模拟雨滴折射，Alpha 沿运动方向衰减
- 涟漪法线：多层正弦波叠加的法线扰动，随时间扩散衰减

### 2. 体积雾 / 大气散射 (Volumetric Fog & Atmospheric Scattering)

**技术要点：** Ray Marching + Beer-Lambert 定律

```
技术栈：Compute Shader / Fragment Shader (Ray Marching)
难度：★★★★☆
```

**实现方案：**

- 使用 Ray Marching 遍历相机射线路径上的雾密度
- 结合 Beer-Lambert 定律计算光线衰减：`T = exp(-∫σ(t)dt)`
- 采样 Shadow Map 实现雾中光影（被建筑遮挡区域的雾更暗）
- 雾的密度用 3D Perlin Noise 生成，营造流动感
- 末日临近时雾密度和颜色动态变化（增强压迫感）

**图形学原理：**

- 参与介质渲染 (Participating Media Rendering)
- Henyey-Greenstein 相函数控制前向/后向散射比
- 时间切片 (Temporal Reprojection) 减少噪点

### 3. 神光 / 丁达尔效应 (God Rays / Crepuscular Rays)

**技术要点：** 屏幕空间径向模糊 / 体积光

```
技术栈：Post-Processing Shader + Light Shaft
难度：★★★☆☆
```

**实现方案：**

- 渲染光源颜色到纹理，场景几何体作为遮挡物绘制为黑色
- 从光源屏幕位置向每个像素方向进行径向模糊采样
- 多 Pass 叠加实现高质量光束效果
- 用于：神明降临时的光柱、最终结局阳光穿云

**应用场景：**

- True Ending 中雨停后阳光穿透云层照亮歌者
- 歌者在音乐厅被灯光照亮的演出时刻

### 4. 声波可视化 (Audio Visualization Shader)

**技术要点：** FFT 频谱分析 + 粒子/网格变形

```
技术栈：AudioSource FFT + Compute Shader / VFX Graph
难度：★★★★☆
```

**实现方案：**

- 实时 FFT 分析歌声音频的频谱数据
- 将频谱数据传入着色器，驱动粒子运动/网格顶点偏移
- 歌声强弱影响可视化效果的振幅和颜色
- 低频 → 大范围缓慢波动；高频 → 细密快速振动
- 以歌者为中心向外扩散的声波涟漪

**图形学原理：**

- 顶点着色器中根据频谱数据进行顶点位移
- Signed Distance Field (SDF) 渲染声波前沿
- 颜色映射：频率 → HSV 色相，振幅 → 亮度

### 5. 水面反射与折射 (Planar Reflection + Refraction)

**技术要点：** 平面反射 + 屏幕空间折射

```
技术栈：Render Texture + Distortion Shader
难度：★★★☆☆
```

**实现方案：**

- 地面积水使用平面反射（翻转相机渲染反射图）
- 法线贴图扰动采样 UV 实现水面波纹折射效果
- 雨滴落入积水时叠加动态法线扰动
- 反射图中可以看到歌者的倒影（叙事暗示孤独主题）

### 6. 全息投影着色器 (Hologram Shader)

**技术要点：** 扫描线 + 边缘发光 + 故障效果

```
技术栈：Fragment Shader
难度：★★☆☆☆
```

**实现方案：**

- 用于神明遗迹中的信息展示（替代 Nomai 文字螺旋）
- 水平扫描线（基于世界坐标 Y 轴的正弦函数）
- Fresnel 边缘发光（视角越接近切线方向越亮）
- 随机 Glitch 偏移（UV 坐标周期性跳跃）
- 半透明叠加混合

### 效果优先级（两周排期·只做 2 个）

| 优先级 | 效果 | 预计工时 | 图形学考核点 | 排期 |
| ------ | ---- | -------- | ------------ | ---- |
| **P0** | **体积雨 + 涟漪** | 1天 | 粒子系统、着色器编程 | Day 11 |
| **P0** | **God Rays** | 1天 | 后处理、屏幕空间效果 | Day 12 |
| P1 | 声波可视化 | — | FFT、程序化动画 | 有余力再做 |
| P2 | 体积雾 | — | Ray Marching | 有余力再做 |
| P2 | 水面反射 | — | 反射/折射 | 有余力再做 |
| P2 | 全息投影 | — | 程序化纹理 | 有余力再做 |

---

## 实现的内容

- **场景**：以 NH 修改器注入原版废岩星（世末之城）与量子月（神谕之境），不需要自建地形
- **角色**：歌者/天依占位模型（索拉努姆）+ 自定义 NPC 行为（面向玩家、剧情状态联动）
- **核心谜题**：修复扩音装置让歌声传遍废墟 → True End
- **信号探测器**：追踪"歌者之声"信号频率找到音乐厅
- **Nomai 翻译器**：歌者日记石碑 + 赌约石碑（可翻译）
- **Ship Log 知识图谱**：8 个日志节点 + 缩略图 + 好奇心配色
- **自定义 Shader**：体积雨粒子（局部降雨，距故事区域 200 m 内）+ 地面涟漪水洼 + God Rays 后处理
- **时间线管理**：22 分钟内 God Ray / 雨量随时间渐变；True End 触发三段演出（雨停→光束爆发→平静）
- **结局演出**：True End（屏幕通知 + 飞船日志揭示 + 天依转向歌者）

---

## 开发路线图（两周冲刺）

### 第 1 周：让世界能跑起来

- [ ] Day 1：编译部署 → 游戏内看到两颗星球
- [ ] Day 2：调整世末之城大气参数（雨/雾/闪电）
- [ ] Day 3：用 Unity Explorer 找 Prop，布置广场/音乐厅/高塔
- [ ] Day 4：配置歌者信号源（信号探测器可追踪）
- [ ] Day 5：编写 Nomai Text XML（歌者日记 + 赌约石碑）
- [ ] Day 6：配置 Ship Log XML + revealVolumes
- [ ] Day 7：配置扩音器谜题（物品拾取 + 插槽）

### 第 2 周：让故事能讲完

- [ ] Day 8：编写天依对话 XML
- [ ] Day 9：C# 结局判定逻辑
- [ ] Day 10：音频素材准备与集成
- [ ] Day 11：实现图形学效果（体积雨 或 God Ray）
- [ ] Day 12：实现第 2 个图形学效果
- [ ] Day 13：全流程测试 + bug 修复
- [ ] Day 14：录制演示视频 + 整理文档提交

---

## 参考资料

### 世末歌者系列

- 世末歌者 MV：https://www.youtube.com/watch?v=Uz42GvdALyg
- 世末歌者系列 Wiki：https://vcpedia.cn/世末歌者系列
- 系列歌曲：《夏风》《世末积雨云》《回音》《hello&bye,days》《凉雨》《世末歌者》

### Outer Wilds MOD 开发

- OWML 文档：https://owml.outerwildsmods.com/
- New Horizons 文档：https://nh.outerwildsmods.com/
- NH Addon 模板：https://github.com/Outer-Wilds-New-Horizons/nh-addon-template
- Vesper's Shaders（着色器参考）：https://github.com/Vesper-Works/Vesper-s-Assorted-OuterWilds-Shaders
- Weather Mod（天气效果参考）：https://outerwildsmods.com/mods/weathermod

### 图形学技术参考

- Unity URP Volumetric Fog：https://www.vertexfragment.com/ramblings/urp-volumetric-fog/
- God Rays Shader Breakdown：https://cyanilux.com/tutorials/god-rays-shader-breakdown/
- Volumetric Light Scattering (Kodeco)：https://www.kodeco.com/22027819-volumetric-light-scattering-as-a-custom-renderer-feature-in-urp
