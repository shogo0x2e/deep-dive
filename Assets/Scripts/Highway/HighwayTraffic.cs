using System;
using UnityEngine;

namespace HighwaySurfing
{
    /// <summary>A single contact. Offset is vehicle minus player; speed is the longitudinal relative speed magnitude.</summary>
    public struct HighwayHit
    {
        public int VehicleId;
        public float LateralOffset;
        public float RelativeSpeedKmh;
    }

    /// <summary>
    /// Camera-relative, deterministic traffic. Call Step only with actual player movement;
    /// inactive steps leave all simulation state unchanged. No GameObjects or physics required.
    /// </summary>
    public sealed class HighwayTraffic
    {
        public const int PerDirectionCount = 18;
        public const int Count = PerDirectionCount * 2;
        public const float VehicleHalfWidth = .85f;
        public const float PlayerRadius = .45f;
        public const float ContactHalfLength = 3.0f;
        public const float CooldownSeconds = .8f;
        public const float SafeLateralClearance = VehicleHalfWidth + PlayerRadius + .2f;
        public const float ReactionSeconds = .8f;
        const float RearLimit = -180f;
        public const float SameDirectionSpeed = 24f;
        const float OppositeSpeed = -28f;

        struct Vehicle
        {
            public float Distance, Lateral, LowerLimit, UpperLimit;
            public bool Visible, Contact;
        }

        readonly Vehicle[] vehicles = new Vehicle[Count];
        readonly int seed;
        readonly float drawDistance;
        readonly bool dense;
        public float MinimumHeadway { get; }
        public bool UsesFixedClearCorridor { get; }
        public float ClearCorridorCentre { get; private set; }
        public float RevealClearance => Mathf.Max(110, MinimumHeadway + ContactHalfLength);
        uint random;
        float playerSpeed, cooldown;

        public HighwayTraffic(int seed, float drawDistance, bool dense = true, float lateralSpeed = 3.5f,
            float inputSmoothingSeconds = 0, float clearCorridorCentre = -3.1f)
        {
            this.seed = seed;
            this.drawDistance = Mathf.Max(500f, drawDistance);
            this.dense = dense;
            ClearCorridorCentre = Mathf.Clamp(clearCorridorCentre, -9.5f, -1.5f);
            // A single obstacle can always be cleared within 2 * SafeLateralClearance,
            // even from a road edge. Account for reaction, both steering filters (including
            // reversal), 50 ms frames and time to clear the full longitudinal contact zone.
            float closingSpeed = Mathf.Max(220f / 3.6f - SameDirectionSpeed, SameDirectionSpeed);
            float manoeuvreSeconds = ReactionSeconds + 2 * (1f / 9 + Mathf.Max(0, inputSmoothingSeconds)) + .1f +
                2 * SafeLateralClearance / Mathf.Max(.01f, lateralSpeed);
            float safeGap = Mathf.Ceil((2 * ContactHalfLength + closingSpeed * manoeuvreSeconds) / 5) * 5;
            // If steering is disabled or too slow for the visible road, leave a continuous
            // straight corridor through every same-direction vehicle instead of requiring a turn.
            UsesFixedClearCorridor = lateralSpeed <= .01f || safeGap > this.drawDistance * .75f;
            MinimumHeadway = Mathf.Max(dense ? 90 : 150, UsesFixedClearCorridor ? 0 : safeGap);
            Reset();
        }

        /// <summary>Restore the exact initial layout and discard contact history.</summary>
        public void Reset(float playerSpeedKmh = 0, float? clearCorridorCentre = null)
        {
            if (clearCorridorCentre.HasValue) ClearCorridorCentre = Mathf.Clamp(clearCorridorCentre.Value, -9.5f, -1.5f);
            random = unchecked((uint)seed) ^ 0x9e3779b9u;
            if (random == 0) random = 0x6d2b79f5u;
            playerSpeed = Mathf.Clamp(playerSpeedKmh, 0, 220) / 3.6f;
            cooldown = 0;
            for (int group = 0; group < 2; group++)
            {
                // No traffic can reach the player during the initial steering grace period.
                float distance = group == 0 ? RevealClearance + Next01() * 30f : 230f + Next01() * 45f;
                for (int j = 0; j < PerDirectionCount; j++)
                {
                    int id = group * PerDirectionCount + j;
                    vehicles[id] = MakeVehicle(id, distance);
                    distance += NextHeadway();
                }
            }
        }

