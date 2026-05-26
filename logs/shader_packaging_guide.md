# 自定义 Shader 打包进 Outer Wilds 的完整指南

> Author: wingtings（指南整理）
>
> 适用对象:本项目 `assets/shaders/` 下的三个自定义 shader
> （`GodRay.shader` / `VolumetricRain.shader` / `RainRipple.shader`），
> 把它们做成游戏里真正能跑、能在答辩里演示的图形学效果。

---

## 〇、为什么必须打包成 AssetBundle?(先理解原理)

很多人卡在"我写好了 `.shader`，为什么游戏里加载不到 / 一片粉红?"——根因有三:

1. **运行时不能加载 `.shader` 源码**。`.shader` 是给 Unity 编辑器看的源文件,游戏运行时只认**已编译**的 shader。编译只发生在 Unity 工程里。
2. **`Shader.Find("Custom/GodRays")` 找不到自定义 shader**。`Shader.Find` 只能找到"已经被打进游戏构建里"的 shader;我们的 shader 不在 OW 的构建里,所以必须自己从 AssetBundle 里把它加载进来。
3. **OW 用的是 Unity 内置渲染管线(Built-in RP),不是 URP/HDRP**。所以后处理走 `OnRenderImage`,水面/粒子走普通材质,**不要**用 URP 的 RendererFeature/Volume 那套。

**结论**:在 Unity 工程里把 shader 编译进 Material → 把 Material 打进 AssetBundle → 部署到 MOD 目录 → C# 在运行时 `AssetBundle.LoadFromFile` 加载 → 取出 Material 应用到相机/粒子/水面。

> 关键点:**打包的对象是 Material,不是 shader 本身**。Material 持有对 shader 的引用,把 Material 打进 bundle 时,它依赖的 shader 会被自动一起打包并随之加载。这是最省心、最不容易出错的做法。

---

## 一、全流程总览

```
┌─ Unity 工程 (outer-wilds-unity-template，已是 OW 对应版本) ─────────────┐
│  1. 把 .shader 放进 Assets/Shaders/                                      │
│  2. 为每个 shader 新建 Material 并调好参数                                │
│  3. 给这些 Material 指定同一个 AssetBundle 名(如 "shaders")             │
│  4. 用 Editor 脚本 BuildPipeline.BuildAssetBundles 构建                   │
│        └─> 产出无扩展名的 bundle 文件: shaders / shaders.manifest         │
└──────────────────────────────────────────────────────────────────────┘
        │  把产出的 bundle 复制到项目
        ▼
TheSingerOfTheEnd/TheSingerOfTheEnd/assets/models/shaders   (随 csproj 自动部署)
        │  dotnet build 自动复制到
        ▼
%AppData%/OuterWildsModManager/OWML/Mods/wingtings.TheSingerOfTheEnd/assets/models/shaders
        │  运行时
        ▼
C# AssetBundle.LoadFromFile(...) → LoadAsset<Material> → 应用到相机/粒子/水面
```

> 本项目已经验证过这条管线:`assets/models/singer` 这个 bundle 里的 `Singer.prefab`
> 已经能被 New Horizons 通过 JSON 的 `assetBundle` 字段加载(见 `singer_world.json` 的歌者条目)。
> Shader 的 bundle 走的是同一套部署机制,只是改为由 C# 端加载。

---

## 二、Unity 端:从 shader 到 bundle

### 2.1 放入 shader

把这三个文件复制到 Unity 工程:

```
outer-wilds-unity-template/Assets/Shaders/
    ├── GodRay.shader
    ├── VolumetricRain.shader
    └── RainRipple.shader
```

回到 Unity,等编译。**这一步就能暴露语法错误**:若 Inspector 里 shader 报红/报错,先在这里改好(此时改的是 Unity 工程里的副本,改好后同步回项目 `assets/shaders/`)。

### 2.2 新建 Material

在 `Assets/Materials/` 右键 `Create > Material`,建三个:

