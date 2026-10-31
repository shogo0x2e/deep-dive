using System;
using System.Reflection;
using HighwaySurfing;
using UnityEngine;

public static class HighwayTrafficContinuityChecks
{
    public static string Run()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var ensure=typeof(NightHighwayWorld).GetMethod("EnsureTraffic",flags);
        var sync=typeof(NightHighwayWorld).GetMethod("SyncTrafficRoutes",flags);
        var travelField=typeof(NightHighwayWorld).GetField("<TravelledMetres>k__BackingField",flags);
        var trafficField=typeof(NightHighwayWorld).GetField("traffic",flags);
        var routeField=typeof(NightHighwayWorld).GetField("route",flags);
        int samples=0, wraps=0;
        foreach(float limit in new[]{220f,400f})
        for(int choices=0;choices<4;choices++)
        {
            var go=new GameObject("TrafficContinuityCheck");
            try
            {
                var world=go.AddComponent<NightHighwayWorld>();world.MaximumSpeedKmh=limit;
                ensure.Invoke(world,null);
                var traffic=(HighwayTraffic)trafficField.GetValue(world);
                var route=(HighwayRouteState)routeField.GetValue(world);
                var positions=new Vector3[HighwayTraffic.Count];var revisions=new int[HighwayTraffic.Count];
                for(int i=0;i<positions.Length;i++){positions[i]=world.TrafficPoint(i);revisions[i]=traffic.Revision(i);}
                for(int frame=0;world.TravelledMetres<4900;frame++)
                {
                    const float dt=.05f;
                    double next=world.TravelledMetres+limit/3.6*dt;
                    travelField.SetValue(world,next);
                    float lane=((choices>>route.Decisions)&1)==0?-8:-3;
                    route.Advance((float)next,lane);
                    traffic.Step(dt,limit/3.6f*dt,world.LateralPosition,world.LateralPosition,limit,true);
                    sync.Invoke(world,null);
                    var rider=route.Sample((float)next);
                    Vector3 forward=rider.Forward;forward.y=0;forward.Normalize();
                    for(int i=0;i<positions.Length;i++)
                    {
                        Vector3 point=world.TrafficPoint(i);
                        if(revisions[i]==traffic.Revision(i))
                            Check(Vector3.Distance(point,positions[i])<3,"rider junction choice cannot teleport existing car");
                        else
                        {
                            Vector3 old=positions[i]-rider.Position, fresh=point-rider.Position;
                            Check(Vector3.Dot(old,forward)<-85 || old.magnitude>world.DrawDistance+80,"old vehicle leaves view before recycle");
                            Check(Vector3.Dot(fresh,forward)<-130 || fresh.magnitude>world.DrawDistance+80,"new vehicle starts outside view");
                            wraps++;
                        }
                        traffic.Sample(i,out _,out _,out _);
                        positions[i]=point;revisions[i]=traffic.Revision(i);samples++;
                    }
                    if(frame==100)
                    {
                        world.TrafficSteeringScale=0;world.DenseTraffic=false;ensure.Invoke(world,null);
                        Check(ReferenceEquals(traffic,trafficField.GetValue(world)),"live tuning preserves traffic instance");
                        for(int i=0;i<positions.Length;i++)Check(Vector3.Distance(world.TrafficPoint(i),positions[i])<.001f,"tracking/config change cannot pop traffic");
                        world.TrafficSteeringScale=1;world.DenseTraffic=true;ensure.Invoke(world,null);
                    }
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        Check(wraps>100,"multiple real wraps exercised");
        return "PASS: "+samples+" world-position continuity samples and "+wraps+" offscreen recycles, all two-junction choices at 220/400 km/h.";
    }
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
}
