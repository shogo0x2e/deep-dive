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
    public static class NightHighwayBuilder
    {
        [MenuItem("Tools/Highway Surfing/Create Main World")]
        public static void CreateMain()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("An open scene has unsaved changes. Save it before creating Main.");
            const string path="Assets/Scenes/Main.unity";
            if(File.Exists(path))throw new InvalidOperationException("Main already exists; edit the existing scene instead of replacing it.");
            if(UniversalRenderPipeline.asset==null)throw new InvalidOperationException("URP must be active.");
            if(!UniversalRenderPipeline.asset.supportsHDR)throw new InvalidOperationException("Enable HDR on the active URP asset.");
            Directory.CreateDirectory("Assets/Materials/Highway");
            Directory.CreateDirectory("Assets/Settings/Highway");
            AssetDatabase.Refresh();
            var surface=MaterialAsset("Highway/Surface","Assets/Materials/Highway/RoadAndCity.mat");
            var glow=MaterialAsset("Highway/Glow","Assets/Materials/Highway/LightPoints.mat");
            var sky=MaterialAsset("Highway/Sky","Assets/Materials/Highway/NightSky.mat");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            scene.name="Main";
            var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";
            var camera=cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags=CameraClearFlags.Skybox;
            camera.backgroundColor=new Color(.002f,.004f,.012f);
            camera.nearClipPlane=.10f;camera.farClipPlane=2200;camera.allowHDR=true;
            camera.fieldOfView=76;
            var data=camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=true;data.volumeLayerMask=1;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            RenderSettings.skybox=sky;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.015f,.02f,.035f);
            RenderSettings.fog=false;
            var world=new GameObject("NightHighway").AddComponent<NightHighwayWorld>();
            world.MainCamera=camera;world.SurfaceMaterial=surface;world.GlowMaterial=glow;
            world.RefreshPreview();
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="NightHighwayVolume";
            var bloom=profile.Add<Bloom>(true);bloom.threshold.value=.8f;bloom.intensity.value=.65f;bloom.scatter.value=.65f;
            bloom.highQualityFiltering.value=true;bloom.clamp.value=15;
            var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;
            var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.22f;vignette.smoothness.value=.50f;
            var adjustments=profile.Add<ColorAdjustments>(true);adjustments.postExposure.value=.4f;adjustments.contrast.value=5;adjustments.saturation.value=8;
            AssetDatabase.CreateAsset(profile,"Assets/Settings/Highway/NightHighwayVolume.asset");
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
            var volume=new GameObject("NightAtmosphere").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
            CreateHud(world);
            SpoutOutputSetup.ConfigureMainCamera();
            PlayerSettings.runInBackground=true;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
            EditorSceneManager.SaveScene(scene,path);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject=world.gameObject;
            Debug.Log("Main created: night highway, camera, atmosphere, operator HUD and Spout camera output.");
        }

        private static Material MaterialAsset(string shaderName,string path)
        {
            var shader=Shader.Find(shaderName);if(shader==null)throw new InvalidOperationException("Missing shader: "+shaderName);
            var material=new Material(shader){name=Path.GetFileNameWithoutExtension(path)};
            AssetDatabase.CreateAsset(material,path);return material;
        }

        private static void CreateHud(NightHighwayWorld world)
        {
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if(font==null)throw new InvalidOperationException("Import bundled TMP Essential Resources before creating the HUD.");
            var root=new GameObject("OperatorHud",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(CanvasGroup));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=50;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var hud=root.AddComponent<NightHighwayHud>();hud.World=world;hud.Group=root.GetComponent<CanvasGroup>();
            hud.Group.interactable=false;hud.Group.blocksRaycasts=false;
            var title=Label(root.transform,"Title","HIGHWAY  /  SURFING",font,22,new Vector2(36,-30),new Vector2(600,40),new Vector2(0,1));
            title.characterSpacing=6;title.color=new Color(.65f,.76f,.86f,.8f);
            hud.StateLabel=Label(root.transform,"RideState","NIGHT RIDE",font,16,new Vector2(38,-75),new Vector2(500,30),new Vector2(0,1));
            hud.StateLabel.characterSpacing=6;hud.StateLabel.color=new Color(.83f,.59f,.30f,.85f);
            hud.SpeedLabel=Label(root.transform,"Speed","138",font,56,new Vector2(38,74),new Vector2(190,80),new Vector2(0,0));
            var units=Label(root.transform,"Units","KM/H",font,14,new Vector2(42,42),new Vector2(130,25),new Vector2(0,0));units.characterSpacing=5;
            hud.DistanceLabel=Label(root.transform,"Distance","0.00 KM",font,20,new Vector2(-38,70),new Vector2(250,40),new Vector2(1,0));hud.DistanceLabel.alignment=TextAlignmentOptions.Right;
            var controls=Label(root.transform,"Controls","A / D  STEER    W / S  SPEED    SPACE  PAUSE    H  HUD",font,13,new Vector2(-60,36),new Vector2(1000,30),new Vector2(1,0));controls.alignment=TextAlignmentOptions.Right;controls.characterSpacing=1;controls.color=new Color(.65f,.75f,.84f,.65f);
        }

        private static TextMeshProUGUI Label(Transform parent,string name,string text,TMP_FontAsset font,int size,Vector2 position,Vector2 dimensions,Vector2 anchor)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.pivot=anchor;rect.anchoredPosition=position;rect.sizeDelta=dimensions;
            var label=go.GetComponent<TextMeshProUGUI>();label.font=font;label.fontSize=size;label.text=text;label.color=new Color(.8f,.9f,1,.9f);label.raycastTarget=false;
            return label;
        }
    }
}
