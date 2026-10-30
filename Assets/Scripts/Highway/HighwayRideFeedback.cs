using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HighwaySurfing
{
    /// <summary>Audio and camera-rendered feedback follow actual speed, including neutral coasting.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class HighwayRideFeedback : MonoBehaviour
    {
        public NightHighwayWorld World;
        public KinectHighwayInput Input;
        [Header("Sound")]
        public AudioClip MotorClip, PassByClip, DepartureClip;
        public bool SoundEnabled = true;
        [Range(0, 1)] public float MasterVolume = .7f;
        [Range(0, 1)] public float MotorVolume = .8f;
        [Range(0, 1)] public float PassByVolume = .7f;
        [Header("Light")]
        public Material StreakMaterial;
        public bool EffectsEnabled = true;
        [Range(0, 2)] public float RoadLightIntensity = 1;
        [Range(0, 2)] public float SpeedLineIntensity = 1;
        [Range(140, 215)] public float SpeedLineThresholdKmh = 185;
        public float RideFade { get; private set; }
        public float SpeedLineAmount { get; private set; }
        public float DepartureGlow { get; private set; }
        public float RoadStreakLength { get; private set; }
        public int DepartureCount { get; private set; }
        public AudioSource MotorSource { get; private set; }
        public AudioSource DepartureSource { get; private set; }
        public int StreetPassCount { get; private set; }
        public int VehiclePassCount { get; private set; }
        public int ActivePassVoices { get; private set; }

        GameObject generated;
        Mesh roadMesh, screenMesh;
        readonly Batch road = new Batch(), screen = new Batch();
        float departureAge = 10, visualClock;
        bool wasRegistered;
        KinectArmSpeedInput armSpeed;
        readonly AudioSource[] passes = new AudioSource[4];
        readonly float[] passAge = new float[4], passPitch = new float[4], passGain = new float[4], passPan = new float[4];
        readonly float[] previousTraffic = new float[NightHighwayWorld.TrafficCount], lastVehicleSound = new float[NightHighwayWorld.TrafficCount];
        float previousRoute;
        int previousLamp, lastLampSound;
        bool observing;

        void OnEnable()
        {
            if (World == null || World.MainCamera == null) return;
            generated = new GameObject("GeneratedRideFeedback") { hideFlags = HideFlags.DontSave };
            generated.transform.SetParent(transform, false);
            armSpeed = Input != null ? Input.GetComponent<KinectArmSpeedInput>() : null;
            MotorSource = MakeSource("Board motor", MotorClip, true);
            DepartureSource = MakeSource("Departure", DepartureClip, false);
            for (int n = 0; n < passes.Length; n++) passes[n] = MakeSource("Pass by " + n, PassByClip, false);
            for (int n = 0; n < lastVehicleSound.Length; n++) lastVehicleSound[n] = -10;
            if (StreakMaterial != null)
            {
                roadMesh = MakeMesh("Road light trails", generated.transform);
                // A camera child is included in the main RenderTexture / Spout output.
                screenMesh = MakeMesh("Peripheral speed lines", World.MainCamera.transform);
            }
        }

        AudioSource MakeSource(string label, AudioClip clip, bool loop)
        {
            var child = new GameObject(label) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(generated.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.clip = clip; source.loop = loop; source.playOnAwake = false;
            source.spatialBlend = 0; source.dopplerLevel = 0; source.volume = 0;
            source.priority = loop ? 128 : 64;
            if (loop && clip != null) source.Play();
            return source;
        }

        Mesh MakeMesh(string label, Transform parent)
        {
            var child = new GameObject(label) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(parent, false);
            // Keep the road mesh in world space; RoadPoint already includes the world's transform.
            if (parent == generated.transform) child.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var mesh = new Mesh { name = label, hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic(); child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = StreakMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (parent != generated.transform) screenObject = child;
            return mesh;
        }
        GameObject screenObject;

        void LateUpdate()
        {
            if (generated == null || World == null || World.MainCamera == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            bool registered = Input != null && Input.KinectMode ? Input.RideRegistered : !World.UseExternalSteering;
            bool active = !World.Paused && Time.timeScale > 0 && (!World.UseExternalSteering || World.ExternalRideReady);
            if (registered && !wasRegistered)
            {
                departureAge = 0; DepartureCount++;
                if (SoundEnabled && DepartureClip != null)
                { DepartureSource.volume = MasterVolume * .65f; DepartureSource.Play(); }
            }
            wasRegistered = registered;
            departureAge += dt;
            RideFade = Mathf.MoveTowards(RideFade, active ? 1 : 0, dt / (active ? .7f : .55f));
            float speed = Mathf.Clamp01(World.SpeedKmh / 220);
            float motion = Mathf.SmoothStep(0, 1, Mathf.Clamp01(World.SpeedKmh / 35));
            float volume = SoundEnabled ? MasterVolume * RideFade * motion : 0;
            float load = armSpeed != null && armSpeed.ArmAvailable ? Mathf.Max(0, armSpeed.Control) : 0;
            MotorSource.volume = Smooth(MotorSource.volume, volume * MotorVolume * (Mathf.Lerp(.38f, .8f, speed) + load * .12f), dt, .2f);
            MotorSource.pitch = Smooth(MotorSource.pitch, Mathf.Lerp(.55f, 1.65f, speed) + load * .06f, dt, .3f);
            UpdatePasses(active, dt);
            // Registration is an event, so pause / brief tracking loss cannot retrigger the chime.
            if (!active || !SoundEnabled)
                DepartureSource.volume = Mathf.MoveTowards(DepartureSource.volume, 0, dt * 2);
            SpeedLineAmount = Smooth(SpeedLineAmount,
                EffectsEnabled ? RideFade * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(SpeedLineThresholdKmh, 220, World.SpeedKmh)) : 0, dt, .22f);
            DepartureGlow = EffectsEnabled && active ? Mathf.Sin(Mathf.Clamp01(departureAge / 1.35f) * Mathf.PI) * .065f : 0;
            RoadStreakLength = Mathf.Lerp(.7f, 12, speed * speed);
            if (active) visualClock += dt * Mathf.Lerp(.6f, 2, speed);
            if (roadMesh != null) DrawRoad(speed, motion);
            if (screenMesh != null) DrawScreen();
        }

        static float Smooth(float value, float target, float dt, float seconds)
            => Mathf.Lerp(value, target, 1 - Mathf.Exp(-dt / seconds));

        void UpdatePasses(bool active, float dt)
        {
            ActivePassVoices = 0;
            for (int n = 0; n < passes.Length; n++)
            {
                var source = passes[n];
                if (!source.isPlaying) continue;
                ActivePassVoices++;
                passAge[n] += dt;
                float phase = Mathf.Clamp01(passAge[n] * passPitch[n] / Mathf.Max(.01f, PassByClip.length));
                source.pitch = passPitch[n] * Mathf.Lerp(1.08f, .88f, phase);
                source.panStereo = passPan[n] * Mathf.Sin(phase * Mathf.PI);
                source.volume = (SoundEnabled ? MasterVolume * PassByVolume : 0) * RideFade * passGain[n];
            }
            float route = World.RouteDistance;
            float speed = World.SpeedKmh / 3.6f;
            int lamp = Mathf.FloorToInt((route + speed * .12f) / 42);
            // Ignore resets, wrapping traffic and the first frame after registration / pause.
            bool continuous = active && observing && route >= previousRoute && route - previousRoute < 10;
            if (continuous && SoundEnabled && speed > 4 && lamp > previousLamp && lamp > lastLampSound)
            {
                float dx = -2.4f - World.LateralPosition;
                if (PlayPass(.18f + Mathf.Clamp01(speed / 60) * .12f, dx / 10, Mathf.Lerp(1.1f, 1.65f, speed / 62)))
                    StreetPassCount++;
                lastLampSound = lamp;
            }
            for (int n = 0; n < NightHighwayWorld.TrafficCount; n++)
            {
                World.SampleTraffic(n, out float distance, out float lane, out float relativeSpeed);
                float magnitude = Mathf.Abs(relativeSpeed);
                float pitch = Mathf.Lerp(.75f, 1.5f, Mathf.Clamp01(magnitude / 80));
                float lead = magnitude * .16f / pitch;
                bool crossing = relativeSpeed < 0 ? previousTraffic[n] > lead && distance <= lead : previousTraffic[n] < -lead && distance >= -lead;
                if (continuous && SoundEnabled && magnitude > 2 && crossing && Mathf.Abs(distance - previousTraffic[n]) < 10 &&
                    Time.unscaledTime - lastVehicleSound[n] > 1)
                {
                    float dx = lane - World.LateralPosition;
                    float proximity = 1 / (1 + Mathf.Abs(dx) * .27f);
                    float gain = proximity * Mathf.Lerp(.18f, 1, Mathf.Clamp01(magnitude / 50));
                    if (PlayPass(gain, Mathf.Clamp(dx / 8, -.85f, .85f), pitch)) VehiclePassCount++;
                    lastVehicleSound[n] = Time.unscaledTime;
                }
                previousTraffic[n] = distance;
            }
            if (!continuous) lastLampSound = lamp;
            previousLamp = lamp; previousRoute = route; observing = active;
        }

        bool PlayPass(float gain, float pan, float pitch)
        {
            if (PassByClip == null || RideFade < .3f) return false;
            for (int n = 0; n < passes.Length; n++)
            {
                var source = passes[n];
                if (source.isPlaying) continue;
                passAge[n] = 0; passGain[n] = gain; passPan[n] = pan; passPitch[n] = pitch;
                source.volume = MasterVolume * PassByVolume * RideFade * gain;
                source.panStereo = pan * .2f; source.pitch = pitch * 1.08f;
                source.Play(); return true;
            }
            return false;
        }

        void DrawRoad(float speed, float motion)
        {
            road.Clear();
            float strength = EffectsEnabled ? RideFade * motion * RoadLightIntensity : 0;
            if (strength > .001f)
            {
                // These are stationary road markers, stretched by speed, so turns and forward motion stay coherent.
                float phase = (float)(World.TravelledMetres % 18);
                for (int side = 0; side < 2; side++)
                for (int n = 0; n < 13; n++)
                {
                    float end = n * 18 + side * 7 - phase;
                    float start = end - RoadStreakLength;
                    float near = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 9, end));
                    float far = 1 - Mathf.InverseLerp(90, 220, end);
                    float x = side == 0 ? -10.36f : -.73f;
                    float width = Mathf.Lerp(.035f, .085f, speed);
                    var color = side == 0 ? new Color(.65f, 1.20f, 1.5f) : new Color(1.5f, .72f, .22f);
                    color.a = strength * near * far * Mathf.Lerp(.18f, .65f, speed);
                    road.Quad(World.RoadPoint(start, x - width, .10f), World.RoadPoint(start, x + width, .10f),
                        World.RoadPoint(end, x + width, .10f), World.RoadPoint(end, x - width, .10f), color);
                }
            }
            road.Upload(roadMesh);
        }

        void DrawScreen()
        {
            screen.Clear();
            float depth = Mathf.Max(1, World.MainCamera.nearClipPlane + .1f);
            float halfHeight = Mathf.Tan(World.MainCamera.fieldOfView * Mathf.Deg2Rad * .5f) * depth;
            float halfWidth = halfHeight * World.MainCamera.aspect;
            if (SpeedLineAmount > .002f)
            {
                for (int i = 0; i < 40; i++)
                {
                    float angle = (i + .25f * Mathf.Sin(i * 13.7f)) * Mathf.PI * 2 / 40;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    direction /= Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                    float phase = Mathf.Repeat(visualClock * (.75f + .21f * Mathf.Sin(i * 8.2f)) + i * .618f, 1);
                    float outer = Mathf.Lerp(.82f, 1.42f, phase);
                    float inner = Mathf.Max(.63f, outer - Mathf.Lerp(.09f, .42f, SpeedLineAmount));
                    Vector2 a = direction * inner, b = direction * outer;
                    Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (.0014f + .0012f * (i % 3));
                    float opacity = Mathf.Sin(phase * Mathf.PI) * SpeedLineAmount * SpeedLineIntensity * .42f;
                    var color = new Color(.60f, .85f, 1, opacity);
                    screen.Quad(Project(a - normal * .12f, halfWidth, halfHeight, depth), Project(a + normal * .12f, halfWidth, halfHeight, depth),
                        Project(b + normal, halfWidth, halfHeight, depth), Project(b - normal, halfWidth, halfHeight, depth), color);
                }
            }
            if (DepartureGlow > .001f)
            {
                // A broad, soft rim: the horizon remains clear, and the tutorial can fade away over it.
                screen.Quad(Project(new Vector2(-1, -1), halfWidth, halfHeight, depth), Project(new Vector2(1, -1), halfWidth, halfHeight, depth),
                    Project(new Vector2(1, 1), halfWidth, halfHeight, depth), Project(new Vector2(-1, 1), halfWidth, halfHeight, depth),
                    new Color(.25f, .95f, .82f, DepartureGlow), true);
            }
            screen.Upload(screenMesh);
        }

        static Vector3 Project(Vector2 p, float w, float h, float z) => new Vector3(p.x * w, p.y * h, z);

        void OnDisable()
        {
            if (generated != null) Destroy(generated);
            if (screenObject != null) Destroy(screenObject);
            if (roadMesh != null) Destroy(roadMesh);
            if (screenMesh != null) Destroy(screenMesh);
            RideFade = SpeedLineAmount = DepartureGlow = 0;
            wasRegistered = false; departureAge = 10;
            observing = false;
        }

        sealed class Batch
        {
            readonly List<Vector3> vertices = new List<Vector3>(400);
            readonly List<Vector2> uvs = new List<Vector2>(400);
            readonly List<Color> colors = new List<Color>(400);
            readonly List<int> triangles = new List<int>(600);
            public void Clear() { vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear(); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, bool rim = false)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                uvs.Add(new Vector2(0, rim ? -2 : 0)); uvs.Add(new Vector2(1, rim ? -2 : 0));
                uvs.Add(new Vector2(1, rim ? -1 : 1)); uvs.Add(new Vector2(0, rim ? -1 : 1));
                for (int n = 0; n < 4; n++) colors.Add(color);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
            }
            public void Upload(Mesh mesh)
            {
                mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0, false);
                mesh.RecalculateBounds();
            }
        }
    }
}
