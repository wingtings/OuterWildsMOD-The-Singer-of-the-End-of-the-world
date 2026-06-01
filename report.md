# 《世末歌者》—— 基于 Outer Wilds 的自定义着色器剧情 MOD

**作者：** wingtings（学号：__________）

**课程：** 中国科学技术大学 · 计算机图形学

**日期：** 2026 年 6 月

---

## 摘要

本项目以游戏 *Outer Wilds*（《星际拓荒》）为载体，借助 OWML + New Horizons 模组框架，复刻了中文 Vocaloid 神话曲《世末歌者》的剧情，并在其中实现了一整套**自定义图形学着色器系统**。项目共编写了 **7 个自定义着色器**（GLSL/HLSL，运行于 Unity 内置渲染管线），覆盖**屏幕空间后处理**（God Rays 神光、体积雾）、**粒子与程序化材质**（体积雨、地面涟漪）、**程序化网格变形**（FFT 声波可视化）、**平面反射/折射**（水面）以及**程序化纹理**（全息投影）等多类图形学技术。所有效果均可通过游戏内配置面板独立开关，并由一套时间线管理器在剧情高潮（True End）时联动演出。实验表明，本系统在 Outer Wilds 的球面星球环境下稳定运行，且各效果与"末日轮回"的叙事氛围高度契合。

> 项目仓库：`OuterWildsMOD - The Singer of the End of the world`
>
> 演示视频见：______________（预留）

---

## 一、项目背景与选题

### 1.1 原作与叙事映射

《世末歌者》由 COP 创作（2016），讲述神明言和与流浪歌手阿绫的赌约：在不允许阿绫主动与他人交流的前提下，只要在末日世界里有人愿意与她共同面对死亡，世界便得以延续，否则孤独的轮回与死亡永不停止。本项目将该故事映射到 Outer Wilds 的世界观：

| 原作角色 | 声库 | MOD 中映射 |
| -------- | ---- | --------- |
| 神明 | 言和 | 量子月上的赌约石碑/录音石（不出场） |
| 歌者 | 乐正绫 | 站在音乐厅舞台上的 NPC（不能主动说话） |
| 凡人 | 洛天依 | 站在广场上的可对话 NPC |

Outer Wilds 的核心机制与原作天然契合：**22 分钟时间循环** ↔ 末日轮回；**无法直接交流的赌约** ↔ 探索驱动叙事；**超新星爆发** ↔ 滂沱大雨中的世界终结。

### 1.2 为何以"自定义着色器"为核心交付物

本课程考核重点为**计算机图形学技术**而非剧情或模组逻辑。因此本项目把可游玩的剧情作为"载体"，把自行编写的着色器作为真正的交付内容。需要特别说明的是：New Horizons 自带的 `hasRain:true` 等内置天气**不计入**自己的图形学工作量，所以本项目的雨、雾、神光等效果全部由自编着色器与控制器实现。

---

## 二、整体技术架构

### 2.1 运行环境与渲染管线

- **引擎**：Unity 2019.4.39f1（Outer Wilds 使用**内置渲染管线 Built-in RP**，而非 URP/HDRP）。
- **模组框架**：OWML（模组加载器）+ New Horizons（NH，星球/剧情注入）+ Harmony（运行时打补丁）。
- **语言**：C#（模组逻辑与效果控制器）+ ShaderLab/CG-HLSL（着色器）。

由于是内置管线，屏幕空间后处理（神光、体积雾）必须采用挂在主相机上的 `OnRenderImage` + `Graphics.Blit` 方案，而非 URP 的 `RendererFeature`。这一选择贯穿全项目，是若干技术细节（如深度图获取、Pass 调度）的根本原因。

### 2.2 着色器资产管线（AssetBundle）

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

### 2.3 模组加载与效果挂载流程（技术路径）

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

### 2.4 项目文件树

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
    │   ├── models/singer                   # 歌者 MMD 模型 bundle
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
    └── TheSingerOfTheEnd.csproj
