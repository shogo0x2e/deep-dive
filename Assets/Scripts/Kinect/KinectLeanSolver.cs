using UnityEngine;

namespace HighwaySurfing.Kinect
{
    /// <summary>Uses world positions in a fixed display coordinate frame, never camera-local X.</summary>
    public sealed class KinectLeanSolver
    {
        public bool Calibrated { get; private set; }
        public float NeutralDegrees { get; private set; }
        public float NeutralPelvisX { get; private set; }
        public float RawDegrees { get; private set; }
        public float RawSteer { get; private set; }
        public float SmoothedSteer { get; private set; }
        public float PelvisOffset { get; private set; }
        public float DeadZoneDegrees = 3f, FullScaleDegrees = 18f, SmoothingSeconds = 0.08f;
        public void Reset()
        { Calibrated = false; RawDegrees = RawSteer = SmoothedSteer = PelvisOffset = 0; }
        public static bool Measure(Vector3 pelvis, Vector3 chest, Transform display, out float degrees, out float pelvisX)
        {
            degrees = pelvisX = 0;
            Vector3 delta = chest - pelvis;
            if (display == null || !Finite(delta) || !Finite(pelvis) || delta.magnitude < 0.12f || delta.magnitude > 0.9f) return false;
            float up = Vector3.Dot(delta, display.up);
            if (up < 0.10f) return false;
            degrees = Mathf.Atan2(Vector3.Dot(delta, display.right), up) * Mathf.Rad2Deg;
            pelvisX = Vector3.Dot(pelvis - display.position, display.right);
            return true;
        }
        public void Calibrate(float degrees, float pelvisX)
        {
            NeutralDegrees = degrees; NeutralPelvisX = pelvisX; Calibrated = true;
            RawDegrees = RawSteer = SmoothedSteer = PelvisOffset = 0;
        }
        public bool Step(Vector3 pelvis, Vector3 chest, Transform display, bool valid, float age, float maxAge, float dt)
        {
            if (!valid || age > maxAge || age < 0 || !Calibrated || !Measure(pelvis, chest, display, out float angle, out float pelvisX))
            { RawDegrees = RawSteer = SmoothedSteer = PelvisOffset = 0; return false; }
            RawDegrees = Mathf.DeltaAngle(NeutralDegrees, angle);
            PelvisOffset = pelvisX - NeutralPelvisX;
            float zone = Mathf.Clamp(DeadZoneDegrees, 0, 20);
            float full = Mathf.Max(zone + 1, FullScaleDegrees);
            RawSteer = Mathf.Sign(RawDegrees) * Mathf.Clamp01((Mathf.Abs(RawDegrees) - zone) / (full - zone));
            float alpha = 1 - Mathf.Exp(-Mathf.Max(0, dt) / Mathf.Max(0.001f, SmoothingSeconds));
            SmoothedSteer = Mathf.Lerp(SmoothedSteer, RawSteer, alpha);
            return true;
        }
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
