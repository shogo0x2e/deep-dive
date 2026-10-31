using HighwaySurfing.Kinect;
using UnityEngine;

namespace HighwaySurfing
{
    /// <summary>Runs the real road as an unattended preview without registering a player.</summary>
    [DefaultExecutionOrder(175)]
    [RequireComponent(typeof(KinectHighwayInput))]
    public sealed class HighwayAttractMode : MonoBehaviour
    {
        public KinectHighwayInput Input;
        public bool EnableAttractMode = true;
        [Min(.5f)] public float IdleSeconds = 5;
        [Range(0, 1)] public float IdleModalOpacity = .15f;
        public KinectPresenceArea PresenceArea = new KinectPresenceArea {
            Enabled = true, DepthMetres = new Vector2(.5f, 3.5f), HalfWidthMetres = 1.5f
        };
        public bool DemoActive { get; private set; }
        public bool PersonInArea { get; private set; }
        public float ModalOpacity => DemoActive ? Mathf.Clamp01(IdleModalOpacity) : 1;

        KinectArmSpeedInput armSpeed;
        bool wasRegistered;
        float lastPresence, targetLateral = -3.1f;
        void Awake()
        {
            if (Input == null) Input = GetComponent<KinectHighwayInput>();
            armSpeed = GetComponent<KinectArmSpeedInput>();
        }
        void OnEnable() { lastPresence = Time.unscaledTime; wasRegistered = false; ApplyArea(); }
        void ApplyArea()
        {
            if (Input != null && Input.Rig != null)
                Input.Rig.EntryArea = EnableAttractMode && Input.KinectMode ? PresenceArea : default;
        }
        void Update()
        {
            if (Input == null || Input.World == null || Input.Rig == null) return;
            ApplyArea();
            PersonInArea = (!Input.Rig.Simulation || Input.AllowSimulationForTesting) && Input.Rig.HasPersonInEntryArea;
            bool available = EnableAttractMode && Input.isActiveAndEnabled && Input.KinectMode;
            bool departed = wasRegistered && !Input.RideRegistered;
            wasRegistered = Input.RideRegistered;
            if (!available || PersonInArea || Input.RideRegistered || Input.Rig.Calibrating)
                lastPresence = Time.unscaledTime;
            else if (departed)
                // Departure already had its own grace period. Do not add another idle wait.
                lastPresence = Time.unscaledTime - Mathf.Max(.5f, IdleSeconds);
            bool demo = available && !Input.RideRegistered && !Input.Rig.Calibrating && !PersonInArea &&
                Time.unscaledTime - lastPresence >= Mathf.Max(.5f, IdleSeconds);
            if (demo && !DemoActive) targetLateral = Input.World.LateralPosition;
            DemoActive = demo;
            Input.World.AttractDemoActive = demo;
            if (!demo) return;
            Input.World.AttractDemoSpeedKmh = armSpeed != null && armSpeed.EnableArmSpeed ?
                armSpeed.StartingSpeedKmh : Input.World.DefaultSpeedKmh;
            Input.World.AttractDemoSteering = DemoSteering();
        }
        float DemoSteering()
        {
            var world = Input.World;
            float best = ClearanceCost(targetLateral);
            // Keep enough room for the body of a car; only change course for a clear improvement.
            for (int n = 0; n < 3; n++)
            {
                float candidate = -8.2f + n * 2.7f;
                float cost = ClearanceCost(candidate) + Mathf.Abs(candidate - world.LateralPosition) * .04f;
                if (cost + .15f < best) { best = cost; targetLateral = candidate; }
            }
            return Mathf.Clamp((targetLateral - world.LateralPosition) * .8f, -.7f, .7f);
        }
        float ClearanceCost(float lateral)
        {
            float cost = 0;
            for (int n = 0; n < NightHighwayWorld.TrafficCount; n++)
            {
                if (!Input.World.TrySampleTraffic(n, out float distance, out float lane, out float relativeSpeed) || lane > 0) continue;
                float t = Mathf.Abs(relativeSpeed) > .01f ? Mathf.Clamp(-distance / relativeSpeed, 0, 5) : 0;
                float nearest = Mathf.Abs(distance + relativeSpeed * t);
                cost += Mathf.Clamp01(1 - nearest / 50) * Mathf.Clamp01(1 - Mathf.Abs(lateral - lane) / 2.3f);
            }
            return cost;
        }
        void OnDisable()
        {
            DemoActive = PersonInArea = wasRegistered = false;
            if (Input == null) return;
            if (Input.World != null) Input.World.AttractDemoActive = false;
            if (Input.Rig != null) Input.Rig.EntryArea = default;
        }
    }
}