        /// <summary>
        /// Returns whether the vehicle should be rendered/audible. The speed is signed m/s,
        /// vehicle relative to player, as used by pass-by audio. Hidden samples are still provided.
        /// </summary>
        public bool Sample(int index, out float distanceAhead, out float lateral, out float relativeSpeed)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            Vehicle vehicle = vehicles[index];
            distanceAhead = vehicle.Distance;
            lateral = vehicle.Lateral;
            relativeSpeed = Speed(index) - playerSpeed;
            return vehicle.Visible;
        }

        /// <summary>
        /// Advances traffic and returns at most one hit. suppressAhead receives metres ahead of
        /// the current player and hides traffic on route transitions. A newly shown vehicle
        /// already overlapping the player cannot generate a spurious hit until it separates.
        /// A discontinuous player move rebases contacts without sweeping across the teleport.
        /// </summary>
        public HighwayHit? Step(float dt, float playerDistanceDelta, float previousLateral,
            float lateral, float playerSpeedKmh, bool active, Func<float, bool> suppressAhead = null,
            float maximumLateralSpeed = 3.5f)
        {
            if (!active || dt <= 0) return null;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || float.IsNaN(playerDistanceDelta) ||
                float.IsInfinity(playerDistanceDelta) || float.IsNaN(previousLateral) ||
                float.IsNaN(lateral) || float.IsInfinity(previousLateral) || float.IsInfinity(lateral) ||
                float.IsNaN(playerSpeedKmh) || float.IsInfinity(playerSpeedKmh))
                throw new ArgumentException("Traffic step requires finite movement values.");
            playerSpeed = Mathf.Clamp(playerSpeedKmh, 0, 220) / 3.6f;
            cooldown = Mathf.Max(0, cooldown - dt);
            bool continuous = playerDistanceDelta >= 0 && playerDistanceDelta <= (220f / 3.6f) * dt + .1f &&
                Mathf.Abs(lateral - previousLateral) <= Mathf.Max(0, maximumLateralSpeed) * dt + .1f;
            HighwayHit? hit = null;
            for (int i = 0; i < Count; i++)
            {
                Vehicle vehicle = vehicles[i];
                float oldDistance = vehicle.Distance;
                float delta = Speed(i) * dt - playerDistanceDelta;
                vehicle.Distance += delta;
                bool recycled = (delta < 0 && vehicle.Distance < vehicle.LowerLimit) ||
                    (delta > 0 && vehicle.Distance > vehicle.UpperLimit);
                if (recycled) Recycle(i, ref vehicle, delta < 0);
                bool wasVisible = vehicle.Visible;
                bool routeAllows = suppressAhead == null || !suppressAhead(vehicle.Distance);
                // Cars hidden on a ramp cannot suddenly reappear just in front of the rider.
                // Keep them hidden until they have enough approach distance on either side.
                vehicle.Visible = routeAllows && (wasVisible || Mathf.Abs(vehicle.Distance) >=
                    (i < PerDirectionCount ? RevealClearance : 80));
                bool endOverlap = Overlaps(vehicle.Distance, vehicle.Lateral - lateral);
                if (!vehicle.Visible || recycled || !continuous || !wasVisible)
                {
                    // A hidden, teleported, or newly revealed overlapping vehicle is latched.
                    vehicle.Contact = endOverlap;
                }
                else
                {
                    bool contact = SweptContact(oldDistance, vehicle.Lateral - previousLateral,
                        vehicle.Distance, vehicle.Lateral - lateral, out float fraction);
                    if (contact && !vehicle.Contact && cooldown <= 0 && !hit.HasValue)
                    {
                        hit = new HighwayHit {
                            VehicleId = i,
                            LateralOffset = vehicle.Lateral - Mathf.Lerp(previousLateral, lateral, fraction),
                            RelativeSpeedKmh = Mathf.Abs(Speed(i) - playerDistanceDelta / dt) * 3.6f
                        };
                        cooldown = CooldownSeconds;
                    }
                    // Latch even when the global cooldown suppressed the event.
                    if (contact) vehicle.Contact = true;
                    else if (!endOverlap) vehicle.Contact = false;
                }
                vehicles[i] = vehicle;
            }
            return hit;
        }

        /// <summary>Segment against the expanded vehicle rectangle in relative coordinates.</summary>
        public static bool SweptContact(float startDistance, float startLateral, float endDistance,
            float endLateral, out float fraction)
        {
            float enter = 0, exit = 1;
            bool intersects = Clip(startDistance, endDistance - startDistance, ContactHalfLength, ref enter, ref exit) &&
                Clip(startLateral, endLateral - startLateral, VehicleHalfWidth + PlayerRadius, ref enter, ref exit);
            fraction = intersects ? enter : 0;
            return intersects;
        }

        static bool Clip(float start, float delta, float halfExtent, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < 0.000001f) return Mathf.Abs(start) <= halfExtent;
            float a = (-halfExtent - start) / delta, b = (halfExtent - start) / delta;
            if (a > b) { float temp = a; a = b; b = temp; }
            enter = Mathf.Max(enter, a); exit = Mathf.Min(exit, b);
            return enter <= exit;
        }
        static bool Overlaps(float distance, float lateral) =>
            Mathf.Abs(distance) <= ContactHalfLength && Mathf.Abs(lateral) <= VehicleHalfWidth + PlayerRadius;
        static float Speed(int index) => index < PerDirectionCount ? SameDirectionSpeed : OppositeSpeed;

        Vehicle MakeVehicle(int id, float distance)
        {
            // A shared velocity preserves safe headway through player braking/acceleration.
            float lane = -8.65f + Next01() * 6.3f;
            if (id < PerDirectionCount && UsesFixedClearCorridor)
            {
                float leftEnd = Mathf.Min(-2.35f, ClearCorridorCentre - SafeLateralClearance);
                float rightStart = Mathf.Max(-8.65f, ClearCorridorCentre + SafeLateralClearance);
                float leftWidth = Mathf.Max(0, leftEnd + 8.65f), rightWidth = Mathf.Max(0, -2.35f - rightStart);
                float pick = Next01() * (leftWidth + rightWidth);
                lane = pick < leftWidth ? -8.65f + pick : rightStart + pick - leftWidth;
            }
            if (id >= PerDirectionCount) lane = -lane;
            return new Vehicle { Distance = distance, Lateral = lane, Visible = true,
                LowerLimit = Mathf.Min(RearLimit, distance - 200f),
                UpperLimit = Mathf.Max(drawDistance + 180f, distance + 200f) };
        }

        void Recycle(int id, ref Vehicle vehicle, bool toFront)
        {
            int start = id < PerDirectionCount ? 0 : PerDirectionCount;
            float edge = toFront ? drawDistance + 80f : RearLimit;
            for (int i = start; i < start + PerDirectionCount; i++)
            {
                if (i == id) continue;
                edge = toFront ? Mathf.Max(edge, vehicles[i].Distance) : Mathf.Min(edge, vehicles[i].Distance);
            }
            float position = edge + (toFront ? 1 : -1) * NextHeadway();
            vehicle = MakeVehicle(id, position);
        }

        float NextHeadway() => MinimumHeadway + Next01() * (dense ? 20 : 45);

        float Next01()
        {
            unchecked { random ^= random << 13; random ^= random >> 17; random ^= random << 5; }
            return (random & 0xffffffu) / 16777216f;
        }
    }
}
