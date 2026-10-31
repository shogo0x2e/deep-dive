using System;
using UnityEngine;

namespace HighwaySurfing.Kinect
{
    [Serializable]
    public struct KinectPresenceArea
    {
        public bool Enabled;
        [Tooltip("Pelvis depth from the sensor, in metres (near / far).")]
        public Vector2 DepthMetres;
        [Tooltip("Half-width to either side of the sensor, in metres.")]
        [Min(.1f)] public float HalfWidthMetres;

        public bool Contains(Vector3 sensorPosition, float extraMarginMetres = 0)
        {
            if (!float.IsFinite(sensorPosition.x) || !float.IsFinite(sensorPosition.y) ||
                !float.IsFinite(sensorPosition.z) || sensorPosition.z <= 0) return false;
            float margin = Mathf.Max(0, extraMarginMetres);
            return !Enabled || (Mathf.Abs(sensorPosition.x) <= Mathf.Max(.1f, HalfWidthMetres) + margin &&
                sensorPosition.z >= Mathf.Min(DepthMetres.x, DepthMetres.y) - margin &&
                sensorPosition.z <= Mathf.Max(DepthMetres.x, DepthMetres.y) + margin);
        }
    }
}
