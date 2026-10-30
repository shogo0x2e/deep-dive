using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HighwaySurfing.EditorTools
{
    /// <summary>Build a native-camera trial scene using the same ride components as Main.</summary>
    public static class HighwayKeyboardSetup
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/HighwayKeyboard.unity";

        [MenuItem("Tools/Highway Surfing/Create Keyboard Sandbox")]
        public static void Create()
        {
            EnsureStoppedAndSaved();
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Keyboard Sandbox already exists. Open it instead of replacing it.");
            var surface = Load<Material>("Assets/Materials/Highway/RoadAndCity.mat");
            var glow = Load<Material>("Assets/Materials/Highway/LightPoints.mat");
            var sky = Load<Material>("Assets/Materials/Highway/NightSky.mat");
            var profile = Load<VolumeProfile>("Assets/Settings/Highway/NightHighwayVolume.asset");
            var font = Load<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.002f, .004f, .012f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 2200; camera.allowHDR = true;
            camera.fieldOfView = 76;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.015f, .02f, .035f);
            RenderSettings.fog = false;
            var volume = new GameObject("NightAtmosphere").AddComponent<Volume>();
            volume.isGlobal = true; volume.sharedProfile = profile;

            var worldObject = new GameObject("NightHighway");
            worldObject.SetActive(false);
            var world = worldObject.AddComponent<NightHighwayWorld>();
            world.MainCamera = camera; world.SurfaceMaterial = surface; world.GlowMaterial = glow;
            world.UseExternalSteering = false;
            world.SpeedKmh = 138; world.LateralPosition = -3.1f; world.ShowHud = true;
            var feedback = worldObject.AddComponent<HighwayRideFeedback>();
            feedback.World = world;
            feedback.MotorClip = Load<AudioClip>("Assets/Audio/Highway/Motor.wav");
            feedback.PassByClip = Load<AudioClip>("Assets/Audio/Highway/Recorded/W3CarPass.wav");
            feedback.StreetPassClip = Load<AudioClip>("Assets/Audio/Highway/Recorded/W3StreetPass.wav");
            feedback.DepartureClip = Load<AudioClip>("Assets/Audio/Highway/Departure.wav");
            feedback.MotorLoops = Load<HighwayMotorLoops>("Assets/Audio/Highway/Recorded/TeslaMotorLoops.asset");
            feedback.StreakMaterial = Load<Material>("Assets/Materials/Highway/RideStreak.mat");
            feedback.RecordedPassBy = true;
            feedback.PassByPeakSeconds = .95f; feedback.StreetPassPeakSeconds = .2625f;
            var impact = worldObject.AddComponent<HighwayImpactFeedback>();
            impact.World = world; impact.RideFeedback = feedback;
            worldObject.SetActive(true);
            world.RefreshPreview();
            CreateHud(world, font);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Keyboard Sandbox created. Play: A/D steer, W/S speed, SPACE pause, R reset, H HUD.");
        }

        [MenuItem("Tools/Highway Surfing/Open Keyboard Sandbox")]
        public static void Open()
        {
            EnsureStoppedAndSaved();
            EditorSceneManager.OpenScene(ScenePath);
        }

        // Call while Main is open, so Kinect and keyboard use the same collision feedback.
        [MenuItem("Tools/Highway Surfing/Install Impact Feedback in Main")]
        public static void ConfigureMain()
        {
            EnsureStoppedAndSaved();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity") throw new InvalidOperationException("Open Main first.");
            var world = UnityEngine.Object.FindFirstObjectByType<NightHighwayWorld>();
            if (world == null) throw new InvalidOperationException("Main has no highway world.");
            var impact = world.GetComponent<HighwayImpactFeedback>();
            if (impact == null) impact = world.gameObject.AddComponent<HighwayImpactFeedback>();
            impact.World = world; impact.RideFeedback = world.GetComponent<HighwayRideFeedback>();
            world.LateralPosition = -3.1f;
            EditorUtility.SetDirty(world); EditorUtility.SetDirty(impact);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void EnsureStoppedAndSaved()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before creating/opening the Sandbox.");
        }
        static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);

        static void CreateHud(NightHighwayWorld world, TMP_FontAsset font)
        {
            var root = new GameObject("KeyboardHud", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var hud = root.AddComponent<HighwayKeyboardHud>();
            hud.World = world; hud.Group = root.GetComponent<CanvasGroup>();
            hud.Group.interactable = false; hud.Group.blocksRaycasts = false;
            Label(root.transform, "Title", "HIGHWAY / NIGHT DRIVE", font, 20, new Vector2(36, -28), new Vector2(600, 36), new Vector2(0, 1));
            hud.Route = Label(root.transform, "Route", world.RouteName, font, 34, new Vector2(36, -68), new Vector2(600, 55), new Vector2(0, 1));
            hud.Junction = Label(root.transform, "Junction", "", font, 23, new Vector2(-36, -30), new Vector2(700, 90), Vector2.one);
            hud.Junction.alignment = TextAlignmentOptions.TopRight;
            hud.Speed = Label(root.transform, "Speed", "138 KM/H", font, 62, new Vector2(36, 100), new Vector2(440, 90), Vector2.zero);
            hud.Status = Label(root.transform, "Status", "", font, 20, new Vector2(40, 68), new Vector2(800, 35), Vector2.zero);
            Label(root.transform, "Controls", "A / D  STEER     W / S  SPEED     SPACE  PAUSE     R  RESET     H  HUD", font, 20,
                new Vector2(40, 24), new Vector2(1500, 30), Vector2.zero);
        }

        static TextMeshProUGUI Label(Transform parent, string name, string value, TMP_FontAsset font,
            int size, Vector2 position, Vector2 dimensions, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size; text.text = value;
            text.color = new Color(.82f, .93f, 1, .94f); text.raycastTarget = false;
            return text;
        }
    }
}
