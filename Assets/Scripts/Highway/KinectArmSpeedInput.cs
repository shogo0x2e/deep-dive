using HighwaySurfing.Kinect;
using UnityEngine;

namespace HighwaySurfing
{
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(KinectHighwayInput))]
    public sealed class KinectArmSpeedInput : MonoBehaviour
    {
        public KinectHighwayInput Input;
        public bool EnableArmSpeed = true;
        [Range(0, 220)] public float StartingSpeedKmh = 138;
        [Range(2, 20)] public float ThresholdDegrees = 8;
        [Range(15, 60)] public float FullScaleDegrees = 28;
        [Range(1, 80)] public float AccelerationKmhPerSecond = 30;
        [Range(1, 120)] public float BrakingKmhPerSecond = 60;
        [Range(.02f, .4f)] public float SmoothingSeconds = .12f;
        public bool Calibrated { get; private set; }
        public bool ArmAvailable { get; private set; }
        public int FrontWrist { get; private set; } = -1;
        public float RelativeDegrees { get; private set; }
        public float Control { get; private set; }
        public string Status { get; private set; } = "Waiting for surfing pose";

        float neutral, sum, minimum, maximum;
        int sampleCount, candidate = -1;
        long lastSample = -1;
        uint body;
        bool wasCalibrating, wasRegistered;

        void Awake() { if (Input == null) Input = GetComponent<KinectHighwayInput>(); }
        void Update()
        {
            if (Input == null || Input.Rig == null || Input.World == null) return;
            var rig = Input.Rig;
            bool registered = Input.RideRegistered;
            if (!EnableArmSpeed || !Input.KinectMode)
            { ResetCalibration(); wasRegistered = wasCalibrating = false; Status = "Arm speed off"; return; }

            if ((!registered && !rig.Calibrating) || (rig.Calibrating && !wasCalibrating) || (Calibrated && body != rig.BodyId))
                ResetCalibration();
            // Collect only fresh sensor samples during the existing automatic T-pose registration.
            // If the front arm was ambiguous then, a stable T-pose can finish this automatically later.
            if (rig.Calibrating || (registered && !Calibrated && rig.SurfPoseReady)) CollectNeutral();
            if (registered && !rig.Calibrating && !Calibrated && sampleCount >= 8 && maximum - minimum <= 6)
            {
                FrontWrist = candidate; neutral = sum / sampleCount; body = rig.BodyId; Calibrated = true;
            }
            if (registered && !wasRegistered) Input.World.SpeedKmh = StartingSpeedKmh;
            wasRegistered = registered; wasCalibrating = rig.Calibrating;

            float angle = 0;
            ArmAvailable = registered && Calibrated && Input.InputAvailable && Measure(FrontWrist, out angle);
            if (!ArmAvailable)
            {
                Control = RelativeDegrees = 0;
                Status = !registered ? "Waiting for surfing pose" : !Calibrated ? "Hold arms forward/back to set speed" : "Arm lost - holding speed";
                return;
            }
            RelativeDegrees = angle - neutral;
            float target = SurfArmSpeedSolver.Map(RelativeDegrees, ThresholdDegrees, FullScaleDegrees);
            // Inside the threshold, stop changing speed immediately; smooth only active input.
            Control = target == 0 ? 0 : Mathf.Lerp(Control, target, 1 - Mathf.Exp(-Time.deltaTime / Mathf.Max(.001f, SmoothingSeconds)));
            if (!Input.World.Paused)
            {
                float rate = Control > 0 ? AccelerationKmhPerSecond : BrakingKmhPerSecond;
                Input.World.SpeedKmh = Mathf.Clamp(Input.World.SpeedKmh + Control * rate * Mathf.Min(Time.deltaTime, .05f), 0, 220);
            }
            Status = target > 0 ? "ACCEL" : target < 0 ? "BRAKE" : "CRUISE";
        }

        void CollectNeutral()
        {
            var rig = Input.Rig;
            if (lastSample == rig.Samples) return;
            lastSample = rig.Samples;
            if (!rig.TryGetJointPosition(7, out var left) || !rig.TryGetJointPosition(14, out var right) ||
                !SurfArmSpeedSolver.TrySelectFrontWrist(left, right, rig.ScreenReference, out int front) || !Measure(front, out float angle))
            { sampleCount = 0; candidate = -1; return; }
            if (front != candidate || sampleCount == 0 || Mathf.Max(maximum, angle) - Mathf.Min(minimum, angle) > 6)
            { candidate = front; sampleCount = 0; sum = 0; minimum = maximum = angle; }
            sum += angle; sampleCount++; minimum = Mathf.Min(minimum, angle); maximum = Mathf.Max(maximum, angle);
        }
        bool Measure(int wrist, out float angle)
        {
            angle = 0;
            var rig = Input.Rig;
            return (wrist == 7 || wrist == 14) && rig.TryGetJointPosition(wrist, out var hand) &&
                rig.TryGetJointPosition(wrist == 7 ? 5 : 12, out var shoulder) &&
                SurfArmSpeedSolver.TryMeasure(shoulder, hand, rig.ScreenReference, rig.MeasuredDegrees, out angle);
        }
        void ResetCalibration()
        {
            Calibrated = ArmAvailable = false; FrontWrist = candidate = -1;
            Control = RelativeDegrees = 0; sampleCount = 0; sum = 0; lastSample = -1;
        }
        void OnDisable() { ResetCalibration(); wasRegistered = wasCalibrating = false; }
    }
}
