using System;
using System.Reflection;
using HighwaySurfing;
using UnityEngine;

public static class TrafficPopulationChecks
{
    public static string Run()
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var ensure=typeof(NightHighwayWorld).GetMethod("EnsureTraffic",flags);
        var advance=typeof(NightHighwayWorld).GetMethod("AdvanceRide",flags);
        var travelField=typeof(NightHighwayWorld).GetField("<TravelledMetres>k__BackingField",flags);
        var trafficField=typeof(NightHighwayWorld).GetField("traffic",flags);
        var routeField=typeof(NightHighwayWorld).GetField("route",flags);
        var deferred=new HighwayTraffic(47,1100,maximumPlayerSpeedKmh:10000);
        deferred.PlaceRecycledVehicle=(_,__)=>float.NaN;
        var deferredCars=(Array)typeof(HighwayTraffic).GetField("vehicles",flags).GetValue(deferred);
        object rear=deferredCars.GetValue(0);rear.GetType().GetField("Distance").SetValue(rear,-179f);deferredCars.SetValue(rear,0);
        int revision=deferred.Revision(0);
        deferred.Step(.05f,10000f/3.6f*.05f,-3.1f,-3.1f,10000,true);
        deferred.Sample(0,out float retained,out _,out _);
        Check(deferred.Revision(0)==revision && retained < -180 && !float.IsNaN(retained),"failed placement retains a finite moving vehicle without respawning");
        int cases=0;
        foreach(float cap in new[]{500f,1000f,10000f})
        foreach(float speed in new[]{500f,cap})
        foreach(float draw in new[]{500f,1100f})
        {
            var go=new GameObject("TrafficPopulationCheck");
            try
            {
                var world=go.AddComponent<NightHighwayWorld>();
                world.MaximumSpeedKmh=cap;world.SpeedKmh=speed;world.DrawDistance=draw;
                world.CollisionSlowdownEnabled=false;ensure.Invoke(world,null);
                var traffic=(HighwayTraffic)trafficField.GetValue(world);
                var route=(HighwayRouteState)routeField.GetValue(world);
                var cars=(Array)typeof(HighwayTraffic).GetField("vehicles",flags).GetValue(traffic);
                // All but one car are far down the motorway the rider is about to leave.
                // They must not reserve the next spawn several kilometres away.
                for(int i=1;i<HighwayTraffic.Count;i++)
                {
                    object car=cars.GetValue(i);
                    car.GetType().GetField("Distance").SetValue(car,20000f+i*500);
                    cars.SetValue(car,i);
                }
                travelField.SetValue(world,700.0);route.Advance(700,-8);
                int nearRevision=traffic.Revision(0),farRevision=traffic.Revision(1);
                Vector3 nearPosition=world.TrafficPoint(0);
                advance.Invoke(world,new object[]{.001f});
                Check(traffic.Revision(0)==nearRevision && Vector3.Distance(nearPosition,world.TrafficPoint(0))<.1f,
                    "visible old-mainline car remains continuous");
                Check(traffic.Revision(1)>farRevision,"offscreen abandoned cars refill without waiting to pass rider");
                traffic.Sample(1,out float first,out _,out _);
                Check(first<draw+1000,"replacement uses nearest physical slot, not old-route outliers");
                Check(world.TrySampleTraffic(1,out _,out _,out _),"replacement belongs to rider itinerary");
                double afterMerge=HighwayRouteState.FirstBranch+HighwayRouteState.ConnectorLength+150;
                while(world.TravelledMetres<afterMerge)advance.Invoke(world,new object[]{.01f});
                var rider=route.Sample((float)world.TravelledMetres);
                int nearby=0;
                for(int i=0;i<HighwayTraffic.PerDirectionCount;i++)
                {
                    Vector3 delta=world.TrafficPoint(i)-rider.Position;
                    if(world.TrySampleTraffic(i,out _,out _,out _) && Vector3.Dot(delta,rider.Forward)>0 && delta.magnitude<draw*.9f)nearby++;
                }
                Check(nearby>0,"traffic remains ahead after merge at "+speed+" km/h, draw "+draw);
                cases++;
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        int longRuns=CheckSustainedPopulation();
        return "PASS: "+cases+" population cases and "+longRuns+" 30 km runs; visible cars retained, abandoned-route outliers cannot starve new traffic, and replenishment survives repeated exits.";
    }

    static int CheckSustainedPopulation()
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var ensure=typeof(NightHighwayWorld).GetMethod("EnsureTraffic",flags);
        var advance=typeof(NightHighwayWorld).GetMethod("AdvanceRide",flags);
        var routeField=typeof(NightHighwayWorld).GetField("route",flags);
        int cases=0;
        foreach(float speed in new[]{500f,1000f,10000f})
        foreach(float draw in new[]{500f,1100f})
        foreach(bool takeExits in new[]{false,true})
        {
            var go=new GameObject("SustainedTrafficPopulationCheck");
            try
            {
                var world=go.AddComponent<NightHighwayWorld>();
                world.MaximumSpeedKmh=speed;world.SpeedKmh=speed;world.DrawDistance=draw;
                world.CollisionSlowdownEnabled=false;world.LateralPosition=takeExits?-8:-3;
                ensure.Invoke(world,null);
                var route=(HighwayRouteState)routeField.GetValue(world);
                double lastTraffic=0;
                while(world.TravelledMetres<30000)
                {
                    advance.Invoke(world,new object[]{.05f});
                    var rider=route.Sample((float)world.TravelledMetres);
                    for(int i=0;i<HighwayTraffic.PerDirectionCount;i++)
                    {
                        Vector3 delta=world.TrafficPoint(i)-rider.Position;
                        if(world.TrySampleTraffic(i,out _,out _,out _) &&
                            Vector3.Dot(delta,rider.Forward)>0 && delta.magnitude<draw*.9f)
                        {
                            lastTraffic=world.TravelledMetres;
                            break;
                        }
                    }
                    // A connector can briefly have no car ahead, but never reserve the
                    // whole pool on old roads and leave subsequent motorways empty.
                    Check(world.TravelledMetres-lastTraffic<=draw+500,
                        "traffic replenishes throughout 30 km at "+speed+" km/h, draw "+draw+", exits "+takeExits);
                }
                Check(route.Decisions>=10,"sustained population check traverses many junctions");
                cases++;
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        return cases;
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
