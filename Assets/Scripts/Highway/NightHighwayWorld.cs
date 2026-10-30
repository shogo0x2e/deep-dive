using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

namespace HighwaySurfing
{
    /// <summary>A continuous, camera-relative night road. Geometry is batched into two reusable meshes.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(200)]
    public sealed class NightHighwayWorld : MonoBehaviour
    {
        [Header("Presentation")]
        public Camera MainCamera;
        public Material SurfaceMaterial;
        public Material GlowMaterial;
        [Range(55, 95)] public float FieldOfView = 76;
        [Range(0.8f, 2.4f)] public float EyeHeight = 1.25f;
        [Range(0, 8)] public float CameraBankDegrees = 2.5f;
        [Header("Ride")]
        [Range(0, 220)] public float SpeedKmh = 138;
        [Range(0, 8)] public float LateralSpeed = 3.5f;
        [Range(-9.5f, -1.5f)] public float LateralPosition = -6.7f;
        public bool Paused;
        [Tooltip("Enable to supply SteeringInput from a Kinect adapter instead of the keyboard.")]
        public bool UseExternalSteering;
        [Range(-1, 1)] public float SteeringInput;
        [HideInInspector] public bool ExternalSteeringAvailable;
        [HideInInspector] public bool ExternalRideReady;
        [Header("World")]
        public int Seed = 47;
        public float StartingDistance = 2200;
        [Range(500, 1800)] public float DrawDistance = 1100;
        public bool ShowHud = true;

        public double TravelledMetres { get; private set; }
        public float CurrentSteering { get; private set; }
        public float RouteDistance => (float)(StartingDistance + TravelledMetres);
        public int RenderedLightCount { get; private set; }
        public const int TrafficCount = 18;

        private readonly MeshBatch surfaces = new MeshBatch();
        private readonly MeshBatch lights = new MeshBatch();
        private Mesh surfaceMesh, glowMesh;
        private GameObject generatedRoot;
        private Vector3 origin;
        private Quaternion inverseHeading;
        private float elapsed, bank;
        private bool built;
        private const float RoadHalfWidth = 11.0f;
        private static readonly Color Amber = new Color(4.5f, 1.65f, .30f);
        private static readonly Color CoolWhite = new Color(2.5f, 3.0f, 3.8f);
        private static readonly Color TailRed = new Color(4f, .07f, .015f);

