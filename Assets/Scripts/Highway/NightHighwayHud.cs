using TMPro;
using UnityEngine;

namespace HighwaySurfing
{
    public sealed class NightHighwayHud : MonoBehaviour
    {
        public NightHighwayWorld World;
        public CanvasGroup Group;
        public TextMeshProUGUI SpeedLabel;
        public TextMeshProUGUI DistanceLabel;
        public TextMeshProUGUI StateLabel;
        private int lastSpeed=-1,lastDistance=-1;

        private void Update()
        {
            if(World==null)return;
            Group.alpha=World.ShowHud?1:0;
            int speed=Mathf.RoundToInt(World.SpeedKmh),distance=(int)World.TravelledMetres;
            if(speed!=lastSpeed){SpeedLabel.SetText("{0:0}",speed);lastSpeed=speed;}
            if(distance!=lastDistance){DistanceLabel.SetText("{0:0.00} KM",distance/1000f);lastDistance=distance;}
            StateLabel.text=World.Paused?"PAUSED":"NIGHT RIDE";
        }
    }
}
