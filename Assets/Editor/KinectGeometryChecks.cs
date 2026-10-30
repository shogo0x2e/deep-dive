using System;
using UnityEngine;
using HighwaySurfing.Kinect;
public static class KinectGeometryChecks
{
    public static string Run()
    {
        var display = new GameObject("CheckDisplay").transform;
        var sensor = new GameObject("CheckSensor").transform;
        int count=0;
        try
        {
            Vector3 pelvis=new Vector3(.2f,1,0), chest=pelvis+new Vector3(.2f,.5f,0);
            float expected=Mathf.Atan2(.2f,.5f)*Mathf.Rad2Deg;
            foreach(float yaw in new float[]{180,225,270})
            {
                sensor.SetPositionAndRotation(new Vector3(1.8f,1.1f,2),Quaternion.Euler(0,yaw,0));
                Vector3 localPelvis=sensor.InverseTransformPoint(pelvis), localChest=sensor.InverseTransformPoint(chest);
                Assert(KinectLeanSolver.Measure(sensor.TransformPoint(localPelvis),sensor.TransformPoint(localChest),display,out float a,out _),"valid transformed geometry");
                Near(a,expected,"sensor yaw preserves display-right lean");count++;
            }
            var solver=new KinectLeanSolver();
            solver.Calibrate(0,.2f);
            Assert(solver.Step(pelvis,chest,display,true,0,.3f,.1f),"positive lean accepted");
            Assert(solver.RawSteer>0,"right lean is positive");count++;
            float before=solver.RawDegrees;
            solver.Step(pelvis+new Vector3(3,1,-2),chest+new Vector3(3,1,-2),display,true,0,.3f,.1f);
            Near(solver.RawDegrees,before,"common translation preserves angle");
            Near(solver.PelvisOffset,3,"translation changes position metric");count++;
            solver.Step(pelvis,pelvis+new Vector3(-.2f,.5f,0),display,true,0,.3f,.1f);
            Assert(solver.RawSteer<0,"left lean is negative");count++;
            Assert(!solver.Step(pelvis,chest,display,true,.31f,.3f,.1f),"stale frame rejected");
            Near(solver.SmoothedSteer,0,"stale clears previously nonzero output");count++;
            solver.Step(pelvis,chest,display,true,0,.3f,.1f);
            Assert(!solver.Step(pelvis,chest,display,false,0,.3f,.1f),"invalid joints rejected");
            Near(solver.SmoothedSteer,0,"tracking loss clears input");count++;
            solver.Reset();
            Assert(!solver.Calibrated,"reset invalidates neutral");
            Assert(!solver.Step(pelvis,chest,display,true,0,.3f,.1f),"uncalibrated input held");count++;
            solver.Calibrate(expected,.2f);
            solver.Step(pelvis,chest,display,true,0,.3f,.1f);
            Near(solver.RawSteer,0,"neutral posture subtracts baseline");count++;
            display.rotation=Quaternion.Euler(0,90,0);
            Vector3 p=display.TransformPoint(new Vector3(0,1,0)), c=display.TransformPoint(new Vector3(.2f,1.5f,0));
            Assert(KinectLeanSolver.Measure(p,c,display,out float rotatedAngle,out _),"rotated display valid");
            Near(rotatedAngle,expected,"fixed display axes determine screen right");count++;
            sensor.SetPositionAndRotation(Vector3.zero,Quaternion.identity);display.rotation=Quaternion.identity;
            Vector3 lp=new Vector3(0,1,2),lc=new Vector3(0,1.5f,2);
            sensor.rotation=Quaternion.Euler(0,0,-15);
            KinectLeanSolver.Measure(sensor.TransformPoint(lp),sensor.TransformPoint(lc),display,out float movedAngle,out _);
            Near(movedAngle,15,"editing parent rotation changes measured world angle");count++;
            Assert(!KinectLeanSolver.Measure(Vector3.zero,new Vector3(float.NaN,1,0),display,out _,out _),"NaN rejected");count++;
            return $"PASS: {count} geometry / calibration / tracking-loss checks";
        }
        finally { UnityEngine.Object.DestroyImmediate(display.gameObject);UnityEngine.Object.DestroyImmediate(sensor.gameObject); }
    }
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Near(float a,float b,string message){Assert(Mathf.Abs(a-b)<.001f,$"{message}: {a} vs {b}");}
}
