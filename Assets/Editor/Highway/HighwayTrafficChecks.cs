using System;
using HighwaySurfing;
using UnityEngine;

public static class HighwayTrafficChecks
{
    public static string Run()
    {
        int checks = 0;
        // Each ID keeps its own speed through player acceleration and vehicle recycling.
        for (int seed = 0; seed < 24; seed++)
        {
            var a = new HighwayTraffic(seed, 1100);
            var b = new HighwayTraffic(seed, 1100);
            a.Sample(0, out float first, out _, out float firstSpeed);
            Check(first / (220f / 3.6f - firstSpeed) > 2.5f, "Initial grace at top speed");
            var speeds = new float[HighwayTraffic.Count];
            for (int i = 0; i < speeds.Length; i++) a.Sample(i, out _, out _, out speeds[i]);
            Check(Mathf.Abs(speeds[0]-speeds[1])>.01f, "Individual speeds vary");
            bool centre = false;
            for (int frame = 0; frame < 1200; frame++)
            {
                float speed = new[] { 0f, 108f, 220f, 50f, 200f, 120f }[(frame / 200) % 6];
                const float dt = .1f;
                a.Step(dt, speed / 3.6f * dt, -5.3f, -5.3f, speed, true);
                b.Step(dt, speed / 3.6f * dt, -5.3f, -5.3f, speed, true);
                for (int i = 0; i < HighwayTraffic.Count; i++)
                {
                    bool av = a.Sample(i, out float ad, out float al, out float ar);
                    bool bv = b.Sample(i, out float bd, out float bl, out float br);
                    Check(av == bv && ad == bd && al == bl && ar == br, "Deterministic state");
                    Check(Mathf.Abs(ar + speed/3.6f - speeds[i]) < .0001f, "Per-car speed stays constant");
                    Check(Mathf.Abs(speeds[i])*3.6f >= 80 && Mathf.Abs(speeds[i])*3.6f <= 120, "80-120 km/h range");
                    Check((i < HighwayTraffic.PerDirectionCount) == (speeds[i]>0), "Correct travel direction");
                    if (i < HighwayTraffic.PerDirectionCount)
                    {
                        Check(al >= -8.65f && al <= -2.35f, "Traffic stays on carriageway");
                        if (Mathf.Abs(al + 5.3f) < HighwayTraffic.VehicleHalfWidth + HighwayTraffic.PlayerRadius) centre = true;
                    }
                }
            }
            Check(centre, "The centre must sometimes be occupied"); checks++;
        }
        Check(HighwayTraffic.SweptContact(10, 0, -10, 0, out float t) && t > 0 && t < 1,
            "Fast longitudinal crossing hits"); checks++;
        Check(HighwayTraffic.SweptContact(0, 2, 0, 1, out _), "Slow side entry hits"); checks++;
        Check(!HighwayTraffic.SweptContact(10, 0, -10, 4, out _), "Separated paths miss"); checks++;
        Check(!HighwayTraffic.SweptContact(4, 2, 4, 0, out _), "Side movement outside length misses"); checks++;

        var traffic = new HighwayTraffic(47, 1100);
        traffic.Sample(0, out float initialDistance, out float lane, out float vehicleSpeed);
        Check(initialDistance >= 110 && initialDistance < 140, "Initial grace and early encounter"); checks++;
        traffic.Step(.1f, 0, lane, lane, 0, false);
        traffic.Sample(0, out float paused, out _, out _);
        Check(initialDistance == paused, "Pause freezes movement"); checks++;
        Check(!traffic.Step(.1f, 500, lane, lane, 220, true).HasValue, "Teleport cannot sweep a hit"); checks++;
        traffic.Reset();
        traffic.Sample(0, out float resetDistance, out float resetLane, out _);
        Check(initialDistance == resetDistance && lane == resetLane, "Reset reproduces initial layout"); checks++;
        traffic.Step(.1f, 0, lane, lane, 0, true, _ => true);
        Check(!traffic.Sample(0, out _, out _, out _), "Suppression hides samples"); checks++;
        traffic.Step(.1f, 0, lane, lane, 0, true, _ => false);
        Check(traffic.Sample(0, out _, out _, out _), "Suppression releases samples"); checks++;

        // Approach in a safe position and then steer into the side at equal speed.
        traffic.Reset(220);
        traffic.Sample(0, out _, out lane, out _);
        float safe = lane < -5.5f ? -1.5f : -9.5f;
        for (int frame = 0; frame < 2000; frame++)
        {
            traffic.Sample(0, out float d, out _, out _);
            if (d <= 0) break;
            float dt = Mathf.Min(.05f, d / (220f / 3.6f - vehicleSpeed));
            traffic.Step(dt, 220f / 3.6f * dt, safe, safe, 220, true);
        }
        traffic.Sample(0, out float arrival, out _, out _);
        Check(Mathf.Abs(arrival) < .01f, "Side collision setup"); checks++;
        int hits = 0;
        float x = safe;
        for (int frame = 0; frame < 100; frame++)
        {
            float next = Mathf.MoveTowards(x, lane, 3.5f * .05f);
            var hit = traffic.Step(.05f, vehicleSpeed * .05f, x, next, vehicleSpeed * 3.6f, true);
            if (hit.HasValue) { Check(hit.Value.VehicleId == 0, "Expected car hit"); hits++; }
            x = next;
        }
        Check(hits == 1, "Side contact emits exactly once, even past cooldown"); checks++;

        // Inspector tuning allows lateral movement up to 8 m/s. It is real movement,
        // including at a 50 ms frame, not a teleport that should suppress contact.
        traffic.Reset(220);
        traffic.Sample(0, out _, out lane, out _);
        safe = lane < -5.5f ? -1.5f : -9.5f;
        for (int frame = 0; frame < 2000; frame++)
        {
            traffic.Sample(0, out float d, out _, out _);
            if (d <= 0) break;
            float dt = Mathf.Min(.05f, d / (220f / 3.6f - vehicleSpeed));
            traffic.Step(dt, 220f / 3.6f * dt, safe, safe, 220, true);
        }
        hits = 0; x = safe;
        for (int frame = 0; frame < 40; frame++)
        {
            float next = Mathf.MoveTowards(x, lane, 8f * .05f);
            if (traffic.Step(.05f, vehicleSpeed * .05f, x, next,
                vehicleSpeed * 3.6f, true, maximumLateralSpeed: 8).HasValue) hits++;
            x = next;
        }
        Check(hits == 1, "Fast configured lateral steering still hits once"); checks++;
        // Revealing a suppressed overlapping car must not count as a fresh collision.
        traffic.Reset(220);
        traffic.Sample(0, out _, out float revealLane, out _);
        for (int frame = 0; frame < 2000; frame++)
        {
            traffic.Sample(0, out float d, out _, out _);
            if (d <= 0) break;
            float dt = Mathf.Min(.05f, d / (220f / 3.6f - vehicleSpeed));
            Check(!traffic.Step(dt, 220f / 3.6f * dt, revealLane, revealLane, 220, true, _ => true).HasValue,
                "Suppressed car cannot hit");
        }
        for (int frame = 0; frame < 30; frame++)
            Check(!traffic.Step(.05f, vehicleSpeed * .05f, revealLane, revealLane,
                vehicleSpeed * 3.6f, true, _ => false).HasValue,
                "Revealing an overlapping car does not create a hit");
        checks++;

        // At a stop, traffic recycled behind the rider can overtake and make contact.
        traffic.Reset();
        int rearId = -1;
        for (int frame = 0; frame < 3000 && rearId < 0; frame++)
        {
            traffic.Step(.1f, 0, -5.3f, -5.3f, 0, true);
            for (int i = 0; i < HighwayTraffic.PerDirectionCount; i++)
            {
                traffic.Sample(i, out float d, out _, out _);
                if (d < -180 && d > -400) { rearId = i; break; }
            }
        }
        Check(rearId >= 0, "Rear recycle available at zero player speed"); checks++;
        traffic.Sample(rearId, out _, out float rearLane, out _);
        bool rearHit = false;
        for (int frame = 0; frame < 200; frame++)
        {
            var hit = traffic.Step(.1f, 0, rearLane, rearLane, 0, true);
            if (hit.HasValue && hit.Value.VehicleId == rearId) { rearHit = true; break; }
        }
        Check(rearHit, "A car approaching from behind can hit a stopped rider"); checks++;
        return $"PASS: {checks} traffic checks (plus deterministic/constant-speed samples)";
    }

    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
}
