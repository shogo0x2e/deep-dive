using System;
using System.IO;
using System.Reflection;
using HighwaySurfing;
using UnityEditor;
using UnityEngine;

/// <summary>Check real contact playback and the mixed listener, not just a nonzero clip.</summary>
public static class HighwayImpactAudioChecks
{
    static NightHighwayWorld world;
    static HighwayImpactFeedback impact;
    static HighwayRideFeedback ride;
    static bool running, savedSound, savedEffects, savedDuck;
    static float savedVolume, peak, mixedPeak, minimumMixGain;
    static int scenario, stage, checks;
    static double since;
    static readonly float[] buffer=new float[2048];
    static readonly System.Collections.Generic.List<string> report=new System.Collections.Generic.List<string>();
    static readonly string[] Names={"normal hit","visuals off","sound off","impact volume zero","ducking off"};

    [MenuItem("Tools/Highway Surfing/Checks/Impact Audio (Play)")]
    public static void Run() => Debug.Log(Start());
    public static string Start()
    {
        if(running || !EditorApplication.isPlaying || EditorApplication.isPaused) throw new Exception("Play HighwayKeyboard first; run one check at a time.");
        world=UnityEngine.Object.FindFirstObjectByType<NightHighwayWorld>();
        if(world==null || world.UseExternalSteering) throw new Exception("Use the keyboard Sandbox.");
        impact=world.GetComponent<HighwayImpactFeedback>(); ride=world.GetComponent<HighwayRideFeedback>();
        savedSound=ride.SoundEnabled; savedEffects=ride.EffectsEnabled; savedDuck=impact.DuckRideSound; savedVolume=impact.ImpactVolume;
        report.Clear(); checks=0; scenario=0; running=true;
        Check(impact.ImpactSource!=null && impact.ImpactSource.clip!=null && impact.ImpactSource.clip.loadState==AudioDataLoadState.Loaded,"impact audio is prepared before first contact");
        world.ResetRide(); world.SpeedKmh=138; ride.SoundEnabled=true;
        stage=-1; since=EditorApplication.timeSinceStartup;
        EditorApplication.update+=Tick; EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        return "Impact audio checks running; .Local/Review/highway-impact-audio.txt";
    }
    static void Prepare()
    {
        world.ResetRide(); world.SpeedKmh=scenario==0 ? 138 : 220;
        ride.SoundEnabled=scenario!=2; ride.EffectsEnabled=scenario!=1;
        impact.ImpactVolume=scenario==3 ? 0 : .85f; impact.DuckRideSound=scenario!=4;
        world.TrySampleTraffic(0,out _,out float lane,out _); world.LateralPosition=lane;
        var traffic=typeof(NightHighwayWorld).GetField("traffic",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(world);
        var vehicles=(Array)typeof(HighwayTraffic).GetField("vehicles",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(traffic);
        object car=vehicles.GetValue(0); car.GetType().GetField("Distance").SetValue(car,12f); vehicles.SetValue(car,0);
        peak=mixedPeak=0; minimumMixGain=1; stage=0; since=EditorApplication.timeSinceStartup;
    }
    static void Check(bool ok,string message)
    {
        if(!ok) throw new Exception(message);
        checks++; report.Add("PASS "+message);
    }
    static void Tick()
    {
        try
        {
            double age=EditorApplication.timeSinceStartup-since;
            if(stage==-1) {if(age>2) Prepare(); return;}
            if(stage==0)
            {
                if(world.CollisionCount==0) {if(age>3)throw new Exception("No real contact");return;}
                stage=1; since=EditorApplication.timeSinceStartup;
            }
            if(stage!=1)return;
            impact.ImpactSource.GetOutputData(buffer,0);
            for(int i=0;i<buffer.Length;i++) peak=Mathf.Max(peak,Mathf.Abs(buffer[i]));
            AudioListener.GetOutputData(buffer,0);
            for(int i=0;i<buffer.Length;i++) mixedPeak=Mathf.Max(mixedPeak,Mathf.Abs(buffer[i]));
            minimumMixGain=Mathf.Min(minimumMixGain,ride.ImpactMixGain);
            if(EditorApplication.timeSinceStartup-since<.7)return;
            bool audible=scenario!=2 && scenario!=3;
            Check(audible ? peak>.10f : peak<.00001f,Names[scenario]+": source output peak="+peak.ToString("F4"));
            Check(mixedPeak<.98f,Names[scenario]+": mixed output has headroom (peak="+mixedPeak.ToString("F4")+")");
            Check(audible && scenario!=4 ? minimumMixGain<.35f : minimumMixGain>.99f,Names[scenario]+": drive attenuation follows sound/duck toggle");
            Check(ride.ImpactMixGain>.99f,Names[scenario]+": drive returns to normal gain");
            Check(world.CollisionCount==1,Names[scenario]+": one contact, one playback");
            scenario++;
            if(scenario<Names.Length)Prepare();else Finish("PASS: "+checks+" impact audio checks");
        }
        catch(Exception e){Finish("FAIL: "+e.Message);}
    }
    static void OnPlayModeChanged(PlayModeStateChange state)
    { if(running && state==PlayModeStateChange.ExitingPlayMode)Finish("CANCELLED: Play stopped"); }
    static void Finish(string result)
    {
        EditorApplication.update-=Tick; EditorApplication.playModeStateChanged-=OnPlayModeChanged; running=false;
        if(world!=null){ride.SoundEnabled=savedSound;ride.EffectsEnabled=savedEffects;impact.DuckRideSound=savedDuck;impact.ImpactVolume=savedVolume;world.ResetRide();}
        Directory.CreateDirectory(".Local/Review");File.WriteAllText(".Local/Review/highway-impact-audio.txt",result+"\n"+string.Join("\n",report));
        if(result.StartsWith("FAIL"))Debug.LogError(result);else Debug.Log(result);
    }
}
