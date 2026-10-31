using System;
using HighwaySurfing.Kinect;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighwaySurfing
{
    [DefaultExecutionOrder(100)]
    public sealed class KinectHighwayInput : MonoBehaviour
    {
        public NightHighwayWorld World;
        public KinectDebugRig Rig;
        public bool AutoStartKinect = true;
        public bool EnableAutomaticCalibration = true;
        [Header("Start pose")]
        [Tooltip("Easier arm pose, shorter neutral registration, and tolerance for brief pose flicker. Turn off to restore the original strict entry.")]
        public bool RelaxedEntry = true;
        [Tooltip("Required pose hold when Relaxed Entry is off.")]
        [Range(.2f,2)] public float PoseHoldSeconds = .65f;
        [Range(.2f,2)] public float RelaxedPoseHoldSeconds = .35f;
        [Tooltip("Brief arm pose/confidence interruptions allowed during relaxed entry. A valid pose is still required to begin registration.")]
        [Range(0,.6f)] public float PoseLossGraceSeconds = .3f;
        [Tooltip("A new coarse instruction must persist this long before the modal changes.")]
        [Range(.2f,1)] public float GuidanceChangeDelaySeconds = .45f;
        [Header("Leaving the ride")]
        [Tooltip("Use the play area plus departure margin to detect departure. Returning within the grace period keeps the same ride. Off uses sensor presence alone.")]
        public bool ReleaseOutsideEntryArea = true;
        [Tooltip("Extra metres around the entry area for an already registered rider. Prevents small boundary movements from starting departure.")]
        [Range(0,1)] public float DepartureAreaMarginMetres = .3f;
        [Tooltip("Optional immediate pause outside the departure area. Off keeps forward motion and available steering during the departure grace period.")]
        public bool PauseDuringDepartureGrace = false;
        [Tooltip("Continuous absence of the registered person from the play area before releasing the ride. Returning cancels the pending departure.")]
        [Range(1,15)] public float DepartureGraceSeconds = 3;
        [Tooltip("Optional: also end a ride after a continuously observed lowered/folded arm pose. Off keeps riding while the person is still present.")]
        public bool ReleaseOnPoseLoss = false;
        [Tooltip("Only used when Release On Pose Loss is enabled. Uncertain/missing joints do not count toward this timer.")]
        [Range(.5f,15)] public float PoseReleaseSeconds = 5;
        [Header("Steering")]
        [Range(.25f,2)] public float Sensitivity = 1;
        public bool InvertSteering;
        [NonSerialized] public bool AllowSimulationForTesting;
        public bool KinectMode { get; private set; } = true;
        public string Status { get; private set; } = "Starting Kinect";
        public bool InputAvailable { get; private set; }
        public bool IdentityRegistered => identitySet && Rig != null && Rig.LockSelectedPerson && Rig.BodyId == registeredId;
        public bool RideRegistered => IdentityRegistered && Rig.Calibrated;
        public bool RegisteredPersonPresent => IdentityRegistered &&
            (ReleaseOutsideEntryArea ? Rig.SelectedBodyInArea(DepartureAreaMarginMetres) : Rig.SelectedBodyPresent);
        public bool ShowTutorial => KinectMode && !RideRegistered;
        public float RequiredPoseHoldSeconds => Mathf.Max(.1f, RelaxedEntry ? RelaxedPoseHoldSeconds : PoseHoldSeconds);
        public float RegistrationProgress => RideRegistered ? 1 : Rig != null && Rig.Calibrating ? .3f + .7f * Rig.CalibrationProgress :
            poseSince >= 0 ? .3f * Mathf.Clamp01((Time.unscaledTime-poseSince)/RequiredPoseHoldSeconds) : 0;
        public string TutorialTitle => Rig != null && Rig.Calibrating ? SurfPoseGuidanceText.HoldTitle :
            SourceReady && showingPoseGuidance ? SurfPoseGuidanceText.Title(displayedGuidance) : "手を前と後ろに\nひろげてください";
        public string TutorialMessage => !SourceReady ? "ただいま準備しています" : !showingPoseGuidance && !Rig.Calibrating ?
            "画面の前に立って、全身を見せてください" : Rig.Calibrating ?
            (RelaxedEntry ? "そのまま1秒、\n維持してください" : "からだをまっすぐにして、そのまま静止します") :
            SurfPoseGuidanceText.Instruction(displayedGuidance, Rig);
        bool identitySet, waitingForCalibration;
        uint registeredId;
        uint poseCandidateId;
        float poseSince = -1, lastPresence;
        float lastReadyPoseTime = float.NegativeInfinity;
        uint lastReadyPoseBody;
        float poseLostSince = -1, guidanceSince;
        float lastGuidanceTracked = float.NegativeInfinity;
        bool showingPoseGuidance;
        uint guidanceBody;
        SurfPoseDetector.Guidance displayedGuidance = new SurfPoseDetector.Guidance(SurfPoseDetector.Adjustment.OpenArms);
        SurfPoseDetector.Guidance pendingGuidance;
        bool SourceReady => Rig != null && (Rig.CaptureActive || (AllowSimulationForTesting && Rig.Simulation));
        bool ValidSource => Rig != null && (!Rig.Simulation || AllowSimulationForTesting);

        void Start()
        {
            if (Rig == null || World == null) { enabled=false; return; }
            Rig.EnableKeyboardShortcuts=false;
            Rig.RelaxedStartPose=RelaxedEntry;
            World.UseExternalSteering=true;
            if (AutoStartKinect) StartSensor();
        }
        void Update()
        {
            if (Rig == null || World == null) return;
            Rig.RelaxedStartPose=RelaxedEntry;
            var keyboard=Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame) RequestCalibration();
            if (keyboard != null && keyboard.kKey.wasPressedThisFrame) UseKeyboard();
            if (!KinectMode)
            {
                World.TrafficSteeringScale=1; World.TrafficInputSmoothingSeconds=0;
                Status="Keyboard preview - A / D to steer"; return;
            }
            World.UseExternalSteering=true;
            if (identitySet && (!Rig.LockSelectedPerson || Rig.BodyId != registeredId)) ClearRegistration();
            if (identitySet)
            {
                if (RideRegistered ? RegisteredPersonPresent : Rig.SelectedBodyInEntryArea) lastPresence=Time.unscaledTime;
                else if (Time.unscaledTime-lastPresence >= DepartureGraceSeconds)
                { ReleasePerson(); World.ResetRide(); }
            }
            UpdateRidePose();
            UpdateAutomaticEntry();
            UpdateTutorialGuidance();
            bool ready=SourceReady && ValidSource && RideRegistered && !waitingForCalibration &&
                (!PauseDuringDepartureGrace || !ReleaseOutsideEntryArea || RegisteredPersonPresent);
            InputAvailable=ready && Rig.IsTracked;
            World.ExternalRideReady=ready;
            World.ExternalSteeringAvailable=InputAvailable;
            World.SteeringInput=InputAvailable ? Mathf.Clamp(Rig.Steer*Sensitivity*(InvertSteering?-1:1),-1,1) : 0;
            // Retain visible cars through tracking loss. Future spawns leave a clear corridor;
            // the world suppresses impact while registered steering is unavailable.
            World.TrafficSteeringScale=RideRegistered && !InputAvailable ? 0 : Mathf.Clamp01(Sensitivity);
            World.TrafficInputSmoothingSeconds=Rig.SmoothingSeconds;
            if (!SourceReady) Status=Rig.Status;
            else if (!ValidSource) Status="Simulation disabled for riding";
            else if (Rig.Calibrating) Status="Hold surfing pose - registering neutral";
            else if (RideRegistered && ReleaseOutsideEntryArea && !RegisteredPersonPresent)
                Status=PauseDuringDepartureGrace ? "Outside play area - paused during grace" : "Outside play area - departure grace";
            else if (ready) Status=Rig.IsTracked ? (World.Paused?"Paused - SPACE to ride":"Ride - lean left / right") : "Tracking lost - lateral movement stopped";
            else Status=Rig.IsTracked ? "Open arms at shoulder height to begin" : "Waiting for nearest person - "+Rig.Status;
        }
        void UpdateRidePose()
        {
            if (!ReleaseOnPoseLoss || !RideRegistered || !Rig.SelectedBodyPresent) { poseLostSince = -1; return; }
            bool known = Rig.TryGetRidingPose(out bool riding);
            // Only a continuously observed released pose is evidence of deliberate departure.
            // An occluded arm or uncertain chest must not end an otherwise present rider's turn.
            if (!known || riding) { poseLostSince = -1; return; }
            if (poseLostSince < 0) poseLostSince = Time.unscaledTime;
            if (Time.unscaledTime - poseLostSince >= PoseReleaseSeconds) { ReleasePerson(); World.ResetRide(); }
        }
        void UpdateTutorialGuidance()
        {
            if (!SourceReady || !Rig.IsTracked)
            {
                // A single missing tracking frame should not flash the waiting message.
                if (SourceReady && Rig.BodyId==guidanceBody && Time.unscaledTime-lastGuidanceTracked<GuidanceChangeDelaySeconds) return;
                showingPoseGuidance=false;
                displayedGuidance = new SurfPoseDetector.Guidance(SurfPoseDetector.Adjustment.OpenArms); guidanceBody = 0; return;
            }
            lastGuidanceTracked=Time.unscaledTime; showingPoseGuidance=true;
            var next = SurfPoseGuidanceText.Simplify(Rig.PoseGuidance);
            if (Rig.Calibrating)
            { displayedGuidance = pendingGuidance = new SurfPoseDetector.Guidance(SurfPoseDetector.Adjustment.Ready); guidanceSince = Time.unscaledTime; guidanceBody = Rig.BodyId; return; }
            if (guidanceBody != Rig.BodyId || next.Action != pendingGuidance.Action)
            { pendingGuidance = next; guidanceSince = Time.unscaledTime; guidanceBody = Rig.BodyId; }
            // Ignore fine height/left-right changes; only switch on a sustained coarse action.
            if (Time.unscaledTime - guidanceSince >= GuidanceChangeDelaySeconds) displayedGuidance = pendingGuidance;
        }
        void UpdateAutomaticEntry()
        {
            if (!EnableAutomaticCalibration || !SourceReady || !ValidSource) return;
            bool poseReady = Rig.SurfPoseReady;
            if (poseReady) { lastReadyPoseTime=Time.unscaledTime; lastReadyPoseBody=Rig.BodyId; }
            if (waitingForCalibration)
            {
                if (Rig.Calibrating && !poseReady && !WithinPoseGrace(registeredId))
                { Rig.ClearCalibration(); waitingForCalibration=false; poseSince=-1; }
                else if (!Rig.Calibrating) { waitingForCalibration=false; poseSince=-1; }
                return;
            }
            if (RideRegistered) return;
            if (!poseReady)
            {
                if (!WithinPoseGrace(poseCandidateId)) poseSince=-1;
                return; // Never start from stale or currently missing arms.
            }
            if (poseSince < 0 || poseCandidateId != Rig.BodyId)
            { poseSince=Time.unscaledTime; poseCandidateId=Rig.BodyId; }
            if (Time.unscaledTime-poseSince < RequiredPoseHoldSeconds) return;
            Rig.LockSelectedPerson=true; registeredId=Rig.BodyId; identitySet=true; lastPresence=Time.unscaledTime;
            Rig.BeginCalibration(); waitingForCalibration=Rig.Calibrating; poseSince=-1;
        }
        bool WithinPoseGrace(uint body) => RelaxedEntry && Rig.IsTracked && Rig.BodyId==body &&
            lastReadyPoseBody==body && Time.unscaledTime-lastReadyPoseTime<=Mathf.Clamp(PoseLossGraceSeconds,0,.6f);
        public void RequestCalibration()
        {
            if (Rig == null || World == null) return;
            if (!KinectMode || !SourceReady) StartSensor();
            Rig.ClearCalibration(); waitingForCalibration=false; poseSince=-1;
            lastReadyPoseTime=float.NegativeInfinity;
            World.SteeringInput=0; World.ExternalRideReady=World.ExternalSteeringAvailable=false;
        }
        public void StartSensor()
        {
            KinectMode=true; ClearRegistration(); Rig.EnableKeyboardShortcuts=false;
            if (!Rig.CaptureActive) Rig.StartKinect();
            World.UseExternalSteering=true;
        }
        public void StopSensor() { ClearRegistration(); Rig.StopKinect(); Status="Kinect stopped"; }
        public void UseKeyboard() { StopSensor(); KinectMode=false; World.UseExternalSteering=false; World.SteeringInput=0; }
        public void ReleasePerson() { ClearRegistration(); Rig.ReleaseBody(); }
        public void SetScreenSensor() { ClearRegistration(); Rig.OppositeSide=false; Rig.ApplyPreset(0); }
        public void SetLeft45() => SetPlacement(true);
        public void SetRight45() => SetPlacement(false);
        void SetPlacement(bool left) { ClearRegistration(); Rig.OppositeSide=left; Rig.ApplyPreset(1); }
        public void ToggleInvert() { InvertSteering=!InvertSteering; }
        public void IncreaseSensitivity() { Sensitivity=Mathf.Min(2,Sensitivity+.1f); }
        public void DecreaseSensitivity() { Sensitivity=Mathf.Max(.25f,Sensitivity-.1f); }
        public void TogglePause() { World.Paused=!World.Paused; }
        public void ResetRide() { World.ResetRide(); }
        void ClearRegistration()
        {
            identitySet=waitingForCalibration=InputAvailable=false; poseSince=poseLostSince=-1;
            lastReadyPoseTime=float.NegativeInfinity;
            if (Rig != null) { Rig.LockSelectedPerson=false; Rig.ClearCalibration(); }
            if (World != null) { World.SteeringInput=0; World.ExternalSteeringAvailable=World.ExternalRideReady=false; }
        }
        void OnDisable()
        {
            ClearRegistration();
            if (World != null) { World.TrafficSteeringScale=1; World.TrafficInputSmoothingSeconds=0; }
            if (Rig != null) { Rig.RelaxedStartPose=false; Rig.StopKinect(); }
        }
    }
}
