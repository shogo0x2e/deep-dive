using System;
using HighwaySurfing.Kinect;
using UnityEngine;

public static class ArmSpeedChecks
{
    public static string Run()
    {
        var display = new GameObject("ArmSpeedCheckDisplay");
        int count = 0;
        try
        {
            foreach (float yaw in new[] { -45f, 0, 45f })
            foreach (float lean in new[] { -20f, 0, 20f })
            {
                Vector3 shoulder = new Vector3(1, 1.4f, 2);
                var flat = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                var roll = Quaternion.AngleAxis(-lean, Vector3.forward);
                Vector3 levelArm = roll * flat * .5f;
                Check(SurfArmSpeedSolver.TryMeasure(shoulder, shoulder + levelArm, display.transform, lean, out float level) && Mathf.Abs(level) < .001f,
                    "Steering alone must not change speed: " + yaw + "/" + lean); count++;
                Vector3 raisedArm = roll * (flat * Mathf.Cos(30 * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(30 * Mathf.Deg2Rad)) * .5f;
                Check(SurfArmSpeedSolver.TryMeasure(shoulder, shoulder + raisedArm, display.transform, lean, out float raised) && Mathf.Abs(raised - 30) < .001f,
                    "Brake pitch survives simultaneous steering: " + yaw + "/" + lean); count++;
            }
            Check(SurfArmSpeedSolver.TrySelectFrontWrist(Vector3.back, Vector3.forward, display.transform, out int r) && r == 14, "Right hand forward"); count++;
            Check(SurfArmSpeedSolver.TrySelectFrontWrist(Vector3.forward, Vector3.back, display.transform, out int l) && l == 7, "Left hand forward"); count++;
            Check(!SurfArmSpeedSolver.TrySelectFrontWrist(Vector3.left, Vector3.right, display.transform, out _), "Ambiguous front hand"); count++;
            Check(SurfArmSpeedSolver.Map(8, 8, 28) == 0 && SurfArmSpeedSolver.Map(-8, 8, 28) == 0, "Threshold inclusive neutral"); count++;
            Check(SurfArmSpeedSolver.Map(28, 8, 28) == -1 && SurfArmSpeedSolver.Map(-28, 8, 28) == 1, "Up brakes and down accelerates"); count++;
            Check(Mathf.Abs(SurfArmSpeedSolver.Map(18, 8, 28) + .5f) < .001f, "Input proportional beyond threshold"); count++;
            Check(SurfArmSpeedSolver.Map(float.NaN, 8, 28) == 0, "Invalid pitch neutral"); count++;
            Check(!SurfArmSpeedSolver.TryMeasure(Vector3.zero, Vector3.down * .5f, display.transform, 0, out _), "Arms hanging are not accelerator"); count++;
            Check(!SurfArmSpeedSolver.TryMeasure(Vector3.zero, Vector3.forward * .05f, display.transform, 0, out _), "Folded arm invalid"); count++;
            Check(!SurfArmSpeedSolver.TryMeasure(Vector3.zero, Vector3.back * .5f, display.transform, 0, out _), "Rear arm cannot take over"); count++;
            return $"PASS: {count} arm speed checks";
        }
        finally { UnityEngine.Object.DestroyImmediate(display); }
    }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
}
