using System;
using System.Reflection;
using HighwaySurfing;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class SpeedLimitChecks
{
    public static string Run()
    {
        var world=UnityEngine.Object.FindFirstObjectByType<NightHighwayWorld>();
        if(!EditorApplication.isPlaying || world==null || world.UseExternalSteering || Time.deltaTime<=0)
            throw new Exception("Run in the playing keyboard Sandbox.");
        float originalLimit=world.MaximumSpeedKmh;
        var originalKeyboard=Keyboard.current;
        var keyboard=InputSystem.AddDevice<Keyboard>("SpeedLimitCheckKeyboard"); keyboard.MakeCurrent();
        var update=typeof(NightHighwayWorld).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
        int checks=0;
        try
        {
            foreach(float limit in new[]{220f,400f,60f})
            {
                world.MaximumSpeedKmh=limit;world.ResetRide();
                Check(Mathf.Abs(world.SpeedKmh-Mathf.Min(138,limit))<.01f,"reset respects cap");checks++;
                world.SpeedKmh=limit-10;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));InputSystem.Update();
                update.Invoke(world,null);
                Check(world.SpeedKmh>limit-10 && world.SpeedKmh<=limit,"keyboard accelerates toward selected cap");checks++;
                world.SpeedKmh=limit;
                update.Invoke(world,null);
                Check(world.SpeedKmh==limit,"keyboard cannot exceed cap");checks++;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S));InputSystem.Update();
                update.Invoke(world,null);
                Check(world.SpeedKmh<limit,"keyboard brakes above 220 too");checks++;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                world.SpeedKmh=limit-.1f;world.ApplySpeedControl(1,30,60,.05f);
                Check(world.SpeedKmh==limit,"shared Kinect speed control respects cap");checks++;
                world.AttractDemoActive=true;world.AttractDemoSpeedKmh=500;
                update.Invoke(world,null);
                Check(world.SpeedKmh==limit,"demo respects cap");checks++;
                world.AttractDemoActive=false;world.SpeedKmh=400;
                double before=world.TravelledMetres;update.Invoke(world,null);
                Check(world.SpeedKmh==limit && Math.Abs(world.TravelledMetres-before-limit/3.6*Mathf.Min(Time.deltaTime,.05f))<.001,
                    "actual distance uses configured cap");checks++;
            }
            return "PASS: "+checks+" live speed-limit checks (keyboard, shared arm control, demo, reset and distance at 60/220/400).";
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            if(originalKeyboard!=null && originalKeyboard.added)originalKeyboard.MakeCurrent();
            world.MaximumSpeedKmh=originalLimit;world.AttractDemoActive=false;world.AttractDemoSpeedKmh=138;world.ResetRide();
        }
    }
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
}
