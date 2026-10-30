using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HighwaySurfing.EditorTools
{
    public static class ArmSpeedSetup
    {
        [MenuItem("Tools/Highway Surfing/Enable Arm Accelerator and Brake")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity" || scene.isDirty) throw new InvalidOperationException("Open saved Main first.");
            var input = UnityEngine.Object.FindFirstObjectByType<KinectHighwayInput>();
            var speed = input.GetComponent<KinectArmSpeedInput>();
            if (speed == null) speed = input.gameObject.AddComponent<KinectArmSpeedInput>();
            speed.Input = input; speed.EnableArmSpeed = true; speed.StartingSpeedKmh = 138;
            input.World.SpeedKmh = 138;
            var hud = UnityEngine.Object.FindFirstObjectByType<KinectHighwayHud>();
            hud.ArmSpeed = speed; hud.TrackingLabel.fontSize = 14;
            SurfTutorialSetup.RefinePresentation();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
