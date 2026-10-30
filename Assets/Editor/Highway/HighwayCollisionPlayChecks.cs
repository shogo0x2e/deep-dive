using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HighwaySurfing;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Real PlayerLoop contacts: run in the keyboard Sandbox, then read the Console/report.</summary>
public static class HighwayCollisionPlayChecks
{
    static NightHighwayWorld world;
    static HighwayImpactFeedback impact;
    static Keyboard keyboard, originalKeyboard;
    static bool running, originalEnabled;
    static float originalLoss, expected, speedAtHit;
    static int scenario, stage, impactBaseline, assertions;
    static double since;
    static readonly List<string> results = new List<string>();
    static readonly float[] Speeds = {220,220,138,138,86.4f,0};
    static readonly float[] Losses = {30,30,10,100,30,30};
    static readonly string[] Names = {"enabled 30%", "disabled", "custom 10%", "full stop 100%", "equal-speed side contact", "stationary rear contact"};

    [MenuItem("Tools/Highway Surfing/Checks/Collision Slowdown (Play)")]
    public static void Run() => Debug.Log(Start());
    public static string Start()
    {
        if (running) throw new InvalidOperationException("Collision checks are already running.");
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Play HighwayKeyboard first.");
        world=UnityEngine.Object.FindFirstObjectByType<NightHighwayWorld>();
        if (world==null || world.UseExternalSteering) throw new InvalidOperationException("Run in the keyboard Sandbox.");
        impact=world.GetComponent<HighwayImpactFeedback>();
        if(impact==null) throw new InvalidOperationException("Impact feedback is required.");
        originalEnabled=world.CollisionSlowdownEnabled; originalLoss=world.CollisionSpeedLossPercent;
        originalKeyboard=Keyboard.current;
        keyboard=InputSystem.AddDevice<Keyboard>("CollisionCheckKeyboard"); keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        results.Clear(); assertions=0; scenario=0; running=true;
        EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        EditorApplication.update+=Tick;
        Prepare();
        return "Collision checks running; report: .Local/Review/highway-collision-trial.txt";
    }
    static void Prepare()
    {
        world.ResetRide(); world.SpeedKmh=Speeds[scenario];
        world.CollisionSlowdownEnabled=scenario!=1; world.CollisionSpeedLossPercent=Losses[scenario];
        expected=Speeds[scenario]*(scenario==1 ? 1 : 1-Losses[scenario]/100);
        world.TrySampleTraffic(0,out _,out float lane,out _);
        // Arrange a near contact, then let normal Update, swept collision and the event run.
        var traffic=(HighwayTraffic)typeof(NightHighwayWorld).GetField("traffic",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(world);
        var vehicles=(Array)typeof(HighwayTraffic).GetField("vehicles",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(traffic);
        object car=vehicles.GetValue(0);
        car.GetType().GetField("Distance").SetValue(car,scenario==5 ? -12f : scenario==4 ? 0f : 12f);
        if(scenario==4)
        {
            lane=-6; car.GetType().GetField("Lateral").SetValue(car,lane);
            world.LateralPosition=-3.1f;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));
        }
        else {world.LateralPosition=lane; InputSystem.QueueStateEvent(keyboard,new KeyboardState());}
        vehicles.SetValue(car,0);
        impactBaseline=impact.HitCount; stage=0; since=EditorApplication.timeSinceStartup;
    }
    static void Check(bool ok,string name)
    {
        if(!ok) throw new Exception(Names[scenario]+": "+name);
        assertions++; results.Add("PASS "+Names[scenario]+": "+name);
    }
    static void Tick()
    {
        try
        {
            double age=EditorApplication.timeSinceStartup-since;
            if(stage==0)
            {
                if(world.CollisionCount==0) {if(age>3) throw new Exception("No contact: "+Names[scenario]); return;}
                Check(Mathf.Abs(world.SpeedKmh-expected)<.05f,"speed changes by configured amount");
                Check(world.CollisionCount==1 && impact.HitCount==impactBaseline+1,"impact feedback still fires once");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                speedAtHit=world.SpeedKmh; stage=1; since=EditorApplication.timeSinceStartup;
            }
            else if(stage==1 && age>.55)
            {
                Check(Mathf.Abs(world.SpeedKmh-speedAtHit)<.05f,"no repeat penalty or automatic speed recovery");
                Check(world.CollisionCount==1,"continuous contact is not counted twice");
                if(scenario==0)
                {
                    world.Paused=true; InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                    stage=2; since=EditorApplication.timeSinceStartup;
                }
                else Next();
            }
            else if(stage==2 && age>.2)
            {
                Check(Mathf.Abs(world.SpeedKmh-speedAtHit)<.05f,"pause holds post-hit speed");
                world.Paused=false; stage=3; since=EditorApplication.timeSinceStartup;
            }
            else if(stage==3 && age>.35)
            {
                Check(world.SpeedKmh>speedAtHit+5,"keyboard acceleration recovers speed");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); Next();
            }
        }
        catch(Exception e) {Finish("FAIL: "+e.Message);}
    }
    static void Next()
    {
        world.ResetRide();
        Check(world.CollisionCount==0 && Mathf.Abs(world.SpeedKmh-138)<.05f,"reset restores starting speed and clears contacts");
        scenario++;
        if(scenario<Speeds.Length) Prepare();
        else Finish("PASS: "+assertions+" collision gameplay checks across "+Speeds.Length+" contact scenarios");
    }
    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingPlayMode && running) Finish("CANCELLED: Play stopped");
    }
    static void Finish(string result)
    {
        EditorApplication.update-=Tick; EditorApplication.playModeStateChanged-=OnPlayModeChanged; running=false;
        if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if(originalKeyboard!=null && originalKeyboard.added) originalKeyboard.MakeCurrent();
        if(world!=null)
        {
            world.CollisionSlowdownEnabled=originalEnabled; world.CollisionSpeedLossPercent=originalLoss; world.ResetRide();
        }
        Directory.CreateDirectory(".Local/Review");
        File.WriteAllText(".Local/Review/highway-collision-trial.txt",result+"\n"+string.Join("\n",results));
        if(result.StartsWith("FAIL")) Debug.LogError(result); else Debug.Log(result);
    }
}
