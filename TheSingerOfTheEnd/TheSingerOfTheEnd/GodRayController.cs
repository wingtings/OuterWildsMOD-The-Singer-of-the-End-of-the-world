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
        public float Intensity = 0.6f;

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

            // 恒星屏幕(视口)坐标;在相机背后时整段跳过,避免反向光束。
            Vector3 vp = _cam.WorldToViewportPoint(_sun.position);
            if (vp.z <= 0f)
            {
                Graphics.Blit(src, dst);
                return;
            }

            _mat.SetVector("_LightPos", new Vector4(vp.x, vp.y, 0f, 0f));
            _mat.SetFloat("_Intensity", Intensity);

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
