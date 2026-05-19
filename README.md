# Outer Wilds 世末歌者同人剧情 MOD 开发

> Author：wingtings
>
> 2026 USTC 计算机图形学期末大作业

## 世末歌者原作背景

### 核心设定

《世末歌者》由 COP 创作，是中文 Vocaloid 神话曲。故事围绕三个角色展开：

| 角色 | 对应声库 | 身份 | MOD中映射 |
|------|---------|------|-----------|
| 神明 | 言和 | 世界的缔造者与毁灭者，发起赌约 | 类似 Outer Wilds 中的 Nomai 遗迹 / 超自然法则 |
| 歌者 | 乐正绫 | 流浪歌手，在末日世界轮回流浪 | 玩家操控的主角 |
| 凡人 | 洛天依 | 普通女孩，每次驻足片刻却总是离去 | 玩家需要追寻的关键NPC |

### 赌约规则

神明与歌者的赌约条件：
- 歌者**不允许主动与凡人交流**（只能歌唱）
- 若有人愿意与歌者一起面对末日的死亡 → 世界得救，赌约结束
- 否则 → 孤独的轮回和死亡永不停止

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

IDE: VS2022/Vscode/etc

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
3. **安装 OWML Mod Template**：
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

使用 VS2022 从 `ow-mod-template` 建立之后（带 New Horizons 支持），项目文件树如下：

```
TheSingerOfTheEnd/
├── .github/
│   └── workflows/
│       └── release.yml              # 自动发布 workflow
├── planets/                          # 星球配置（JSON）
│   ├── singer_world.json            # 歌者流浪的末日星球
│   ├── god_realm.json               # 神明领域（言和的空间）
│   └── mortal_city.json             # 凡人城市（洛天依所在地）
├── systems/                          # 星系配置
│   └── shimo_system.json            # 世末星系定义
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
  "filename": "TheSingerOfTheEnd.dll",
  "author": "wingtings",
  "name": "The Singer of the End - 世末歌者",
  "uniqueName": "wingtings.TheSingerOfTheEnd",
  "version": "0.1.0",
  "owmlVersion": "2.14.2",
  "dependencies": ["xen.NewHorizons"]
}
```

#### 星球配置示例 (planets/singer_world.json)

