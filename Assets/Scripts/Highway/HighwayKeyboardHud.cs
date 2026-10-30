using TMPro;
using UnityEngine;

namespace HighwaySurfing
{
    /// <summary>Compact trial HUD; all gameplay remains in the shared highway world.</summary>
    public sealed class HighwayKeyboardHud : MonoBehaviour
    {
        public NightHighwayWorld World;
        public CanvasGroup Group;
        public TextMeshProUGUI Speed, Route, Junction, Status;

        void LateUpdate()
        {
            if (World == null) return;
            Group.alpha = World.ShowHud ? 1 : 0;
            Speed.SetText("{0:0}<size=35%> KM/H</size>", World.SpeedKmh);
            Route.text = World.RouteName;
            if (World.RoutePhase == "CONNECTOR")
                Junction.text = "CONNECTING TO " + World.RouteName;
            else
                Junction.text = "LEFT EXIT  /  " + World.UpcomingBranchName + "  " +
                    Mathf.CeilToInt(World.UpcomingBranchDistance / 10) * 10 + " m\n" +
                    (World.UpcomingBranchDistance <= 250
                        ? World.LateralPosition <= HighwayRouteState.ExitThreshold ? "LEFT LANE: TAKE EXIT" : "RIGHT LANE: STAY ON HIGHWAY"
                        : "A / D TO CHOOSE YOUR LANE");
            Status.text = World.Paused ? "PAUSED  /  SPACE TO RESUME" :
                (World.TravelledMetres / 1000).ToString("0.00") + " KM  /  CONTACTS " + World.CollisionCount;
        }
    }
}
