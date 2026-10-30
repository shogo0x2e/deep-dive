using TMPro;
using UnityEngine;

namespace HighwaySurfing.Kinect
{
    public sealed class KinectDebugHud : MonoBehaviour
    {
        public KinectDebugRig Rig;
        public TextMeshProUGUI StateText, TelemetryText, CalibrationText, ModeText, PlacementText, AngleText;
        public RectTransform RawMarker, SmoothMarker;
        public UnityEngine.UI.Image TrackingLamp;
        float nextRefresh;
        void Update()
        {
            if (Rig == null) return;
            if (RawMarker != null) RawMarker.anchoredPosition = new Vector2(Rig.RawSteer * 210, 0);
            if (SmoothMarker != null) SmoothMarker.anchoredPosition = new Vector2(Rig.Steer * 210, 0);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .08f;
            Color teal = new Color(.22f, .95f, .78f);
            Color amber = new Color(1f, .68f, .28f);
            TrackingLamp.color = Rig.IsTracked ? teal : amber;
            ModeText.text = Rig.Simulation ? "SIMULATION / NOT LIVE" : Rig.CaptureActive ? "LIVE KINECT / NEAREST BODY" : "KINECT READY / MANUAL START";
            ModeText.color = Rig.Simulation ? amber : teal;
            StateText.text = Rig.Status;
            CalibrationText.text = Rig.Calibrating ? $"HOLD NEUTRAL    {Rig.CalibrationProgress:P0}" :
                Rig.Calibrated ? "NEUTRAL REGISTERED" : "NEUTRAL REQUIRED";
            CalibrationText.color = Rig.Calibrated ? teal : amber;
            string age = float.IsFinite(Rig.ResultAgeMs) ? $"{Rig.ResultAgeMs:0} ms" : "--";
            TelemetryText.text =
                $"<color=#91A6BA>BODY ID</color>  {(Rig.IsTracked ? Rig.BodyId.ToString() : "--")}   <color=#91A6BA>PEOPLE</color>  {Rig.Bodies}\n" +
                $"<color=#91A6BA>PELVIS / CHEST</color>  {Confidence(Rig.PelvisConfidence)} / {Confidence(Rig.ChestConfidence)}\n" +
                $"<color=#91A6BA>TORSO ANGLE</color>  {Rig.MeasuredDegrees:+0.0;-0.0;0.0} deg\n" +
                $"<color=#91A6BA>INPUT RATE</color>  {Rig.InputFps:0.0} fps\n" +
                $"<color=#91A6BA>RESULT AGE</color>  {age}\n" +
                $"<color=#91A6BA>VALID SAMPLES</color>  {Rig.ValidSamples} / {Rig.Samples}\n" +
                $"<color=#91A6BA>TRACKING LOSSES</color>  {Rig.Disconnections}\n" +
                $"<color=#91A6BA>DROPPED INPUT</color>  {Rig.Dropped}\n" +
                $"<color=#91A6BA>{(Rig.LockSelectedPerson ? "LOCKED" : "NEAREST")}</color>  {(float.IsFinite(Rig.SelectedDepthMeters) ? Rig.SelectedDepthMeters.ToString("0.00") + " m" : "--")}  <color=#91A6BA>SELECTS</color>  {Rig.BodySelectionChanges}";
            var p = Rig.SensorOrigin.position; var e = Rig.SensorOrigin.eulerAngles;
            PlacementText.text = $"{Rig.SensorPlacement}  {(Rig.OppositeSide ? "/ opposite side" : "")}\n" +
                $"POSITION  {p.x:0.00}, {p.y:0.00}, {p.z:0.00} m\nROTATION  {e.x:0}, {e.y:0}, {e.z:0} deg";
            AngleText.text = $"LEAN  {Rig.RawDegrees:+0.0;-0.0;0.0}°     RAW  {Rig.RawSteer:+0.00;-0.00;0.00}     FILTERED  {Rig.Steer:+0.00;-0.00;0.00}";
        }
        static string Confidence(int value) => value >= 2 ? "M" : value == 1 ? "LOW" : "--";
    }
}

