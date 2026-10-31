using HighwaySurfing.Output;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace HighwaySurfing.EditorTools
{
    public static class PlayerSpeedViewSetup
    {
        public static void Configure(Camera camera)
        {
            NightHighwayWorld world = null;
            foreach (var candidate in Object.FindObjectsByType<NightHighwayWorld>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.MainCamera == camera) { world = candidate; break; }
            }
            if (world == null) return;
            foreach (var existing in Object.FindObjectsByType<PlayerSpeedView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing.gameObject.scene == camera.gameObject.scene) return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new System.InvalidOperationException("Speed HUD font is missing.");
            var output = new SerializedObject(camera.GetComponent<MainCameraSpoutOutput>());
            var resolution = new Vector2(output.FindProperty("width").intValue, output.FindProperty("height").intValue);

            var root = new GameObject("PlayerSpeedHud", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, camera.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(root, "Create player speed HUD");
            root.layer = 5;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = .5f;
            canvas.sortingOrder = 50; // Behind PlayerTutorial (100).
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = resolution;
            scaler.matchWidthOrHeight = .5f;

            var panel = new GameObject("Speed", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.layer = 5;
            panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .7f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(420, 136);
            var background = panel.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(.008f, .018f, .03f, .72f);
            background.raycastTarget = false;

            var labelObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.layer = 5;
            labelObject.transform.SetParent(panel.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(20, 0);
            labelRect.offsetMax = new Vector2(-20, 0);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 84;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = new Color(.94f, .98f, 1, 1);
            label.raycastTarget = false;
            root.AddComponent<PlayerSpeedView>().Configure(world, label);
        }
    }
}
