using System.Collections.Generic;
using UnityEngine;

namespace HighwaySurfing
{
    /// <summary>A persistent road graph. A choice changes the rider's path, never the junction geometry.</summary>
    public sealed class HighwayRouteState
    {
        public const float FirstBranch = 600f;
        public const float BranchInterval = 2600f;
        public const float ExitThreshold = -5.4f;
        public const float ExitRise = 14f;
        private const int CurveSteps = 2048;
        private static readonly float[] ArcLengths = BuildArcLengths();
        public static float ConnectorLength => ArcLengths[CurveSteps];
        public static float ParallelStart => ArcLengths[CurveSteps / 2];
        public static float MergeStart => ArcLengths[CurveSteps * 3 / 4];
        public const float MergeRun = 350f;

        public struct Frame
        {
            public Vector3 Position, Forward;
            public Vector3 Right => new Vector3(Forward.z, 0, -Forward.x).normalized;
            public Vector3 Point(float lateral, float height = 0) => Position + Right * lateral + Vector3.up * height;
        }

        private readonly List<bool> exits = new List<bool>();
        // Frame n is the fixed entry of junction n, including the next unchosen junction.
        private readonly List<Frame> entries = new List<Frame> { new Frame { Position = Vector3.forward * FirstBranch, Forward = Vector3.forward } };
        private int exitCount;
        private static readonly string[] Names = { "BAYSHORE", "HARBOR", "SKYLINE", "EAST LOOP" };
        public int Decisions => exits.Count;
        public string RouteName => Names[exitCount % Names.Length];
        public string UpcomingBranchName => Names[(exitCount + 1) % Names.Length];
        public float UpcomingBranchDistance(float travel) => Mathf.Max(0, BranchAt(exits.Count) - travel);
        public static float BranchAt(int index) => FirstBranch + index * BranchInterval;
        public bool TookExit(int index) => index >= 0 && index < exits.Count && exits[index];
        public void Reset()
        {
            exits.Clear(); entries.Clear(); exitCount = 0;
            entries.Add(new Frame { Position = Vector3.forward * FirstBranch, Forward = Vector3.forward });
        }
        public void Advance(float travel, float lateral)
        {
            while (travel >= BranchAt(exits.Count))
            {
                bool exit = lateral <= ExitThreshold;
                Frame next = Branch(exits.Count, BranchInterval, exit);
                exits.Add(exit); entries.Add(next);
                if (exit) exitCount++;
            }
        }
        public string Phase(float travel)
        {
            if (exits.Count > 0 && travel < BranchAt(exits.Count - 1) + ConnectorLength)
                return TookExit(exits.Count - 1) ? "CONNECTOR" : "MAINLINE";
            return UpcomingBranchDistance(travel) <= 250 ? "APPROACH" : "MAINLINE";
        }
        /// <summary>Sample the travelled itinerary by actual metres along each road.</summary>
        public Frame Sample(float travel)
        {
            if (travel < FirstBranch) return new Frame { Position = Vector3.forward * travel, Forward = Vector3.forward };
            int index = Mathf.Min(Mathf.FloorToInt((travel - FirstBranch) / BranchInterval), exits.Count);
            return Branch(index, travel - BranchAt(index), TookExit(index));
        }

        /// <summary>Both arms exist before the rider chooses. distance is measured from the fork.</summary>
        public Frame Branch(int index, float distance, bool exit)
        {
            Frame entry = entries[Mathf.Clamp(index, 0, entries.Count - 1)];
            if (!exit || distance <= 0)
                return new Frame { Position = entry.Position + entry.Forward * distance, Forward = entry.Forward };
            Frame local = Ramp(distance);
            Quaternion rotation = Quaternion.LookRotation(entry.Forward, Vector3.up);
            return new Frame { Position = entry.Position + rotation * local.Position, Forward = rotation * local.Forward };
        }

        /// <summary>The destination motorway is perpendicular and already visible on approach.</summary>
        public Frame Crossing(int index, float distanceFromMerge)
        {
            Frame merge = Branch(index, ConnectorLength, true);
            merge.Position += merge.Forward * distanceFromMerge;
            return merge;
        }

        private static Frame Ramp(float distance)
        {
            if (distance >= ConnectorLength)
                return new Frame { Position = Curve(4) + Vector3.right * (distance - ConnectorLength), Forward = Vector3.right };
            int low = 0, high = CurveSteps;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (ArcLengths[mid] <= distance) low = mid; else high = mid;
            }
            float t = (low + Mathf.InverseLerp(ArcLengths[low], ArcLengths[high], distance)) * 4 / CurveSteps;
            // Analytic curve positions avoid a polygonal camera turn. The lookup only inverts arc length.
            return new Frame { Position = Curve(t), Forward = CurveTangent(t) };
        }
        // The turn passes OVER both motorways, then descends on the destination's
        // outside (left) shoulder. Only the final parallel taper joins live lanes.
        private static void Controls(float t, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d, out float local)
        {
            if (t <= 1)
            {
                a=Vector3.zero; b=new Vector3(0,0,60); c=new Vector3(-55,10,90); d=new Vector3(-55,14,150); local=t;
            }
            else if (t <= 2)
            {
                a=new Vector3(-55,14,150); b=new Vector3(-55,18,210); c=new Vector3(30,28,365); d=new Vector3(200,28,365); local=t-1;
            }
            else if (t <= 3)
            {
                a=new Vector3(200,28,365); b=new Vector3(300,28,365); c=new Vector3(400,14,365); d=new Vector3(500,14,365); local=t-2;
            }
            else
            {
                a=new Vector3(500,14,365); b=new Vector3(617,14,365); c=new Vector3(733,14,330); d=new Vector3(850,14,330); local=t-3;
            }
        }
        private static Vector3 Curve(float t)
        {
            Controls(t,out Vector3 a,out Vector3 b,out Vector3 c,out Vector3 d,out float local);
            float u=1-local;
            return u*u*u*a+3*u*u*local*b+3*u*local*local*c+local*local*local*d;
        }
        private static Vector3 CurveTangent(float t)
        {
            Controls(t,out Vector3 a,out Vector3 b,out Vector3 c,out Vector3 d,out float local);
            float u=1-local;
            return (3*u*u*(b-a)+6*u*local*(c-b)+3*local*local*(d-c)).normalized;
        }
        private static float[] BuildArcLengths()
        {
            var distances = new float[CurveSteps + 1];
            Vector3 last = Curve(0);
            for (int i = 1; i <= CurveSteps; i++)
            {
                Vector3 next = Curve(i * 4f / CurveSteps);
                distances[i] = distances[i - 1] + Vector3.Distance(last, next); last = next;
            }
            return distances;
        }
    }
}
