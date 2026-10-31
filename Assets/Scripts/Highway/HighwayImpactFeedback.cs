using UnityEngine;
using UnityEngine.Rendering;

namespace HighwaySurfing
{
    /// <summary>Brief, camera-rendered and audible feedback for contact with traffic.</summary>
    [DefaultExecutionOrder(400)]
    public sealed class HighwayImpactFeedback : MonoBehaviour
    {
        public NightHighwayWorld World;
        public HighwayRideFeedback RideFeedback;
        [Tooltip("Optional override; otherwise the ride feedback's streak material is used.")]
        public Material ImpactMaterial;
        [Range(0, 2)] public float ImpactStrength = 1;
        [Tooltip("Volume of the low impact thud, independent of the camera effect strength.")]
        [Range(0, 1)] public float ImpactVolume = .85f;
        [Tooltip("Briefly lower drive and pass-by sounds so the impact's low thud stays audible.")]
        public bool DuckRideSound = true;
        public AudioSource ImpactSource => source;
        public int HitCount { get; private set; }
        public float LastStrength { get; private set; }
        public bool IsImpactActive => impactAge < Duration || (source != null && source.isPlaying);

        const float Duration = .36f;
        const float HitInterval = .16f;
        NightHighwayWorld subscribedWorld;
        int revision;
        float impactAge = Duration, direction, lastHitTime = float.NegativeInfinity;
        GameObject generated, overlay;
        AudioSource source;
        AudioClip clip;
        Mesh mesh;
        Material ownedMaterial;
        readonly Vector3[] vertices = new Vector3[4];
        readonly Vector2[] uv = { new Vector2(0, -2), new Vector2(1, -2), new Vector2(1, -1), new Vector2(0, -1) };
        readonly Color[] colors = new Color[4];
        readonly int[] triangles = { 0, 1, 2, 0, 2, 3 };
        Transform affectedCamera;
        Vector3 basePosition, appliedPosition;
        Quaternion baseRotation, appliedRotation;
        bool appliedPose;

        void OnEnable()
        {
            BindWorld();
            // Prepare the non-streaming clip before the first hit, including on Windows.
            EnsureAudio();
        }

        void BindWorld()
        {
            if (subscribedWorld == World) return;
            ClearImpact();
            ReleaseOverlay();
            if (subscribedWorld != null) subscribedWorld.Collided -= OnCollision;
            subscribedWorld = World;
            if (subscribedWorld == null) return;
            revision = subscribedWorld.RideRevision;
            subscribedWorld.Collided += OnCollision;
        }

        bool RideActive()
        {
            return World != null && World.isActiveAndEnabled && World.MainCamera != null &&
                World.SurfaceMaterial != null && World.GlowMaterial != null && !World.Paused &&
                Time.timeScale > 0 && !World.AttractDemoActive && (!World.UseExternalSteering || World.ExternalRideReady);
        }

        void OnCollision(HighwayHit hit)
        {
            if (World != subscribedWorld) return;
            if (revision != World.RideRevision)
            {
                ClearImpact();
                revision = World.RideRevision;
                lastHitTime = float.NegativeInfinity;
            }
            if (!RideActive() || Time.unscaledTime - lastHitTime < HitInterval) return;
            lastHitTime = Time.unscaledTime;
            HitCount++;
            LastStrength = Mathf.Clamp01(Mathf.Lerp(.35f, 1, Mathf.Clamp01(hit.RelativeSpeedKmh / 110f)) * ImpactStrength);
            direction = hit.LateralOffset >= 0 ? -1 : 1;
            impactAge = 0;
            EnsureAudio();
            if (source != null && Volume() > 0 && (RideFeedback == null || RideFeedback.SoundEnabled))
            {
                source.Stop();
                source.volume = Volume();
                source.panStereo = Mathf.Clamp(hit.LateralOffset / 4, -.55f, .55f);
                source.pitch = Mathf.Lerp(.92f, 1.12f, LastStrength);
                source.Play();
                if (DuckRideSound && RideFeedback != null) RideFeedback.DuckForImpact();
            }
        }

        // Even a side scrape needs an audible cue. Visual intensity is not an audio mute.
        float Volume() => ImpactVolume * Mathf.Lerp(.7f, 1, LastStrength) * (RideFeedback != null ? RideFeedback.MasterVolume : 1);

        void LateUpdate()
        {
            BindWorld();
            if (World == null) return;
            if (revision != World.RideRevision)
            {
                ClearImpact();
                revision = World.RideRevision;
                lastHitTime = float.NegativeInfinity;
            }
            if (!RideActive()) { ClearImpact(); return; }
            // World normally overwrites the camera each Update. Restore only a pose that is
            // still ours, so a stopped/disabled world cannot accumulate offsets or be displaced.
            UnwindPose();
            impactAge += Mathf.Min(Time.unscaledDeltaTime, .1f);
            bool effects = RideFeedback == null || RideFeedback.EffectsEnabled;
            if (source != null && source.isPlaying)
            {
                if (RideFeedback != null && !RideFeedback.SoundEnabled) source.Stop();
                else source.volume = Volume();
            }
            if (!effects || impactAge >= Duration) { HideOverlay(); return; }
            float t = Mathf.Clamp01(impactAge / Duration);
            float envelope = (1 - t) * (1 - t);
            float strength = LastStrength * envelope;
            Transform cameraTransform = World.MainCamera.transform;
            basePosition = cameraTransform.localPosition;
            baseRotation = cameraTransform.localRotation;
            float shake = Mathf.Sin(impactAge * 62) * (1 - t);
            cameraTransform.localPosition = basePosition + new Vector3(direction * .035f * strength + .008f * shake * strength, -.012f * strength, -.025f * strength);
            cameraTransform.localRotation = baseRotation * Quaternion.Euler(-.35f * strength, direction * .65f * strength, -direction * .8f * strength + .18f * shake * strength);
            affectedCamera = cameraTransform;
            appliedPosition = cameraTransform.localPosition;
            appliedRotation = cameraTransform.localRotation;
            appliedPose = true;
            DrawOverlay(strength);
        }

