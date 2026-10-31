using UnityEngine;

namespace HighwaySurfing
{
    public sealed partial class NightHighwayWorld
    {
        private void SyncTrafficRoutes()
        {
            for (int i = 0; i < TrafficCount; i++)
            {
                int revision = traffic.Revision(i);
                if (trafficRouteRevisions[i] == revision) continue;
                traffic.Sample(i, out float d, out _, out _);
                trafficRoutes.Reset(i, route, (float)TravelledMetres + d, i >= HighwayTraffic.PerDirectionCount);
                trafficRouteRevisions[i] = revision;
            }
        }

        private Vector3 TrafficViewPoint(Vector3 point)
        {
            var rider = route.Sample((float)TravelledMetres);
            Vector3 forward = rider.Forward; forward.y = 0;
            return Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up)) * (point - rider.Point(LateralPosition, EyeHeight));
        }

        private bool TrafficSharesRiderRoad(int index)
        {
            var rider = route.Sample((float)TravelledMetres);
            var car = trafficRoutes.Sample(index, (float)TravelledMetres);
            return (rider.Position - car.Position).sqrMagnitude < .25f && Vector3.Dot(rider.Forward, car.Forward) > .999f;
        }

        private bool CanTrafficCollide(int index) =>
            (!UseExternalSteering || ExternalSteeringAvailable || AttractDemoActive) && TrafficSharesRiderRoad(index);

        private bool CanRecycleTraffic(int index, float distance)
        {
            var frame = trafficRoutes.Sample(index, (float)TravelledMetres + distance);
            Vector3 p = TrafficViewPoint(frame.Position);
            // A longitudinal wrap alone is insufficient on a curved ramp: the old mainline
            // may still be in view. Retain the car until it is behind or beyond the far fade.
            return p.z < -100 || p.sqrMagnitude > (DrawDistance + 100) * (DrawDistance + 100);
        }

        private float PlaceRecycledTraffic(int index, float distance)
        {
            float direction = Mathf.Sign(distance);
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float at = (float)TravelledMetres + distance;
                trafficRoutes.Reset(index, route, at, index >= HighwayTraffic.PerDirectionCount);
                Vector3 p = TrafficViewPoint(trafficRoutes.Sample(index, at).Position);
                if (p.z < -Mathf.Max(150, traffic.RevealClearance) ||
                    p.sqrMagnitude > (DrawDistance + 100) * (DrawDistance + 100)) return distance;
                distance += direction * Mathf.Max(300, DrawDistance);
            }
            return distance;
        }

        /// <summary>World-space traffic point for continuity checks and presentation.</summary>
        public Vector3 TrafficPoint(int index)
        {
            EnsureTraffic();
            traffic.Sample(index, out float d, out float lane, out _);
            return trafficRoutes.Sample(index, (float)TravelledMetres + d).Point(lane);
        }

        // A rigid frame at the traffic sample keeps the vehicle straight on bends while
        // matching both the heading and the grade of the road at its centre.
        private struct TrafficFrame
        {
            public Vector3 Origin, Right, Up, Forward;
            public Vector3 At(float x, float y, float z) => Origin + Right * x + Up * y + Forward * z;
        }

        private void BuildTraffic(float s)
        {
            EnsureTraffic();
            for (int i = 0; i < TrafficCount; i++)
            {
                if (!traffic.Sample(i, out float d, out float lane, out _)) continue;
                var road = trafficRoutes.Sample(i, (float)TravelledMetres + d);
                Vector3 point = road.Point(lane);
                Vector3 view = TrafficViewPoint(point);
                float range = view.magnitude;
                if (view.z < -70 || range > DrawDistance) continue;
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(DrawDistance * .65f, DrawDistance, range));
                bool opposite = i >= HighwayTraffic.PerDirectionCount;
                float facing = opposite ? -1f : 1f;
                Vector3 roadForward = road.Forward;
                Vector3 roadRight = road.Right;
                var frame = new TrafficFrame {
                    Origin = WorldPoint(point),
                    Forward = inverseHeading * (roadForward * facing),
                    Right = inverseHeading * (roadRight * facing),
                    Up = inverseHeading * Vector3.Cross(roadForward, roadRight).normalized
                };
                float top = Noise(i, 5) > .8f ? 2.2f : 1.15f;
                // Alpha is used only for dithered distance fading in the opaque surface shader.
                surfaces.Opacity = fade;
                BuildTrafficBody(frame, top, i);
                surfaces.Opacity = 1;

                // Billboards are additive, so only add the lamps on the face the
                // camera can actually see. Both ends retain their physical lamp panels.
                Vector3 toCamera = MainCamera.transform.localPosition - frame.Origin;
                bool frontVisible = Vector3.Dot(toCamera, frame.Forward) > 0;
                float lampZ = frontVisible ? 2.425f : -2.425f;
                Color lamp = frontVisible ? new Color(4.7f, 4.2f, 3.4f) : TailRed;
                float blink = !frontVisible && Mathf.Sin(elapsed * .22f + i * 3) > .96f ? 1.8f : 1f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Glow(frame.At(side * .64f, .55f, lampZ), frontVisible ? .38f : .22f, lamp * (blink * fade));
                }

                // Light falls on the road in front of the headlights and behind the tail.
                for (int side = -1; side <= 1; side += 2)
                {
                    TrafficGroundGlow(frame, side * .64f, 7.4f, .17f, 7, new Color(4.7f, 4.2f, 3.4f) * (.012f * fade));
                    TrafficGroundGlow(frame, side * .64f, -3.9f, .17f, 3, TailRed * (.012f * fade));
                }
            }
        }

        private void TrafficGroundGlow(TrafficFrame frame, float x, float z, float width, float length, Color color)
        {
            color.a = 2;
            lights.Quad(frame.At(x-width,.025f,z-length),frame.At(x+width,.025f,z-length),
                frame.At(x+width,.025f,z+length),frame.At(x-width,.025f,z+length),color);
        }

        private void BuildTrafficBody(TrafficFrame f, float top, int index)
        {
            // Keep the silhouette readable against the dark road without a separate
            // lighting pass. Subtle face differences suggest streetlight reflections.
            float variation = Noise(index, 25);
            Color side = Color.Lerp(new Color(.022f, .032f, .048f), new Color(.045f, .050f, .059f), variation);
            Color end = side * .78f;
            Color upper = side * 1.22f;
            Color glass = new Color(.008f, .015f, .024f);
            Color wheel = new Color(.004f, .006f, .009f);
            const float w = .85f, bottom = .18f, belt = .76f, halfLength = 2.4f;

            // Lower body, exactly 1.7 m wide and 4.8 m long.
            surfaces.Quad(f.At(-w, bottom, -halfLength), f.At(w, bottom, -halfLength),
                f.At(w, belt, -halfLength), f.At(-w, belt, -halfLength), end);
            surfaces.Quad(f.At(w, bottom, halfLength), f.At(-w, bottom, halfLength),
                f.At(-w, belt, halfLength), f.At(w, belt, halfLength), end);
            surfaces.Quad(f.At(-w, bottom, halfLength), f.At(-w, bottom, -halfLength),
                f.At(-w, belt, -halfLength), f.At(-w, belt, halfLength), side);
            surfaces.Quad(f.At(w, bottom, -halfLength), f.At(w, bottom, halfLength),
                f.At(w, belt, halfLength), f.At(w, belt, -halfLength), side);
            surfaces.Quad(f.At(-w, belt, -halfLength), f.At(w, belt, -halfLength),
                f.At(w, belt, halfLength), f.At(-w, belt, halfLength), upper);

            // Tapered cabin, with sloped windscreens and a proper roof.
            const float roofW = .70f, cabinBaseZ = 1.38f, roofZ = .88f;
            surfaces.Quad(f.At(-w, belt, -cabinBaseZ), f.At(w, belt, -cabinBaseZ),
                f.At(roofW, top, -roofZ), f.At(-roofW, top, -roofZ), side);
            surfaces.Quad(f.At(w, belt, cabinBaseZ), f.At(-w, belt, cabinBaseZ),
                f.At(-roofW, top, roofZ), f.At(roofW, top, roofZ), side);
            surfaces.Quad(f.At(-w, belt, cabinBaseZ), f.At(-w, belt, -cabinBaseZ),
                f.At(-roofW, top, -roofZ), f.At(-roofW, top, roofZ), side);
            surfaces.Quad(f.At(w, belt, -cabinBaseZ), f.At(w, belt, cabinBaseZ),
                f.At(roofW, top, roofZ), f.At(roofW, top, -roofZ), side);
            surfaces.Quad(f.At(-roofW, top, -roofZ), f.At(roofW, top, -roofZ),
                f.At(roofW, top, roofZ), f.At(-roofW, top, roofZ), upper);

            // Windows sit just above the cabin surfaces to avoid coplanar flicker.
            float lowY = Mathf.Lerp(belt, top, .15f), highY = Mathf.Lerp(belt, top, .88f);
            float lowZ = Mathf.Lerp(cabinBaseZ, roofZ, .15f) + .012f;
            float highZ = Mathf.Lerp(cabinBaseZ, roofZ, .88f) + .012f;
            float lowW = Mathf.Lerp(w, roofW, .15f) - .08f;
            float highW = Mathf.Lerp(w, roofW, .88f) - .06f;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                surfaces.Quad(f.At(-lowW, lowY, sign * lowZ), f.At(lowW, lowY, sign * lowZ),
                    f.At(highW, highY, sign * highZ), f.At(-highW, highY, sign * highZ), glass);
                float lowX = Mathf.Lerp(w, roofW, .15f) + .012f;
                float highX = Mathf.Lerp(w, roofW, .88f) + .012f;
                surfaces.Quad(f.At(sign * lowX, lowY, -lowZ + .12f), f.At(sign * lowX, lowY, lowZ - .12f),
                    f.At(sign * highX, highY, highZ - .12f), f.At(sign * highX, highY, -highZ + .12f), glass);
                for (int axle = -1; axle <= 1; axle += 2)
                {
                    float z = axle * 1.55f;
                    surfaces.Quad(f.At(sign * (w + .008f), .20f, z - .38f),
                        f.At(sign * (w + .008f), .20f, z + .38f),
                        f.At(sign * (w + .008f), .52f, z + .38f),
                        f.At(sign * (w + .008f), .52f, z - .38f), wheel);
                }
            }

            Color headPanel = new Color(.32f, .30f, .25f);
            Color tailPanel = new Color(.32f, .015f, .008f);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float x = sign * .64f;
                surfaces.Quad(f.At(x - .14f, .49f, halfLength + .008f), f.At(x + .14f, .49f, halfLength + .008f),
                    f.At(x + .14f, .62f, halfLength + .008f), f.At(x - .14f, .62f, halfLength + .008f), headPanel);
                surfaces.Quad(f.At(x - .14f, .49f, -halfLength - .008f), f.At(x + .14f, .49f, -halfLength - .008f),
                    f.At(x + .14f, .62f, -halfLength - .008f), f.At(x - .14f, .62f, -halfLength - .008f), tailPanel);
            }
        }
    }
}
