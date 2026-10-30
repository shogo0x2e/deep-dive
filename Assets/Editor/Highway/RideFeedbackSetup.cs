using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HighwaySurfing.EditorTools
{
    /// <summary>Deterministic, original synthesis; no third-party audio or runtime DSP allocation.</summary>
    public static class RideFeedbackSetup
    {
        const int SampleRate = 48000;
        const string AudioFolder = "Assets/Audio/Highway";

        [MenuItem("Tools/Highway Surfing/Install Ride Sound and Light")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity" || scene.isDirty)
                throw new InvalidOperationException("Open saved Main first.");
            var world = UnityEngine.Object.FindFirstObjectByType<NightHighwayWorld>();
            if (world == null) throw new InvalidOperationException("Main has no highway world.");
            Directory.CreateDirectory(AudioFolder);
            WriteWave(AudioFolder + "/PassBy.wav", PassBy());
            WriteWave(AudioFolder + "/Motor.wav", Motor());
            WriteWave(AudioFolder + "/Departure.wav", Departure());
            AssetDatabase.Refresh();
            foreach (var name in new[] { "PassBy", "Motor", "Departure" })
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AudioFolder + "/" + name + ".wav");
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = false; importer.ambisonic = false;
                importer.SaveAndReimport();
            }
            const string materialPath = "Assets/Materials/Highway/RideStreak.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Highway/RideStreak");
                if (shader == null) throw new InvalidOperationException("RideStreak shader has not imported.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var feedback = world.GetComponent<HighwayRideFeedback>();
            if (feedback == null) feedback = world.gameObject.AddComponent<HighwayRideFeedback>();
            feedback.World = world;
            feedback.Input = UnityEngine.Object.FindFirstObjectByType<KinectHighwayInput>();
            feedback.StreakMaterial = material;
            feedback.PassByClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/PassBy.wav");
            feedback.MotorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/Motor.wav");
            feedback.DepartureClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/Departure.wav");
            EditorUtility.SetDirty(feedback);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        static float[] PassBy()
        {
            // A short, rounded air displacement. Silence at both ends, no continuous noise bed.
            int frames = (int)(SampleRate * .55);
            var result = new float[frames * 2];
            var rng = new System.Random(4731);
            double low = 0, mid = 0;
            for (int i = 0; i < frames; i++)
            {
                double t = (double)i / SampleRate;
                double noise = rng.NextDouble() * 2 - 1;
                double cutoff = .18 - .12 * Math.Min(1, t / .55);
                low += .018 * (noise - low); mid += cutoff * (noise - mid);
                double width = t < .15 ? .065 : .12;
                double envelope = Math.Exp(-Math.Pow((t - .15) / width, 2));
                envelope *= Math.Min(1, t / .025) * Math.Min(1, (.55 - t) / .025);
                float sound = (float)((mid - low) * envelope * 1.4);
                result[i * 2] = result[i * 2 + 1] = sound;
            }
            return result;
        }

        static float[] Motor()
        {
            var data = new float[SampleRate * 8 * 2];
            for (int i = 0; i < data.Length / 2; i++)
            {
                double t = (double)i / SampleRate;
                // Quiet electric drive: tonal rotor harmonics, a warm subharmonic and no noise.
                // Frequencies / phase modulation complete whole cycles at the loop boundary.
                double phase = 2 * Math.PI * 130 * t + .035 * Math.Sin(2 * Math.PI * .375 * t);
                double rotor = Math.Sin(phase) * .62 + Math.Sin(phase * 2) * .22 + Math.Sin(phase * 3) * .075 + Math.Sin(phase * 6) * .025;
                double body = Math.Sin(2 * Math.PI * 65 * t) * .18;
                float sound = (float)((rotor + body) * .28);
                data[i * 2] = data[i * 2 + 1] = sound;
            }
            return data;
        }

        static float[] Departure()
        {
            var data = new float[(int)(SampleRate * 1.15) * 2];
            double phase = 0;
            for (int i = 0; i < data.Length / 2; i++)
            {
                double t = (double)i / SampleRate;
                double u = Math.Min(1, t / .60), sweep = u * u * (3 - 2 * u);
                phase += 2 * Math.PI * (330 + 330 * sweep) / SampleRate;
                double attack = Math.Min(1, t / .045); attack = attack * attack * (3 - 2 * attack);
                double release = Math.Max(0, 1 - t / 1.15);
                double envelope = attack * release * release;
                float sound = (float)((Math.Sin(phase) + Math.Sin(phase * 2) * .12) * envelope * .28);
                data[i * 2] = sound; data[i * 2 + 1] = sound;
            }
            return data;
        }

        static void WriteWave(string path, float[] samples)
        {
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)2); writer.Write(SampleRate); writer.Write(SampleRate * 4);
            writer.Write((short)4); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            foreach (float sample in samples) writer.Write((short)Math.Round(Math.Clamp(sample, -1, 1) * 32767));
        }
    }
}