| Material | Shader 选择 | 备注 |
|----------|------------|------|
| `GodRayMat` | `Custom/GodRays` | 后处理材质,参数后面 C# 也可改 |
| `RainMat` | `Custom/VolumetricRain` | 给粒子系统用;给 `_MainTex` 放一张白色软圆点/竖条贴图 |
| `RippleMat` | `Custom/RainRipple` | 给水洼 Quad 用 |

> `RainMat` 建议配一张雨滴贴图(一条上下渐隐的白色竖条,或一个软圆点),没有也能跑(默认白)。

### 2.3 指定 AssetBundle 名

选中三个 Material(以及 `RainMat` 要用的贴图),在 **Inspector 最底部** 的 AssetBundle 下拉里,新建并选择同一个名字,例如 `shaders`:

```
Inspector 底部:  AssetBundle:  [ shaders ▾ ]   [ None ▾ ]
```

> 三个 Material 用同一个 bundle 名 → 它们会被打进同一个 `shaders` bundle,C# 只需加载一个文件。

### 2.4 用 Editor 脚本构建 bundle

在 `Assets/Editor/` 新建 `BuildBundles.cs`:

```csharp
using UnityEditor;
using System.IO;

public static class BuildBundles
{
    [MenuItem("Singer/Build AssetBundles")]
    public static void Build()
    {
        // 输出到工程外的一个文件夹,避免污染 Assets
        string outDir = "AssetBundles";
        Directory.CreateDirectory(outDir);

        BuildPipeline.BuildAssetBundles(
            outDir,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);   // OW 是 Win64

        UnityEngine.Debug.Log("AssetBundles built to: " + Path.GetFullPath(outDir));
    }
}
```

点菜单 `Singer > Build AssetBundles`。`AssetBundles/` 里会出现:

```
AssetBundles/
    ├── shaders            ← 我们要的 bundle(无扩展名)
    ├── shaders.manifest   ← 编辑器元数据(运行时不需要)
    ├── AssetBundles       ← 主清单 bundle(名字=输出文件夹名)
    └── AssetBundles.manifest
```

### 2.5 复制到项目并部署

把 `shaders` 文件复制到:

```
TheSingerOfTheEnd/TheSingerOfTheEnd/assets/models/shaders
```

> 本项目 csproj 的 `<None Include="assets\**\*.*">` 会把 `assets/` 下所有文件(含无扩展名的 bundle)
> 复制进部署目录。已实测:`singer`/`models`/`test` 这些无扩展名文件都能正确部署。
> 所以放进来后 `dotnet build` 一次即可。

---

## 三、C# 端:加载 bundle 并应用

### 3.1 一个共用的 bundle 加载器(避免重复加载)

> 重要坑:**同一个 bundle 文件不能被 `LoadFromFile` 加载两次**(第二次返回 null 并报错)。所以必须缓存。

在项目里新建 `AssetLoader.cs`:

```csharp
using System.IO;
using UnityEngine;

namespace TheSingerOfTheEnd
{
    public static class AssetLoader
    {
        private static AssetBundle _shaders;

        public static AssetBundle Shaders
        {
            get
            {
                if (_shaders == null)
                {
                    var path = Path.Combine(
                        TheSingerOfTheEnd.Instance.ModHelper.Manifest.ModFolderPath,
                        "assets/models/shaders");
                    _shaders = AssetBundle.LoadFromFile(path);
                    if (_shaders == null)
                        TheSingerOfTheEnd.Instance.ModHelper.Console.WriteLine(
                            "[世末歌者] 加载 shaders bundle 失败: " + path, OWML.Common.MessageType.Error);
                }
                return _shaders;
            }
        }

        public static Material LoadMaterial(string assetPath)
        {
            // assetPath 形如 "Assets/Materials/GodRayMat.mat"(在 Unity 工程里的路径)
            return Shaders != null ? Shaders.LoadAsset<Material>(assetPath) : null;
        }
    }
}
```

> 用 `LoadAsset<Material>` 时,参数是该资源**在 Unity 工程里的资产路径**(如 `Assets/Materials/GodRayMat.mat`),
> 大小写需一致。也可以用 `bundle.LoadAllAssets<Material>()` 然后按名字取。

### 3.2 God Rays:挂到玩家相机的后处理

