using System;
using System.Reflection;
using HighwaySurfing;
using UnityEngine;

public static class DenseTrafficChecks
{
    public static string Run()
    {
        int paths=0, denseVisible=0, classicVisible=0;
        CheckSpeedCapIndependentLayout();
        // Independent integration through max/zero/matching/variable player speeds and
        // recycling. Ongoing gaps may close; braking is part of normal traffic gameplay.
        foreach(float limit in new[]{220f,400f})
        foreach(float capacity in new[]{3.5f,.875f,.125f,0f,8f})
        foreach(float smoothing in new[]{.08f,.3f})
        foreach(float draw in new[]{500f,1100f})
        for(int seed=0;seed<12;seed++)
        foreach(float start in new[]{-9.5f,-5.5f,-1.5f})
        {
            var traffic=new HighwayTraffic(seed,draw,true,capacity,smoothing,start,limit);
            traffic.Sample(0,out float first,out _,out _);
            Check(first>=traffic.RevealClearance,"initial reaction distance");
            var speeds=new float[HighwayTraffic.Count];
            var previous=new float[HighwayTraffic.Count];
            for(int i=0;i<speeds.Length;i++)traffic.Sample(i,out previous[i],out _,out speeds[i]);
            bool recycled=false;
            for(int frame=0;frame<1800;frame++)
            {
                const float dt=.05f;
                float speed=frame<500?limit:frame<1000?0:frame<1400?speeds[0]*3.6f:limit*(.5f+.5f*Mathf.Sin(frame*.1f));
                var hit=traffic.Step(dt,speed/3.6f*dt,start,start,speed,true,maximumLateralSpeed:capacity);
                if(traffic.UsesFixedClearCorridor)Check(!hit.HasValue,"disabled-steering corridor avoids impact");
                for(int i=0;i<speeds.Length;i++)
                {
                    traffic.Sample(i,out float d,out float lane,out float relative);
                    Check(Mathf.Abs(relative+speed/3.6f-speeds[i])<.0001f,"traffic never follows player speed");
                    float expected=previous[i]+(speeds[i]-speed/3.6f)*dt;
                    if(Mathf.Abs(d-expected)>.1f)recycled=true;
                    previous[i]=d;
                    if(i>=HighwayTraffic.PerDirectionCount)continue;
                    if(traffic.UsesFixedClearCorridor)Check(Mathf.Abs(lane-start)>=1.499f,"current-position corridor remains clear");
                    else Check(lane>=-8.651f && lane<=-2.349f,"normal traffic remains on carriageway");
                }
            }
            Check(recycled,"long simulation covers recycling");
            traffic.Reset(0,-3.1f);
            for(int i=0;i<speeds.Length;i++)
            {
                traffic.Sample(i,out _,out float lane,out float speed);
                Check(speed==speeds[i],"reset preserves assigned speeds");
                if(traffic.UsesFixedClearCorridor && i<HighwayTraffic.PerDirectionCount)
                    Check(Mathf.Abs(lane+3.1f)>=1.499f,"reset recentres disabled-steering corridor");
            }
            paths++;
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
        return "PASS: "+paths+" traffic runs at 220/400 km/h with braking, recycling and fixed vehicle speeds; speed-cap-independent layouts; visible density x"+
            ((float)denseVisible/classicVisible).ToString("F2")+"; slow/zero-steer corridors and ramp reveals safe.";
    }

    static void CheckSpeedCapIndependentLayout()
    {
        float[] limits={220f,400f,500f,600f,1000f,10000f};
        foreach(float capacity in new[]{3.5f,.875f,0f})
        foreach(float smoothing in new[]{.08f,.3f})
        foreach(float draw in new[]{500f,1100f})
        for(int seed=0;seed<3;seed++)
        {
            var traffic=new HighwayTraffic[limits.Length];
            for(int n=0;n<limits.Length;n++)
            {
                traffic[n]=new HighwayTraffic(seed,draw,true,capacity,smoothing,-3.1f,limits[n]);
                if(capacity==3.5f && smoothing==.08f)
                    Check(traffic[n].MinimumHeadway==90f,"normal headway remains 90 at every speed cap");
                for(int i=0;i<HighwayTraffic.Count;i++)
                {
                    traffic[n].Sample(i,out _,out _,out float speed);
                    Check(Mathf.Abs(speed)*3.6f>=80f-.001f && Mathf.Abs(speed)*3.6f<=120f+.001f,
                        "assigned vehicle speed is 80-120 km/h");
                }
            }
            CheckSameLayout(traffic,"initial layout independent of speed cap");
            for(int frame=0;frame<1200;frame++)
            {
                const float dt=.05f, speed=220f;
                for(int n=0;n<traffic.Length;n++)
                    traffic[n].Step(dt,speed/3.6f*dt,-3.1f,-3.1f,speed,true,maximumLateralSpeed:capacity);
                if(frame==599)
                {
                    for(int n=0;n<traffic.Length;n++)
                    {
                        var distances=new float[HighwayTraffic.Count];
                        var lanes=new float[HighwayTraffic.Count];
                        var speeds=new float[HighwayTraffic.Count];
                        for(int i=0;i<HighwayTraffic.Count;i++)
                            traffic[n].Sample(i,out distances[i],out lanes[i],out speeds[i]);
                        traffic[n].Configure(draw,true,capacity,smoothing,-3.1f,limits[(n+1)%limits.Length]);
                        for(int i=0;i<HighwayTraffic.Count;i++)
                        {
                            traffic[n].Sample(i,out float d,out float lane,out float relative);
                            Check(d==distances[i] && lane==lanes[i] && relative==speeds[i],"Configure preserves existing positions and speeds");
                        }
                    }
                    CheckSameLayout(traffic,"layout after reconfiguration independent of speed cap");
                }
            }
            bool recycled=false;
            for(int i=0;i<HighwayTraffic.Count;i++)if(traffic[0].Revision(i)>1)recycled=true;
            Check(recycled,"speed-cap comparison covers recycling");
            CheckSameLayout(traffic,"recycled layout independent of speed cap");
        }
    }

    static void CheckSameLayout(HighwayTraffic[] traffic,string name)
    {
        for(int n=1;n<traffic.Length;n++)
        {
            Check(traffic[n].MinimumHeadway==traffic[0].MinimumHeadway &&
                traffic[n].UsesFixedClearCorridor==traffic[0].UsesFixedClearCorridor,name);
            for(int i=0;i<HighwayTraffic.Count;i++)
            {
                bool visible=traffic[0].Sample(i,out float d,out float lane,out float speed);
                bool otherVisible=traffic[n].Sample(i,out float otherD,out float otherLane,out float otherSpeed);
                Check(visible==otherVisible && d==otherD && lane==otherLane && speed==otherSpeed &&
                    traffic[0].Revision(i)==traffic[n].Revision(i),name);
            }
        }
    }
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
}