        private void OnEnable()
        {
            built = false;
            if(Application.isPlaying){Application.runInBackground=true;Application.targetFrameRate=60;}
        }
        private void Update()
        {
            if (MainCamera == null || SurfaceMaterial == null || GlowMaterial == null) return;
            if (!built) BuildMeshes();
            if (Application.isPlaying)
            {
                float dt = Mathf.Min(Time.deltaTime, .05f);
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.spaceKey.wasPressedThisFrame) Paused = !Paused;
                    if (k.hKey.wasPressedThisFrame) ShowHud = !ShowHud;
                    if (k.rKey.wasPressedThisFrame) ResetRide();
                    if (k.upArrowKey.isPressed || k.wKey.isPressed) SpeedKmh += dt * 25;
                    if (k.downArrowKey.isPressed || k.sKey.isPressed) SpeedKmh -= dt * 25;
                }
                SpeedKmh = Mathf.Clamp(SpeedKmh, 0, 220);
                float steer = UseExternalSteering ? SteeringInput : k == null ? 0 :
                    ((k.rightArrowKey.isPressed || k.dKey.isPressed) ? 1 : 0) - ((k.leftArrowKey.isPressed || k.aKey.isPressed) ? 1 : 0);
                CurrentSteering = UseExternalSteering && !ExternalSteeringAvailable ? 0 :
                    Mathf.Lerp(CurrentSteering, Mathf.Clamp(steer, -1, 1), 1 - Mathf.Exp(-dt * 9));
                if (!Paused && (!UseExternalSteering || ExternalRideReady))
                {
                    TravelledMetres += SpeedKmh / 3.6 * dt;
                    elapsed += dt;
                    LateralPosition = Mathf.Clamp(LateralPosition + CurrentSteering * LateralSpeed * dt, -9.5f, -1.5f);
                }
                bank = Mathf.Lerp(bank, -CurrentSteering * CameraBankDegrees, 1 - Mathf.Exp(-dt * 4));
            }
            RenderWorld();
        }

        public void ResetRide()
        {
            TravelledMetres = 0; elapsed = 0; LateralPosition = -6.7f;
            CurrentSteering = 0; SteeringInput = 0; bank = 0; Paused = false;
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
            built = true;
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

        private static Vector3 Center(float s) => new Vector3(
            95 * Mathf.Sin(s / 500) + 27 * Mathf.Sin(s / 210),
            2.8f * Mathf.Sin(s / 700) + 1.5f * Mathf.Sin(s / 340), s);
        private static Vector3 Tangent(float s) => new Vector3(
            95f / 500 * Mathf.Cos(s / 500) + 27f / 210 * Mathf.Cos(s / 210), 0, 1).normalized;
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
            BuildStreetFurniture(s);
            BuildTraffic(s);
            BuildCity(s);
            BuildStars(s);
            surfaces.Upload(surfaceMesh); lights.Upload(glowMesh);
            RenderedLightCount = lights.VertexCount / 4;
        }

        private void Strip(float s0, float s1, float left, float right, float h, Color c0, Color c1)
        { surfaces.Quad(Point(s0, left, h), Point(s0, right, h), Point(s1, right, h), Point(s1, left, h), c0, c0, c1, c1); }

        private void BuildRoad(float s)
        {
            float first = Mathf.Floor((s - 70) / 5) * 5;
            for (float a = first; a < s + DrawDistance; a += 5)
            {
                float d = a - s;
                float nearGlow = Mathf.Exp(-Mathf.Pow((d - 22) / 32, 2));
                float lampPool = Mathf.Pow(.5f + .5f * Mathf.Cos(a / 42 * Mathf.PI * 2), 12);
                Color asphalt = new Color(.001f, .0018f, .0035f) + new Color(.002f, .003f, .0045f) * nearGlow;
                asphalt += new Color(.003f, .0011f, .0002f) * lampPool;
                Strip(a, a + 5, -RoadHalfWidth, RoadHalfWidth, -.02f, asphalt, asphalt);
                Strip(a, a + 5, -300, -RoadHalfWidth, -.12f, new Color(.0015f,.003f,.006f), new Color(.0015f,.003f,.006f));
                Strip(a, a + 5, RoadHalfWidth, 300, -.12f, new Color(.0015f,.003f,.006f), new Color(.0015f,.003f,.006f));
                Color edge = new Color(.055f, .085f, .13f) * (.3f + .7f * Mathf.Exp(-Mathf.Max(d, 0) / 160));
                Strip(a, a + 5, -10, -9.88f, .015f, edge, edge);
                Strip(a, a + 5, -.9f, -.80f, .015f, new Color(.35f,.19f,.04f), new Color(.35f,.19f,.04f));
                Strip(a, a + 5, .8f, .9f, .015f, new Color(.12f,.08f,.03f), new Color(.12f,.08f,.03f));
                Strip(a, a + 5, 9.88f, 10, .015f, edge * .4f, edge * .4f);
                Strip(a, a + 5, -.62f, .62f, .35f, new Color(.008f,.014f,.022f), new Color(.008f,.014f,.022f));
                if (Mathf.RoundToInt(a / 5) % 3 == 0)
                {
                    Color dash = new Color(.45f, .52f, .59f) * (.3f + .7f * Mathf.Exp(-Mathf.Max(d, 0) / 130));
                    Strip(a, a + 4, -5.46f, -5.3f, .02f, dash, dash);
                    Strip(a, a + 4, 5.3f, 5.46f, .02f, dash * .35f, dash * .35f);
                }
                // Narrow reflective guardrails catch the staccato roadside lights.
                for(int guardSide=-1;guardSide<=1;guardSide+=2)
                {
                    float x=10.7f*guardSide;
                    surfaces.Quad(Point(a,x,.65f),Point(a,x,.80f),Point(a+5,x,.80f),Point(a+5,x,.65f),new Color(.004f,.007f,.013f));
                    surfaces.Quad(Point(a,x,.30f),Point(a,x,.39f),Point(a+5,x,.39f),Point(a+5,x,.30f),new Color(.002f,.004f,.008f));
                }
            }
        }

        private void BuildStreetFurniture(float s)
        {
            int start = Mathf.FloorToInt((s - 40) / 42);
            for (int n = start; n < start + DrawDistance / 42 + 2; n++)
            {
                float at = n * 42;
                Color pole = new Color(.004f,.006f,.010f);
                LineBox(Point(at,0,.35f),Point(at,0,8.5f),.055f,pole);
                LineBox(Point(at,0,8.5f),Point(at,-2.4f,8.7f),.05f,pole);
                LineBox(Point(at,0,8.5f),Point(at,2.4f,8.7f),.05f,pole);
                Glow(Point(at-.22f,-2.4f,8.64f), .85f, Amber);
                Glow(Point(at-.22f,2.4f,8.64f), .7f, Amber * .75f);
                // Long soft pools, drawn in road space so they follow the curvature.
                GroundGlow(at, -5.6f, 3.5f, 13, new Color(.012f,.004f,.0006f));
                GroundGlow(at, 5.6f, 3.2f, 11, new Color(.008f,.003f,.0005f));
            }
            int markerStart = Mathf.FloorToInt((s - 30) / 10);
            for (int n = markerStart; n < markerStart + DrawDistance / 10 + 2; n++)
            {
                float at=n*10;
                Glow(Point(at,-10.58f,.76f),.17f,CoolWhite * .65f);
                Glow(Point(at,-.68f,.58f),.16f,Amber * .55f);
                Glow(Point(at,10.58f,.76f),.13f,CoolWhite * .20f);
                // Very subdued streaks on the near shoulder reinforce speed without obscuring the road.
                if (at-s < 75 && at-s > 2)
                    Strip(at-2.5f,at,-10.35f,-10.30f,.025f,new Color(.004f,.008f,.014f),new Color(.06f,.12f,.18f));
            }
            int signStart=Mathf.FloorToInt((s-40)/650);
            for(int n=signStart;n<signStart+3;n++)
            {
                float at=n*650+300;
                if(at-s>DrawDistance||at-s<-30) continue;
                Color metal=new Color(.026f,.037f,.048f);
                LineBox(Point(at,-10.9f,0),Point(at,-10.9f,7.4f),.10f,metal);
                LineBox(Point(at,10.9f,0),Point(at,10.9f,7.4f),.10f,metal);
                LineBox(Point(at,-10.9f,7.2f),Point(at,10.9f,7.2f),.10f,metal);
                surfaces.Quad(Point(at,-8.9f,5.6f),Point(at,-2.1f,5.6f),Point(at,-2.1f,7.2f),Point(at,-8.9f,7.2f),new Color(.009f,.09f,.064f));
                // Abstract route bars remain crisp at speed, without claiming a real destination.
                for(int j=0;j<2;j++)
                {
                    float x=-7.5f+j*3.2f;
                    LineBox(Point(at-.04f,x,5.9f),Point(at-.04f,x,6.65f),.055f,new Color(.7f,.95f,.85f));
                    LineBox(Point(at-.04f,x,6.65f),Point(at-.04f,x-.23f,6.40f),.05f,new Color(.7f,.95f,.85f));
                    LineBox(Point(at-.04f,x,6.65f),Point(at-.04f,x+.23f,6.40f),.05f,new Color(.7f,.95f,.85f));
                }
                Glow(Point(at,-5.5f,7.25f),.36f,new Color(1.0f,1.5f,1.3f));
            }
        }

        // Shared by rendering and pass-by audio. Integrating player travel avoids cars
        // teleporting when speed changes (elapsed * current relative speed does not).
        public void SampleTraffic(int index, out float distanceAhead, out float lateral, out float relativeSpeed)
        {
            bool opposite = index >= 9;
            float vehicleSpeed = opposite ? -24 - Noise(index, 8) * 10 : 24 + Noise(index, 8) * 14;
            relativeSpeed = vehicleSpeed - SpeedKmh / 3.6f;
            distanceAhead = Mathf.Repeat(90 + index * 117 + elapsed * vehicleSpeed - (float)TravelledMetres, DrawDistance + 180) - 80;
            lateral = opposite ? (index % 2 == 0 ? 3.1f : 7.5f) : (index % 2 == 0 ? -3.1f : -7.5f);
        }

        private void BuildTraffic(float s)
        {
            for(int i=0;i<TrafficCount;i++)
            {
                bool opposite=i>=9;
                SampleTraffic(i, out float d, out float lane, out _);
                float at=s+d;
                Color body=new Color(.0015f,.0025f,.0045f);
                float top=Noise(i,5)>.8f?2.2f:1.15f;
                surfaces.Quad(Point(at,lane-.85f,.22f),Point(at,lane+.85f,.22f),Point(at,lane+.85f,top),Point(at,lane-.85f,top),body);
                Color col=opposite ? new Color(4.7f,4.2f,3.4f) : TailRed;
                float blink=!opposite && Mathf.Sin(elapsed*.22f+i*3)> .96f ? 1.8f : 1f;
                for(int side=-1;side<=1;side+=2)
                {
                    Glow(Point(at-.05f,lane+side*.64f,.65f),opposite?.38f:.22f,col*blink);
                    GroundGlow(at,lane+side*.64f,.17f,opposite?13:5,col*.012f);
                    if(top>2) Glow(Point(at-.05f,lane+side*.79f,top-.1f),.10f,opposite?Amber:TailRed*.5f);
                }
            }
        }

        private void BuildCity(float s)
        {
            int tile=Mathf.FloorToInt(s/85);
            for(int n=tile-2;n<tile+19;n++)
            for(int side=-1;side<=1;side+=2)
            {
                int id=n*3+side;
                float at=n*85+Noise(id,10)*40;
                float x=side*(65+Noise(id,11)*240);
                float width=8+Noise(id,12)*17;
                float height=8+Mathf.Pow(Noise(id,13),2)*57;
                Vector3 a=Point(at,x-width,0),b=Point(at,x+width,0),c=Point(at,x+width,height),d=Point(at,x-width,height);
                surfaces.Quad(a,b,c,d,new Color(.0025f,.0045f,.009f));
                for(int row=0;row<height/3;row++)
                for(int col=0;col<7;col++)
                {
                    if(Noise(id*1000+row*10+col,14)<.38f) continue;
                    Color window=Color.Lerp(new Color(.8f,.24f,.04f),new Color(.55f,.75f,1.0f),Noise(id,15));
                    Glow(Point(at-.1f,x-width+2+col*(width*2-4)/6,2+row*3),.28f,window*(.2f+Noise(id*1000+row*10+col,16)*.7f));
                }
                if(height>35) Glow(Point(at,x,height+1),.22f,new Color(1.6f,.018f,.006f));
                for(int j=0;j<10;j++)
                    Glow(Point(at+j*5,x-width*2+j*width*.4f,1+Noise(n*10+j,17)*2),.22f,Amber*.25f);
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
            private readonly List<Vector3> vertices=new List<Vector3>(50000);
            private readonly List<Color> colors=new List<Color>(50000);
            private readonly List<Vector2> uvs=new List<Vector2>(50000);
            private readonly List<int> triangles=new List<int>(75000);
            public int VertexCount=>vertices.Count;
            public void Clear(){vertices.Clear();colors.Clear();uvs.Clear();triangles.Clear();}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)=>Quad(a,b,c,d,color,color,color,color);
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color ca,Color cb,Color cc,Color cd)
            {
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
