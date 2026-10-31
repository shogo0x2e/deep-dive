using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighwaySurfing.Kinect
{
    public sealed class KinectDebugRig : MonoBehaviour
    {
        public enum Placement { Screen, Diagonal45, Side90, Custom }
        public enum Backend { Cpu = 1, DirectML = 4 }
        [Header("Physical installation - edit this parent Transform")]
        public Transform SensorOrigin;
        public Transform ScreenReference;
        public Placement SensorPlacement = Placement.Diagonal45;
        public bool OppositeSide;
        [Min(1)] public float SensorDistance = 2.5f;
        public float SensorHeight = 1.1f;
        [Header("Native input (manual Start only)")]
        public string RuntimeDirectory = ".Local/AzureKinect/Runtime";
        public Backend ProcessingBackend = Backend.DirectML;
        public bool Use30Fps;
        [Range(0.1f, 1f)] public float MaximumFrameAge = 0.3f;
        [Header("Nearest person selection (sensor depth)")]
        [Min(0)] public float BodySwitchMargin = .25f;
        [Min(0)] public float BodySwitchDelay = .5f;
        [Min(0)] public float BodyLostGrace = .75f;
        public bool LockSelectedPerson;
        public bool EnableKeyboardShortcuts = true;
        [Header("Steering")]
        [Range(0, 12)] public float DeadZoneDegrees = 3;
        [Range(5, 35)] public float FullScaleDegrees = 18;
        [Range(0.01f, 0.3f)] public float SmoothingSeconds = 0.08f;
        [Header("Explicit simulation (never an automatic fallback)")]
        public bool Simulation;
        [Range(-30, 30)] public float SimulationLeanDegrees;
        [Range(-60, 60)] public float SimulationFrontArmPitchDegrees;
        public bool SimulationSweep;
        [Header("Serialized skeleton")]
        public Transform[] Joints;
        public LineRenderer[] Bones;
        public Renderer[] JointRenderers;
        public Material TrackedMaterial, PredictedMaterial;
        public Transform LeanIndicator;
        public string Status { get; private set; } = "Ready - press START KINECT";
        public bool IsTracked { get; private set; }
        public bool Calibrated => solver.Calibrated;
        public bool Calibrating => calibrating;
        // The ride adapter selects this profile; the standalone skeleton Sandbox stays strict.
        public bool RelaxedStartPose { get; set; }
        public float CalibrationHoldSeconds => RelaxedStartPose ? 1f : 1.5f;
        public bool SelectedBodyPresent => bodyAvailable && float.IsFinite(ResultAgeMs) && ResultAgeMs <= MaximumFrameAge * 1000;
        public bool SurfPoseReady => IsTracked && confidence[5] >= 1 && confidence[12] >= 1 && confidence[7] >= 1 && confidence[14] >= 1 &&
            SurfPoseDetector.IsOpen(Joints[0].position, Joints[2].position, Joints[5].position, Joints[12].position,
                Joints[7].position, Joints[14].position, ScreenReference.up, ScreenReference.right, RelaxedStartPose);
        public string CalibrationFeedback { get; private set; } = "";
        public float CalibrationProgress => calibrating ? Mathf.Clamp01((Time.unscaledTime - calibrationStarted) / CalibrationHoldSeconds) : 0;
        public float RawDegrees => solver.RawDegrees;
        public float MeasuredDegrees { get; private set; }
        public float RawSteer => solver.RawSteer;
        public float Steer => solver.SmoothedSteer;
        public float PelvisOffset => solver.PelvisOffset;
        public uint BodyId { get; private set; }
        public int Bodies { get; private set; }
        public float SelectedDepthMeters { get; private set; } = float.NaN;
        public int BodySelectionChanges { get; private set; }
        public int PelvisConfidence { get; private set; }
        public int ChestConfidence { get; private set; }
        public float ResultAgeMs { get; private set; }
        public float InputFps { get; private set; }
        public long Samples { get; private set; }
        public long ValidSamples { get; private set; }
        public long Dropped => capture?.Dropped ?? 0;
        public int Disconnections { get; private set; }
        public bool CaptureActive => capture != null && capture.IsAlive;
        public bool TryGetJointPosition(int index, out Vector3 position)
        {
            position = Vector3.zero;
            if (!IsTracked || index < 0 || index >= 32 || confidence[index] < 1 || Joints[index] == null) return false;
            position = Joints[index].position;
            return float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z);
        }
        public SurfPoseDetector.Guidance PoseGuidance => IsTracked && confidence[5] >= 1 && confidence[12] >= 1 && confidence[7] >= 1 && confidence[14] >= 1 ?
            SurfPoseDetector.GetGuidance(Joints[0].position, Joints[2].position, Joints[5].position, Joints[12].position,
                Joints[7].position, Joints[14].position, ScreenReference.up, ScreenReference.right, RelaxedStartPose) : new SurfPoseDetector.Guidance(SurfPoseDetector.Adjustment.ShowArms);
        public bool TryGetRidingPose(out bool riding)
        {
            riding = false;
            if (!TryGetJointPosition(5, out var ls) || !TryGetJointPosition(12, out var rs) ||
                !TryGetJointPosition(7, out var lw) || !TryGetJointPosition(14, out var rw)) return false;
            riding = SurfPoseDetector.IsRiding(ls, rs, lw, rw, ScreenReference.up, ScreenReference.forward, MeasuredDegrees);
            return true;
        }
        public static readonly string[] JointNames = {
            "Pelvis","SpineNavel","SpineChest","Neck","ClavicleLeft","ShoulderLeft","ElbowLeft","WristLeft","HandLeft","HandTipLeft","ThumbLeft",
            "ClavicleRight","ShoulderRight","ElbowRight","WristRight","HandRight","HandTipRight","ThumbRight",
            "HipLeft","KneeLeft","AnkleLeft","FootLeft","HipRight","KneeRight","AnkleRight","FootRight","Head","Nose","EyeLeft","EarLeft","EyeRight","EarRight"
        };
        public static readonly int[] Parents = { -1,0,1,2,2,4,5,6,7,8,7,2,11,12,13,14,15,14,0,18,19,20,0,22,23,24,3,26,26,28,26,30 };
        readonly KinectLeanSolver solver = new KinectLeanSolver();
        readonly int[] confidence = new int[32];
        readonly KinectBodySelector bodySelector = new KinectBodySelector();
        readonly KinectBodySelector.Candidate[] candidates = new KinectBodySelector.Candidate[6];
        KinectBodyCapture capture;
        bool hasBody, bodyAvailable, lastSimulation, calibrating, wasTracked;
        long lastFrame = -1;
        float lastSeen, calibrationStarted, calibrationSum, calibrationPelvisSum, calibrationMin, calibrationMax;
        float fpsStarted, calibrationPelvisMin, calibrationPelvisMax;
        int calibrationCount, fpsFrames;
        Matrix4x4 previousOrigin, previousScreen, simulationSensorInverse;
        bool transformInitialized, simulationKeyWasDown;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            ResetTracking();
            previousOrigin = SensorOrigin.localToWorldMatrix;
            previousScreen = ScreenReference.localToWorldMatrix;
            transformInitialized = true;
            simulationSensorInverse = SensorOrigin.worldToLocalMatrix;
            lastSimulation = Simulation;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += StopBeforeReload;
#endif
        }
        void StopBeforeReload() { capture?.Stop(10000); }
        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= StopBeforeReload;
