using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 屏幕空间 God Rays（圣光）后处理。挂在玩家相机上(OnRenderImage)。
    // 三个 Pass 由 Custom/GodRays 提供:0=Occlusion 1=RadialBlur 2=Composite。
    //
    // 设计(按用户要求):圣光【只在 True End 演出期间】出现,平时(含面朝太阳)一律不渲染,
    // 避免胜利前就看到。True End 时由 TimelineManager 打开 ForceRays + 调 Intensity,演出结束后
    // 自动关闭 ForceRays → 恢复正常(无圣光)。
    // 强制模式原理:把 shader 的天空阈值 _DepthThreshold 降到 0,使整屏都算亮源,从而在固定屏幕
    // 位置 ForcedLightPos 合成出一个"人造太阳盘",无论玩家朝哪都能看到光束("阳光穿透乌云")。
    [RequireComponent(typeof(Camera))]
    public class GodRayController : MonoBehaviour
    {
        private Material _mat;
        private Camera _cam;

        // 运行时由 TimelineManager 控制。
        public float Intensity = 0.3f;
        public bool ForceRays = false;
        public Vector2 ForcedLightPos = new Vector2(0.5f, 0.72f);

        // sun 参数保留以兼容调用方;圣光改为仅 True End 强制出现,不再依赖太阳朝向。
        public void Init(Material godRayMat, Transform sun)
        {
            _mat = godRayMat;
            _cam = GetComponent<Camera>();
            // Occlusion pass 采样 _CameraDepthTexture,必须显式开启相机深度图。
            _cam.depthTextureMode |= DepthTextureMode.Depth;
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            // 仅在 True End 强制模式下渲染圣光;其余情况直接透传(胜利前不出现)。
            if (_mat == null || _cam == null || !ForceRays || Intensity <= 0.001f)
            {
                Graphics.Blit(src, dst);
                return;
            }

            // 固定屏幕位置当"合成太阳";_DepthThreshold=0 让整屏都算亮源 → ForcedLightPos 处合成光盘。
            _mat.SetVector("_LightPos", new Vector4(ForcedLightPos.x, ForcedLightPos.y, 1f, 0f));
            _mat.SetFloat("_Intensity", Intensity);
            _mat.SetFloat("_DepthThreshold", 0f);

            int w = src.width, h = src.height;
            var occ = RenderTexture.GetTemporary(w, h, 0, src.format);
            var blur = RenderTexture.GetTemporary(w, h, 0, src.format);

            Graphics.Blit(src, occ, _mat, 0);    // Occlusion(合成光盘)
            Graphics.Blit(occ, blur, _mat, 1);   // RadialBlur
            Graphics.Blit(blur, occ, _mat, 1);   // 再来一次,光束更长
            _mat.SetTexture("_SceneTex", src);
            Graphics.Blit(occ, dst, _mat, 2);    // Composite(Screen 混合)

            RenderTexture.ReleaseTemporary(occ);
            RenderTexture.ReleaseTemporary(blur);
        }
    }
}
