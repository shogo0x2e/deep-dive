using System;
using HighwaySurfing;
using UnityEngine;

public static class HighwayRouteChecks
{
    public static string Run()
    {
        var route = new HighwayRouteState();
        float first=HighwayRouteState.FirstBranch, length=HighwayRouteState.ConnectorLength;
        int checks=0;
        Action<bool,string> check=(ok,name)=> { checks++; if(!ok) throw new Exception(name); };
        check(route.RouteName=="BAYSHORE" && route.UpcomingBranchDistance(0)==first,"initial route");
        var before = new HighwayRouteState.Frame[24];
        for(int i=0;i<before.Length;i++) before[i]=route.Branch(0,i*40,true);
        route.Advance(first-.01f,-7.5f);
        check(route.Decisions==0,"no early commitment");
        route.Advance(first,-7.5f);
        check(route.TookExit(0) && route.RouteName=="HARBOR","left lane exit");
        check(Vector3.Distance(route.Sample(first-.01f).Position,route.Sample(first+.01f).Position)<.021f,"position continuous at choice");
        check(Vector3.Angle(route.Sample(first-.01f).Forward,route.Sample(first+.01f).Forward)<.02f,"heading continuous at choice");
        for(int i=0;i<before.Length;i++)
            check(Vector3.Distance(before[i].Position,route.Branch(0,i*40,true).Position)<.001f,"visible ramp does not move after choice");
        float minX=0,maxAngle=0,minCrossingHeight=100;
        for(float d=1;d<length;d+=1)
        {
            var a=route.Branch(0,d-1,true); var b=route.Branch(0,d,true);
            check(Mathf.Abs(Vector3.Distance(a.Position,b.Position)-1)<.008f,"arc length keeps speed constant");
            maxAngle=Mathf.Max(maxAngle,Vector3.Angle(a.Forward,b.Forward));
            minX=Mathf.Min(minX,b.Position.x);
            if(d>200 && Mathf.Abs(b.Position.x)<11) minCrossingHeight=Mathf.Min(minCrossingHeight,b.Position.y);
        }
        check(minX < -50,"ramp leaves to the left first");
        check(maxAngle<1.3f,"no abrupt camera turn anywhere on ramp");
        check(minCrossingHeight>20 && minCrossingHeight<30,"flyover clears original motorway");
        var merge=route.Branch(0,length,true);
        for(float d=1;d<length;d+=2)
        foreach(float lane in new[]{-9.5f,-1.5f})
        {
            var frame=route.Branch(0,d,true);
            Vector3 rider=frame.Point(lane);
            float across=rider.z-merge.Position.z;
            // In a left-driving motorway, eastbound lanes are north of its centre.
            // Any crossing of the opposing lanes must be an elevated flyover.
            if(across>=-12 && across<=0)
                check(rider.y-merge.Position.y>8,"ramp never crosses opposing lanes at road level");
            if(d>=HighwayRouteState.ParallelStart)
                check(across>0,"parallel approach and merge stay on the correct side");
        }
        var parallel=route.Branch(0,HighwayRouteState.ParallelStart,true);
        check(Vector3.Angle(parallel.Forward,merge.Forward)<.01f && parallel.Position.z-merge.Position.z>30,"acceleration road starts parallel outside left shoulder");
        check(Vector3.Angle(merge.Forward,Vector3.right)<.01f,"destination turns ninety degrees right");
        check(Vector3.Distance(merge.Position,route.Crossing(0,0).Position)<.001f,"merge shares destination centreline");
        check(Vector3.Distance(route.Sample(first+length-.01f).Position,route.Sample(first+length+.01f).Position)<.021f,"position continuous at merge");
        check(Vector3.Angle(route.Sample(first+length-.01f).Forward,route.Sample(first+length+.01f).Forward)<.02f,"heading continuous at merge");
        check(route.IsTrafficSuppressed(0,first-180) && route.IsTrafficSuppressed(0,first+length+80) && !route.IsTrafficSuppressed(0,first-181),"traffic cleared through full ramp");
        var oldRamp=route.Branch(0,330,true); var oldCross=route.Crossing(0,-300);
        route.Advance(HighwayRouteState.BranchAt(1),-7.5f);
        check(Vector3.Distance(oldRamp.Position,route.Branch(0,330,true).Position)<.001f && Vector3.Distance(oldCross.Position,route.Crossing(0,-300).Position)<.001f,"later choice cannot move old roads");
        foreach(bool exit0 in new[]{false,true})
        foreach(bool exit1 in new[]{false,true})
        {
            route.Reset(); route.Advance(first,exit0?-7.5f:-3.1f);
            float second=HighwayRouteState.BranchAt(1);
            var previous=route.Sample(second-.01f);
            route.Advance(second,exit1?-7.5f:-3.1f);
            var next=route.Sample(second+.01f);
            check(Vector3.Distance(previous.Position,next.Position)<.025f,"second junction position continuity for both choices");
            check(Vector3.Angle(previous.Forward,next.Forward)<.02f,"second junction heading continuity for both choices");
        }
        route.Reset(); route.Advance(first,-3.1f); route.Advance(first+1,-9);
        check(!route.TookExit(0) && route.RouteName=="BAYSHORE","mainline choice is stable");
        check(Vector3.Distance(route.Sample(first+length).Position,Vector3.forward*(first+length))<.001f,"staying continues under flyover");
        route.Reset();
        check(route.Decisions==0 && route.RouteName=="BAYSHORE" && route.Sample(0).Position==Vector3.zero,"reset restores road graph");
        return "PASS: "+checks+" route assertions; ramp "+length.ToString("F1")+" m, max heading step "+maxAngle.ToString("F3")+" deg/m, crossing clearance "+minCrossingHeight.ToString("F1")+" m";
    }
}
