using System;
using HighwaySurfing.Kinect;
using UnityEngine;

public static class AttractModeChecks
{
    public static string Run()
    {
        int count = 0;
        var area = new KinectPresenceArea { Enabled=true, DepthMetres=new Vector2(.5f,3.5f), HalfWidthMetres=1.5f };
        Check(area.Contains(new Vector3(0, -.2f, 2)), "pelvis inside ROI"); count++;
        Check(area.Contains(new Vector3(-1.5f, .5f, .5f)) && area.Contains(new Vector3(1.5f, 0, 3.5f)), "ROI edges included"); count++;
        Check(!area.Contains(new Vector3(1.65f,0,2)) && area.Contains(new Vector3(1.65f,0,2),.3f), "departure margin does not widen entry area"); count++;
        Check(area.Contains(new Vector3(0,0,3.7f),.3f) && area.Contains(new Vector3(0,0,.3f),.3f), "departure margin covers front and rear edges"); count++;
        Check(!area.Contains(new Vector3(1.9f,0,2),.3f) && !area.Contains(new Vector3(0,0,3.9f),.3f), "clear departure remains outside margin"); count++;
        foreach(var p in new[]{new Vector3(1.51f,0,2),new Vector3(-1.51f,0,2),new Vector3(0,0,.49f),new Vector3(0,0,3.51f),new Vector3(float.NaN,0,2)})
        { Check(!area.Contains(p), "outside/invalid pelvis rejected"); count++; }
        area.Enabled=false;
        Check(area.Contains(new Vector3(4,0,5)), "ROI disabled accepts full sensor area"); count++;
        Check(!area.Contains(new Vector3(0,0,float.NaN)), "invalid position still rejected"); count++;
        var selector=new KinectBodySelector();
        var bodies=new[]{new KinectBodySelector.Candidate(1,1,2,false),new KinectBodySelector.Candidate(2,2)};
        Check(selector.Step(bodies,2,0)==1, "closer bystander outside ROI cannot block visitor"); count++;
        bodies[1]=new KinectBodySelector.Candidate(2,2,2,false);
        Check(selector.Step(bodies,2,1,lockSelection:true)==1 && selector.SelectedId==2, "locked player kept at ROI edge"); count++;
        bodies[0]=new KinectBodySelector.Candidate(1,1);
        Check(selector.Step(bodies,2,2)==0 && selector.SelectedId==1, "unregistered outside visitor yields selection"); count++;
        return "PASS: "+count+" attract area and selection checks";
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
