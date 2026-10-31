using System;
using System.IO;
using HighwaySurfing;
using HighwaySurfing.Kinect;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit synthetic skeletons drive the actual registration components; never starts native Kinect.</summary>
public static class KinectEntryPlayChecks
{
    static GameObject root;
    static NightHighwayWorld world;
    static KinectDebugRig rig;
    static KinectHighwayInput input;
    static KinectArmSpeedInput arm;
    static int phase, assertions;
    static double since;
    static float calibrationStart;
    static bool running;
    static readonly System.Collections.Generic.List<string> report=new System.Collections.Generic.List<string>();

    [MenuItem("Tools/Highway Surfing/Checks/Automatic Entry (Simulated Play)")]
    public static void Run()=>Debug.Log(Start());
    public static string Start()
    {
        if(running || !EditorApplication.isPlaying || EditorApplication.isPaused)throw new Exception("Play HighwayKeyboard first; run one check at a time.");
        world=UnityEngine.Object.FindFirstObjectByType<NightHighwayWorld>();
        if(world==null || world.UseExternalSteering)throw new Exception("Use the keyboard Sandbox; this test uses a temporary simulated rig.");
        report.Clear();assertions=0;running=true;world.ResetRide();
        root=new GameObject("Simulated entry checks"){hideFlags=HideFlags.DontSave};root.SetActive(false);
        rig=root.AddComponent<KinectDebugRig>();
        rig.SensorOrigin=new GameObject("Sensor").transform;rig.SensorOrigin.SetParent(root.transform,false);
        rig.ScreenReference=new GameObject("Screen").transform;rig.ScreenReference.SetParent(root.transform,false);
        rig.Joints=new Transform[32];rig.Bones=new LineRenderer[32];rig.JointRenderers=new Renderer[32];
        for(int i=0;i<32;i++){rig.Joints[i]=new GameObject("Joint"+i).transform;rig.Joints[i].SetParent(rig.SensorOrigin,false);rig.Joints[i].localPosition=KinectDebugRig.NeutralJoint(i);}
        rig.Simulation=true;rig.SimulationFrontArmPitchDegrees=-28;
        input=root.AddComponent<KinectHighwayInput>();input.World=world;input.Rig=rig;input.AutoStartKinect=false;input.AllowSimulationForTesting=true;
        arm=root.AddComponent<KinectArmSpeedInput>();arm.Input=input;
        root.SetActive(true);phase=0;since=EditorApplication.timeSinceStartup;
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        return "Synthetic entry / instruction checks running; .Local/Review/highway-entry-trial.txt";
    }
    static void Next(int next){phase=next;since=EditorApplication.timeSinceStartup;}
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);assertions++;report.Add("PASS "+name);}
    static void Tick()
    {
        try
        {
            double age=EditorApplication.timeSinceStartup-since;
            switch(phase)
            {
                case 0:
                    if(age<.13)return;
                    Check(rig.SurfPoseReady,"naturally lowered front arm is accepted");
                    Check(input.RegistrationProgress>0,"entry hold is progressing");
                    rig.SimulationFrontArmPitchDegrees=-70;Next(1);break;
                case 1:
                    if(age<.14)return;
                    Check(!rig.Calibrating && input.RegistrationProgress>0,"brief invalid pose keeps progress but cannot begin calibration");
                    rig.SimulationFrontArmPitchDegrees=-28;Next(2);break;
                case 2:
                    if(!rig.Calibrating){if(age>1)throw new Exception("Relaxed calibration did not start");return;}
                    calibrationStart=Time.unscaledTime;
                    Check(input.TutorialTitle==SurfPoseGuidanceText.HoldTitle,"calibration uses the stable hold instruction");
                    rig.SimulationFrontArmPitchDegrees=-70;Next(3);break;
                case 3:
                    if(age<.15)return;
                    Check(rig.Calibrating,"brief arm pose interruption does not restart calibration");
                    Check(input.TutorialTitle==SurfPoseGuidanceText.HoldTitle,"hold instruction does not flash during the interruption");
                    rig.SimulationFrontArmPitchDegrees=-28;Next(4);break;
                case 4:
                    rig.SimulationLeanDegrees=((int)(age*15)%2==0)?-3:3;
                    if(!input.RideRegistered){if(age>2)throw new Exception("Relaxed entry did not complete");return;}
                    Check(Time.unscaledTime-calibrationStart>=.9f && Time.unscaledTime-calibrationStart<1.3f,"neutral posture holds for one second");
                    Check(world.ExternalRideReady && !input.ShowTutorial,"automatic entry starts the ride");
                    Check(arm.Calibrated && Mathf.Abs(arm.Control)<.02f,"lowered front arm is calibrated as neutral speed input");
                    input.RelaxedEntry=false;rig.SimulationLeanDegrees=0;input.RequestCalibration();Next(5);break;
                case 5:
                    if(age<.9)return;
                    Check(!rig.SurfPoseReady && !rig.Calibrating && !input.RideRegistered,"OFF restores strict arm requirements");
                    rig.SimulationFrontArmPitchDegrees=0;Next(6);break;
                case 6:
                    if(!rig.Calibrating){if(age>1.5)throw new Exception("Strict normal pose did not begin");return;}
                    Check(Mathf.Abs(rig.CalibrationHoldSeconds-1.5f)<.01f,"strict calibration duration restored");
                    rig.SimulationFrontArmPitchDegrees=-70;Next(7);break;
                case 7:
                    if(age<.15)return;
                    Check(!rig.Calibrating,"strict entry retains immediate pose reset");
                    input.RelaxedEntry=true;rig.SimulationFrontArmPitchDegrees=-28;input.RequestCalibration();Next(8);break;
                case 8:
                    if(!rig.Calibrating){if(age>1)throw new Exception("Second relaxed calibration did not start");return;}
                    rig.SimulationFrontArmPitchDegrees=-70;Next(9);break;
                case 9:
                    if(age<.45)return;
                    Check(!rig.Calibrating && !input.RideRegistered,"sustained lost pose cancels registration");
                    input.EnableAutomaticCalibration=false;input.RelaxedEntry=false;input.RequestCalibration();Next(10);break;
                case 10:
                    rig.SimulationFrontArmPitchDegrees=((int)(age*10)%2==0)?-30:-15;
                    if(age<.8)return;
                    Check(input.TutorialTitle=="腕を\n上げてください","fine height changes keep one raise instruction");
                    Next(11);break;
                case 11:
                    rig.SimulationFrontArmPitchDegrees=((int)(age*10)%2==0)?50:-28;
                    Check(input.TutorialTitle=="腕を\n上げてください","rapid opposite corrections do not flicker");
                    if(age<.6)return;
                    rig.SimulationFrontArmPitchDegrees=50;Next(12);break;
                case 12:
                    if(age<.55)return;
                    Check(input.TutorialTitle=="腕を\n下げてください","sustained upper pose changes to lower instruction");
                    rig.SimulationFrontArmPitchDegrees=0;Next(13);break;
                case 13:
                    if(age<.55)return;
                    Check(input.TutorialTitle==SurfPoseGuidanceText.HoldTitle,"stable pose changes to hold instruction");
                    Finish("PASS: "+assertions+" simulated entry and modal checks");break;
            }
        }
        catch(Exception e){Finish("FAIL phase "+phase+": "+e.Message);}
    }
    static void OnPlayModeChanged(PlayModeStateChange state){if(running && state==PlayModeStateChange.ExitingPlayMode)Finish("CANCELLED: Play stopped");}
    static void Finish(string result)
    {
        EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlayModeChanged;running=false;
        if(root!=null){root.SetActive(false);UnityEngine.Object.Destroy(root);}
        if(world!=null){world.UseExternalSteering=false;world.ExternalRideReady=world.ExternalSteeringAvailable=false;world.ResetRide();}
        Directory.CreateDirectory(".Local/Review");File.WriteAllText(".Local/Review/highway-entry-trial.txt",result+"\n"+string.Join("\n",report));
        if(result.StartsWith("FAIL"))Debug.LogError(result);else Debug.Log(result);
    }
}
