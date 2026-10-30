using UnityEngine;

namespace HighwaySurfing.Kinect
{
    public static class SurfPoseDetector
    {
        public static bool IsOpen(Vector3 pelvis, Vector3 chest, Vector3 leftShoulder, Vector3 rightShoulder,
            Vector3 leftWrist, Vector3 rightWrist, Vector3 up, Vector3 screenRight)
        {
            Vector3 shoulders = leftShoulder - rightShoulder;
            Vector3 left = leftWrist - leftShoulder, right = rightWrist - rightShoulder;
            Vector3 torso = chest - pelvis;
            if (!Finite(shoulders) || !Finite(left) || !Finite(right) || !Finite(torso)) return false;
            float width = shoulders.magnitude, height = Vector3.Dot(torso, up);
            if (width < .14f || width > .8f || height < .12f || torso.magnitude > .9f) return false;
            if (Mathf.Abs(Mathf.Atan2(Vector3.Dot(torso, screenRight), height) * Mathf.Rad2Deg) > 12) return false;
            float minimumReach = Mathf.Clamp(width * .9f, .2f, .35f);
            return Arm(left, shoulders.normalized, up, minimumReach) && Arm(right, -shoulders.normalized, up, minimumReach);
        }
        static bool Arm(Vector3 arm, Vector3 outward, Vector3 up, float minimumReach)
        {
            float reach = arm.magnitude;
            return reach >= minimumReach && reach < 1.1f && Vector3.Dot(arm.normalized, outward) > .72f &&
                Mathf.Abs(Vector3.Dot(arm, up)) <= Mathf.Max(.14f, reach * .4f);
        }
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
