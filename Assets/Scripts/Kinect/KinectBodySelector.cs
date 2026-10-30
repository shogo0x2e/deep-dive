using System;

namespace HighwaySurfing.Kinect
{
    /// <summary>Selects the closest tracked pelvis in sensor depth, with stable handover.</summary>
    public sealed class KinectBodySelector
    {
        public struct Candidate
        {
            public uint Id;
            public float DepthMeters;
            public int Confidence;
            public Candidate(uint id, float depth, int confidence = 2)
            { Id = id; DepthMeters = depth; Confidence = confidence; }
        }

        public bool HasSelection { get; private set; }
        public uint SelectedId { get; private set; }
        public bool SelectionChanged { get; private set; }
        bool pending;
        uint pendingId;
        float pendingSince, lastSeen;

        public void Reset()
        {
            SelectionChanged = HasSelection;
            HasSelection = pending = false;
            SelectedId = 0;
        }

        public int Step(Candidate[] bodies, int count, float now,
            float switchMargin = .25f, float switchDelay = .5f, float lostGrace = .75f, bool lockSelection = false)
        {
            SelectionChanged = false;
            int current = -1, nearest = -1;
            for (int i = 0; i < count; i++)
            {
                var body = bodies[i];
                if (HasSelection && body.Id == SelectedId) current = i;
                if (!ValidDepth(body.DepthMeters) || body.Confidence < 2) continue;
                if (nearest < 0 || body.DepthMeters < bodies[nearest].DepthMeters ||
                    (body.DepthMeters == bodies[nearest].DepthMeters && body.Id < bodies[nearest].Id)) nearest = i;
            }
            if (!HasSelection) return nearest < 0 ? -1 : Select(bodies, nearest, now);
            if (current < 0)
            {
                pending = false;
                if (lockSelection) return -1;
                if (now - lastSeen < Math.Max(0, lostGrace)) return -1;
                if (nearest >= 0) return Select(bodies, nearest, now);
                Reset();
                return -1;
            }
            lastSeen = now;
            if (lockSelection) { pending = false; return current; }
            // A low-confidence current pelvis is held; steering independently rejects it.
            // Do not switch to a more distant bystander merely because a joint was inferred.
            if (nearest >= 0 && nearest != current && bodies[current].Confidence >= 2 && ValidDepth(bodies[current].DepthMeters) &&
                bodies[nearest].DepthMeters + Math.Max(0, switchMargin) < bodies[current].DepthMeters)
            {
                if (!pending || pendingId != bodies[nearest].Id)
                { pending = true; pendingId = bodies[nearest].Id; pendingSince = now; }
                if (now - pendingSince >= Math.Max(0, switchDelay)) return Select(bodies, nearest, now);
            }
            else pending = false;
            return current;
        }

        int Select(Candidate[] bodies, int index, float now)
        {
            SelectionChanged = !HasSelection || SelectedId != bodies[index].Id;
            HasSelection = true; SelectedId = bodies[index].Id;
            lastSeen = now; pending = false;
            return index;
        }
        static bool ValidDepth(float depth) => float.IsFinite(depth) && depth > 0;
    }
}
