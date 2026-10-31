using System;
using System.Reflection;
using HighwaySurfing;
using HighwaySurfing.Kinect;
using UnityEditor;
using UnityEngine;

public static class TrafficControlLossChecks
{
    public static string Run()
    {
        var input=UnityEngine.Object.FindFirstObjectByType<KinectHighwayInput>();
        if(!EditorApplication.isPlaying || input==null || !input.Rig.Simulation || !input.AllowSimulationForTesting ||
            !input.RideRegistered || input.Rig.CaptureActive)throw new Exception("Register an explicit synthetic Main rider first.");
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var world=input.World;var rig=input.Rig;
        var inputUpdate=typeof(KinectHighwayInput).GetMethod("Update",flags);
        var worldUpdate=typeof(NightHighwayWorld).GetMethod("Update",flags);
        var tracked=typeof(KinectDebugRig).GetField("<IsTracked>k__BackingField",flags);
        var trafficField=typeof(NightHighwayWorld).GetField("traffic",flags);
        int count=world.CollisionCount;
        float originalSensitivity=input.Sensitivity;
        try
        {
            world.TrySampleTraffic(0,out _,out _,out _);
            var traffic=(HighwayTraffic)trafficField.GetValue(world);
            var vehicles=(Array)typeof(HighwayTraffic).GetField("vehicles",flags).GetValue(traffic);
            object car=vehicles.GetValue(0);
            car.GetType().GetField("Distance").SetValue(car,3.05f);
            car.GetType().GetField("Lateral").SetValue(car,world.LateralPosition);vehicles.SetValue(car,0);
            var before=traffic;var lanes=new float[HighwayTraffic.PerDirectionCount];
            for(int i=0;i<lanes.Length;i++)traffic.Sample(i,out _,out lanes[i],out _);
            tracked.SetValue(rig,false);inputUpdate.Invoke(input,null);worldUpdate.Invoke(world,null);
            traffic=(HighwayTraffic)trafficField.GetValue(world);
            Check(input.RideRegistered && !input.InputAvailable && traffic.UsesFixedClearCorridor,"tracking loss retains ride and opens a clear corridor");
            Check(world.CollisionCount==count,"near car cannot hit after steering becomes unavailable");
            Check(ReferenceEquals(before,traffic),"tracking loss does not replace visible traffic");
            for(int i=0;i<lanes.Length;i++)
            {traffic.Sample(i,out _,out float x,out _);Check(x==lanes[i],"tracking loss does not move visible cars sideways");}
            tracked.SetValue(rig,true);inputUpdate.Invoke(input,null);worldUpdate.Invoke(world,null);
            traffic=(HighwayTraffic)trafficField.GetValue(world);
            Check(input.InputAvailable && !traffic.UsesFixedClearCorridor,"tracking restoration safely rebuilds normal traffic");
            Check(world.CollisionCount==count,"restoration creates no phantom impact");
            float normalGap=traffic.MinimumHeadway;input.Sensitivity=.25f;
            inputUpdate.Invoke(input,null);worldUpdate.Invoke(world,null);traffic=(HighwayTraffic)trafficField.GetValue(world);
            Check(traffic.MinimumHeadway>normalGap,"live Kinect sensitivity widens required headway");
            Check(ReferenceEquals(before,traffic),"live sensitivity changes future spacing without replacing traffic");
            return "PASS: tracked/lost/restored Kinect steering and live sensitivity preserve traffic continuity without false impact.";
        }
        finally{tracked.SetValue(rig,true);input.Sensitivity=originalSensitivity;inputUpdate.Invoke(input,null);worldUpdate.Invoke(world,null);}
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
