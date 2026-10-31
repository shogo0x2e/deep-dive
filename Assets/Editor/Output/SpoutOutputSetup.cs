using HighwaySurfing.Output;
using Klak.Spout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HighwaySurfing.EditorTools
{
    public static class SpoutOutputSetup
    {
        [MenuItem("Tools/Highway Surfing/Configure Main Camera Spout Output")]
        public static void ConfigureMainCamera()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play mode before configuring Spout output.");
            var camera = Camera.main;
            if (camera == null) throw new System.InvalidOperationException("No Main Camera in the active scene.");
            var resources = AssetDatabase.LoadAssetAtPath<SpoutResources>(
                "Packages/jp.keijiro.klak.spout/Editor/SpoutResources.asset");
            if (resources == null) throw new System.InvalidOperationException("KlakSpout resources are missing.");
            var sender = camera.GetComponent<SpoutSender>();
            if (sender == null) sender = Undo.AddComponent<SpoutSender>(camera.gameObject);
            Undo.RecordObject(sender, "Configure Main Spout sender");
            sender.enabled = false;
            sender.SetResources(resources);
            sender.spoutName = "HighwaySurfing.Main";
            sender.captureMethod = CaptureMethod.Texture;
            sender.keepAlpha = false;
            var output = camera.GetComponent<MainCameraSpoutOutput>();
            if (output == null) output = Undo.AddComponent<MainCameraSpoutOutput>(camera.gameObject);
            Undo.RecordObject(output, "Configure Main Camera output");
            output.Configure(camera, resources, GetOrCreateProjectionDebugView(camera.gameObject.scene));
            PlayerSpeedViewSetup.Configure(camera);
            EditorUtility.SetDirty(sender);
            EditorUtility.SetDirty(output);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        }

        private static ProjectionDebugView GetOrCreateProjectionDebugView(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var existing in Object.FindObjectsByType<ProjectionDebugView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing.gameObject.scene == scene) return existing;
            }

            var go = new GameObject("ProjectionDebugView");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create projection debug view");
            var view = Undo.AddComponent<ProjectionDebugView>(go);
            go.SetActive(false);
            return view;
        }
    }
}