```

> ★ 标注的为本项目自行编写的图形学相关核心代码。

---

## 三、自定义图形学效果：原理与实现

> 以下七节为本项目的核心图形学内容。每节给出**图形学原理 → 关键代码 → 实现要点 → 效果展示**，效果截图处已预留位置。

### 3.1 体积雨 Volumetric Rain（粒子系统 + 运动模糊着色器）

**图形学原理。** 用 Unity 粒子系统提交大量 billboard 雨滴（GPU Instancing 批量渲染），着色器在顶点阶段沿屏幕竖直方向拉伸四边形以模拟高速下落的**运动模糊拖尾**；片元阶段用到中心轴的距离做软衰减得到"雨丝"截面，并叠加 **Fresnel** 边缘高光模拟水的折射感。

**关键修复（值得记录的踩坑）。** 早期实现用 `UnityObjectToViewPos(0)`（系统原点）重建 billboard，导致所有粒子塌缩到玩家脚下一点、整片雨不可见；修正为按粒子自身顶点 `UnityObjectToClipPos(v.vertex)` 渲染，竖直拖尾改由 C# 端的细高 `startSize3D` 实现。

```hlsl
// 片元：雨丝截面 + 拖尾淡出 + Fresnel 高光
float distToAxis = abs(i.uv.x - 0.5) * 2.0;
float lineAlpha  = saturate(1.0 - distToAxis / _Width);   // 到竖直中轴 → 雨丝
float headTail   = saturate(1.0 - abs(i.uv.y - 0.5) * 2.0 / _Softness); // 首尾淡出
float fresnel    = pow(1.0 - saturate(i.viewDir.z), _FresnelPower);     // 边缘折射高光
col.rgb += fresnel * 0.3;
col.a   *= lineAlpha * headTail;
```

**实现要点（球面星球的特殊处理）。** Outer Wilds 的星球是球体，没有统一的"下方"。`RainController` 把粒子系统挂在玩家身上、在**玩家本地空间**沿 `-Y` 下落，从而无论玩家在球面何处雨都朝脚下落；并用"距星心 < 大气层半径(150m)"做门控，离开大气层（太空 / 量子月）自动停雨：

```csharp
main.simulationSpace = ParticleSystemSimulationSpace.Local;
vol.y = new ParticleSystem.MinMaxCurve(-30f);   // 本地空间向脚下落
// Update 中：
bool inAtmosphere = Vector3.Distance(player.position, _planet.position)
                      < TheSingerOfTheEnd.AttlerockAtmosphereRadius;
