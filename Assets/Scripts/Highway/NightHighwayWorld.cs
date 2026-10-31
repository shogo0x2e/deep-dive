using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using TMPro;

namespace HighwaySurfing
{
    /// <summary>A continuous, camera-relative night road. Geometry is batched into two reusable meshes.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(200)]
    public sealed partial class NightHighwayWorld : MonoBehaviour
    {
        [Header("Presentation")]
        public Camera MainCamera;
        public Material SurfaceMaterial;
        public Material GlowMaterial;
        [Range(55, 95)] public float FieldOfView = 76;
        [Range(0.8f, 2.4f)] public float EyeHeight = 1.25f;
        [Range(0, 8)] public float CameraBankDegrees = 2.5f;
        [Header("Ride")]
        [Tooltip("Player speed limit in km/h. Applies to keyboard, Kinect and the unattended demo; traffic keeps its own speed.")]
        [Range(1, 400)] public float MaximumSpeedKmh = 220;
        [Range(0, 400)] public float SpeedKmh = 138;
        [Range(0, 8)] public float LateralSpeed = 3.5f;
        [Min(0)] public float KeyboardAccelerationKmhPerSecond = 30;
        [Min(0)] public float KeyboardBrakingKmhPerSecond = 60;
        [Range(-9.5f, -1.5f)] public float LateralPosition = -3.1f;
        public bool Paused;
        [Tooltip("Enable to supply SteeringInput from a Kinect adapter instead of the keyboard.")]
        public bool UseExternalSteering;
        [Range(-1, 1)] public float SteeringInput;
        [HideInInspector] public bool ExternalSteeringAvailable;
        [HideInInspector] public bool ExternalRideReady;
        [Header("Collision Gameplay")]
        [Tooltip("Reduce speed once per vehicle contact. Turn off to keep only the impact visuals and sound. Can be changed during Play.")]
        public bool CollisionSlowdownEnabled = true;
        [Tooltip("Percentage of the current speed lost on contact. 30 means 138 km/h becomes 96.6 km/h. No automatic speed recovery; accelerate to regain speed.")]
        [Range(0, 100)] public float CollisionSpeedLossPercent = 30;
        [Header("Traffic")]
        [Tooltip("More frequent traffic. Each car maintains a random 80-120 km/h speed, so gaps can close: brake or steer to avoid contact. Low steering speed increases spawn spacing.")]
        public bool DenseTraffic = true;
        [Header("World")]
        public int Seed = 47;
        public float StartingDistance = 2200;
        [Range(500, 1800)] public float DrawDistance = 1100;
        public bool ShowHud = true;

        public double TravelledMetres { get; private set; }
        public float CurrentSteering { get; private set; }
        public float KeyboardSpeedControl { get; private set; }
        public float SpeedLimitKmh => Mathf.Clamp(MaximumSpeedKmh, 1, 400);
        public float DefaultSpeedKmh => Mathf.Clamp(initialSpeed, 0, SpeedLimitKmh);
        public bool AttractDemoActive { get; set; }
        public float AttractDemoSpeedKmh { get; set; } = 138;
        public float AttractDemoSteering { get; set; }
        public float TrafficSteeringScale { get; set; } = 1;
        public float TrafficInputSmoothingSeconds { get; set; }
        public bool MotionActive => !Paused && Time.timeScale > 0 &&
            (!UseExternalSteering || ExternalRideReady || AttractDemoActive);
        public int RideRevision { get; private set; }
        public int CollisionCount { get; private set; }
        public event Action<HighwayHit> Collided;
        public float RouteDistance => (float)(StartingDistance + TravelledMetres);
        public int RenderedLightCount { get; private set; }
        private readonly HighwayRouteState route = new HighwayRouteState();
        public string RouteName => route.RouteName;
        public string UpcomingBranchName => route.UpcomingBranchName;
        public float UpcomingBranchDistance => route.UpcomingBranchDistance((float)TravelledMetres);
        public string RoutePhase => route.Phase((float)TravelledMetres);
        public const int TrafficCount = HighwayTraffic.Count;
        private HighwayTraffic traffic;
        private int trafficSeed;
        private float trafficDrawDistance, trafficLateralSpeed, trafficSmoothing, trafficSpeedLimit, initialSpeed = 138;
        private bool trafficDense;
        private readonly HighwayTrafficRoutes trafficRoutes = new HighwayTrafficRoutes(TrafficCount);
        private readonly int[] trafficRouteRevisions = new int[TrafficCount];

