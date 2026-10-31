using System;
using HighwaySurfing;
using HighwaySurfing.Kinect;
using UnityEngine;

public static class RelaxedEntryChecks
{
    public static string Run()
    {
        int count=0;
        Action<bool,string> check=(ok,name)=>{count++;if(!ok)throw new Exception(name);};
        Vector3 p=KinectDebugRig.NeutralJoint(0), c=KinectDebugRig.NeutralJoint(2), l=KinectDebugRig.NeutralJoint(5), r=KinectDebugRig.NeutralJoint(12);
        foreach(float yaw in new[]{-45f,0,45f,90f})
        foreach(float pitch in new[]{-45f,-30f,0,30f,45f})
        {
            var q=Quaternion.Euler(0,yaw,0);
            Vector3 arm=Vector3.forward*(.5f*Mathf.Cos(pitch*Mathf.Deg2Rad))+Vector3.up*(.5f*Mathf.Sin(pitch*Mathf.Deg2Rad));
            Vector3 left=l+new Vector3(arm.x,arm.y,-arm.z),right=r+arm;
            check(SurfPoseDetector.IsOpen(q*p,q*c,q*l,q*r,q*left,q*right,Vector3.up,Vector3.right,true),"relaxed arm pitch/yaw accepted: "+pitch+"/"+yaw);
            var hint=SurfPoseDetector.GetGuidance(q*p,q*c,q*l,q*r,q*left,q*right,Vector3.up,Vector3.right,true);
            check(hint.Action==SurfPoseDetector.Adjustment.Ready,"guidance agrees with relaxed start gate");
            if(Mathf.Abs(pitch)>=30) check(!SurfPoseDetector.IsOpen(q*p,q*c,q*l,q*r,q*left,q*right,Vector3.up,Vector3.right),"strict pose remains stricter");
        }
        check(SurfPoseDetector.IsOpen(p,c,l,r,l+Vector3.back*.26f,r+Vector3.forward*.26f,Vector3.up,Vector3.right,true),"partly bent arms accepted");
        check(!SurfPoseDetector.IsOpen(p,c,l,r,l+Vector3.back*.26f,r+Vector3.forward*.26f,Vector3.up,Vector3.right),"strict reach unchanged");
        check(SurfPoseDetector.IsOpen(p,c+Vector3.right*.14f,l,r,l+Vector3.back*.5f,r+Vector3.forward*.5f,Vector3.up,Vector3.right,true),"modest standing lean accepted");
        foreach(bool relaxed in new[]{false,true})
        {
            check(!SurfPoseDetector.IsOpen(p,c,l,r,l+Vector3.down*.5f,r+Vector3.down*.5f,Vector3.up,Vector3.right,relaxed),"arms hanging still rejected");
            check(!SurfPoseDetector.IsOpen(p,c,l,r,l+Vector3.up*.5f,r+Vector3.up*.5f,Vector3.up,Vector3.right,relaxed),"overhead hands still rejected");
            check(!SurfPoseDetector.IsOpen(p,c,l,r,l+Vector3.back*.1f,r+Vector3.forward*.1f,Vector3.up,Vector3.right,relaxed),"fully folded arms rejected");
            check(!SurfPoseDetector.IsOpen(p,c,l,r,new Vector3(float.NaN,0,0),r+Vector3.forward*.5f,Vector3.up,Vector3.right,relaxed),"invalid tracking rejected");
        }
        var raise=SurfPoseGuidanceText.Title(new SurfPoseDetector.Guidance(SurfPoseDetector.Adjustment.RaiseArms));
        foreach(var action in new[]{SurfPoseDetector.Adjustment.RaiseMore,SurfPoseDetector.Adjustment.RaiseSlightly})
            check(SurfPoseGuidanceText.Title(new SurfPoseDetector.Guidance(action,7))==raise,"height and side variants share one raise instruction");
        check(SurfPoseGuidanceText.Title(new SurfPoseDetector.Guidance(SurfPoseDetector.Adjustment.LowerArms))==SurfPoseGuidanceText.Title(new SurfPoseDetector.Guidance(SurfPoseDetector.Adjustment.LowerSlightly,14)),"one lower instruction");
        check(SurfPoseGuidanceText.Title(new SurfPoseDetector.Guidance(SurfPoseDetector.Adjustment.Ready)).Contains("維持"),"clear hold instruction");
        return "PASS: "+count+" relaxed entry and coarse guidance checks";
    }
}