#endif
            capture?.Stop(10000);
            solver.Reset();
            SetVisible(false);
        }
        public void StartKinect()
        {
            if (capture != null && capture.IsAlive)
            { Status = capture.Status; return; }
            Simulation = false; lastSimulation = false;
            ResetStatistics(); ResetTracking();
            string path = Path.IsPathRooted(RuntimeDirectory) ? RuntimeDirectory : Path.Combine(Application.dataPath, "..", RuntimeDirectory);
            capture = new KinectBodyCapture(Path.GetFullPath(path), Use30Fps ? 30 : 15, (int)ProcessingBackend);
            capture.Start();
            Status = "Starting Kinect...";
        }
        public void StopKinect()
        {
            capture?.Stop();
            Simulation = false; lastSimulation = false; ResetTracking(); SetVisible(false);
            Status = capture != null && capture.IsAlive ? capture.Status : "Stopped - input is zero";
        }
        public void UseSimulation()
        {
            if (capture != null && capture.IsAlive && !capture.Stop())
            { Status = "Wait for Kinect to stop before simulation"; return; }
            Simulation = true; lastSimulation = true;
            SimulationLeanDegrees = SimulationFrontArmPitchDegrees = 0; SimulationSweep = false;
            simulationSensorInverse = SensorOrigin.worldToLocalMatrix;
            ResetStatistics(); ResetTracking();
            Status = "SIMULATION - synthetic bones, no Kinect";
        }
        public void ApplyPreset(int index)
        {
            SensorPlacement = (Placement)Mathf.Clamp(index, 0, 2);
            float angle = index == 0 ? 0 : index == 1 ? 45 : 90;
            if (OppositeSide) angle = -angle;
            float rad = angle * Mathf.Deg2Rad;
            SensorOrigin.position = ScreenReference.TransformPoint(new Vector3(Mathf.Sin(rad) * SensorDistance, SensorHeight, Mathf.Cos(rad) * SensorDistance));
            Vector3 direction = Vector3.ProjectOnPlane(ScreenReference.position - SensorOrigin.position, ScreenReference.up);
            SensorOrigin.rotation = Quaternion.LookRotation(direction, ScreenReference.up);
            SensorOrigin.localScale = Vector3.one;
            simulationSensorInverse = SensorOrigin.worldToLocalMatrix;
            previousOrigin = SensorOrigin.localToWorldMatrix; previousScreen = ScreenReference.localToWorldMatrix;
            ResetStatistics(); ResetTracking();
            Status = "Placement changed - move physical Kinect, then calibrate";
        }
        public void FlipSide()
        { OppositeSide = !OppositeSide; ApplyPreset(SensorPlacement == Placement.Custom ? 1 : (int)SensorPlacement); }
        public void ReleaseBody()
        { ResetTracking(); Status = "Released body - waiting for nearest participant"; }
        public void BeginCalibration()
        {
            if (!IsTracked) { CalibrationFeedback = Status = "Calibration needs fresh pelvis + chest (Medium)"; return; }
            solver.Reset();
            calibrating = true; calibrationStarted = Time.unscaledTime;
            calibrationSum = calibrationPelvisSum = 0; calibrationCount = 0;
            calibrationMin = calibrationPelvisMin = float.PositiveInfinity;
            calibrationMax = calibrationPelvisMax = float.NegativeInfinity;
            CalibrationFeedback = Status = $"Hold your neutral surfing stance for {CalibrationHoldSeconds:0.0} s";
        }
        public void ClearCalibration()
        { solver.Reset(); calibrating = false; CalibrationFeedback = ""; }
        public void ResetStatistics()
        {
            Samples = ValidSamples = 0; Disconnections = BodySelectionChanges = 0;
            InputFps = 0; fpsFrames = 0; fpsStarted = Time.unscaledTime;
        }
        void ResetTracking()
        {
            hasBody = false; BodyId = 0; Bodies = 0; lastSeen = 0; lastFrame = -1;
            bodySelector.Reset(); SelectedDepthMeters = float.NaN; LockSelectedPerson = false;
            CalibrationFeedback = "";
            bodyAvailable = IsTracked = calibrating = wasTracked = false;
            solver.Reset();
        }
        void Update()
        {
            if (SensorOrigin == null || ScreenReference == null || Joints == null || Joints.Length != 32) return;
            if (Simulation != lastSimulation)
            {
                if (Simulation) UseSimulation(); else StopKinect();
            }
            if (transformInitialized && (!MatrixEqual(previousOrigin, SensorOrigin.localToWorldMatrix) || !MatrixEqual(previousScreen, ScreenReference.localToWorldMatrix)))
            {
                previousOrigin = SensorOrigin.localToWorldMatrix; previousScreen = ScreenReference.localToWorldMatrix;
                SensorPlacement = Placement.Custom;
                solver.Reset(); calibrating = false;
                Status = "Transform changed - recalibrate neutral";
            }
            if (EnableKeyboardShortcuts && Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) ApplyPreset(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) ApplyPreset(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) ApplyPreset(2);
                if (Keyboard.current.cKey.wasPressedThisFrame) BeginCalibration();
                if (Simulation && !SimulationSweep)
                {
                    bool keyDown = Keyboard.current.leftArrowKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
                    if (keyDown) SimulationLeanDegrees = Keyboard.current.leftArrowKey.isPressed ? -15 : 15;
                    else if (simulationKeyWasDown) SimulationLeanDegrees = 0;
                    simulationKeyWasDown = keyDown;
                }
            }
            bool newFrame = false;
            float age = 0;
            if (Simulation)
            {
                newFrame = bodyAvailable = true; Bodies = 1; BodyId = 999;
                float angle = SimulationSweep ? 15 * Mathf.Sin(Time.unscaledTime * 0.9f) : SimulationLeanDegrees;
                for (int i = 0; i < 32; i++)
                {
                    Vector3 world = NeutralJoint(i);
                    if (i >= 13 && i <= 17)
                        world = NeutralJoint(12) + Quaternion.AngleAxis(-SimulationFrontArmPitchDegrees, Vector3.right) * (world - NeutralJoint(12));
                    if (i < 18 || i >= 26)
                        world = new Vector3(0, 0.95f, 0) + Quaternion.AngleAxis(-angle, Vector3.forward) * (world - new Vector3(0, 0.95f, 0));
                    Joints[i].localPosition = simulationSensorInverse.MultiplyPoint3x4(world);
                    confidence[i] = 2;
                }
                Status = calibrating ? "SIMULATION - hold neutral" : "SIMULATION - synthetic bones, not sensor evidence";
            }
            else
            {
                var snapshot = capture?.Latest;
                age = snapshot == null ? float.PositiveInfinity :
                    (float)((System.Diagnostics.Stopwatch.GetTimestamp() - snapshot.ReceivedTicks) / (double)System.Diagnostics.Stopwatch.Frequency);
                if (snapshot != null && snapshot.FrameNumber != lastFrame)
                {
                    newFrame = true; lastFrame = snapshot.FrameNumber; Bodies = snapshot.Bodies.Length;
                    if (hasBody && !LockSelectedPerson && Time.unscaledTime - lastSeen > 2f)
                    { bodySelector.Reset(); hasBody = false; solver.Reset(); calibrating = false; }
                    for (int b = 0; b < Bodies; b++)
                    {
                        var pelvis = snapshot.Bodies[b].Joints[0];
                        candidates[b] = new KinectBodySelector.Candidate(snapshot.Bodies[b].Id, pelvis.Z * .001f, pelvis.Confidence);
                    }
                    int selected = bodySelector.Step(candidates, Bodies, Time.unscaledTime,
                        BodySwitchMargin, BodySwitchDelay, BodyLostGrace, LockSelectedPerson);
                    if (bodySelector.SelectionChanged)
                    { solver.Reset(); calibrating = false; if (bodySelector.HasSelection) BodySelectionChanges++; }
                    hasBody = bodySelector.HasSelection;
                    BodyId = bodySelector.SelectedId;
                    KinectBodyCapture.Body chosen = selected >= 0 ? snapshot.Bodies[selected] : null;
                    bodyAvailable = chosen != null;
                    SelectedDepthMeters = bodyAvailable ? candidates[selected].DepthMeters : float.NaN;
                    if (bodyAvailable)
                    {
                        lastSeen = Time.unscaledTime;
                        for (int i = 0; i < 32; i++)
                        {
                            var joint = chosen.Joints[i];
                            var point = new Vector3(joint.X, -joint.Y, joint.Z) * 0.001f;
                            bool finite = float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z) && point.z > 0;
                            confidence[i] = finite ? joint.Confidence : 0;
                            if (finite) Joints[i].localPosition = point;
                        }
                    }
                }
                if (capture != null && !calibrating)
                    Status = LockSelectedPerson && hasBody && (!bodyAvailable || age > MaximumFrameAge)
                        ? "Locked body lost - input is zero"
                        : capture.Status + (Bodies > 0 && !hasBody ? " - waiting for reliable nearest pelvis" : "");
            }
            if (!Simulation && age > MaximumFrameAge) SelectedDepthMeters = float.NaN;
            ResultAgeMs = age * 1000;
            PelvisConfidence = bodyAvailable ? confidence[0] : 0;
            ChestConfidence = bodyAvailable ? confidence[2] : 0;
            IsTracked = bodyAvailable && age <= MaximumFrameAge && confidence[0] >= 2 && confidence[2] >= 2 &&
                KinectLeanSolver.Measure(Joints[0].position, Joints[2].position, ScreenReference, out _, out _);
            if (IsTracked && KinectLeanSolver.Measure(Joints[0].position, Joints[2].position, ScreenReference, out float absoluteAngle, out _))
                MeasuredDegrees = absoluteAngle;
            else MeasuredDegrees = 0;
            if (!Simulation && age > MaximumFrameAge) InputFps = 0;
            if (wasTracked && !IsTracked) Disconnections++;
            wasTracked = IsTracked;
            if (newFrame)
            {
                Samples++; if (IsTracked) ValidSamples++;
                fpsFrames++;
                float elapsed = Time.unscaledTime - fpsStarted;
                if (elapsed >= 1) { InputFps = fpsFrames / elapsed; fpsFrames = 0; fpsStarted = Time.unscaledTime; }
            }
            if (calibrating)
            {
                if (!IsTracked)
                { calibrating = false; CalibrationFeedback = Status = "Calibration cancelled: tracking lost"; }
                else if (newFrame)
                {
                    KinectLeanSolver.Measure(Joints[0].position, Joints[2].position, ScreenReference, out float angle, out float x);
                    calibrationSum += angle; calibrationPelvisSum += x; calibrationCount++;
                    calibrationMin = Mathf.Min(calibrationMin, angle); calibrationMax = Mathf.Max(calibrationMax, angle);
                    calibrationPelvisMin = Mathf.Min(calibrationPelvisMin, x); calibrationPelvisMax = Mathf.Max(calibrationPelvisMax, x);
                    if (Time.unscaledTime - calibrationStarted >= CalibrationHoldSeconds && calibrationCount >= 8)
                    {
                        calibrating = false;
                        if (calibrationMax - calibrationMin > (RelaxedStartPose ? 8 : 4) ||
                            calibrationPelvisMax - calibrationPelvisMin > (RelaxedStartPose ? .14f : .08f))
                            CalibrationFeedback = Status = "Moved during calibration. Hold still and retry.";
                        else { solver.Calibrate(calibrationSum / calibrationCount, calibrationPelvisSum / calibrationCount); CalibrationFeedback = Status = "Neutral calibrated"; }
                    }
                }
            }
            solver.DeadZoneDegrees = DeadZoneDegrees; solver.FullScaleDegrees = FullScaleDegrees; solver.SmoothingSeconds = SmoothingSeconds;
            solver.Step(Joints[0].position, Joints[2].position, ScreenReference, IsTracked, age, MaximumFrameAge, Time.unscaledDeltaTime);
            if (LeanIndicator != null) LeanIndicator.localRotation = Quaternion.Euler(0, 0, -Steer * 22);
            SetVisible(bodyAvailable && age <= MaximumFrameAge);
            for (int i = 0; i < 32; i++)
            {
                if (JointRenderers[i] != null)
                { JointRenderers[i].enabled &= confidence[i] > 0; JointRenderers[i].sharedMaterial = confidence[i] >= 2 ? TrackedMaterial : PredictedMaterial; }
                if (i > 0 && Bones[i] != null)
                {
                    Bones[i].enabled &= confidence[i] > 0 && confidence[Parents[i]] > 0;
                    Bones[i].sharedMaterial = confidence[i] >= 2 && confidence[Parents[i]] >= 2 ? TrackedMaterial : PredictedMaterial;
                    Bones[i].SetPosition(0, Joints[Parents[i]].localPosition); Bones[i].SetPosition(1, Joints[i].localPosition);
                }
            }
        }
        void SetVisible(bool show)
        {
            if (JointRenderers != null) foreach (var item in JointRenderers) if (item != null) item.enabled = show;
            if (Bones != null) foreach (var item in Bones) if (item != null) item.enabled = show;
        }
        static bool MatrixEqual(Matrix4x4 a, Matrix4x4 b)
        { for (int i = 0; i < 16; i++) if (Mathf.Abs(a[i] - b[i]) > 0.00001f) return false; return true; }
        public static Vector3 NeutralJoint(int i)
        {
            // Side-on surfer, shoulder axis aligned with display depth (+Z).
            switch (i)
            {
                case 0: return new Vector3(0,.95f,0); case 1: return new Vector3(0,1.15f,0);
                case 2: return new Vector3(0,1.43f,0); case 3: return new Vector3(0,1.57f,0);
                case 4: return new Vector3(0,1.43f,-.08f); case 5: return new Vector3(0,1.43f,-.20f);
                case 6: return new Vector3(-.02f,1.38f,-.46f); case 7: return new Vector3(-.04f,1.35f,-.70f);
                case 8: return new Vector3(-.04f,1.35f,-.78f); case 9: return new Vector3(-.04f,1.35f,-.86f); case 10: return new Vector3(-.10f,1.35f,-.76f);
                case 11: return new Vector3(0,1.43f,.08f); case 12: return new Vector3(0,1.43f,.20f);
                case 13: return new Vector3(-.02f,1.38f,.46f); case 14: return new Vector3(-.04f,1.35f,.70f);
                case 15: return new Vector3(-.04f,1.35f,.78f); case 16: return new Vector3(-.04f,1.35f,.86f); case 17: return new Vector3(-.10f,1.35f,.76f);
                case 18: return new Vector3(0,.92f,-.12f); case 19: return new Vector3(-.12f,.53f,-.30f); case 20: return new Vector3(0,.10f,-.40f); case 21: return new Vector3(-.18f,.05f,-.40f);
                case 22: return new Vector3(0,.92f,.12f); case 23: return new Vector3(-.12f,.53f,.30f); case 24: return new Vector3(0,.10f,.40f); case 25: return new Vector3(-.18f,.05f,.40f);
                case 26: return new Vector3(0,1.73f,0); case 27: return new Vector3(-.10f,1.72f,.05f);
                case 28: return new Vector3(-.06f,1.76f,-.04f); case 29: return new Vector3(0,1.74f,-.08f);
                case 30: return new Vector3(-.06f,1.76f,.04f); default: return new Vector3(0,1.74f,.08f);
            }
        }
    }
}