```json
{
  "name": "世末之城",
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

### 剧情大纲：末日轮回中的歌声

#### 世界观

在 Outer Wilds 星系的边缘，存在一个被遗忘的小型星系——**鸥停星系**。这里的恒星正在缓慢死亡，每 22 分钟便会引发一次"末日之雨"——致命的能量风暴以暴雨的形式摧毁一切生命。

一位流浪歌手（阿绫）被困在这个星系的时间循环中。她与神明的赌约使她无法主动与任何人交流——她只能唱歌。如果有人愿意在末日降临时留在她身边，循环就会终结。

#### 三幕结构

**第一幕：困惑与探索（循环 1-3）**

玩家以"旅行者"身份降落在鸥停星系，发现：
- 一座被遗弃的城市正在经历永恒的阴雨
- 城市废墟中散落着类似 Nomai 文字的壁画，记录着赌约的片段
- 远处传来若有若无的歌声（歌者阿绫的位置线索）
- 一个蓝发女孩（凡人·天依）在城市中游荡，看起来很悲伤

**关键探索点：**
| 地点 | 可发现的信息 | 图形学效果 |
|------|------------|-----------|
| 废弃音乐厅 | 赌约的第一条规则："不可主动言说" | 声波可视化粒子效果 |
| 雨中广场 | 歌者日记碎片，记录第1次循环 | 体积雨 + 涟漪着色器 |
| 高塔观测台 | 末日倒计时装置，可观测恒星异变 | 体积光/God Ray |
| 神明神殿遗址 | 赌约的完整条款石碑 | 全息投影着色器 |

**第二幕：理解与尝试（循环 4-6）**

玩家逐渐理解故事的全貌：
- 找到散落各处的歌者日记（对应不同循环次数的心境变化）
- 观察到凡人（天依）每次循环都有固定的行动路线
- 发现歌者（阿绫）总在特定的地点唱歌
- 尝试在末日来临前让两人相遇——但总是失败

**核心谜题设计：**
1. **声音传导谜题**：利用城市建筑结构将歌声引向凡人（类似 OW 中的信号定位）
2. **时间规划谜题**：在 22 分钟内完成特定事件序列，改变凡人的行动路线
3. **环境互动谜题**：清除阻碍路径的障碍物，让凡人能走到歌者面前

**第三幕：觉悟与抉择（循环 7+）**

玩家发现真相：
- 赌约的规则限制歌者不能主动交流，但**没有限制第三者（玩家）**
- 玩家不能直接告诉凡人去找歌者（这会违反赌约的精神）
- 解法是：玩家通过改变环境，让凡人**自发地**决定留下

**最终解法（True Ending）：**
玩家需要在最后一个循环中：
1. 修复废弃音乐厅的扩音装置（让歌声传遍全城）
2. 移除凡人日常路径上的"逃避诱因"（她总是在末日前逃向飞船）
3. 在正确的时间点触发城市灯光（让雨中的歌者被光柱照亮）
4. 最终，凡人在暴雨中听到歌声，看到光中的歌者，选择走向她而非逃离

末日的雨停了。循环终结。

#### 多结局设计

| 结局 | 触发条件 | 描述 |
|------|---------|------|
| True End: 十指相扣 | 完成所有环境改造，凡人主动留下 | 雨停，阳光穿透乌云，两人十指相扣。世界得救 |
| Normal End: 无尽轮回 | 22分钟耗尽，未满足条件 | 末日照常降临，一切重置 |
| Bad End: 歌者放弃 | 触发特定的绝望日记后不行动 | 歌者停止歌唱，世界彻底沉寂 |
| Secret End: 旅行者之歌 | 找到隐藏乐器，与歌者合奏 | 玩家也成为歌者的一部分，二重唱打破赌约 |

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
- 神明（言和）出场时的金色光柱
- True Ending 中雨停后阳光穿透云层照亮歌者
- 歌者被音乐厅灯光照亮的关键剧情时刻

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

### 效果优先级与开发排期

| 优先级 | 效果 | 预计工时 | 图形学考核点 |
|--------|------|---------|-------------|
| P0 | 体积雨 + 涟漪 | 2-3天 | 粒子系统、着色器编程 |
| P0 | 体积雾 | 3-4天 | Ray Marching、光照模型 |
| P1 | God Rays | 2天 | 后处理、屏幕空间效果 |
| P1 | 声波可视化 | 3天 | FFT、程序化动画 |
| P2 | 水面反射 | 2天 | 反射/折射、Render Texture |
| P2 | 全息投影 | 1天 | 程序化纹理、混合模式 |

---

## 尝试制作的内容

- 将哈斯人替换为世末歌者背景故事人物（世末绫已有，言和和洛用公式服）

    （使用模之屋 MMD 资产 https://www.aplaybox.com/details/model/hHfwCU9wl51r）

- 尝试新建星球场景

- 设计剧情，契合原作 22 分钟一个循环（在神明的赌约之下，如何打破世末滂沱大雨的循环？）

---

## 开发路线图

### Phase 1：环境搭建与基础验证（第1周）

- [ ] 安装 OWML + Mod Manager，跑通 Hello World MOD
- [ ] 使用 New Horizons 创建第一个自定义星球（能加载进游戏）
- [ ] 配置 `hasRain: true` 验证基础雨效果
- [ ] 导入一个 MMD 模型（FBX 格式）到游戏中显示

### Phase 2：核心图形学效果（第2-3周）

- [ ] 实现自定义体积雨着色器（替代默认雨效果）
- [ ] 实现体积雾 Ray Marching 着色器
- [ ] 实现基础 God Ray 后处理效果
- [ ] 编写声波可视化原型

### Phase 3：剧情与关卡搭建（第4周）

- [ ] 搭建世末之城的场景布局（建筑、街道、音乐厅）
- [ ] 实现凡人 NPC 的 AI 行为路径
- [ ] 配置对话树和可探索文本（Nomai 壁画风格）
- [ ] 实现 22 分钟循环内的事件时间线

### Phase 4：整合与打磨（第5周）

- [ ] 串联所有谜题和剧情触发器
- [ ] 实现多结局分支判定
- [ ] 音频集成（BGM + 环境音 + 歌声）
- [ ] 性能优化与最终测试

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
