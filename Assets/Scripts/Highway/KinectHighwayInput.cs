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
        [Range(.2f,2)] public float PoseHoldSeconds = .65f;
        [Range(1,10)] public float DepartureGraceSeconds = 3;
        [Range(.25f,2)] public float Sensitivity = 1;
        public bool InvertSteering;
        [NonSerialized] public bool AllowSimulationForTesting;
        public bool KinectMode { get; private set; } = true;
        public string Status { get; private set; } = "Starting Kinect";
        public bool InputAvailable { get; private set; }
        public bool IdentityRegistered => identitySet && Rig != null && Rig.LockSelectedPerson && Rig.BodyId == registeredId;
        public bool RideRegistered => IdentityRegistered && Rig.Calibrated;
        public bool ShowTutorial => KinectMode && !RideRegistered;
        public float RegistrationProgress => RideRegistered ? 1 : Rig != null && Rig.Calibrating ? .3f + .7f * Rig.CalibrationProgress :
            poseSince >= 0 ? .3f * Mathf.Clamp01((Time.unscaledTime-poseSince)/PoseHoldSeconds) : 0;
        public string TutorialTitle => Rig != null && Rig.Calibrating ? "そのまま、少しだけキープ" : "手を前と後ろに\nひろげてください";
        public string TutorialMessage => !SourceReady ? "ただいま準備しています" : Rig == null || !Rig.IsTracked ?
            "画面の前に立って、全身を見せてください" : Rig.Calibrating ? "からだをまっすぐにして、そのまま静止します" :
            "肩の高さで腕をのばして、\nサーフィンのポーズ！";
        bool identitySet, waitingForCalibration;
        uint registeredId;
        uint poseCandidateId;
        float poseSince = -1, lastPresence;
        bool SourceReady => Rig != null && (Rig.CaptureActive || (AllowSimulationForTesting && Rig.Simulation));
        bool ValidSource => Rig != null && (!Rig.Simulation || AllowSimulationForTesting);

        void Start()
        {
            if (Rig == null || World == null) { enabled=false; return; }
            Rig.EnableKeyboardShortcuts=false;
            World.UseExternalSteering=true;
            if (AutoStartKinect) StartSensor();
        }
        void Update()
        {
            if (Rig == null || World == null) return;
            var keyboard=Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame) RequestCalibration();
            if (keyboard != null && keyboard.kKey.wasPressedThisFrame) UseKeyboard();
            if (!KinectMode) { Status="Keyboard preview - A / D to steer"; return; }
            World.UseExternalSteering=true;
            if (identitySet && (!Rig.LockSelectedPerson || Rig.BodyId != registeredId)) ClearRegistration();
            if (identitySet)
            {
                if (Rig.SelectedBodyPresent) lastPresence=Time.unscaledTime;
                else if (Time.unscaledTime-lastPresence >= DepartureGraceSeconds)
                { ReleasePerson(); World.ResetRide(); }
            }
            UpdateAutomaticEntry();
            bool ready=SourceReady && ValidSource && RideRegistered && !waitingForCalibration;
            InputAvailable=ready && Rig.IsTracked;
            World.ExternalRideReady=ready;
            World.ExternalSteeringAvailable=InputAvailable;
            World.SteeringInput=InputAvailable ? Mathf.Clamp(Rig.Steer*Sensitivity*(InvertSteering?-1:1),-1,1) : 0;
            if (!SourceReady) Status=Rig.Status;
            else if (!ValidSource) Status="Simulation disabled for riding";
            else if (Rig.Calibrating) Status="Hold surfing pose - registering neutral";
            else if (ready) Status=Rig.IsTracked ? (World.Paused?"Paused - SPACE to ride":"Ride - lean left / right") : "Tracking lost - lateral movement stopped";
            else Status=Rig.IsTracked ? "Open arms at shoulder height to begin" : "Waiting for nearest person - "+Rig.Status;
        }
        void UpdateAutomaticEntry()
        {
            if (!EnableAutomaticCalibration || !SourceReady || !ValidSource) return;
            if (waitingForCalibration)
            {
                if (Rig.Calibrating && !Rig.SurfPoseReady)
                { Rig.ClearCalibration(); waitingForCalibration=false; poseSince=-1; }
                else if (!Rig.Calibrating) { waitingForCalibration=false; poseSince=-1; }
                return;
            }
            if (RideRegistered) return;
            if (!Rig.SurfPoseReady) { poseSince=-1; return; }
            if (poseSince < 0 || poseCandidateId != Rig.BodyId)
            { poseSince=Time.unscaledTime; poseCandidateId=Rig.BodyId; }
            if (Time.unscaledTime-poseSince < PoseHoldSeconds) return;
            Rig.LockSelectedPerson=true; registeredId=Rig.BodyId; identitySet=true; lastPresence=Time.unscaledTime;
            Rig.BeginCalibration(); waitingForCalibration=Rig.Calibrating; poseSince=-1;
        }
        public void RequestCalibration()
        {
            if (Rig == null || World == null) return;
            if (!KinectMode || !SourceReady) StartSensor();
            Rig.ClearCalibration(); waitingForCalibration=false; poseSince=-1;
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
            identitySet=waitingForCalibration=InputAvailable=false; poseSince=-1;
            if (Rig != null) { Rig.LockSelectedPerson=false; Rig.ClearCalibration(); }
            if (World != null) { World.SteeringInput=0; World.ExternalSteeringAvailable=World.ExternalRideReady=false; }
        }
        void OnDisable() { ClearRegistration(); if (Rig != null) Rig.StopKinect(); }
    }
}
