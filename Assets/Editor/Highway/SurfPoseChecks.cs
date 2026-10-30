using System;
using HighwaySurfing.Kinect;
using UnityEngine;

public static class SurfPoseChecks
{
    public static string Run()
    {
        int count=0;
        foreach(float yaw in new[]{0f,45f,90f,-45f})
        {
            var q=Quaternion.Euler(0,yaw,0);
            Check(SurfPoseDetector.IsOpen(q*KinectDebugRig.NeutralJoint(0),q*KinectDebugRig.NeutralJoint(2),q*KinectDebugRig.NeutralJoint(5),q*KinectDebugRig.NeutralJoint(12),q*KinectDebugRig.NeutralJoint(7),q*KinectDebugRig.NeutralJoint(14),Vector3.up,Vector3.right),"open arms at participant yaw "+yaw);count++;
        }
        var p=KinectDebugRig.NeutralJoint(0);var c=KinectDebugRig.NeutralJoint(2);
        var l=KinectDebugRig.NeutralJoint(5);var r=KinectDebugRig.NeutralJoint(12);
        Check(!SurfPoseDetector.IsOpen(p,c,l,r,l+Vector3.down*.5f,r+Vector3.down*.5f,Vector3.up,Vector3.right),"arms down cannot start");count++;
        Check(!SurfPoseDetector.IsOpen(p,c,l,r,l+Vector3.up*.5f,r+Vector3.up*.5f,Vector3.up,Vector3.right),"hands overhead are not the tutorial pose");count++;
        Check(!SurfPoseDetector.IsOpen(p,c,l,r,r+Vector3.forward*.3f,l-Vector3.forward*.3f,Vector3.up,Vector3.right),"crossed arms cannot start");count++;
        Check(!SurfPoseDetector.IsOpen(p,c+Vector3.right*.25f,l,r,KinectDebugRig.NeutralJoint(7),KinectDebugRig.NeutralJoint(14),Vector3.up,Vector3.right),"large side lean cannot become neutral");count++;
        Check(!SurfPoseDetector.IsOpen(p,c,l,r,new Vector3(float.NaN,0,0),KinectDebugRig.NeutralJoint(14),Vector3.up,Vector3.right),"invalid wrist cannot start");count++;
        return $"PASS: {count} surfing pose checks";
    }
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
}
