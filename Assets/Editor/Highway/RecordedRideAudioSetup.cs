using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HighwaySurfing.EditorTools
{
    public static class RecordedRideAudioSetup
    {
        const string Folder = "Assets/Audio/Highway/Recorded/";

        [MenuItem("Tools/Highway Surfing/Use Recorded Ride Audio")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity" || scene.isDirty)
                throw new InvalidOperationException("Open saved Main first.");
            var feedback = UnityEngine.Object.FindFirstObjectByType<HighwayRideFeedback>();
            if (feedback == null) throw new InvalidOperationException("Main has no ride feedback.");
            foreach (var name in new[] { "MotorLow", "MotorMedium", "MotorHigh", "W3CarPass", "W3StreetPass" })
            {
                var importer = AssetImporter.GetAtPath(Folder + name + ".wav") as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Prepare recorded clips first: " + name);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = false;
                importer.SaveAndReimport();
            }
            string path = Folder + "TeslaMotorLoops.asset";
            var bank = AssetDatabase.LoadAssetAtPath<HighwayMotorLoops>(path);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<HighwayMotorLoops>();
                AssetDatabase.CreateAsset(bank, path);
            }
            bank.Low = Clip("MotorLow"); bank.Medium = Clip("MotorMedium"); bank.High = Clip("MotorHigh");
            bank.SpeedKmh = new Vector3(70, 138, 220);
            bank.ReferenceHz = new Vector3(1692.1106f, 2033.5463f, 2403.6747f);
            bank.PitchScale = .85f;
            EditorUtility.SetDirty(bank);
            feedback.MotorLoops = bank;
            feedback.PassByClip = Clip("W3CarPass");
            feedback.StreetPassClip = Clip("W3StreetPass");
            feedback.RecordedPassBy = true;
            feedback.PassByPeakSeconds = .95f;
            feedback.StreetPassPeakSeconds = .2625f;
            EditorUtility.SetDirty(feedback);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name + ".wav");
    }
}
