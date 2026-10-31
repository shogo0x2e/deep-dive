using UnityEngine;

namespace HighwaySurfing.Kinect
{
    public static class SurfPoseDetector
    {
        public enum Adjustment { Ready, RaiseArms, RaiseMore, RaiseSlightly, LowerArms, LowerSlightly, ExtendArms, OpenArms, StraightenBody, ShowArms }
        public readonly struct Guidance
        {
            public readonly Adjustment Action;
            // 0 means both arms; otherwise Azure Kinect's anatomical wrist index.
            public readonly int Wrist;
            public readonly float Error;
            public Guidance(Adjustment action, int wrist = 0, float error = 0) { Action = action; Wrist = wrist; Error = error; }
        }
        public static bool IsOpen(Vector3 pelvis, Vector3 chest, Vector3 leftShoulder, Vector3 rightShoulder,
            Vector3 leftWrist, Vector3 rightWrist, Vector3 up, Vector3 screenRight, bool relaxed = false)
            => GetGuidance(pelvis, chest, leftShoulder, rightShoulder, leftWrist, rightWrist, up, screenRight, relaxed).Action == Adjustment.Ready;

        public static Guidance GetGuidance(Vector3 pelvis, Vector3 chest, Vector3 leftShoulder, Vector3 rightShoulder,
            Vector3 leftWrist, Vector3 rightWrist, Vector3 up, Vector3 screenRight, bool relaxed = false)
        {
            Vector3 shoulders = leftShoulder - rightShoulder;
            Vector3 left = leftWrist - leftShoulder, right = rightWrist - rightShoulder;
            Vector3 torso = chest - pelvis;
            if (!Finite(shoulders) || !Finite(left) || !Finite(right) || !Finite(torso)) return new Guidance(Adjustment.ShowArms);
            float width = shoulders.magnitude, height = Vector3.Dot(torso, up);
            if (width < .14f || width > .8f || height < .12f || torso.magnitude > .9f) return new Guidance(Adjustment.StraightenBody);
            if (Mathf.Abs(Mathf.Atan2(Vector3.Dot(torso, screenRight), height) * Mathf.Rad2Deg) > (relaxed ? 20 : 12)) return new Guidance(Adjustment.StraightenBody);
            float minimumReach = relaxed ? Mathf.Clamp(width * .6f, .2f, .28f) : Mathf.Clamp(width * .9f, .2f, .35f);
            var l = Arm(left, shoulders.normalized, up, minimumReach, 7, relaxed);
            var r = Arm(right, -shoulders.normalized, up, minimumReach, 14, relaxed);
            if (l.Action == Adjustment.Ready) return r;
            if (r.Action == Adjustment.Ready) return l;
            var worst = l.Error >= r.Error ? l : r;
            // For opposite corrections, name just the arm which needs the larger adjustment.
            return l.Action == r.Action ? new Guidance(worst.Action, 0, worst.Error) : worst;
        }
        static Guidance Arm(Vector3 arm, Vector3 outward, Vector3 up, float minimumReach, int wrist, bool relaxed)
        {
            float reach = arm.magnitude;
            if (reach < minimumReach) return new Guidance(Adjustment.ExtendArms, wrist, 1);
            if (reach >= 1.1f) return new Guidance(Adjustment.ShowArms, wrist, 1);
            float height = Vector3.Dot(arm, up), limit = relaxed ? Mathf.Max(.18f, reach * .72f) : Mathf.Max(.14f, reach * .4f);
            float ratio = height / reach;
            if (height < -limit)
                return new Guidance(ratio < -.85f ? Adjustment.RaiseArms : ratio < -.6f ? Adjustment.RaiseMore : Adjustment.RaiseSlightly, wrist, -ratio);
            if (height > limit)
                return new Guidance(ratio > .65f ? Adjustment.LowerArms : Adjustment.LowerSlightly, wrist, ratio);
            return Vector3.Dot(arm.normalized, outward) > (relaxed ? .45f : .72f) ? new Guidance(Adjustment.Ready) : new Guidance(Adjustment.OpenArms, wrist, .8f);
        }

        // Riding permits accelerator/brake pitch and steering lean. Only dropping/folding
        // the extended arms counts as leaving the surfing stance; this is not the start gate.
        public static bool IsRiding(Vector3 leftShoulder, Vector3 rightShoulder, Vector3 leftWrist, Vector3 rightWrist, Vector3 up, Vector3 screenForward, float torsoLean)
        {
            var unroll = Quaternion.AngleAxis(torsoLean, screenForward);
            var outward = Vector3.ProjectOnPlane(unroll * (leftShoulder - rightShoulder), up).normalized;
            var left = unroll * (leftWrist - leftShoulder); var right = unroll * (rightWrist - rightShoulder);
            return Finite(left) && Finite(right) && outward.sqrMagnitude > .5f &&
                left.magnitude >= .2f && left.magnitude < 1.1f && right.magnitude >= .2f && right.magnitude < 1.1f &&
                Vector3.Dot(left, outward) > .12f && Vector3.Dot(right, -outward) > .12f;
        }
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
