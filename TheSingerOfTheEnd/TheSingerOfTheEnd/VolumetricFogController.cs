using UnityEngine;

namespace TheSingerOfTheEnd
{
    // 体积雾后处理(README #2)。必须挂在带 Camera 的 GameObject(玩家相机)上,
    // OnRenderImage 才会在该相机渲染后被调用。Custom/VolumetricFog 做 Ray Marching + Beer-Lambert。
    // 每帧把相机四角的世界空间射线打进 _FrustumCornersWS,供 shader 用深度重建世界位置。
    // 材质缺失时直接透传,不影响其它效果。
    [RequireComponent(typeof(Camera))]
    public class VolumetricFogController : MonoBehaviour
    {
        private Material _mat;
        private Camera _cam;

        // 供 TimelineManager 调节末日临近时的雾密度(可选)
        public float DensityScale = 1f;
        private float _baseDensity = 0.012f;

        public void Init(Material fogMat)
        {
            _mat = fogMat;
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
            _mat.SetFloat("_FogDensity", _baseDensity * DensityScale);

            Graphics.Blit(src, dst, _mat, 0);
        }
    }
}
