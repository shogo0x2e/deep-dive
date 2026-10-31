using Klak.Spout;
using UnityEngine;
using UnityEngine.Rendering;

namespace HighwaySurfing.Output
{
    /// <summary>Renders a clean, fixed-resolution camera feed for OBS and previews it locally.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpoutSender))]
    public sealed class MainCameraSpoutOutput : MonoBehaviour
    {
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private SpoutResources resources;
        [SerializeField] private string senderName = "HighwaySurfing.Main";
        [SerializeField, Min(16)] private int width = 2160;
        [SerializeField, Min(16)] private int height = 2160;
        [SerializeField] private bool previewInGameView = true;
        [SerializeField] private ProjectionDebugView projectionDebugView;

        public RenderTexture OutputTexture => output;
        public string SenderName => senderName;
        public int RenderedFrames { get; private set; }
        public bool IsOutputActive => output != null && sender != null && sender.enabled;

        private SpoutSender sender;
        private Camera renderCamera;
        private RenderTexture output, previousTarget;
        private GameObject preview;

        public void Configure(Camera camera, SpoutResources spoutResources, ProjectionDebugView debugView)
        {
            sourceCamera = camera;
            resources = spoutResources;
            projectionDebugView = debugView;
        }

        private void OnEnable()
        {
            sender = GetComponent<SpoutSender>();
            sender.enabled = false;
            if (!Application.isPlaying) return;
            if (sourceCamera == null || resources == null)
            {
                Debug.LogError("Main Camera Spout Output needs a camera and SpoutResources asset.", this);
                return;
            }
            var api = SystemInfo.graphicsDeviceType;
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
            {
                Debug.LogWarning("Spout output requires Windows. Camera rendering is unchanged.", this);
                return;
            }
            if (api != GraphicsDeviceType.Direct3D11 && api != GraphicsDeviceType.Direct3D12)
            {
                Debug.LogWarning("Spout output requires Direct3D 11 or 12. Camera rendering is unchanged.", this);
                return;
            }

            renderCamera = sourceCamera;
            previousTarget = renderCamera.targetTexture;
            output = new RenderTexture(Mathf.Clamp(width, 16, 8192), Mathf.Clamp(height, 16, 8192),
                24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "HighwayMainOutput", antiAliasing = 1, useMipMap = false,
                autoGenerateMips = false, hideFlags = HideFlags.DontSave
            };
            if (!output.Create())
            {
                Debug.LogError("Could not allocate the Main Camera output texture.", this);
                Destroy(output);
                output = null;
                return;
            }
            renderCamera.targetTexture = output;
            sender.SetResources(resources);
            sender.spoutName = string.IsNullOrWhiteSpace(senderName) ? "HighwaySurfing.Main" : senderName;
            sender.captureMethod = CaptureMethod.Texture;
            sender.sourceTexture = output;
            sender.keepAlpha = false;
            sender.enabled = true;
            RenderedFrames = 0;
            RenderPipelineManager.endCameraRendering += CameraRendered;
            if (previewInGameView) CreatePreview();
        }

        private void CameraRendered(ScriptableRenderContext context, Camera camera)
        {
            if (camera != renderCamera) return;
            RenderedFrames++;
            if (output == null || projectionDebugView == null || !projectionDebugView.isActiveAndEnabled) return;

            // Replace the completed frame, including tutorial UI and post-processing,
            // before the local preview and Spout's end-of-frame texture capture.
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = output;
                GL.Clear(false, true, projectionDebugView.OutputColor);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private void CreatePreview()
        {
            preview = new GameObject("MainOutputPreview", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            preview.hideFlags = HideFlags.DontSave;
            preview.transform.SetParent(transform, false);
            var canvas = preview.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            var scaler = preview.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(width, height);
            scaler.matchWidthOrHeight = 0.5f;
            var background = new GameObject("Letterbox", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(preview.transform, false);
            var rect = (RectTransform)background.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = background.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            var picture = new GameObject("CameraFeed", typeof(RectTransform), typeof(UnityEngine.UI.RawImage),
                typeof(UnityEngine.UI.AspectRatioFitter));
            picture.transform.SetParent(background.transform, false);
            var raw = picture.GetComponent<UnityEngine.UI.RawImage>();
            raw.texture = output;
            raw.raycastTarget = false;
            var fitter = picture.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)output.width / output.height;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= CameraRendered;
            if (sender != null)
            {
                sender.enabled = false;
                sender.sourceTexture = null;
            }
            if (renderCamera != null && renderCamera.targetTexture == output)
                renderCamera.targetTexture = previousTarget;
            if (preview != null) Destroy(preview);
            if (output != null)
            {
                output.Release();
                Destroy(output);
            }
            preview = null;
            output = null;
            renderCamera = null;
            previousTarget = null;
        }
    }
}
