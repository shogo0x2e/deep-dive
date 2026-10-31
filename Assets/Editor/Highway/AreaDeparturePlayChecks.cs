using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HighwaySurfing;
using HighwaySurfing.Kinect;
using UnityEditor;
using UnityEngine;

/// <summary>Moves an explicitly synthetic body across the sensor-space ROI without losing tracking.</summary>
public static class AreaDeparturePlayChecks
{
    static KinectHighwayInput input;
    static KinectDebugRig rig;
    static HighwayAttractMode attract;
    static SurfTutorialView view;
    static Matrix4x4 sensorInverse;
    static bool running;
    static int phase, revision;
    static double since, distance;
    static readonly List<string> report=new List<string>();
    static readonly FieldInfo simulationFrame=typeof(KinectDebugRig).GetField("simulationSensorInverse",BindingFlags.Instance|BindingFlags.NonPublic);
    [MenuItem("Tools/Highway Surfing/Checks/Area Departure (Simulated Main)")]
    public static void Run()=>Debug.Log(Start());
    public static string Start()
    {
        input=UnityEngine.Object.FindFirstObjectByType<KinectHighwayInput>();
        if(running || !EditorApplication.isPlaying || EditorApplication.isPaused || input==null || input.AutoStartKinect || input.Rig.CaptureActive)
            throw new Exception("Play Main with Auto Start Kinect temporarily OFF; one explicit simulation at a time.");
        rig=input.Rig;attract=input.GetComponent<HighwayAttractMode>();view=UnityEngine.Object.FindFirstObjectByType<SurfTutorialView>();
        report.Clear();
        Check(input.ReleaseOutsideEntryArea && input.DepartureGraceSeconds==3 && !input.ReleaseOnPoseLoss &&
            !input.PauseDuringDepartureGrace && Mathf.Approximately(input.DepartureAreaMarginMetres,.3f),
            "saved Main keeps three-second grace, thirty-centimetre departure margin, no immediate pause");
        input.AllowSimulationForTesting=true;rig.UseSimulation();input.ReleasePerson();
        sensorInverse=(Matrix4x4)simulationFrame.GetValue(rig);attract.IdleSeconds=20;input.World.ResetRide();
        running=true;Next(0);EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        return "Area-departure simulation running; .Local/Review/area-departure-trial.txt";
    }
    static void Move(bool outside)=>simulationFrame.SetValue(rig,Matrix4x4.Translate(outside?new Vector3(2,0,0):Vector3.zero)*sensorInverse);
    static void Next(int next){phase=next;since=EditorApplication.timeSinceStartup;}
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);report.Add("PASS "+name);}
    static void Tick()
    {
        try
        {
            double age=EditorApplication.timeSinceStartup-since;
            switch(phase)
            {
                case 0:
                    if(!input.RideRegistered){if(age>3)throw new Exception("Registration timed out");return;}
                    rig.SimulationFrontArmPitchDegrees=-70;Next(1);break;
                case 1:
                    if(age<1)return;
                    Check(input.RideRegistered && input.RegisteredPersonPresent,"arms down inside the area keeps the same ride");
                    revision=input.World.RideRevision;
                    simulationFrame.SetValue(rig,Matrix4x4.Translate(new Vector3(1.65f,0,0))*sensorInverse);
                    Next(10);break;
                case 2:
                    if(age<.2)return;
                    Check(rig.IsTracked && rig.SelectedBodyPresent && !input.RegisteredPersonPresent,"tracked body outside ROI counts as absent from play area");
                    Check(input.RideRegistered && !input.ShowTutorial,"brief exit retains registration without flashing modal");
                    Check(input.InputAvailable && input.World.MotionActive,"leaving the departure area keeps steering and forward motion during grace");
                    distance=input.World.TravelledMetres;Next(3);break;
                case 3:
                    if(age<.6)return;
                    Check(input.World.TravelledMetres>distance,"road continues during departure grace");
                    Move(false);Next(4);break;
                case 4:
                    if(age<.3)return;
                    Check(input.RideRegistered && input.InputAvailable && input.World.MotionActive,"return inside grace resumes without new registration");
                    Check(input.World.RideRevision==revision,"brief exit does not reset progress");
                    Move(true);Next(5);break;
                case 5:
                    if(age<2.4)return;
                    Check(input.RideRegistered,"separate area exits do not accumulate");Next(6);break;
                case 6:
                    if(age<.9)return;
                    Check(!input.RideRegistered && !rig.LockSelectedPerson,"three continuous seconds outside releases identity");
                    Check(attract.DemoActive && view.Group.alpha<.2f,"confirmed departure starts thin-modal demo without a second idle wait");
                    Move(false);rig.SimulationFrontArmPitchDegrees=0;Next(7);break;
                case 7:
                    if(age<.4)return;
                    Check(attract.PersonInArea && !attract.DemoActive && view.Group.alpha>.99f,"returning visitor immediately restores full modal");
                    Next(8);break;
                case 8:
                    if(!input.RideRegistered){if(age>3)throw new Exception("Next registration timed out");return;}
                    input.ReleaseOutsideEntryArea=false;Move(true);Next(9);break;
                case 9:
                    if(age<3.3)return;
                    Check(input.RideRegistered && input.World.MotionActive,"Inspector OFF preserves sensor-presence departure behavior");
                    input.ReleaseOutsideEntryArea=true;input.PauseDuringDepartureGrace=true;Next(11);break;
                case 10:
                    if(age<3.3)return;
                    Check(!rig.SelectedBodyInEntryArea && input.RegisteredPersonPresent,"slight exit from entry area remains inside wider departure area");
                    Check(input.RideRegistered && input.InputAvailable && input.World.MotionActive,"boundary margin permits sustained steering without a pause or release");
                    Move(true);Next(2);break;
                case 11:
                    if(age<.2)return;
                    Check(input.RideRegistered && !input.World.MotionActive && !input.InputAvailable,"optional immediate pause remains configurable");
                    Finish("PASS: "+report.Count+" area-departure play checks");break;
            }
        }
        catch(Exception e){Finish("FAIL phase "+phase+": "+e.Message);}
    }
    static void OnPlayModeChanged(PlayModeStateChange state){if(running && state==PlayModeStateChange.ExitingPlayMode)Finish("CANCELLED: Play stopped");}
    static void Finish(string result)
    {
        running=false;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlayModeChanged;
        Move(false);input.ReleaseOutsideEntryArea=true;input.PauseDuringDepartureGrace=false;attract.IdleSeconds=5;
        Directory.CreateDirectory(".Local/Review");File.WriteAllText(".Local/Review/area-departure-trial.txt",result+"\n"+string.Join("\n",report));
        if(result.StartsWith("FAIL"))Debug.LogError(result);else Debug.Log(result);
    }
}
