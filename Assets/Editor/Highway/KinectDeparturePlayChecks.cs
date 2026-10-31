using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HighwaySurfing;
using HighwaySurfing.Kinect;
using UnityEditor;
using UnityEngine;

/// <summary>Runs only in an explicitly disconnected Main Play session; never starts native Kinect.</summary>
public static class KinectDeparturePlayChecks
{
    static KinectHighwayInput input;
    static KinectDebugRig rig;
    static HighwayAttractMode attract;
    static bool running;
    static int phase;
    static double since;
    static readonly List<string> report=new List<string>();
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Tools/Highway Surfing/Checks/Departure (Simulated Main)")]
    public static void Run()=>Debug.Log(Start());
    public static string Start()
    {
        if(running || !EditorApplication.isPlaying || EditorApplication.isPaused)throw new Exception("Play Main with Auto Start Kinect temporarily OFF.");
        input=UnityEngine.Object.FindFirstObjectByType<KinectHighwayInput>();
        if(input==null || input.AutoStartKinect || input.Rig.CaptureActive)throw new Exception("An explicitly disconnected Main is required.");
        rig=input.Rig;attract=input.GetComponent<HighwayAttractMode>();
        report.Clear();
        Check(!input.ReleaseOnPoseLoss && input.DepartureGraceSeconds==6 && input.PoseReleaseSeconds==5,"Main persists tolerant departure defaults");
        input.AllowSimulationForTesting=true;rig.UseSimulation();input.ReleasePerson();
        attract.IdleSeconds=.6f;input.World.ResetRide();running=true;Next(0);
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        return "Departure simulation running; .Local/Review/kinect-departure-trial.txt";
    }
    static void Next(int next){phase=next;since=EditorApplication.timeSinceStartup;}
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);report.Add("PASS "+name);}
    static void Presence(bool present)
    {
        // Keep the same identity/calibration while changing only the simulated stream's presence.
        // Calling StopKinect/UseSimulation would clear calibration and would not test a dropout.
        rig.Simulation=present;
        typeof(KinectDebugRig).GetField("lastSimulation",Private).SetValue(rig,present);
        if(!present)typeof(KinectDebugRig).GetField("bodyAvailable",Private).SetValue(rig,false);
    }
    static void Tick()
    {
        try
        {
            double age=EditorApplication.timeSinceStartup-since;
            switch(phase)
            {
                case 0:
                    if(!input.RideRegistered){if(age>3)throw new Exception("Initial registration timed out");return;}
                    rig.SimulationFrontArmPitchDegrees=-70;Next(1);break;
                case 1:
                    if(age<6.2)return;
                    Check(input.RideRegistered && !input.ShowTutorial,"lowered arm for over six seconds does not release default ride");
                    input.ReleaseOnPoseLoss=true;input.PoseReleaseSeconds=.6f;
                    typeof(KinectHighwayInput).GetField("poseLostSince",Private).SetValue(input,Time.unscaledTime-2);
                    var confidence=(int[])typeof(KinectDebugRig).GetField("confidence",Private).GetValue(rig);
                    confidence[7]=0;
                    typeof(KinectHighwayInput).GetMethod("UpdateRidePose",Private).Invoke(input,null);
                    Check(input.RideRegistered && (float)typeof(KinectHighwayInput).GetField("poseLostSince",Private).GetValue(input)<0,
                        "unknown arm cannot finish an optional pose departure and resets its timer");
                    confidence[7]=2;Next(2);break;
                case 2:
                    if(age<.3)return;
                    Check(input.RideRegistered,"optional pose departure waits for its configured hold");
                    rig.SimulationFrontArmPitchDegrees=0;Next(3);break;
                case 3:
                    if(age<.25)return;
                    Check(input.RideRegistered,"restoring pose cancels optional departure");
                    rig.SimulationFrontArmPitchDegrees=-70;Next(4);break;
                case 4:
                    if(age<.3)return;
                    Check(input.RideRegistered,"repeated pose drop starts a fresh hold");Next(5);break;
                case 5:
                    if(age<.45)return;
                    Check(!input.RideRegistered && input.ShowTutorial,"optional continuously observed pose loss still releases");
                    input.ReleaseOnPoseLoss=false;rig.SimulationFrontArmPitchDegrees=0;Next(6);break;
                case 6:
                    if(!input.RideRegistered){if(age>3)throw new Exception("Re-registration timed out");return;}
                    Presence(false);Next(7);break;
                case 7:
                    if(age<3.2)return;
                    Check(input.RideRegistered && !input.ShowTutorial,"three-second body dropout retains registration and hides modal");
                    Check(!input.InputAvailable && input.World.SteeringInput==0,"missing body cannot steer");
                    Presence(true);Next(8);break;
                case 8:
                    if(age<.3)return;
                    Check(input.RideRegistered && input.InputAvailable,"same person resumes without new registration");
                    Presence(false);Next(9);break;
                case 9:
                    if(age<3.2)return;
                    Check(input.RideRegistered && !input.ShowTutorial,"separate dropouts are not accumulated");Next(10);break;
                case 10:
                    if(age<3.1)return;
                    Check(!input.RideRegistered && input.ShowTutorial,"six continuous seconds absent releases the person");
                    Next(11);break;
                case 11:
                    if(age<.9)return;
                    Check(attract.DemoActive,"actual departure returns to unattended demo");
                    Finish("PASS: "+report.Count+" departure play checks");break;
            }
        }
        catch(Exception e){Finish("FAIL phase "+phase+": "+e.Message);}
    }
    static void OnPlayModeChanged(PlayModeStateChange state){if(running && state==PlayModeStateChange.ExitingPlayMode)Finish("CANCELLED: Play stopped");}
    static void Finish(string result)
    {
        running=false;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlayModeChanged;
        Directory.CreateDirectory(".Local/Review");File.WriteAllText(".Local/Review/kinect-departure-trial.txt",result+"\n"+string.Join("\n",report));
        if(result.StartsWith("FAIL"))Debug.LogError(result);else Debug.Log(result);
    }
}
