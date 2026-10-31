using System;
using System.Collections.Generic;
using UnityEngine;

namespace HighwaySurfing
{
    /// <summary>
    /// Independent, reusable itinerary snapshots for the traffic pool. A car retains its
    /// road until Reset is called for that ID; branches not yet chosen at Reset go straight.
    /// </summary>
    public sealed class HighwayTrafficRoutes
    {
        sealed class Itinerary
        {
            public readonly List<HighwayRouteState.Frame> Entries = new List<HighwayRouteState.Frame>();
            public readonly List<bool> Exits = new List<bool>();
            public bool Initialized, Opposite;
            public float AnchorTravel;
            public HighwayRouteState.Frame Anchor;
        }

        readonly Itinerary[] itineraries;
        public HighwayTrafficRoutes(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            itineraries = new Itinerary[capacity];
            for (int i = 0; i < capacity; i++) itineraries[i] = new Itinerary();
        }

        /// <summary>
        /// Freeze the rider's currently chosen road at spawn/recycle. An opposite car
        /// travels on a fixed straight motorway: connector spawns are projected onto the destination motorway.
        /// </summary>
        public void Reset(int id, HighwayRouteState riderRoute, float spawnTravel, bool opposite = false)
        {
            if (riderRoute == null) throw new ArgumentNullException(nameof(riderRoute));
            if (!Finite(spawnTravel)) throw new ArgumentException("Travel must be finite.");
            Itinerary route = Get(id);
            route.Entries.Clear();
            route.Exits.Clear();
            for (int i = 0; i < riderRoute.Decisions; i++)
            {
                route.Entries.Add(riderRoute.Branch(i, 0, false));
                route.Exits.Add(riderRoute.TookExit(i));
            }
            route.Entries.Add(riderRoute.Branch(riderRoute.Decisions, 0, false));
            route.Opposite = opposite;
            route.AnchorTravel = spawnTravel;
            if (opposite)
            {
                int index = Mathf.FloorToInt((spawnTravel - HighwayRouteState.FirstBranch) / HighwayRouteState.BranchInterval);
                float distance = spawnTravel - HighwayRouteState.BranchAt(index);
                route.Anchor = index >= 0 && riderRoute.TookExit(index) && distance >= 0 && distance < HighwayRouteState.ConnectorLength
                    ? riderRoute.Crossing(index, distance - HighwayRouteState.ConnectorLength)
                    : riderRoute.Sample(spawnTravel);
            }
            route.Initialized = true;
        }

        /// <summary>Sample at playerTravel + the car's longitudinal distance.</summary>
        public HighwayRouteState.Frame Sample(int id, float absoluteTravel)
        {
            if (!Finite(absoluteTravel)) throw new ArgumentException("Travel must be finite.", nameof(absoluteTravel));
            Itinerary route = Get(id);
            if (!route.Initialized) throw new InvalidOperationException("Reset the car's route before sampling it.");
            if (route.Opposite)
                return new HighwayRouteState.Frame { Position = route.Anchor.Position + route.Anchor.Forward * (absoluteTravel - route.AnchorTravel), Forward = route.Anchor.Forward };
            if (absoluteTravel < HighwayRouteState.FirstBranch)
                return new HighwayRouteState.Frame { Position = Vector3.forward * absoluteTravel, Forward = Vector3.forward };
            int index = Mathf.FloorToInt((absoluteTravel - HighwayRouteState.FirstBranch) / HighwayRouteState.BranchInterval);
            int offset = Mathf.Min(index, route.Exits.Count);
            HighwayRouteState.Frame entry = route.Entries[offset];
            float distance = absoluteTravel - HighwayRouteState.BranchAt(offset);
            if (offset < route.Exits.Count && route.Exits[offset])
            {
                // Branch geometry is invariant: rotate/translate the canonical first ramp
                // into the frozen junction entry, independently of subsequent rider choices.
                HighwayRouteState.Frame local = canonical.Branch(0, distance, true);
                Quaternion rotation = Quaternion.LookRotation(entry.Forward, Vector3.up);
                return new HighwayRouteState.Frame { Position = entry.Position + rotation * (local.Position - Vector3.forward * HighwayRouteState.FirstBranch), Forward = rotation * local.Forward };
            }
            return new HighwayRouteState.Frame { Position = entry.Position + entry.Forward * distance, Forward = entry.Forward };
        }

        /// <summary>Cars created before a new exit keep their old road until out of view.</summary>
        public bool FollowsItinerary(int id, HighwayRouteState rider)
        {
            Itinerary route = Get(id);
            if (!route.Initialized) return false;
            for (int i = 0; i < rider.Decisions; i++)
                if ((i < route.Exits.Count && route.Exits[i]) != rider.TookExit(i)) return false;
            return true;
        }

        /// <summary>Whether this car's frozen itinerary is on a one-way connector.</summary>
        public bool IsOnConnector(int id, float absoluteTravel)
        {
            if (!Finite(absoluteTravel)) throw new ArgumentException("Travel must be finite.", nameof(absoluteTravel));
            Itinerary route = Get(id);
            if (!route.Initialized) throw new InvalidOperationException("Reset the car's route before sampling it.");
            if (route.Opposite || absoluteTravel < HighwayRouteState.FirstBranch) return false;
            int index = Mathf.FloorToInt((absoluteTravel - HighwayRouteState.FirstBranch) / HighwayRouteState.BranchInterval);
            return index < route.Exits.Count && route.Exits[index] &&
                absoluteTravel - HighwayRouteState.BranchAt(index) < HighwayRouteState.ConnectorLength;
        }

        static readonly HighwayRouteState canonical = new HighwayRouteState();
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        Itinerary Get(int id)
        {
            if (id < 0 || id >= itineraries.Length) throw new ArgumentOutOfRangeException(nameof(id));
            return itineraries[id];
        }
    }
}
