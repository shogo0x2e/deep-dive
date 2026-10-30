using UnityEngine;

namespace HighwaySurfing.Kinect
{
    /// <summary>Screen-facing arm pitch, with steering roll removed. Positive input accelerates.</summary>
    public static class SurfArmSpeedSolver
    {
        public static bool TrySelectFrontWrist(Vector3 left, Vector3 right, Transform display, out int wrist)
        {
            wrist = -1;
            if (display == null || !Finite(left) || !Finite(right)) return false;
            float separation = Vector3.Dot(right - left, display.forward);
            if (Mathf.Abs(separation) < .15f) return false;
            wrist = separation > 0 ? 14 : 7;
            return true;
        }

        public static bool TryMeasure(Vector3 shoulder, Vector3 wrist, Transform display, float torsoLean, out float degrees)
        {
            degrees = 0;
            Vector3 arm = wrist - shoulder;
            if (display == null || !Finite(arm) || !float.IsFinite(torsoLean) || arm.magnitude < .2f || arm.magnitude > 1.1f) return false;
            // A lean to screen-right rotates the skeleton by -lean around screen-forward.
            // Undo it so steering alone does not become accelerator/brake input.
            arm = Quaternion.AngleAxis(torsoLean, display.forward) * arm;
            float horizontal = Vector3.ProjectOnPlane(arm, display.up).magnitude;
            // Hanging/folded arms are neutral input, not full accelerator.
            if (horizontal < .18f || Vector3.Dot(arm, display.forward) < .12f) return false;
            degrees = Mathf.Atan2(Vector3.Dot(arm, display.up), horizontal) * Mathf.Rad2Deg;
            return float.IsFinite(degrees);
        }

        public static float Map(float relativeDegrees, float threshold, float fullScale)
        {
            if (!float.IsFinite(relativeDegrees)) return 0;
            threshold = Mathf.Max(0, threshold);
            fullScale = Mathf.Max(threshold + 1, fullScale);
            return -Mathf.Sign(relativeDegrees) * Mathf.Clamp01((Mathf.Abs(relativeDegrees) - threshold) / (fullScale - threshold));
        }
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