```

**效果展示。**

![体积雨 - 开启](图片占位：rain_on.png)
![体积雨 - 关闭](图片占位：rain_off.png)

> *图 3.1：开启 / 关闭体积雨着色器的对比（预留截图）*

---

### 3.2 地面涟漪 Rain Ripple（程序化高度场 + 屏幕空间偏导法线）

**图形学原理。** 在地面贴片上用若干个"扩散环"叠加出高度场 `h(x,z,t)`，每个环是一条沿半径传播、随距离指数衰减的正弦波，模拟雨滴落点向外扩散的水纹：

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

**实现要点。** 着色器加 `Cull Off`，C# 端把水洼 Quad 的法线对齐到球面径向（`Quaternion.FromToRotation(Vector3.forward, radial)`）并投影到舞台所在半径，修复了早期"涟漪平面竖起来夹住歌者"的方向 bug。

**效果展示。**

![地面涟漪](图片占位：ripple.png)

> *图 3.2：歌者舞台周围的程序化涟漪水洼（预留截图）*

---

### 3.3 God Rays 神光 / 丁达尔效应（屏幕空间体积光散射，三 Pass 后处理）

**图形学原理。** 采用经典的 GPU Gems / Kodeco "径向模糊光散射"近似，由 C# 端用 `Graphics.Blit` 依次调度三个 Pass：

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

**实现要点（叙事驱动的设计）。** 按剧情需要，神光**只在 True End 演出期间**出现。`GodRayController` 提供 `ForceRays` 强制模式：把天空阈值 `_DepthThreshold` 降为 0、在固定屏幕坐标合成一个"人造太阳盘"，于是胜利时无论玩家朝哪都能看到"阳光穿透乌云"的光柱，而不受太阳实际朝向限制。

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

> *图 3.3：True End 时"阳光穿透乌云"的神光演出（预留截图）*

---

### 3.4 体积雾 Volumetric Fog（Ray Marching + Beer-Lambert 大气散射）

**图形学原理。** 屏幕空间后处理，对每个像素：

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

**实现要点。** 3D 值噪声用 hash + 三线性插值在着色器内程序化生成（无需 3D 纹理）。`VolumetricFogController` 把雾限制在废岩星大气层内并随距离渐隐；同时为避免雾的 `OnRenderImage` 盖住神光，True End 时由时间线先把雾密度淡出再关闭该控制器。

**效果展示。**

![体积雾 - 开启](图片占位：fog_on.png)
![体积雾 - 关闭](图片占位：fog_off.png)

> *图 3.4：开启 / 关闭体积雾的大气透视对比（预留截图）*

---

### 3.5 声波可视化 Audio Wave（实时 FFT + 程序化网格顶点位移）

**图形学原理。** C# 端用 `AudioListener.GetSpectrumData`（FFTWindow.BlackmanHarris）做实时频谱分析，把 256 点频谱归并为 32 段写入着色器 uniform 数组 `_Spectrum[32]`。顶点着色器按角向坐标取对应频段幅值、沿法线位移，叠加沿 uv.y 向外传播的正弦相位，形成"以歌者为中心向外扩散的声波环"；片元按 **频率→HSV 色相、幅值→亮度** 上色并加色发光。

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

**实现要点。** 环形（annulus）网格由 C# 程序化生成（`BuildRing`），uv.x = 绕环角度（→频段），uv.y = 内圈到外圈（→涟漪传播），并对齐到歌者所在的球面径向。

**效果展示。**

![声波可视化](图片占位：audiowave.png)

> *图 3.5：随歌声起伏的声波环（预留截图）*

---

### 3.6 水面反射与折射 Water Reflection（平面反射相机 + 屏幕空间扰动）

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

> *图 3.6：歌者舞台前反射水池中的倒影（预留截图）*

---

### 3.7 全息投影 Hologram（程序化扫描线 + Fresnel 边缘光 + Glitch 抖动）

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

![全息投影](图片占位：hologram.png)

> *图 3.7：神谕之境的全息信息面板（预留截图）*

---

## 四、效果联动：时间线与结局演出

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

## 五、运行与使用方法

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

## 六、总结与展望

本项目在 Outer Wilds 内置渲染管线下，从零实现了覆盖后处理、粒子、程序化材质、网格变形、平面反射、程序化纹理等多个图形学分支的 **7 个自定义着色器**，并以一套配置/控制器/时间线系统把它们组织成服务于《世末歌者》叙事的整体。过程中解决了球面星球的雨向、屏幕空间深度获取、平面反射递归、后处理渲染顺序等一系列工程问题。

**可改进方向**：体积雾可引入 Henyey-Greenstein 相函数控制前/后向散射、用时间重投影降噪；神光可改为世界空间体积光锥以支持任意角度光柱；声波可视化可加入 SDF 渲染声波前沿。

### 分工

| 成员 | 分工 |
| ---- | ---- |
| wingtings | 着色器编写、效果控制器、剧情与场景搭建、报告撰写 |
| （如有） | ____________ |

---

## 附录：参考资料

- God Rays Shader Breakdown — https://cyanilux.com/tutorials/god-rays-shader-breakdown/
- Volumetric Light Scattering (Kodeco)
- Unity URP Volumetric Fog — https://www.vertexfragment.com/ramblings/urp-volumetric-fog/
- OWML / New Horizons 官方文档 — https://owml.outerwildsmods.com/ , https://nh.outerwildsmods.com/
- Vesper's Assorted Outer Wilds Shaders（着色器集成参考）
</content>
</invoke>
