using System;
using UnityEngine;

namespace HighwaySurfing
{
    /// <summary>Original, seamless low drive layer. No per-frame synthesis or audio-thread allocation.</summary>
    public static class HighwayDriveBass
    {
        public const int SampleRate = 22050;
        public const int Seconds = 4;

        public static AudioClip CreateClip()
        {
            var samples = CreateSamples();
            var clip = AudioClip.Create("Low drive thrust", samples.Length, 1, SampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        public static float[] CreateSamples()
        {
            var samples = new float[SampleRate * Seconds];
            var phases = new double[24];
            var random = new System.Random(86747);
            for (int i = 0; i < phases.Length; i++) phases[i] = random.NextDouble() * Math.PI * 2;
            double peak = 0, mean = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)SampleRate;
                double turn = Math.PI * 2 * t;
                // Every frequency is a multiple of 1 / Seconds, including modulation,
                // so the waveform and its slope meet at the loop boundary.
                double rotor = turn * 66 + .38 * Math.Sin(turn * 1.25) + .12 * Math.Sin(turn * 3.5);
                double tone = .46 * Math.Sin(rotor) + .26 * Math.Sin(2 * rotor + .2) +
                    .15 * Math.Sin(3 * rotor + .5) + .07 * Math.Sin(4 * rotor + .8);
                double rumble = 0;
                for (int band = 0; band < phases.Length; band++)
                {
                    double hz = 38 + band * 8.25;
                    rumble += Math.Sin(turn * hz + phases[band]) / Math.Sqrt(hz / 38);
                }
                double breath = .94 + .04 * Math.Sin(turn * .75) + .02 * Math.Sin(turn * 1.75);
                double sample = (tone + rumble * .055 + .075 * Math.Sin(turn * 33)) * breath;
                samples[i] = (float)sample;
                mean += sample; peak = Math.Max(peak, Math.Abs(sample));
            }
            mean /= samples.Length;
            double gain = .78 / (peak + Math.Abs(mean));
            for (int i = 0; i < samples.Length; i++) samples[i] = (float)((samples[i] - mean) * gain);
            return samples;
        }

        public static float Pitch(float speedKmh) => Mathf.Lerp(.82f, 1.28f, Mathf.Clamp01(speedKmh / 220));

        public static float Gain(float speedKmh, float control)
        {
            float speed = Mathf.Clamp01(speedKmh / 220);
            float motion = Mathf.SmoothStep(0, 1, Mathf.Clamp01(speedKmh / 35));
            float load = Mathf.Clamp01(control), brake = Mathf.Clamp01(-control);
            return motion * (Mathf.Lerp(.30f, .86f, speed) + load * .18f) * (1 - brake * .5f);
        }
    }
}
