using UnityEngine;

namespace HighwaySurfing
{
    /// <summary>Three recordings with measured reference tones, blended at a common motor pitch.</summary>
    [CreateAssetMenu(menuName = "Highway Surfing/Motor Loops")]
    public sealed class HighwayMotorLoops : ScriptableObject
    {
        public AudioClip Low, Medium, High;
        public Vector3 SpeedKmh = new Vector3(70, 138, 220);
        public Vector3 ReferenceHz = new Vector3(1600, 2050, 2400);
        [Range(.5f, 1.5f)] public float PitchScale = .85f;
        public bool IsReady => Low != null && Medium != null && High != null &&
            SpeedKmh.x > 0 && SpeedKmh.y > SpeedKmh.x && SpeedKmh.z > SpeedKmh.y &&
            ReferenceHz.x > 0 && ReferenceHz.y > ReferenceHz.x && ReferenceHz.z > ReferenceHz.y;

        public AudioClip Clip(int index) => index == 0 ? Low : index == 1 ? Medium : High;

        public void Evaluate(float speed, out Vector3 weights, out Vector3 pitches)
        {
            speed = Mathf.Clamp(speed, 0, SpeedKmh.z);
            float blend, hz;
            if (speed <= SpeedKmh.y)
            {
                blend = Mathf.InverseLerp(SpeedKmh.x, SpeedKmh.y, speed);
                weights = new Vector3(Mathf.Cos(blend * Mathf.PI / 2), Mathf.Sin(blend * Mathf.PI / 2), 0);
                hz = speed < SpeedKmh.x ? Mathf.Lerp(ReferenceHz.x * .78f, ReferenceHz.x, speed / SpeedKmh.x) :
                    Mathf.Lerp(ReferenceHz.x, ReferenceHz.y, blend);
            }
            else
            {
                blend = Mathf.InverseLerp(SpeedKmh.y, SpeedKmh.z, speed);
                weights = new Vector3(0, Mathf.Cos(blend * Mathf.PI / 2), Mathf.Sin(blend * Mathf.PI / 2));
                hz = Mathf.Lerp(ReferenceHz.y, ReferenceHz.z, blend);
            }
            // Adjacent recordings meet at the same measured tone; no gearshift or pitch reset.
            pitches = new Vector3(hz / ReferenceHz.x, hz / ReferenceHz.y, hz / ReferenceHz.z) * PitchScale;
        }
    }
}
