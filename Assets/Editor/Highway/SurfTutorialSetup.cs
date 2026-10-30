using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Rendering.Universal;

namespace HighwaySurfing.EditorTools
{
    public static class SurfTutorialSetup
    {
        [MenuItem("Tools/Highway Surfing/Add Automatic Surf Tutorial")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/Main.unity"||scene.isDirty)throw new InvalidOperationException("Open saved Main first.");
            if(UnityEngine.Object.FindFirstObjectByType<SurfTutorialView>()!=null)throw new InvalidOperationException("Tutorial already exists.");
            var input=UnityEngine.Object.FindFirstObjectByType<KinectHighwayInput>();
            if(input==null)throw new InvalidOperationException("Integrate Kinect first.");
            var font=CreateFont();
            var go=new GameObject("PlayerTutorial",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(CanvasGroup));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=input.World.MainCamera;canvas.planeDistance=1;canvas.sortingOrder=100;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var view=go.AddComponent<SurfTutorialView>();view.Input=input;view.Group=go.GetComponent<CanvasGroup>();view.Group.blocksRaycasts=view.Group.interactable=false;
            var shade=Box(go.transform,"Backdrop",Vector2.zero,new Vector2(1920,1080),new Color(.006f,.02f,.035f,.72f));
            var sr=(RectTransform)shade.transform;sr.anchorMin=Vector2.zero;sr.anchorMax=Vector2.one;sr.offsetMin=sr.offsetMax=Vector2.zero;
            var card=Box(go.transform,"WelcomeCard",Vector2.zero,new Vector2(1120,640),new Color(.97f,.985f,.975f));
            var stripe=Box(card.transform,"Accent",new Vector2(-552,0),new Vector2(16,640),new Color(.12f,.66f,.59f));
            Label(card.transform,"Brand","HIGHWAY SURFING",font,20,new Vector2(-500,260),new Vector2(700,40),new Color(.2f,.45f,.46f));
            Label(card.transform,"Welcome","夜のハイウェイへようこそ",font,20,new Vector2(-500,220),new Vector2(700,35),new Color(.35f,.45f,.48f));
            var art=new GameObject("OpenArmsIllustration",typeof(RectTransform),typeof(CanvasRenderer),typeof(SurfPoseGraphic));art.transform.SetParent(card.transform,false);
            var ar=(RectTransform)art.transform;ar.anchorMin=ar.anchorMax=ar.pivot=new Vector2(.5f,.5f);ar.anchoredPosition=new Vector2(-295,-20);ar.sizeDelta=new Vector2(440,440);
            art.GetComponent<SurfPoseGraphic>().raycastTarget=false;
            view.Title=Label(card.transform,"Title","手を前と後ろにひろげてください",font,36,new Vector2(-40,140),new Vector2(535,135),new Color(.10f,.22f,.27f));
            view.Instruction=Label(card.transform,"Instruction","肩の高さで腕をのばして、サーフィンのポーズ！",font,23,new Vector2(-40,-10),new Vector2(525,100),new Color(.25f,.37f,.40f));
            var track=Box(card.transform,"ProgressTrack",new Vector2(210,-135),new Vector2(500,12),new Color(.81f,.88f,.86f));
            var fill=Box(track.transform,"Progress",Vector2.zero,new Vector2(500,12),new Color(.12f,.66f,.59f)).GetComponent<UnityEngine.UI.Image>();
            fill.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fill.fillOrigin=0;fill.fillAmount=0;view.ProgressFill=fill;
            view.ProgressText=Label(card.transform,"ProgressText","ポーズを合わせると、自動で始まります",font,18,new Vector2(-40,-160),new Vector2(525,70),new Color(.25f,.48f,.46f));
            Label(card.transform,"SteeringHint",SurfTutorialView.ControlsHint,font,21,new Vector2(-500,-235),new Vector2(1010,75),new Color(.31f,.43f,.45f));
            input.EnableAutomaticCalibration=true;input.World.ShowHud=false;
            var hint=GameObject.Find("KinectOperatorPanel/Panel/Hint")?.GetComponent<TextMeshProUGUI>();
            if(hint!=null)hint.text="Open arms at shoulder height; hold the surfing pose.\nTutorial appears on OBS; this operator panel does not.";
            var controls=GameObject.Find("OperatorHud/Controls")?.GetComponent<TextMeshProUGUI>();
            if(controls!=null)controls.text="AUTO START: OPEN ARMS    SPACE  PAUSE    R  RESET    H  OPERATOR PANEL";
            ConfigureOverlay(view);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        public static void RefinePresentation()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            var view=UnityEngine.Object.FindFirstObjectByType<SurfTutorialView>();
            if(view==null)throw new InvalidOperationException("Tutorial missing.");
            var graphic=view.GetComponentInChildren<SurfPoseGraphic>();
            if(graphic.GetComponent<CanvasRenderer>()==null)graphic.gameObject.AddComponent<CanvasRenderer>();
            graphic.SetAllDirty();
            var font=CreateFont();
            var additions=new string(SurfTutorialView.FontCharacters.Distinct().Where(c=>!char.IsControl(c)&&!font.HasCharacter(c)).ToArray());
            if(additions.Length>0)
            {
                font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
                var serializedFont=new SerializedObject(font);
                serializedFont.FindProperty("m_SourceFontFile").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansJP/NotoSansCJKjp-Regular.otf");
                serializedFont.ApplyModifiedPropertiesWithoutUndo();
                if(!font.TryAddCharacters(additions,out string missing))throw new InvalidOperationException("Missing tutorial glyphs: "+missing);
            }
            font.atlasPopulationMode=AtlasPopulationMode.Static;EditorUtility.SetDirty(font);
            view.Title.text=view.Input.TutorialTitle;view.Instruction.text=view.Input.TutorialMessage;
            var hint=view.transform.Find("WelcomeCard/SteeringHint").GetComponent<TextMeshProUGUI>();
            hint.text=SurfTutorialView.ControlsHint;hint.rectTransform.anchoredPosition=new Vector2(-500,-235);hint.rectTransform.sizeDelta=new Vector2(1010,75);
            ConfigureOverlay(view);
            var scene=view.gameObject.scene;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static void ConfigureOverlay(SurfTutorialView view)
        {
            var baseCamera=view.Input.World.MainCamera;
            var found=baseCamera.transform.Find("PlayerTutorialCamera");
            var data=baseCamera.GetUniversalAdditionalCameraData();
            if(found!=null)
            {
                data.cameraStack.Remove(found.GetComponent<Camera>());
                UnityEngine.Object.DestroyImmediate(found.gameObject);
            }
            baseCamera.cullingMask |= 1<<5;
            foreach(var child in view.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            var canvas=view.GetComponent<Canvas>();canvas.worldCamera=baseCamera;canvas.planeDistance=1;
        }
        static TMP_FontAsset CreateFont()
        {
            const string path="Assets/Fonts/NotoSansJP/TutorialJapanese.asset";
            var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(existing!=null)return existing;
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansJP/NotoSansCJKjp-Regular.otf");
            if(source==null)throw new InvalidOperationException("Japanese source font missing.");
            var font=TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            font.name="TutorialJapanese";
            var characters=new string((SurfTutorialView.FontCharacters+new string(Enumerable.Range(32,95).Select(x=>(char)x).ToArray())).Where(c=>!char.IsControl(c)).Distinct().ToArray());
            if(!font.TryAddCharacters(characters,out string missing))throw new InvalidOperationException("Missing tutorial glyphs: "+missing);
            font.atlasPopulationMode=AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font,path);
            foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
            AssetDatabase.AddObjectToAsset(font.material,font);EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();return font;
        }
        static GameObject Box(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return go;
        }
        static TextMeshProUGUI Label(Transform parent,string name,string value,TMP_FontAsset font,float size,Vector2 position,Vector2 dimensions,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=dimensions;
            var text=go.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.text=value;text.color=color;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
    }
}
