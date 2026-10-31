using System;
using System.Collections.Generic;
using System.IO;
using HighwaySurfing;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Explicit Main simulation only. Never opens a native sensor or saves a scene.</summary>
public static class AttractModePlayChecks
{
    static HighwayAttractMode attract;
    static KinectHighwayInput input;
    static SurfTutorialView view;
    static NightHighwayWorld world;
    static HighwayRideFeedback feedback;
    static bool running;
    static int phase;
    static double since, distance;
    static readonly List<string> report=new List<string>();
    [MenuItem("Tools/Highway Surfing/Checks/Attract Mode (Simulated Main)")]
    public static void Run()=>Debug.Log(Start());
    public static string Start()
    {
        if(running || !EditorApplication.isPlaying || EditorApplication.isPaused)throw new Exception("Play saved Main with Auto Start Kinect temporarily OFF first.");
        attract=UnityEngine.Object.FindFirstObjectByType<HighwayAttractMode>();
        if(attract==null || attract.Input.AutoStartKinect || attract.Input.Rig.CaptureActive)throw new Exception("Use a disconnected, explicitly simulated Main only.");
        input=attract.Input;world=input.World;view=UnityEngine.Object.FindFirstObjectByType<SurfTutorialView>();
        feedback=UnityEngine.Object.FindFirstObjectByType<HighwayRideFeedback>();
        input.AllowSimulationForTesting=true;input.Rig.UseSimulation();input.ReleasePerson();
        input.Rig.SimulationFrontArmPitchDegrees=-70;
        attract.enabled=false;attract.enabled=true;
        attract.EnableAttractMode=true;attract.IdleSeconds=5;Outside();world.ResetRide();
        report.Clear();running=true;Next(0);
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        return "Attract Main simulation running; .Local/Review/attract-mode-trial.txt";
    }
    static void Outside(){attract.PresenceArea.DepthMetres=new Vector2(10,11);}
    static void Inside(){attract.PresenceArea.DepthMetres=new Vector2(.5f,3.5f);}
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
                    if(age<1)return;
                    Check(!attract.DemoActive && world.TravelledMetres==0,"no demo before the idle delay");
                    Check(!attract.PersonInArea && !input.RideRegistered,"outside ROI cannot register a visitor");
                    Next(1);break;
                case 1:
                    if(age<4.6)return;
                    Check(attract.DemoActive && world.TravelledMetres>5,"five seconds idle starts real road motion");
                    Check(Mathf.Abs(view.Group.alpha-.15f)<.01f,"idle modal settles at fifteen percent");
                    Check(Mathf.Abs(world.SpeedKmh-input.GetComponent<KinectArmSpeedInput>().StartingSpeedKmh)<.01f,"demo uses the configured starting speed");
                    Check(!input.RideRegistered && !world.ExternalRideReady && feedback.DepartureCount==0,"demo never registers a player or plays departure chime");
                    Check(world.MainCamera.GetUniversalAdditionalCameraData().renderPostProcessing,"night lighting remains enabled in demo");
                    foreach(var text in new[]{view.Title,view.Instruction,view.ProgressText})
                    {text.ForceMeshUpdate();Check(!text.isTextOverflowing,"demo instruction fits: "+text.name);}
                    distance=world.TravelledMetres;Next(2);break;
                case 2:
                    if(age<.5)return;
                    Check(world.TravelledMetres>distance+5,"demo keeps advancing across frames");
                    Check(world.CollisionCount==0,"demo does not count damage");
                    world.Paused=true;distance=world.TravelledMetres;Next(3);break;
                case 3:
                    if(age<.3)return;
                    Check(world.TravelledMetres==distance,"operator pause also pauses demo");
                    world.Paused=false;attract.IdleSeconds=.6f;Inside();Next(4);break;
                case 4:
                    if(age<.5)return;
                    Check(attract.PersonInArea && !attract.DemoActive,"visitor with arms down stops demo");
                    Check(Mathf.Abs(view.Group.alpha-1)<.01f,"visitor restores full opacity");
                    Check(!input.RideRegistered && !world.MotionActive,"visitor must still register before riding");
                    Check(!world.MainCamera.GetUniversalAdditionalCameraData().renderPostProcessing,"full modal restores readable presentation");
                    distance=world.TravelledMetres;Next(5);break;
                case 5:
                    if(age<.25)return;
                    Check(world.TravelledMetres==distance,"road stops while visitor reads instructions");
                    Outside();Next(6);break;
                case 6:
                    if(age<.25)return;
                    Check(!attract.DemoActive,"brief presence loss cannot start demo");
                    Inside();input.Rig.SimulationFrontArmPitchDegrees=0;Next(7);break;
                case 7:
                    if(!input.RideRegistered){if(age>3)throw new Exception("Registration did not complete");return;}
                    Next(8);break;
                case 8:
                    if(age<.5)return;
                    Check(!attract.DemoActive && world.ExternalRideReady && view.Group.alpha<.01f,"registered gameplay owns movement and hides modal");
                    Check(feedback.DepartureCount==1,"real registration plays departure once");
                    Outside();Next(9);break;
                case 9:
                    if(age<3.3)return;
                    Check(input.RideRegistered && !attract.DemoActive,"registered tracked player is not released at ROI boundary");
                    input.ReleasePerson();input.Rig.StopKinect();Next(10);break;
                case 10:
                    if(age<1)return;
                    Check(attract.DemoActive && !input.RideRegistered,"empty/disconnected sensor returns to demo");
                    attract.EnableAttractMode=false;Next(11);break;
                case 11:
                    if(age<.5)return;
                    Check(!attract.DemoActive && !world.AttractDemoActive && !world.MotionActive && view.Group.alpha>.99f,"Inspector OFF restores stationary full-opacity wait");
                    attract.EnableAttractMode=true;input.UseKeyboard();Next(12);break;
                case 12:
                    if(age<.8)return;
                    Check(!attract.DemoActive && !world.UseExternalSteering && world.MotionActive,"keyboard preview is not taken over by demo");
                    Finish("PASS: "+report.Count+" Main attract-mode play checks");break;
            }
        }
        catch(Exception e){Finish("FAIL phase "+phase+": "+e.Message);}
    }
    static void OnPlayModeChanged(PlayModeStateChange state){if(running && state==PlayModeStateChange.ExitingPlayMode)Finish("CANCELLED: Play stopped");}
    static void Finish(string result)
    {
        running=false;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlayModeChanged;
        Directory.CreateDirectory(".Local/Review");File.WriteAllText(".Local/Review/attract-mode-trial.txt",result+"\n"+string.Join("\n",report));
        if(result.StartsWith("FAIL"))Debug.LogError(result);else Debug.Log(result);
    }
}