新建 `GodRayController.cs`。`OnRenderImage` 必须和 `Camera` 在**同一个 GameObject** 上:

```csharp
using UnityEngine;

namespace TheSingerOfTheEnd
{
    public class GodRayController : MonoBehaviour
    {
        private Material _mat;
        private Camera _cam;
        private Transform _sun;   // 光源(恒星)

        public void Init(Transform sun)
        {
            _sun = sun;
            _cam = GetComponent<Camera>();
            _cam.depthTextureMode |= DepthTextureMode.Depth;   // Occlusion pass 需要深度图
            _mat = AssetLoader.LoadMaterial("Assets/Materials/GodRayMat.mat");
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null || _sun == null) { Graphics.Blit(src, dst); return; }

            // 恒星的屏幕(视口)坐标;在相机背后时跳过,避免反向光束
            Vector3 vp = _cam.WorldToViewportPoint(_sun.position);
            if (vp.z <= 0f) { Graphics.Blit(src, dst); return; }
            _mat.SetVector("_LightPos", new Vector4(vp.x, vp.y, 0, 0));

            int w = src.width, h = src.height;
            var occ  = RenderTexture.GetTemporary(w, h, 0, src.format);
            var blur = RenderTexture.GetTemporary(w, h, 0, src.format);

            Graphics.Blit(src, occ, _mat, 0);   // Pass0: Occlusion(从深度提天空)
            Graphics.Blit(occ, blur, _mat, 1);  // Pass1: RadialBlur
            Graphics.Blit(blur, occ, _mat, 1);  // 再模糊一次 → 光束更长(可选)
            _mat.SetTexture("_SceneTex", src);
            Graphics.Blit(occ, dst, _mat, 2);   // Pass2: Composite(叠回原场景)

            RenderTexture.ReleaseTemporary(occ);
            RenderTexture.ReleaseTemporary(blur);
        }
    }
}
```

挂载时机(玩家相机要在场景加载后才存在)。在 `TheSingerOfTheEnd.cs` 的 `OnStarSystemLoaded`
里启动一个协程等待相机就绪:

```csharp
private System.Collections.IEnumerator AttachGodRays()
{
    OWCamera owCam = null;
    while ((owCam = Locator.GetPlayerCamera()) == null) yield return null;

    var sun = GameObject.Find("鸥停之星_Body");   // NH 生成的恒星 Body(名字以 _Body 结尾)
    var ctrl = owCam.mainCamera.gameObject.AddComponent<GodRayController>();
    ctrl.Init(sun != null ? sun.transform : null);
    ModHelper.Console.WriteLine("[世末歌者] GodRayController attached.", MessageType.Success);
}
// 在 OnStarSystemLoaded 里: StartCoroutine(AttachGodRays());
```

> God Rays 用途:**True End 雨停穿云**(在 `EndingJudge.TriggerTrueEnding` 里把 `_Intensity` 拉高、
> 把雾散掉),以及音乐厅舞台的追光。常驻时可以把 `_Intensity` 调低当作大气光。

### 3.3 Volumetric Rain:粒子系统 + 自定义材质

新建 `RainController.cs`:

```csharp
using UnityEngine;

namespace TheSingerOfTheEnd
{
    public class RainController : MonoBehaviour
    {
        public static GameObject Spawn(Transform follow)
        {
            var go = new GameObject("SingerRain");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.startSpeed = 30f;                 // 下落速度
            main.startLifetime = 1.5f;
            main.startSize = 0.15f;
            main.maxParticles = 12000;             // README 目标 1万+ 粒子
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;

            var emission = ps.emission;
            emission.rateOverTime = 6000f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60, 1, 60);  // 在玩家头顶一大片区域下雨

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;  // shader 自己做拉伸
            rend.material = AssetLoader.LoadMaterial("Assets/Materials/RainMat.mat");

            if (follow != null) { go.transform.SetParent(follow, false);
                                  go.transform.localPosition = new Vector3(0, 25, 0); }
            ps.Play();
            return go;
        }
    }
}
```

> 让雨跟着玩家相机/玩家身体走(`follow` 传 `Locator.GetPlayerTransform()`),这样无论走到哪都在下雨。
> 也可以只在世末之城范围内启用。

