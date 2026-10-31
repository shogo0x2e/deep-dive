using System;
using System.Collections.Generic;
using System.Reflection;
using HighwaySurfing;
using UnityEngine;

public static class DenseTrafficChecks
{
    struct Interval { public float Low, High; public Interval(float low,float high){Low=low;High=high;} }
    public static string Run()
    {
        int paths=0, denseVisible=0, classicVisible=0;
        // An independent reachable-interval sweep through actual spawned positions. Between
        // obstacle contact windows, allow reaction and the lost displacement of both filters.
        foreach(float capacity in new[]{3.5f, .875f, .125f, 0f, 8f})
        foreach(float smoothing in new[]{.08f,.3f})
        foreach(float draw in new[]{500f,1100f})
        for(int seed=0;seed<48;seed++)
        foreach(float start in new[]{-9.5f,-5.3f,-1.5f})
        {
            var traffic=new HighwayTraffic(seed,draw,true,capacity,smoothing,start);
            if(traffic.UsesFixedClearCorridor)
            {
                // Include several recycle cycles with both approach directions.
                for(int frame=0;frame<1800;frame++)
                {
                    float speed=frame<900?220:0;
                    Check(!traffic.Step(.1f,speed/3.6f*.1f,start,start,speed,true).HasValue,"clear corridor has no swept collisions");
                    if(frame%60!=0)continue;
                    for(int i=0;i<HighwayTraffic.PerDirectionCount;i++)
                    {
                        traffic.Sample(i,out _,out float x,out _);
                        Check(Mathf.Abs(x-start)>=1.5f-.001f,"fixed corridor survives recycling");
                    }
                }
                traffic.Reset(0,-3.1f);
                for(int i=0;i<HighwayTraffic.PerDirectionCount;i++)
                {traffic.Sample(i,out _,out float x,out _);Check(Mathf.Abs(x+3.1f)>=1.499f,"reset moves corridor to new player start");}
            }
            else
            {
                CheckPath(traffic,start,capacity,smoothing,220f/3.6f-24f);
                CheckPath(traffic,start,capacity,smoothing,24f); // same layout mirrored behind a stopped rider
            }
            paths+=2;
        }
        for(int seed=0;seed<48;seed++)
        {
            var dense=new HighwayTraffic(seed,1100,true,3.5f,.08f);
            var classic=new HighwayTraffic(seed,1100,false,3.5f,.08f);
            for(int i=0;i<HighwayTraffic.Count;i++)
            {
                dense.Sample(i,out float d,out _,out _);classic.Sample(i,out float c,out _,out _);
                if(d<=1100)denseVisible++;if(c<=1100)classicVisible++;
            }
        }
        Check(denseVisible>classicVisible*1.5f,"visible traffic is materially denser");
        foreach(float initial in new[]{4f,-4f})
        {
            var traffic=new HighwayTraffic(47,1100);
            var vehicles=(Array)typeof(HighwayTraffic).GetField("vehicles",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(traffic);
            object car=vehicles.GetValue(0);
            car.GetType().GetField("Distance").SetValue(car,initial);
            car.GetType().GetField("Lateral").SetValue(car,-3.1f);
            vehicles.SetValue(car,0);
            float speed=initial>0?220:0;
            traffic.Step(.01f,speed/3.6f*.01f,-3.1f,-3.1f,speed,true,_=>true);
            for(int n=0;n<100;n++)
            {
                Check(!traffic.Step(.01f,speed/3.6f*.01f,-3.1f,-3.1f,speed,true,_=>false).HasValue,"near ramp reveal cannot cause an unavoidable hit");
                Check(!traffic.Sample(0,out _,out _,out _),"near ramp traffic remains hidden");
            }
        }
        return "PASS: "+paths+" front/rear reachable paths across seeds, steering capacities, smoothing and draw ranges; visible density x"+
            ((float)denseVisible/classicVisible).ToString("F2")+"; slow/zero-steer corridors and ramp reveals safe.";
    }
    static void CheckPath(HighwayTraffic traffic,float start,float capacity,float smoothing,float closing)
    {
        var reachable=new List<Interval>{new Interval(start,start)};
        float previousExit=0;
        for(int i=0;i<HighwayTraffic.PerDirectionCount;i++)
        {
            traffic.Sample(i,out float distance,out float lane,out _);
            float enter=(distance-3)/closing, exit=(distance+3)/closing;
            float time=enter-previousExit-.8f-2*(1f/9+smoothing)-.1f;
            Check(time>=0,"reaction/steering time exists between contact windows");
            float movement=time*capacity;
            var next=new List<Interval>();
            foreach(var interval in reachable)
            {
                float low=Mathf.Max(-9.5f,interval.Low-movement), high=Mathf.Min(-1.5f,interval.High+movement);
                Add(next,low,Mathf.Min(high,lane-1.5f));
                Add(next,Mathf.Max(low,lane+1.5f),high);
            }
            Check(next.Count>0,"reachable safe path survives every car");
            next.Sort((a,b)=>a.Low.CompareTo(b.Low));reachable.Clear();
            foreach(var interval in next)
            {
                if(reachable.Count==0 || reachable[reachable.Count-1].High<interval.Low)reachable.Add(interval);
                else {var last=reachable[reachable.Count-1];last.High=Mathf.Max(last.High,interval.High);reachable[reachable.Count-1]=last;}
            }
            previousExit=exit;
        }
    }
    static void Add(List<Interval> list,float low,float high){if(low<=high)list.Add(new Interval(low,high));}
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
}
