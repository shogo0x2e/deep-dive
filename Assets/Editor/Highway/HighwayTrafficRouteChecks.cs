using System;
using HighwaySurfing;
using UnityEngine;

public static class HighwayTrafficRouteChecks
{
    public static string Run()
    {
        int checks = 0;
        Action<bool, string> check = (ok, name) => { checks++; if (!ok) throw new Exception(name); };
        float first = HighwayRouteState.FirstBranch, second = HighwayRouteState.BranchAt(1);
        float length = HighwayRouteState.ConnectorLength;
        var rider = new HighwayRouteState();
        var routes = new HighwayTrafficRoutes(4);
        routes.Reset(0, rider, first + 100);
        Vector3 oldCar = routes.Sample(0, first + 100).Position;
        rider.Advance(first, -8);
        check(Vector3.Distance(oldCar, routes.Sample(0, first + 100).Position) < .001f, "rider exit cannot move old car");
        check(Vector3.Distance(routes.Sample(0, second + 100).Position, Vector3.forward * (second + 100)) < .001f, "undecided future branches stay on mainline");
        routes.Reset(1, rider, first + 100);
        for (float d = 0; d < length + 50; d += 31)
        {
            var actual = routes.Sample(1, first + d); var expected = rider.Sample(first + d);
            check(Vector3.Distance(actual.Position, expected.Position) < .002f && Vector3.Angle(actual.Forward, expected.Forward) < .05f, "copied exit geometry");
        }
        var beforeChoice = routes.Sample(1, second + 50);
        rider.Advance(second, -8);
        check(Vector3.Distance(beforeChoice.Position, routes.Sample(1, second + 50).Position) < .001f, "next junction choice does not move existing car");
        routes.Reset(1, rider, second + 50);
        check(Vector3.Distance(routes.Sample(1, second + 50).Position, rider.Sample(second + 50).Position) < .002f, "recycle rebases to new itinerary");
        routes.Reset(2, rider, second + length / 2, true);
        check(routes.IsOnConnector(1, second + length / 2) && !routes.IsOnConnector(2, second + length / 2), "only same-direction car uses connector");
        var crossing = rider.Crossing(1, -length / 2);
        check(Vector3.Distance(routes.Sample(2, second + length / 2).Position, crossing.Position) < .002f, "opposite connector spawn projects onto destination motorway");
        for (float d = -length; d <= length; d += 50)
        {
            var actual = routes.Sample(2, second + length / 2 + d);
            check(Vector3.Distance(actual.Position, crossing.Position + crossing.Forward * d) < .002f && Vector3.Angle(actual.Forward, crossing.Forward) < .05f, "opposite car remains on straight motorway");
        }
        // Large histories retain their earlier geometry.
        for (int i = 2; i < 500; i++) rider.Advance(HighwayRouteState.BranchAt(i), i % 3 == 0 ? -8 : -3);
        float travel = HighwayRouteState.BranchAt(499);
        routes.Reset(3, rider, travel);
        check(Vector3.Distance(routes.Sample(3, travel - 2999).Position, rider.Sample(travel - 2999).Position) < .01f, "long history retains earlier geometry");
        check(Vector3.Distance(routes.Sample(3, travel + 4000).Position, rider.Sample(travel + 4000).Position) < .1f, "future mainline after long history");
        return "PASS: " + checks + " traffic route assertions";
    }
}
