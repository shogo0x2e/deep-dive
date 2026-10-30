using TMPro;
using UnityEngine;

namespace HighwaySurfing
{
    public sealed class KinectHighwayHud : MonoBehaviour
    {
        public KinectHighwayInput Input;
        public CanvasGroup Group;
        public TextMeshProUGUI StatusLabel, TrackingLabel, SettingsLabel, PlacementLabel;
        public RectTransform SteeringMarker;
        void LateUpdate()
        {
            if (Input == null || Input.Rig == null) return;
            bool show = Input.World.ShowHud;
            Group.alpha = show ? 1 : 0; Group.interactable = Group.blocksRaycasts = show;
            var rig = Input.Rig;
            StatusLabel.text = Input.Status;
            StatusLabel.color = Input.InputAvailable ? new Color(.22f,.95f,.78f) : new Color(1,.72f,.38f);
            string distance = float.IsFinite(rig.SelectedDepthMeters) ? rig.SelectedDepthMeters.ToString("0.00") + " m" : "--";
            TrackingLabel.text = $"{(rig.LockSelectedPerson ? "LOCKED" : "NEAREST")}  ID {(rig.IsTracked || rig.LockSelectedPerson ? rig.BodyId.ToString() : "--")}   {distance}   PEOPLE {rig.Bodies}\n" +
                $"LEAN {rig.RawDegrees:+0.0;-0.0;0.0} deg   INPUT {Input.World.SteeringInput:+0.00;-0.00;0.00}   {rig.InputFps:0} fps";
            SettingsLabel.text = $"Sensitivity x{Input.Sensitivity:0.0}    Direction {(Input.InvertSteering ? "INVERTED" : "NORMAL")}";
            PlacementLabel.text = rig.SensorPlacement == Kinect.KinectDebugRig.Placement.Screen ? "SENSOR: SCREEN 0 DEG / PARTICIPANT: 45 DEG" : rig.SensorPlacement == Kinect.KinectDebugRig.Placement.Diagonal45 ?
                $"SENSOR: {(rig.OppositeSide ? "LEFT" : "RIGHT")} 45 DEG - facing the player" : "SENSOR: CUSTOM - match the physical setup";
            SteeringMarker.anchoredPosition = new Vector2(Input.World.SteeringInput * 175, 0);
        }
    }
}
