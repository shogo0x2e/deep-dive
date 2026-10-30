using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using HighwaySurfing.Kinect;

public static class KinectDebugSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Sandbox/KinectDebug.unity";
    const string MaterialPath = "Assets/Materials/Kinect";
    static TMP_FontAsset font;
    static Color textColor = new Color(.89f,.94f,1f);
    [MenuItem("Tools/Highway Surfing/Create Kinect Debug Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before scene creation.");
        for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your scene first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("KinectDebug scene already exists; edit it instead.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) throw new InvalidOperationException("Import TMP essentials non-interactively before Build.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Folders("Assets/Scenes/Sandbox"); Folders(MaterialPath);
        var tracked = Material("Tracked", new Color(.12f,.95f,.82f));
        var predicted = Material("Predicted", new Color(1f,.57f,.17f));
        var dim = Material("Grid", new Color(.10f,.17f,.23f));
        var floor = Material("Floor", new Color(.035f,.055f,.08f));
        var blue = Material("Screen", new Color(.05f,.18f,.25f));
        var purple = Material("Sensor", new Color(.75f,.45f,1f));
        var white = Material("Label", new Color(.7f,.83f,.94f));
        var reference = new GameObject("ScreenReference_FixedAxes").transform;
        var rig = new GameObject("KinectDebug").AddComponent<KinectDebugRig>();
        rig.ScreenReference = reference;
        rig.SensorOrigin = new GameObject("SensorOrigin_EDIT_POSITION_AND_ROTATION").transform;
        rig.TrackedMaterial=tracked; rig.PredictedMaterial=predicted;
        rig.Joints = new Transform[32]; rig.JointRenderers=new Renderer[32]; rig.Bones=new LineRenderer[32];
        rig.ApplyPreset(1);
        for(int i=0;i<32;i++)
        {
            var joint=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            joint.name=$"{i:00}_{KinectDebugRig.JointNames[i]}";
            UnityEngine.Object.DestroyImmediate(joint.GetComponent<Collider>());
            joint.transform.SetParent(rig.SensorOrigin,false);
            joint.transform.localPosition=rig.SensorOrigin.InverseTransformPoint(KinectDebugRig.NeutralJoint(i));
            joint.transform.localScale=Vector3.one*(i==0||i==2?.075f:.045f);
            rig.Joints[i]=joint.transform;rig.JointRenderers[i]=joint.GetComponent<Renderer>();
            rig.JointRenderers[i].sharedMaterial=tracked;
            if(i>0)
            {
                var bone=new GameObject("Bone_"+KinectDebugRig.JointNames[i]).AddComponent<LineRenderer>();
                bone.transform.SetParent(rig.SensorOrigin,false);bone.useWorldSpace=false;bone.positionCount=2;
                bone.startWidth=bone.endWidth=.018f;bone.sharedMaterial=tracked;bone.numCapVertices=3;
                bone.SetPosition(0,rig.SensorOrigin.InverseTransformPoint(KinectDebugRig.NeutralJoint(KinectDebugRig.Parents[i])));
                bone.SetPosition(1,rig.Joints[i].localPosition);rig.Bones[i]=bone;
            }
        }
        var bodyRoot = new GameObject("InstallationReference").transform;
        Cube("Floor",new Vector3(0,-.04f,0),new Vector3(7,.05f,7),floor,bodyRoot);
        for(int i=-6;i<=6;i++)
        {
            Line("GridX",new Vector3(i*.5f,0,-3),new Vector3(i*.5f,0,3),.009f,dim,bodyRoot);
            Line("GridZ",new Vector3(-3,0,i*.5f),new Vector3(3,0,i*.5f),.009f,dim,bodyRoot);
        }
        Cube("PhysicalDisplay",new Vector3(0,1.25f,3.2f),new Vector3(4.2f,2.4f,.06f),blue,bodyRoot);
        WorldLabel("SCREEN",new Vector3(0,2.7f,3.1f),.34f,bodyRoot);
        WorldLabel("LEFT",new Vector3(-1.65f,.04f,-.4f),.22f,bodyRoot,new Vector3(90,0,0));
        WorldLabel("RIGHT",new Vector3(1.65f,.04f,-.4f),.22f,bodyRoot,new Vector3(90,0,0));
        Line("ScreenRightAxis",new Vector3(-2,.015f,0),new Vector3(2,.015f,0),.02f,tracked,bodyRoot);
        Line("TowardScreenAxis",new Vector3(0,.015f,-1.5f),new Vector3(0,.015f,2.7f),.02f,blue,bodyRoot);
        var sensor = Cube("KinectPosition",Vector3.zero,new Vector3(.22f,.1f,.08f),purple,rig.SensorOrigin);
        sensor.transform.localPosition=Vector3.zero;sensor.transform.localRotation=Quaternion.identity;
        Line("SensorOpticalAxis",Vector3.zero,new Vector3(0,0,.65f),.015f,purple,rig.SensorOrigin,false);
        var board = Cube("SteeringPreviewBoard",new Vector3(0,.08f,0),new Vector3(.45f,.06f,1.25f),white,bodyRoot);
        rig.LeanIndicator=board.transform;
        var camera=new GameObject("DebugCamera").AddComponent<Camera>();
        camera.tag="MainCamera";camera.transform.position=new Vector3(4.6f,3.3f,-5.6f);
        camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,1,0.6f)-camera.transform.position);
        camera.orthographic=true;camera.orthographicSize=2.6f;camera.nearClipPlane=.05f;camera.farClipPlane=80;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.028f,.045f);
        camera.rect=new Rect(.27f,.15f,.73f,.73f);
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        camera.gameObject.AddComponent<AudioListener>();
        var top=new GameObject("PlacementTopView").AddComponent<Camera>();
        top.transform.position=new Vector3(0,8,0.3f);top.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
        top.orthographic=true;top.orthographicSize=3.6f;top.nearClipPlane=.1f;top.farClipPlane=20;
        top.clearFlags=CameraClearFlags.SolidColor;top.backgroundColor=new Color(.025f,.043f,.065f);
        top.rect=new Rect(.80f,.64f,.19f,.28f);top.depth=2;
        top.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        CreateHud(rig);
        EditorSceneManager.SaveScene(scene,ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject=rig.SensorOrigin.gameObject;
    }
    static void CreateHud(KinectDebugRig rig)
    {
        var canvasGo=new GameObject("KinectDebugHUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=1f;
        var root=canvasGo.transform;
        Panel(root,"SidePanel",new Vector2(0,0),new Vector2(.27f,1),new Color(.025f,.041f,.062f,.98f));
        Text(root,"Title","BODY / STEERING LAB",30,new Vector2(30,-28),new Vector2(800,45));
        Text(root,"Subtitle","HIGHWAY SURFING     /     SENSOR PLACEMENT STUDY",13,new Vector2(31,-77),new Vector2(750,28),new Color(.5f,.67f,.78f));
        var hud=canvasGo.AddComponent<KinectDebugHud>();hud.Rig=rig;
        hud.ModeText=Text(root,"Mode","KINECT READY / MANUAL START",14,new Vector2(30,-123),new Vector2(380,28));
        var lamp=Panel(root,"TrackingLamp",new Vector2(0,1),new Vector2(0,1),new Color(1,.7f,.3f));
        lamp.rectTransform.pivot=new Vector2(0,1);lamp.rectTransform.anchoredPosition=new Vector2(30,-163);lamp.rectTransform.sizeDelta=new Vector2(8,40);hud.TrackingLamp=lamp;
        hud.StateText=Text(root,"State","Ready - press START KINECT",17,new Vector2(50,-157),new Vector2(345,60));
        Button(root,"Start","START KINECT",new Vector2(30,-231),new Vector2(175,42),rig.StartKinect,true);
        Button(root,"Stop","STOP",new Vector2(218,-231),new Vector2(175,42),rig.StopKinect);
        Text(root,"PlacementHeading","01   SENSOR PLACEMENT",14,new Vector2(30,-296),new Vector2(370,25),new Color(.5f,.67f,.78f));
        Button(root,"ScreenPreset","1 / SCREEN",new Vector2(30,-331),new Vector2(112,37),()=>rig.ApplyPreset(0));
        Button(root,"DiagonalPreset","2 / 45 DEG",new Vector2(153,-331),new Vector2(117,37),()=>rig.ApplyPreset(1));
        Button(root,"SidePreset","3 / SIDE",new Vector2(281,-331),new Vector2(112,37),()=>rig.ApplyPreset(2));
        // Anonymous listeners are not serialized; wire these named wrappers instead below.
        var buttons=canvasGo.GetComponentsInChildren<UnityEngine.UI.Button>();
        foreach(var button in buttons)
        {
            if(button.name=="ScreenPreset") UnityEventTools.AddIntPersistentListener(button.onClick,rig.ApplyPreset,0);
            if(button.name=="DiagonalPreset") UnityEventTools.AddIntPersistentListener(button.onClick,rig.ApplyPreset,1);
            if(button.name=="SidePreset") UnityEventTools.AddIntPersistentListener(button.onClick,rig.ApplyPreset,2);
        }
        Button(root,"Opposite","SWAP SENSOR SIDE",new Vector2(30,-380),new Vector2(363,35),rig.FlipSide);
        hud.PlacementText=Text(root,"Placement","Diagonal45",14,new Vector2(30,-431),new Vector2(370,65));
        Text(root,"PhysicalNote","Presets move the virtual rig only.\nMove the physical Kinect to match.",13,new Vector2(30,-503),new Vector2(370,42),new Color(1f,.72f,.38f));
        Text(root,"CalHeading","02   REGISTER YOUR NEUTRAL STANCE",14,new Vector2(30,-568),new Vector2(380,25),new Color(.5f,.67f,.78f));
        Button(root,"Calibrate","CALIBRATE / C",new Vector2(30,-604),new Vector2(363,42),rig.BeginCalibration,true);
        hud.CalibrationText=Text(root,"Calibration","NEUTRAL REQUIRED",15,new Vector2(30,-660),new Vector2(370,30));
        Button(root,"Release","RELEASE BODY",new Vector2(30,-709),new Vector2(175,36),rig.ReleaseBody);
        Button(root,"ResetStats","RESET STATS",new Vector2(218,-709),new Vector2(175,36),rig.ResetStatistics);
        Button(root,"Simulation","SIMULATION / ARROW KEYS",new Vector2(30,-766),new Vector2(363,40),rig.UseSimulation);
        Text(root,"EditHint","Edit SensorOrigin position + rotation in Inspector.\nTransform changes require recalibration.",12,new Vector2(30,-827),new Vector2(380,40),new Color(.5f,.67f,.78f));
        hud.TelemetryText=Text(root,"Telemetry","",15,new Vector2(456,-121),new Vector2(360,280));
        Text(root,"TopViewLabel","TOP VIEW / DISPLAY AT TOP",12,new Vector2(1280,-45),new Vector2(300,23),new Color(.5f,.67f,.78f));
        hud.AngleText=Text(root,"Angle","LEAN 0.0",20,new Vector2(540,-710),new Vector2(980,35));
        var rawTrack=Panel(root,"RawTrack",new Vector2(.64f,0),new Vector2(.64f,0),new Color(.11f,.18f,.24f));
        rawTrack.rectTransform.sizeDelta=new Vector2(420,6);rawTrack.rectTransform.anchoredPosition=new Vector2(0,110);
        hud.RawMarker=Marker(rawTrack.transform,"RawMarker",new Color(1,.68f,.28f),new Vector2(5,22));
        var smoothTrack=Panel(root,"FilteredTrack",new Vector2(.64f,0),new Vector2(.64f,0),new Color(.11f,.18f,.24f));
        smoothTrack.rectTransform.sizeDelta=new Vector2(420,6);smoothTrack.rectTransform.anchoredPosition=new Vector2(0,73);
        hud.SmoothMarker=Marker(smoothTrack.transform,"SmoothedMarker",new Color(.22f,.95f,.78f),new Vector2(10,23));
        Text(root,"LeftHint","LEFT",12,new Vector2(751,-807),new Vector2(100,22),new Color(.5f,.67f,.78f));
        Text(root,"RightHint","RIGHT",12,new Vector2(1235,-807),new Vector2(100,22),new Color(.5f,.67f,.78f));
        Text(root,"Footer","AMBER: RAW / PREDICTED    TEAL: FILTERED / TRACKED     Result age excludes camera + inference latency.",12,new Vector2(458,-865),new Vector2(1100,23),new Color(.5f,.67f,.78f));
        var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
    static RectTransform Marker(Transform parent,string name,Color color,Vector2 size)
    {var item=Panel(parent,name,new Vector2(.5f,.5f),new Vector2(.5f,.5f),color);item.rectTransform.sizeDelta=size;return item.rectTransform;}
    static void Button(Transform parent,string name,string label,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action,bool bright=false)
    {
        var image=Panel(parent,name,new Vector2(0,1),new Vector2(0,1),bright?new Color(.06f,.29f,.30f):new Color(.075f,.13f,.18f));
        var rt=image.rectTransform;rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;image.raycastTarget=true;
        var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        if(action.Target is UnityEngine.Object) UnityEventTools.AddPersistentListener(button.onClick,action);
        var text=Text(image.transform,"Label",label,13,Vector2.zero,size);
        text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=Vector2.zero;text.rectTransform.offsetMax=Vector2.zero;
        text.alignment=TextAlignmentOptions.Center;
    }
    static TextMeshProUGUI Text(Transform parent,string name,string value,float size,Vector2 position,Vector2 dimensions,Color? color=null)
    {
        var text=new GameObject(name,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();text.transform.SetParent(parent,false);
        text.font=font;text.fontSize=size;text.color=color??textColor;text.text=value;text.raycastTarget=false;
        text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;
        var rt=text.rectTransform;rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=dimensions;
        return text;
    }
    static UnityEngine.UI.Image Panel(Transform parent,string name,Vector2 min,Vector2 max,Color color)
    {
        var image=new GameObject(name,typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();image.transform.SetParent(parent,false);
        image.color=color;image.raycastTarget=false;image.rectTransform.anchorMin=min;image.rectTransform.anchorMax=max;
        image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;return image;
    }
    static Material Material(string name,Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new InvalidOperationException("URP shader missing");
        var material=new Material(shader){name=name};material.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(material,MaterialPath+"/"+name+".mat");return material;
    }
    static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material,Transform parent)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static void Line(string name,Vector3 a,Vector3 b,float width,Material material,Transform parent,bool world=true)
    {
        var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);line.useWorldSpace=world;
        line.positionCount=2;line.SetPosition(0,a);line.SetPosition(1,b);line.startWidth=line.endWidth=width;line.sharedMaterial=material;
    }
    static void WorldLabel(string text,Vector3 position,float size,Transform parent,Vector3? rotation=null)
    {
        var label=new GameObject(text).AddComponent<TextMeshPro>();label.transform.SetParent(parent,false);label.transform.position=position;
        label.transform.eulerAngles=rotation??new Vector3(0,0,0);label.font=font;label.fontSize=size*10;label.text=text;label.alignment=TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta=new Vector2(5,1);label.color=new Color(.5f,.75f,.9f);
    }
    static void Folders(string path)
    {
        string[] parts=path.Split('/');string current=parts[0];
        for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
    }
}

