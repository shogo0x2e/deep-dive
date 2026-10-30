using System;
using HighwaySurfing.Kinect;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace HighwaySurfing.EditorTools
{
    public static class KinectHighwaySetup
    {
        [MenuItem("Tools/Highway Surfing/Integrate Kinect into Main")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity" || scene.isDirty) throw new InvalidOperationException("Open the saved Main scene first.");
            if (UnityEngine.Object.FindFirstObjectByType<KinectHighwayInput>() != null) throw new InvalidOperationException("Main already has Kinect input.");
            var world = UnityEngine.Object.FindFirstObjectByType<NightHighwayWorld>();
            if (world == null) throw new InvalidOperationException("Main highway is missing.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("TMP font missing.");
            var root = new GameObject("KinectSurfing");
            var rig = root.AddComponent<KinectDebugRig>();
            var controller = root.AddComponent<KinectHighwayInput>();
            controller.Rig = rig; controller.World = world;
            rig.EnableKeyboardShortcuts = false;
            rig.ScreenReference = new GameObject("ScreenReference_FixedAxes").transform;
            rig.ScreenReference.SetParent(root.transform, false);
            rig.SensorOrigin = new GameObject("SensorOrigin_EDIT_POSITION_AND_ROTATION").transform;
            rig.SensorOrigin.SetParent(root.transform, false);
            rig.SensorDistance = 2; rig.SensorHeight = 1.1f;
            rig.ApplyPreset(0); // Tested setup: fixed screen-side sensor, participant turned 45 degrees.
            rig.Joints = new Transform[32]; rig.JointRenderers = new Renderer[32]; rig.Bones = new LineRenderer[32];
            for (int i = 0; i < 32; i++)
            {
                var joint = new GameObject($"{i:00}_{KinectDebugRig.JointNames[i]}").transform;
                joint.SetParent(rig.SensorOrigin, false);
                joint.localPosition = rig.SensorOrigin.InverseTransformPoint(KinectDebugRig.NeutralJoint(i));
                rig.Joints[i] = joint;
            }
            world.UseExternalSteering = true;
            world.ExternalRideReady = world.ExternalSteeringAvailable = false;
            world.SteeringInput = 0;
            CreateControls(controller, font);
            var oldControls = GameObject.Find("OperatorHud/Controls")?.GetComponent<TextMeshProUGUI>();
            if (oldControls != null) oldControls.text = "C  REGISTER NEUTRAL    W / S  SPEED    SPACE  PAUSE    R  RESET    H  HUD";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(scene.path, true) };
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Sandbox/KinectDebug.unity", false));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        static void CreateControls(KinectHighwayInput input, TMP_FontAsset font)
        {
            var canvasObject = new GameObject("KinectOperatorPanel", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var hud = canvasObject.AddComponent<KinectHighwayHud>(); hud.Input = input; hud.Group = canvasObject.GetComponent<CanvasGroup>();
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image)); panel.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)panel.transform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-26,-28); rect.sizeDelta = new Vector2(480,650);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.018f,.033f,.055f,.94f);
            Label(panel.transform,"Heading","KINECT  /  SURFING",font,18,22,18,436,32).characterSpacing = 3;
            hud.StatusLabel = Label(panel.transform,"Status","Starting Kinect",font,20,22,60,436,60);
            hud.TrackingLabel = Label(panel.transform,"Tracking","",font,15,22,130,436,52);
            var track = Box(panel.transform,"SteeringTrack",65,194,350,5,new Color(.16f,.27f,.34f));
            var marker = Box(track.transform,"Marker",0,0,9,22,new Color(.22f,.95f,.78f));
            hud.SteeringMarker = (RectTransform)marker.transform; hud.SteeringMarker.anchorMin = hud.SteeringMarker.anchorMax = hud.SteeringMarker.pivot = new Vector2(.5f,.5f);
            hud.SteeringMarker.anchoredPosition = Vector2.zero;
            Button(panel.transform,"Start","START KINECT",font,22,226,212,input.StartSensor);
            Button(panel.transform,"Stop","STOP",font,246,226,212,input.StopSensor);
            Button(panel.transform,"Calibrate","C / REGISTER NEUTRAL",font,22,273,272,input.RequestCalibration);
            Button(panel.transform,"Reselect","RESELECT",font,306,273,152,input.ReleasePerson);
            Button(panel.transform,"Pause","SPACE / PAUSE",font,22,320,212,input.TogglePause);
            Button(panel.transform,"Reset","R / RESET RIDE",font,246,320,212,input.ResetRide);
            hud.PlacementLabel = Label(panel.transform,"Placement","",font,12,22,374,436,25);
            Button(panel.transform,"Screen0","SCREEN 0",font,22,405,138,input.SetScreenSensor);
            Button(panel.transform,"Left45","LEFT 45",font,171,405,138,input.SetLeft45);
            Button(panel.transform,"Right45","RIGHT 45",font,320,405,138,input.SetRight45);
            hud.SettingsLabel = Label(panel.transform,"Settings","",font,14,22,459,436,26);
            Button(panel.transform,"Less","SENS -",font,22,490,100,input.DecreaseSensitivity);
            Button(panel.transform,"More","SENS +",font,134,490,100,input.IncreaseSensitivity);
            Button(panel.transform,"Invert","FLIP LEFT / RIGHT",font,246,490,212,input.ToggleInvert);
            Button(panel.transform,"Keyboard","K / KEYBOARD PREVIEW",font,22,537,436,input.UseKeyboard);
            Label(panel.transform,"Hint","Open arms at shoulder height; hold the surfing pose.\nTutorial appears on OBS; this operator panel does not.",font,12,22,592,436,42).color = new Color(.62f,.74f,.82f);
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var system = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        public static void ApplyMeasuredPlacement()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var input=UnityEngine.Object.FindFirstObjectByType<KinectHighwayInput>();
            input.SetScreenSensor();
            var panel=GameObject.Find("KinectOperatorPanel/Panel").transform;
            var font=panel.Find("Left45/Label").GetComponent<TextMeshProUGUI>().font;
            if(panel.Find("Screen0")==null)Button(panel,"Screen0","SCREEN 0",font,22,405,138,input.SetScreenSensor);
            foreach(var name in new[]{"Left45","Right45"})
            {
                var rt=(RectTransform)panel.Find(name);rt.anchoredPosition=new Vector2(name=="Left45"?171:320,-405);rt.sizeDelta=new Vector2(138,36);
                var label=rt.Find("Label").GetComponent<TextMeshProUGUI>();label.rectTransform.sizeDelta=new Vector2(138,36);label.text=name=="Left45"?"LEFT 45":"RIGHT 45";
            }
            EditorSceneManager.MarkSceneDirty(input.gameObject.scene);EditorSceneManager.SaveScene(input.gameObject.scene);
        }
        static GameObject Box(Transform parent,string name,float x,float y,float width,float height,Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent,false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0,1); rt.anchoredPosition = new Vector2(x,-y); rt.sizeDelta = new Vector2(width,height);
            go.GetComponent<UnityEngine.UI.Image>().color = color; go.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            return go;
        }
        static TextMeshProUGUI Label(Transform parent,string name,string value,TMP_FontAsset font,float size,float x,float y,float width,float height)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0,1); rt.anchoredPosition = new Vector2(x,-y); rt.sizeDelta = new Vector2(width,height);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.text = value; text.color = new Color(.88f,.94f,1); text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
        static void Button(Transform parent,string name,string value,TMP_FontAsset font,float x,float y,float width,UnityAction action)
        {
            var go = Box(parent,name,x,y,width,36,new Color(.06f,.14f,.19f));
            var graphic = go.GetComponent<UnityEngine.UI.Image>(); graphic.raycastTarget = true;
            var button = go.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = graphic;
            UnityEventTools.AddPersistentListener(button.onClick,action);
            var label = Label(go.transform,"Label",value,font,13,0,0,width,36); label.alignment = TextAlignmentOptions.Center;
        }
    }
}
