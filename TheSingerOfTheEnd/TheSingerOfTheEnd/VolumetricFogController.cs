using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 体积雾后处理(README #2)。必须挂在带 Camera 的 GameObject(玩家相机)上,
    // OnRenderImage 才会在该相机渲染后被调用。Custom/VolumetricFog 做 Ray Marching + Beer-Lambert。
    // 每帧把相机四角的世界空间射线打进 _FrustumCornersWS,供 shader 用深度重建世界位置。
    // 材质缺失时直接透传,不影响其它效果。
    //
    // 说明:之前尝试用 CommandBuffer 在 AfterSkybox 合成以避免遮挡 HUD,但在 OW 的相机管线下
    // 该时机深度/目标不稳定,导致废岩星上完全看不到雾。这里回退到稳定可见的 OnRenderImage 方案。
    // 雾被限制在废岩星大气层以内(距星心 < AttlerockAtmosphereRadius),离开大气层即透传。
    [RequireComponent(typeof(Camera))]
    public class VolumetricFogController : MonoBehaviour
    {
        private Material _mat;
        private Camera _cam;
        private Transform _planet;     // 废岩星(Attlerock)根,用于把雾限制在大气层内

        // 供 TimelineManager 调节末日临近时的雾密度(可选)
        public float DensityScale = 1f;
        private float _baseDensity = 0.012f;

        // 大气层渐隐:距星心 < FullRadius 满雾,到 FadeRadius 渐隐为 0,更远直接透传。
        private static readonly float FogFullRadius = TheSingerOfTheEnd.AttlerockAtmosphereRadius * 0.85f;
        private static readonly float FogFadeRadius = TheSingerOfTheEnd.AttlerockAtmosphereRadius;

        public void Init(Material fogMat, Transform planet)
        {
            _mat = fogMat;
            _planet = planet;
            _cam = GetComponent<Camera>();
            _cam.depthTextureMode |= DepthTextureMode.Depth;
            if (_mat != null) _baseDensity = _mat.GetFloat("_FogDensity");
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null || _cam == null)
            {
                Graphics.Blit(src, dst);
                return;
            }

            // 大气层门控:离废岩星星心太远(太空/量子月)→ 不起雾,直接透传。
            float distFactor = 1f;
            if (_planet != null)
            {
                float dist = Vector3.Distance(_cam.transform.position, _planet.position);
                distFactor = Mathf.Clamp01((FogFadeRadius - dist) / (FogFadeRadius - FogFullRadius));
                if (distFactor <= 0.001f)
                {
                    Graphics.Blit(src, dst);
                    return;
                }
            }

            // 相机四角(相机空间)→ 世界空间方向(相机→远平面角射线)
            var corners = new Vector3[4];
            _cam.CalculateFrustumCorners(
                new Rect(0, 0, 1, 1), _cam.farClipPlane,
                Camera.MonoOrStereoscopicEye.Mono, corners);

            // CalculateFrustumCorners 顺序: [0]=BL [1]=TL [2]=TR [3]=BR
            Vector3 bl = _cam.transform.TransformVector(corners[0]);
            Vector3 tl = _cam.transform.TransformVector(corners[1]);
            Vector3 tr = _cam.transform.TransformVector(corners[2]);
            Vector3 br = _cam.transform.TransformVector(corners[3]);

            var m = Matrix4x4.identity;
            m.SetRow(0, bl);   // shader 约定 行0=BL 行1=BR 行2=TL 行3=TR
            m.SetRow(1, br);
            m.SetRow(2, tl);
            m.SetRow(3, tr);

            _mat.SetMatrix("_FrustumCornersWS", m);
            _mat.SetVector("_CameraWS", _cam.transform.position);
            _mat.SetFloat("_FogDensity", _baseDensity * DensityScale * distFactor);

            Graphics.Blit(src, dst, _mat, 0);
        }
    }
}