        private readonly MeshBatch surfaces = new MeshBatch { Opacity = 1 };
        private readonly MeshBatch lights = new MeshBatch();
        private Mesh surfaceMesh, glowMesh;
        private GameObject generatedRoot;
        private TextMeshPro exitSignText, mainSignText;
        private Vector3 origin;
        private Quaternion inverseHeading;
        private float elapsed, bank;
        private bool built;
        private const float RoadHalfWidth = 11.0f;
        private static readonly Color Amber = new Color(4.5f, 1.65f, .30f);
        private static readonly Color CoolWhite = new Color(2.5f, 3.0f, 3.8f);
        private static readonly Color RampTeal = new Color(.25f, 2.2f, 1.9f);
        private static readonly Color TailRed = new Color(4f, .07f, .015f);

        private void OnEnable()
        {
            built = false;
            initialSpeed = SpeedKmh;
            if(Application.isPlaying){Application.runInBackground=true;Application.targetFrameRate=60;}
        }
        private void Update()
        {
            if (MainCamera == null || SurfaceMaterial == null || GlowMaterial == null) return;
            if (!built) BuildMeshes();
            if (Application.isPlaying)
            {
                float dt = Mathf.Min(Time.deltaTime, .05f);
                double previousDistance = TravelledMetres;
                float previousLateral = LateralPosition;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.spaceKey.wasPressedThisFrame) Paused = !Paused;
                    if (k.hKey.wasPressedThisFrame) ShowHud = !ShowHud;
                    if (k.rKey.wasPressedThisFrame)
                    {
                        ResetRide(); previousDistance = TravelledMetres; previousLateral = LateralPosition;
                    }
                }
                bool active = MotionActive;
                if (AttractDemoActive) SpeedKmh = AttractDemoSpeedKmh;
                KeyboardSpeedControl = active && !AttractDemoActive && k != null ?
                    ((k.upArrowKey.isPressed || k.wKey.isPressed) ? 1 : 0) -
                    ((k.downArrowKey.isPressed || k.sKey.isPressed) ? 1 : 0) : 0;
                ApplySpeedControl(KeyboardSpeedControl, KeyboardAccelerationKmhPerSecond, KeyboardBrakingKmhPerSecond, dt);
                SpeedKmh = Mathf.Clamp(SpeedKmh, 0, SpeedLimitKmh);
                float steer = AttractDemoActive ? AttractDemoSteering : UseExternalSteering ? SteeringInput : k == null ? 0 :
                    ((k.rightArrowKey.isPressed || k.dKey.isPressed) ? 1 : 0) - ((k.leftArrowKey.isPressed || k.aKey.isPressed) ? 1 : 0);
                CurrentSteering = !AttractDemoActive && UseExternalSteering && !ExternalSteeringAvailable ? 0 :
                    Mathf.Lerp(CurrentSteering, Mathf.Clamp(steer, -1, 1), 1 - Mathf.Exp(-dt * 9));
                if (active)
                {
                    TravelledMetres += SpeedKmh / 3.6 * dt;
                    elapsed += dt;
                    LateralPosition = Mathf.Clamp(LateralPosition + CurrentSteering * LateralSpeed * dt, -9.5f, -1.5f);
                    route.Advance((float)TravelledMetres, LateralPosition);
                }
                bank = Mathf.Lerp(bank, -CurrentSteering * CameraBankDegrees, 1 - Mathf.Exp(-dt * 4));
                EnsureTraffic();
                var hit = traffic.Step(dt, (float)(TravelledMetres - previousDistance), previousLateral,
                    LateralPosition, SpeedKmh, active, maximumLateralSpeed: LateralSpeed);
                SyncTrafficRoutes();
                if (hit.HasValue && !AttractDemoActive)
                {
                    // Traffic latches a continuous contact, so this penalty happens once.
                    // Keep the pre-impact relative speed in the event for sound and camera strength.
                    if (CollisionSlowdownEnabled)
                        SpeedKmh = Mathf.Clamp(SpeedKmh, 0, SpeedLimitKmh) * (1 - Mathf.Clamp01(CollisionSpeedLossPercent / 100));
                    CollisionCount++;
                    Collided?.Invoke(hit.Value);
                }
            }
            RenderWorld();
        }

        public void ResetRide()
        {
            TravelledMetres = 0; elapsed = 0; LateralPosition = -3.1f; route.Reset();
            CurrentSteering = 0; SteeringInput = 0; bank = 0; Paused = false;
            SpeedKmh = DefaultSpeedKmh; KeyboardSpeedControl = 0; CollisionCount = 0; RideRevision++;
            EnsureTraffic(); traffic.Reset(SpeedKmh, LateralPosition); SyncTrafficRoutes();
        }

        public void ApplySpeedControl(float control, float acceleration, float braking, float dt)
        {
            if (Paused || Time.timeScale <= 0 || AttractDemoActive) return;
            SpeedKmh = Mathf.Clamp(SpeedKmh + Mathf.Clamp(control, -1, 1) *
                (control > 0 ? acceleration : braking) * Mathf.Clamp(dt, 0, .05f), 0, SpeedLimitKmh);
        }

        private void EnsureTraffic()
        {
            float availableLateral = LateralSpeed * Mathf.Clamp01(TrafficSteeringScale);
            if (traffic != null && trafficSeed == Seed && trafficDense == DenseTraffic &&
                Mathf.Approximately(trafficDrawDistance, DrawDistance) &&
                Mathf.Approximately(trafficLateralSpeed, availableLateral) &&
                Mathf.Approximately(trafficSmoothing, TrafficInputSmoothingSeconds) &&
                Mathf.Approximately(trafficSpeedLimit, SpeedLimitKmh)) return;
            bool create = traffic == null || trafficSeed != Seed;
            trafficSeed = Seed; trafficDrawDistance = DrawDistance; trafficDense = DenseTraffic;
            trafficLateralSpeed = availableLateral; trafficSmoothing = TrafficInputSmoothingSeconds;
            trafficSpeedLimit = SpeedLimitKmh;
            if (create)
            {
                traffic = new HighwayTraffic(Seed, DrawDistance, DenseTraffic, availableLateral, trafficSmoothing, LateralPosition, trafficSpeedLimit);
                traffic.CanRecycle = CanRecycleTraffic;
                traffic.PlaceRecycledVehicle = PlaceRecycledTraffic;
                traffic.CollisionAllowed = CanTrafficCollide;
                Array.Clear(trafficRouteRevisions, 0, TrafficCount);
                traffic.Reset(SpeedKmh, LateralPosition);
                SyncTrafficRoutes();
            }
            else traffic.Configure(DrawDistance, DenseTraffic, availableLateral, trafficSmoothing, LateralPosition, trafficSpeedLimit);
        }

        [ContextMenu("Refresh preview")]
        public void RefreshPreview()
        {
            if (MainCamera == null || SurfaceMaterial == null || GlowMaterial == null) return;
            if (!built) BuildMeshes();
            RenderWorld();
        }

        private void BuildMeshes()
        {
            ReleaseMeshes();
            generatedRoot = new GameObject("GeneratedRoadGeometry") { hideFlags = HideFlags.DontSave };
            generatedRoot.transform.SetParent(transform, false);
            surfaceMesh = CreateMesh("RoadAndCity", SurfaceMaterial);
            glowMesh = CreateMesh("LightsAndStars", GlowMaterial);
            exitSignText = CreateSignText("ExitLabel");
            mainSignText = CreateSignText("MainlineLabel");
            built = true;
        }

        private TextMeshPro CreateSignText(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(generatedRoot.transform, false);
            var text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 6f;
            text.color = new Color(.8f, 1f, .9f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(5, 1);
            return text;
        }

        private Mesh CreateMesh(string name, Material material)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(generatedRoot.transform, false);
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mesh;
        }

        private void OnDisable() { ReleaseMeshes(); }
        private void ReleaseMeshes()
        {
            if (surfaceMesh != null) Dispose(surfaceMesh);
            if (glowMesh != null) Dispose(glowMesh);
            if (generatedRoot != null) Dispose(generatedRoot);
            built = false;
        }
        private static void Dispose(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }

        private Vector3 Center(float s) => route.Sample(s - StartingDistance).Position;
        private Vector3 Tangent(float s)
        {
            Vector3 d = route.Sample(s - StartingDistance).Forward; d.y = 0; return d.normalized;
        }
        private Vector3 Point(float s, float lateral, float height = 0)
        {
            Vector3 tangent = Tangent(s);
            Vector3 right = new Vector3(tangent.z, 0, -tangent.x);
            return inverseHeading * (Center(s) + right * lateral + Vector3.up * height - origin);
        }
        // Sample the same camera-relative road used by the world, after its Update.
        public Vector3 RoadPoint(float distanceAhead, float lateral, float height = 0)
            => transform.TransformPoint(Point(RouteDistance + distanceAhead, lateral, height));
        private float Noise(int index, int salt = 0)
        {
            unchecked
            {
                uint x = (uint)(index * 374761393 + (salt + Seed) * 668265263);
                x = (x ^ (x >> 13)) * 1274126177; x ^= x >> 16;
                return (x & 0xFFFFFF) / 16777215f;
            }
        }

        private void RenderWorld()
        {
            float s = RouteDistance;
            origin = Center(s);
            inverseHeading = Quaternion.Inverse(Quaternion.LookRotation(Tangent(s), Vector3.up));
            MainCamera.transform.localPosition = new Vector3(LateralPosition, EyeHeight + .008f * Mathf.Sin(elapsed * 4), 0);
            float grade = (Center(s + 2).y - Center(s - 2).y) / 4;
            MainCamera.transform.localRotation = Quaternion.Euler(1.0f - Mathf.Atan(grade) * Mathf.Rad2Deg, CurrentSteering * 1.2f, bank);
            MainCamera.fieldOfView = FieldOfView + (SpeedKmh - 138) * .025f;
            surfaces.Clear(); lights.Clear();
            BuildRoad(s);
            BuildDirectionSigns(s);
            BuildTraffic(s);
            BuildCity(s);
            BuildStars(s);
            surfaces.Upload(surfaceMesh); lights.Upload(glowMesh);
            RenderedLightCount = lights.VertexCount / 4;
        }

        private void Strip(float s0, float s1, float left, float right, float h, Color c0, Color c1)
        { surfaces.Quad(Point(s0, left, h), Point(s0, right, h), Point(s1, right, h), Point(s1, left, h), c0, c0, c1, c1); }

        // Shared by rendering and pass-by audio. Integrating player travel avoids cars
        // teleporting when speed changes (elapsed * current relative speed does not).
        public void SampleTraffic(int index, out float distanceAhead, out float lateral, out float relativeSpeed)
        {
            if (!TrySampleTraffic(index, out distanceAhead, out lateral, out relativeSpeed))
            { distanceAhead = DrawDistance + 1000; relativeSpeed = 0; }
        }

        public bool TrySampleTraffic(int index, out float distanceAhead, out float lateral, out float relativeSpeed)
        {
            EnsureTraffic();
            bool visible = traffic.Sample(index, out distanceAhead, out lateral, out relativeSpeed);
            // Audio/autopilot samples only cars on the rider's road. Rendering uses the
            // persistent world-space frame separately, so neighbouring motorway cars stay visible.
            return visible && TrafficSharesRiderRoad(index);
        }

        private void BuildCity(float s)
        {
            int row = Mathf.FloorToInt(origin.z / 110), column = Mathf.FloorToInt(origin.x / 120);
            for (int z = row - 11; z <= row + 11; z++)
            for (int x = column - 11; x <= column + 11; x++)
            {
                int id = unchecked(z * 73856093 ^ x * 19349663);
                if (Noise(id, 9) > .32f) continue;
                Vector3 world = new Vector3(x * 120 + (Noise(id,11)-.5f)*72, -8, z * 110 + Noise(id,10)*40);
                Vector3 local = inverseHeading * (world - origin);
                if (local.z < -80 || local.z > DrawDistance + 150 || Mathf.Abs(local.x) > local.z * 1.5f + 120) continue;
                float width = 8 + Noise(id,12)*17, height = 8 + Mathf.Pow(Noise(id,13),2)*57;
                if (NearVisibleRoad(world, width + 20)) continue;
                // Fixed world-facing blocks remain stationary throughout the turn.
                Vector3 right = Vector3.right * width, back = Vector3.forward * width;
                CityFace(world-right-back, world+right-back, height, id, false);
                CityFace(world+right+back, world-right+back, height, id, false);
                CityFace(world-right+back, world-right-back, height, id, true);
                CityFace(world+right-back, world+right+back, height, id, true);
                surfaces.Quad(WorldPoint(world-right-back+Vector3.up*height), WorldPoint(world+right-back+Vector3.up*height),
                    WorldPoint(world+right+back+Vector3.up*height), WorldPoint(world-right+back+Vector3.up*height), new Color(.003f,.005f,.009f));
                if (height > 35) Glow(WorldPoint(world+Vector3.up*(height+1)), .22f, new Color(1.6f,.018f,.006f));
            }
        }
        private Vector3 WorldPoint(Vector3 world) => inverseHeading * (world - origin);
        private void CityFace(Vector3 a, Vector3 b, float height, int id, bool side)
        {
            // Only windows facing the rider need billboards.
            Vector3 normal = Vector3.Cross(Vector3.up, b-a).normalized;
            surfaces.Quad(WorldPoint(a), WorldPoint(b), WorldPoint(b+Vector3.up*height), WorldPoint(a+Vector3.up*height),
                side ? new Color(.002f,.0035f,.007f) : new Color(.0025f,.0045f,.009f));
            if (Vector3.Dot(normal, origin-(a+b)*.5f) <= 0) return;
            for (int row = 0; row < height / 3; row++)
            for (int col = 0; col < 7; col++)
            {
                if (Noise(id*1000+row*10+col,14) < .38f) continue;
                Color window = Color.Lerp(new Color(.8f,.24f,.04f),new Color(.55f,.75f,1),Noise(id,15));
                Glow(WorldPoint(Vector3.Lerp(a,b,(col+1)/8f)+Vector3.up*(2+row*3)+normal*.05f),.28f,
                    window*(.2f+Noise(id*1000+row*10+col,16)*.7f));
            }
        }

        private void BuildStars(float s)
        {
            // Fixed world directions; camera-relative rotation gives the sky its slow motion on bends.
            Vector3 camera=MainCamera.transform.localPosition;
            for(int i=0;i<2200;i++)
            {
                float yaw=Noise(i,20)*Mathf.PI*2;
                float elevation=.025f+Noise(i,21)*1.2f;
                Vector3 dir=new Vector3(Mathf.Sin(yaw)*Mathf.Cos(elevation),Mathf.Sin(elevation),Mathf.Cos(yaw)*Mathf.Cos(elevation));
                dir=inverseHeading*dir;
                if(dir.z<.02f)continue;
                float brightness=.18f+Mathf.Pow(Noise(i,22),6)*1.8f;
                float pulse=.90f+.10f*Mathf.Sin(elapsed*.4f+i);
                Glow(camera+dir*1600,4+Noise(i,23)*4,new Color(.55f,.68f,1)*brightness*pulse,false);
            }
        }

        private void Glow(Vector3 p,float radius,Color color,bool distanceFade=true)
        {
            Vector3 camera=MainCamera.transform.localPosition;
            float depth=p.z-camera.z;
            if(depth<.35f || depth>1900 || Mathf.Abs(p.x-camera.x)>depth*1.8f+10)return;
            if(distanceFade)color*=Mathf.Clamp01((DrawDistance+100-depth)/300);
            radius=Mathf.Max(radius,depth*(distanceFade?.005f:.0025f));
            Vector3 right=MainCamera.transform.right*radius,up=MainCamera.transform.up*radius;
            lights.Quad(p-right-up,p+right-up,p+right+up,p-right+up,color);
        }
        private void GroundGlow(float s,float x,float width,float length,Color color)
        { color.a=2;lights.Quad(Point(s-length,x-width,.035f),Point(s-length,x+width,.035f),Point(s+length,x+width,.035f),Point(s+length,x-width,.035f),color); }
        private void LineBox(Vector3 a,Vector3 b,float width,Color color)
        {
            Vector3 side=Vector3.Cross(b-a,MainCamera.transform.forward).normalized*width;
            surfaces.Quad(a-side,a+side,b+side,b-side,color);
        }

        private sealed class MeshBatch
        {
            public float Opacity = -1;
            private readonly List<Vector3> vertices=new List<Vector3>(50000);
            private readonly List<Color> colors=new List<Color>(50000);
            private readonly List<Vector2> uvs=new List<Vector2>(50000);
            private readonly List<int> triangles=new List<int>(75000);
            public int VertexCount=>vertices.Count;
            public void Clear(){vertices.Clear();colors.Clear();uvs.Clear();triangles.Clear();}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)=>Quad(a,b,c,d,color,color,color,color);
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color ca,Color cb,Color cc,Color cd)
            {
                if(Opacity>=0)ca.a=cb.a=cc.a=cd.a=Opacity;
                int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                colors.Add(ca);colors.Add(cb);colors.Add(cc);colors.Add(cd);
                uvs.Add(Vector2.zero);uvs.Add(Vector2.right);uvs.Add(Vector2.one);uvs.Add(Vector2.up);
                triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);
            }
            public void Upload(Mesh mesh)
            {
                mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0,false);
                mesh.bounds=new Bounds(Vector3.zero,Vector3.one*10000);
            }
        }
    }
}