### 3.4 Rain Ripple:贴地水洼

最简单的接法是用 NH 在地面放一块 Quad(也可以 C# 生成),再把 `RippleMat` 赋上去:

```csharp
var puddle = GameObject.CreatePrimitive(PrimitiveType.Quad);
puddle.transform.rotation = Quaternion.Euler(90, 0, 0);   // 平铺在地面
puddle.transform.localScale = Vector3.one * 8f;
Object.Destroy(puddle.GetComponent<Collider>());
puddle.GetComponent<MeshRenderer>().material = AssetLoader.LoadMaterial("Assets/Materials/RippleMat.mat");
// 放到广场积水处,贴合球面法线
```

> `RainRipple` 的倒影(`_ReflectionTex`)是可选的高级项:需要再加一个"平面反射相机"渲染到 RenderTexture
> 再 `SetTexture` 进去。不做倒影时,水面靠 Fresnel + 高光也已经有"湿/反光"的效果。

---

## 四、常见问题排查(Troubleshooting)

| 现象 | 原因 | 解决 |
|------|------|------|
| 物体一片**粉红/品红** | shader 没被打进 bundle,或管线不兼容 | 确认 Material 指定了 bundle 名;确认 shader 是 Built-in RP 写法(本项目的都是) |
| `LoadFromFile` 返回 **null** | 路径错 / bundle 已被加载过一次 | 用 `ModFolderPath` 拼路径;用 3.1 的缓存,别重复加载 |
| `LoadAsset<Material>` 返回 **null** | 资产路径写错 | 路径要和 Unity 工程里的完全一致(含 `Assets/` 前缀和大小写);或用 `LoadAllAssets` 打印名字核对 |
| God Rays **没效果** | 没挂到相机 / 没开深度图 / 太阳在背后 | 确认挂在 `mainCamera.gameObject`;`depthTextureMode |= Depth`;面朝恒星看 |
| God Rays **整屏发白** | 阈值/强度过高 | 调低 `_Intensity`、`_Weight`,调高 `_DepthThreshold`(更严格地只取天空) |
| bundle **加载报版本错** | Unity 版本/平台不匹配 | **必须**用 OW 对应的 Unity 版本(就是 `outer-wilds-unity-template` 那个工程)构建,平台选 StandaloneWindows64 |
| 雨**看不见** | 材质透明/朝向问题 | 确认 `RainMat` 的 `_Color` alpha > 0;粒子 Render Mode = Billboard |

---

## 五、接入剧情的建议(让图形学服务叙事 = 加分点)

1. **常驻**:进入世末之城即 `RainController.Spawn` 下雨;广场积水处铺 `RippleMat`。
2. **God Rays 常驻弱光**:`_Intensity` 调低,营造末日昏黄大气。
3. **`TimelineManager.cs`**(目前空文件):随 22 分钟循环把 `RainMat._Color` 从灰蓝渐变到暗红、
   把雨量 `emission.rateOverTime` 调大 → 用图形学表现"末日临近"。
4. **True End 高潮**:在 `EndingJudge.TriggerTrueEnding` 里:
   - 停雨(`ps.Stop()` 或快速降 emission)、散雾;
   - 把 `GodRayMat._Intensity` 拉满 → 阳光穿透乌云照亮歌者(对应歌词意象)。
   这一段把"修复扩音器 → 雨停穿云"的演出和图形学效果绑在一起,是答辩里最值得展示的镜头。

---

## 六、本指南对应的代码现状

- ✅ 三个 shader 源码已就绪并随构建部署:`assets/shaders/{GodRay,VolumetricRain,RainRipple}.shader`
- ⬜ 上面 C# 控制器(`AssetLoader` / `GodRayController` / `RainController`)**尚未加入项目**——
  按本指南在你把 shader 的 bundle 打好之后再加入并接线(它们引用的 bundle 此刻还不存在)。
- ⬜ Unity 端的 Material 创建 + bundle 构建需要你在 `outer-wilds-unity-template` 工程里操作
  (这一步必须在装了对应 Unity 版本的机器上手动做,无法由脚本代劳)。