        void UnwindPose()
        {
            if (appliedPose && affectedCamera != null &&
                (affectedCamera.localPosition - appliedPosition).sqrMagnitude < .00000001f &&
                Quaternion.Angle(affectedCamera.localRotation, appliedRotation) < .001f)
            {
                affectedCamera.localPosition = basePosition;
                affectedCamera.localRotation = baseRotation;
            }
            appliedPose = false;
            affectedCamera = null;
        }

        void EnsureAudio()
        {
            if (source != null) return;
            if (generated == null)
            {
                generated = new GameObject("GeneratedImpactAudio") { hideFlags = HideFlags.DontSave };
                generated.transform.SetParent(transform, false);
            }
            source = generated.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
            source.dopplerLevel = 0; source.priority = 32;
            if (clip == null) clip = MakeImpactClip();
            source.clip = clip;
        }

        static AudioClip MakeImpactClip()
        {
            const int rate = 44100;
            const int count = 22050; // 500 ms; preloaded once, with a soft, low tail.
            var samples = new float[count];
            uint random = 0x7451u;
            float low = 0, bodyNoise = 0, phase = 0, peak = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                random = 1664525u * random + 1013904223u;
                float noise = ((random >> 8) / 8388607.5f) - 1;
                low += .015f * (noise - low);
                bodyNoise += .07f * (noise - bodyNoise);
                phase += 2 * Mathf.PI * Mathf.Lerp(108, 68, Mathf.Clamp01(t / .20f)) / rate;
                // Weight and rounded body in the low band; no sharp metallic crack or hiss.
                // Quiet 2nd/3rd harmonics keep the thud audible on smaller venue speakers.
                float thunk = (Mathf.Sin(phase) * .62f + Mathf.Sin(phase * 2 + .2f) * .25f +
                    Mathf.Sin(phase * 3 + .35f) * .12f) * Mathf.Exp(-t * 9.5f);
                float knock = (bodyNoise - low) * Mathf.Exp(-t * 20) * .55f + low * Mathf.Exp(-t * 12) * .3f;
                float attack = Mathf.Min(1, t / .004f);
                float release = Mathf.Clamp01((.5f - t) / .035f);
                samples[i] = (thunk + knock) * attack * release;
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }
            for (int i = 0; i < count; i++) samples[i] *= .95f / Mathf.Max(.001f, peak);
            var result = AudioClip.Create("Low vehicle impact thud", count, 1, rate, false);
            result.hideFlags = HideFlags.DontSave;
            result.SetData(samples, 0);
            return result;
        }

        void DrawOverlay(float strength)
        {
            Camera cam = World.MainCamera;
            if (overlay != null && overlay.transform.parent != cam.transform) ReleaseOverlay();
            if (overlay == null)
            {
                Material material = ImpactMaterial != null ? ImpactMaterial : RideFeedback != null ? RideFeedback.StreakMaterial : null;
                if (material == null)
                {
                    Shader shader = Shader.Find("Highway/RideStreak");
                    if (shader == null) return;
                    ownedMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                    material = ownedMaterial;
                }
                overlay = new GameObject("Peripheral impact flash") { hideFlags = HideFlags.DontSave, layer = cam.gameObject.layer };
                overlay.transform.SetParent(cam.transform, false);
                mesh = new Mesh { name = "Impact rim", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
                mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
                overlay.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = overlay.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            overlay.SetActive(true);
            float depth = Mathf.Max(1, cam.nearClipPlane + .1f);
            float height = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * .5f) * depth;
            float width = height * cam.aspect;
            vertices[0] = new Vector3(-width, -height, depth); vertices[1] = new Vector3(width, -height, depth);
            vertices[2] = new Vector3(width, height, depth); vertices[3] = new Vector3(-width, height, depth);
            var color = new Color(1, .19f, .035f, .22f * strength);
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            mesh.vertices = vertices; mesh.colors = colors; mesh.RecalculateBounds();
        }

        void HideOverlay() { if (overlay != null) overlay.SetActive(false); }
        void ClearImpact()
        {
            UnwindPose();
            impactAge = Duration;
            if (source != null) source.Stop();
            HideOverlay();
        }
        void ReleaseOverlay()
        {
            if (overlay != null) Destroy(overlay);
            if (mesh != null) Destroy(mesh);
            if (ownedMaterial != null) Destroy(ownedMaterial);
            overlay = null; mesh = null; ownedMaterial = null;
        }
        void OnDisable()
        {
            ClearImpact();
            if (subscribedWorld != null) subscribedWorld.Collided -= OnCollision;
            subscribedWorld = null;
            ReleaseOverlay();
            if (generated != null) Destroy(generated);
            generated = null; source = null;
            lastHitTime = float.NegativeInfinity;
        }
        void OnDestroy() { if (clip != null) Destroy(clip); }
    }
}
