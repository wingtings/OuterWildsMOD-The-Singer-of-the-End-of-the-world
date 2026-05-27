using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 屏幕空间 God Rays 后处理。必须挂在带 Camera 的 GameObject 上(玩家相机),
    // 因为 OnRenderImage 只在该相机渲染完成后被调用。
    // 三个 Pass 由 Custom/GodRays 提供:0=Occlusion(从深度提天空)1=RadialBlur 2=Composite。
    [RequireComponent(typeof(Camera))]
    public class GodRayController : MonoBehaviour
    {
        private Material _mat;
        private Camera _cam;
        private Transform _sun;

        // 运行时可调:供 TimelineManager / EndingJudge 控制光束强度(True End 拉满)。
        // 默认值调低(原 0.6 → 0.3),配合 shader 的有界亮源 + Screen 合成,避免"圣光过亮"。
        public float Intensity = 0.3f;

        // 太阳偏离画面多远(视口比例)仍允许光束从屏幕边缘射入。越大可见角度越宽。
        private const float EdgeFalloff = 0.6f;

        public void Init(Material godRayMat, Transform sun)
        {
            _mat = godRayMat;
            _sun = sun;
            _cam = GetComponent<Camera>();
            // Occlusion pass 采样 _CameraDepthTexture,必须显式开启相机深度图。
            _cam.depthTextureMode |= DepthTextureMode.Depth;
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null || _sun == null || _cam == null)
            {
                Graphics.Blit(src, dst);
                return;
            }

            // 恒星屏幕(视口)坐标
            Vector3 vp = _cam.WorldToViewportPoint(_sun.position);

            // 太阳在相机背后(z <= 0):径向模糊原点会翻折到屏幕中心造成穿帮,且物理上背对太阳本就无丁达尔光 → 直接跳过。
            if (vp.z <= 0f)
            {
                Graphics.Blit(src, dst);
                return;
            }

            // 把太阳的视口坐标夹到屏幕范围 [0,1]。太阳偏到画面外时,光束改从最近的屏幕边缘射入,
            // 从而在"侧对太阳"时仍然可见(拓宽可见角度);偏离越远越淡出。
            Vector2 lp = new Vector2(vp.x, vp.y);
            Vector2 clamped = new Vector2(Mathf.Clamp01(lp.x), Mathf.Clamp01(lp.y));
            float offDist = Vector2.Distance(lp, clamped);          // 在屏幕内时为 0
            float edgeFade = Mathf.Clamp01(1f - offDist / EdgeFalloff);

            float weight = edgeFade * Intensity;
            if (weight <= 0.001f)
            {
                Graphics.Blit(src, dst);
                return;
            }

            // z 传 1 表示"在前方"(shader 用它做 forward 判定);xy 用夹取后的边缘坐标。
            _mat.SetVector("_LightPos", new Vector4(clamped.x, clamped.y, 1f, 0f));
            _mat.SetFloat("_Intensity", weight);

            int w = src.width, h = src.height;
            var occ = RenderTexture.GetTemporary(w, h, 0, src.format);
            var blur = RenderTexture.GetTemporary(w, h, 0, src.format);

            Graphics.Blit(src, occ, _mat, 0);    // Occlusion
            Graphics.Blit(occ, blur, _mat, 1);   // RadialBlur
            Graphics.Blit(blur, occ, _mat, 1);   // 再来一次,光束更长
            _mat.SetTexture("_SceneTex", src);
            Graphics.Blit(occ, dst, _mat, 2);    // Composite

            RenderTexture.ReleaseTemporary(occ);
            RenderTexture.ReleaseTemporary(blur);
        }
    }
}
