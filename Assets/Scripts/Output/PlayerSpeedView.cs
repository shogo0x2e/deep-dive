using TMPro;
using UnityEngine;

namespace HighwaySurfing.Output
{
    /// <summary>Player-facing speed readout rendered by the camera, including in Spout.</summary>
    public sealed class PlayerSpeedView : MonoBehaviour
    {
        [SerializeField] private NightHighwayWorld world;
        [SerializeField] private TextMeshProUGUI speedLabel;
        private int lastSpeed = -1;

        public void Configure(NightHighwayWorld source, TextMeshProUGUI label)
        {
            world = source;
            speedLabel = label;
            lastSpeed = -1;
            UpdateSpeed();
        }

        private void OnEnable() { lastSpeed = -1; UpdateSpeed(); }
        private void LateUpdate() { UpdateSpeed(); }

        private void UpdateSpeed()
        {
            if (world == null || speedLabel == null) return;
            int speed = Mathf.RoundToInt(Mathf.Max(0, world.SpeedKmh));
            if (speed == lastSpeed) return;
            speedLabel.SetText("{0:0} <size=45%>km/h</size>", speed);
            lastSpeed = speed;
        }
    }
}
